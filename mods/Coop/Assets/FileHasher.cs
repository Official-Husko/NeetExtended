using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Coop.Assets;

/// <summary>
/// Works out files' ids (SHA-256) on a background thread, one file at a time, and hands the results
/// back on the main thread in <see cref="Tick"/>. Importing also copies the file into the cache under
/// its hash, in the same read. Hashes of files that haven't changed (same size and write time) come
/// from <see cref="HashIndex"/>, so big videos aren't read again every session.
/// </summary>
internal static class FileHasher
{
    private sealed class Job
    {
        public string Path;
        public bool Import;
        public Action<AssetId> Done;
        public AssetId Result;
        public string Error;
    }

    private const int BufferSize = 128 * 1024;

    private static readonly object Lock = new object();
    private static readonly Queue<Job> Waiting = new Queue<Job>();
    private static readonly List<Job> Finished = new List<Job>();
    private static readonly List<Job> Draining = new List<Job>();
    private static bool _working;

    /// <summary>The file's id; <paramref name="done"/> gets an invalid id if it can't be read or isn't a type that's shared.</summary>
    public static void Hash(string path, Action<AssetId> done) => Enqueue(new Job { Path = path, Done = done });

    /// <summary>Like <see cref="Hash"/>, and the file is also copied into the cache as "hash.ext".</summary>
    public static void Import(string path, Action<AssetId> done) => Enqueue(new Job { Path = path, Import = true, Done = done });

    /// <summary>Imports right here on the calling thread (a click that needs the answer now).</summary>
    public static AssetId ImportNow(string path, out string error)
    {
        var job = new Job { Path = path, Import = true };
        Run(job);
        error = job.Error;
        return job.Result;
    }

    /// <summary>Main thread, every frame: hands out finished results.</summary>
    public static void Tick()
    {
        lock (Lock)
        {
            if (Finished.Count == 0)
                return;
            Draining.AddRange(Finished);
            Finished.Clear();
        }
        foreach (var job in Draining)
        {
            if (job.Error != null)
                CoopMod.Log.Info($"Files: {Path.GetFileName(job.Path)} isn't shared: {job.Error}");
            try { job.Done?.Invoke(job.Result); }
            catch (Exception e) { CoopMod.Log.Error($"Files: handling {Path.GetFileName(job.Path)} failed: {e}"); }
        }
        Draining.Clear();
        HashIndex.SaveIfChanged();
    }

    private static void Enqueue(Job job)
    {
        lock (Lock)
        {
            Waiting.Enqueue(job);
            if (_working)
                return;
            _working = true;
        }
        ThreadPool.QueueUserWorkItem(_ => Work());
    }

    private static void Work()
    {
        while (true)
        {
            Job job;
            lock (Lock)
            {
                if (Waiting.Count == 0)
                {
                    _working = false;
                    return;
                }
                job = Waiting.Dequeue();
            }
            Run(job);
            lock (Lock)
                Finished.Add(job);
        }
    }

    private static void Run(Job job)
    {
        try
        {
            job.Result = job.Import ? Copy(job.Path, out job.Error) : Read(job.Path, out job.Error);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
        {
            job.Error = e.Message;
            job.Result = default;
        }
    }

    private static AssetId Read(string path, out string error)
    {
        if (!Check(path, out var info, out string ext, out error))
            return default;
        string known = HashIndex.Find(info);
        if (known != null)
            return new AssetId(known, info.Length, ext);
        using (var sha = SHA256.Create())
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize))
        {
            string hash = AssetId.ToHex(sha.ComputeHash(stream));
            HashIndex.Remember(info, hash);
            return new AssetId(hash, info.Length, ext);
        }
    }

    // One read: hashed while it's written to a temporary file, which then takes its hash as its name.
    private static AssetId Copy(string path, out string error)
    {
        if (!Check(path, out var info, out string ext, out error))
            return default;
        string known = HashIndex.Find(info);
        if (known != null)
        {
            var id = new AssetId(known, info.Length, ext);
            if (FileCache.Find(id) != null)
                return id;
        }

        string temp = Path.Combine(FileCache.Folder, "import-" + Guid.NewGuid().ToString("N") + FileCache.PartExt);
        try
        {
            string hash;
            using (var sha = SHA256.Create())
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize))
            using (var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    target.Write(buffer, 0, read);
                }
                sha.TransformFinalBlock(buffer, 0, 0);
                hash = AssetId.ToHex(sha.Hash);
            }
            HashIndex.Remember(info, hash);
            var id = new AssetId(hash, info.Length, ext);
            string final = FileCache.PathOf(id);
            if (File.Exists(final))
                File.Delete(temp);
            else
                File.Move(temp, final);
            return id;
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private static bool Check(string path, out FileInfo info, out string ext, out string error)
    {
        info = null;
        error = null;
        ext = FileTypes.ExtOf(path);
        if (ext == null)
        {
            error = "not a picture, song or video co-op sends";
            return false;
        }
        info = new FileInfo(path);
        if (!info.Exists)
        {
            error = "the file isn't there";
            return false;
        }
        long max = FileTypes.MaxBytes(ext);
        if (info.Length > max)
        {
            error = $"it's {FileSizes.Describe(info.Length)}, more than the {FileSizes.Describe(max)} the settings allow";
            return false;
        }
        return true;
    }
}
