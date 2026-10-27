using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PerformanceFixes.Painting;

/// <summary>
/// A background thread that turns pixel buffers into PNG files, in the order they were queued.
/// Unity marks its PNG encoder as safe to call from other threads. Files are written next to
/// their target and then moved over it, so a crash mid-write leaves the old picture intact.
/// </summary>
internal sealed class PaintWriter : IDisposable
{
    private readonly object _lock = new object();
    private readonly Queue<Job> _jobs = new Queue<Job>();
    private readonly HashSet<int> _failed = new HashSet<int>();
    private readonly List<string> _errors = new List<string>();
    private readonly byte[] _chunk = new byte[64 * 1024];
    private readonly Thread _thread;
    private bool _busy;
    private long _queuedBytes;
    private bool _stopping;

    public PaintWriter()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Paint saves" };
        _thread.Start();
    }

    /// <summary>Queues a picture. The writer returns <paramref name="pixels"/> to <see cref="PixelBuffers"/> when done.</summary>
    public void Write(string path, int saveId, NativeArray<byte> pixels, GraphicsFormat format, int width, int height) =>
        Enqueue(new Job { Path = path, SaveId = saveId, Pixels = pixels, Bytes = pixels.Length, Format = format, Width = width, Height = height });

    /// <summary>Queues deleting every file in <paramref name="folder"/> that isn't the picture of a surface in <paramref name="keep"/>.</summary>
    public void Prune(string folder, HashSet<int> keep) => Enqueue(new Job { Path = folder, Keep = keep });

    /// <summary>
    /// Waits until the pictures queued but not written yet hold at most <paramref name="bytes"/> of
    /// pixels. False if that took longer than <paramref name="timeoutMs"/>.
    /// </summary>
    public bool WaitBelow(long bytes, int timeoutMs)
    {
        var clock = Stopwatch.StartNew();
        lock (_lock)
        {
            while (_queuedBytes > bytes)
            {
                int left = timeoutMs - (int)clock.ElapsedMilliseconds;
                if (left <= 0)
                    return false;
                Monitor.Wait(_lock, left);
            }
        }
        return true;
    }

    /// <summary>Waits until everything queued is on disk. False if that took longer than <paramref name="timeoutMs"/>.</summary>
    public bool Flush(int timeoutMs)
    {
        var clock = Stopwatch.StartNew();
        lock (_lock)
        {
            while (_jobs.Count > 0 || _busy)
            {
                int left = timeoutMs - (int)clock.ElapsedMilliseconds;
                if (left <= 0)
                    return false;
                Monitor.Wait(_lock, left);
            }
        }
        return true;
    }

    /// <summary>Moves the save ids of pictures that couldn't be written, and the errors, into the given collections.</summary>
    public void TakeFailures(HashSet<int> saveIds, List<string> errors)
    {
        lock (_lock)
        {
            saveIds.UnionWith(_failed);
            errors.AddRange(_errors);
            _failed.Clear();
            _errors.Clear();
        }
    }

    public void Dispose()
    {
        Flush(10000);
        lock (_lock)
        {
            _stopping = true;
            Monitor.PulseAll(_lock);
        }
        _thread.Join(2000);
        // Anything still queued (only after a timeout) gives its buffer back.
        lock (_lock)
        {
            while (_jobs.Count > 0)
            {
                var job = _jobs.Dequeue();
                if (job.Keep == null)
                    PixelBuffers.Return(job.Pixels);
            }
        }
    }

    private void Enqueue(Job job)
    {
        lock (_lock)
        {
            _jobs.Enqueue(job);
            if (job.Keep == null)
                _queuedBytes += job.Bytes;
            Monitor.PulseAll(_lock);
        }
    }

    private void Run()
    {
        while (true)
        {
            Job job;
            lock (_lock)
            {
                while (_jobs.Count == 0 && !_stopping)
                    Monitor.Wait(_lock);
                if (_jobs.Count == 0)
                    return;
                job = _jobs.Dequeue();
                _busy = true;
            }

            try
            {
                if (job.Keep != null)
                    DeleteOthers(job.Path, job.Keep);
                else
                    WritePicture(job);
            }
            catch (Exception e)
            {
                lock (_lock)
                {
                    if (job.Keep == null)
                        _failed.Add(job.SaveId);
                    _errors.Add($"{Path.GetFileName(job.Path)}: {e.GetType().Name}: {e.Message}");
                }
            }
            finally
            {
                lock (_lock)
                {
                    _busy = false;
                    if (job.Keep == null)
                        _queuedBytes -= job.Bytes;
                    Monitor.PulseAll(_lock);
                }
            }
        }
    }

    private void WritePicture(Job job)
    {
        try
        {
            var png = ImageConversion.EncodeNativeArrayToPNG(job.Pixels, job.Format, (uint)job.Width, (uint)job.Height);
            try
            {
                string temp = job.Path + ".tmp";
                using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
                {
                    for (int offset = 0; offset < png.Length; offset += _chunk.Length)
                    {
                        int count = Math.Min(_chunk.Length, png.Length - offset);
                        NativeArray<byte>.Copy(png, offset, _chunk, 0, count);
                        file.Write(_chunk, 0, count);
                    }
                }
                MoveOver(temp, job.Path);
            }
            finally
            {
                png.Dispose();
            }
        }
        finally
        {
            PixelBuffers.Return(job.Pixels);
        }
    }

    private static void MoveOver(string source, string target)
    {
        if (!File.Exists(target))
        {
            File.Move(source, target);
            return;
        }
        try
        {
            File.Replace(source, target, null);
        }
        catch (Exception e) when (e is IOException || e is PlatformNotSupportedException || e is UnauthorizedAccessException)
        {
            File.Delete(target);
            File.Move(source, target);
        }
    }

    // The game itself deletes the whole folder before every save; this keeps the pictures still in use.
    private static void DeleteOthers(string folder, HashSet<int> keep)
    {
        foreach (string file in Directory.GetFiles(folder))
        {
            string name = Path.GetFileName(file);
            if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(Path.GetFileNameWithoutExtension(name), out int saveId)
                && keep.Contains(saveId))
                continue;
            File.Delete(file);
        }
    }

    private sealed class Job
    {
        public string Path;
        public int SaveId;
        public NativeArray<byte> Pixels;

        // Counted when queued: the buffer is handed back (and may be freed) before the job is done.
        public long Bytes;
        public GraphicsFormat Format;
        public int Width;
        public int Height;
        public HashSet<int> Keep;
    }
}
