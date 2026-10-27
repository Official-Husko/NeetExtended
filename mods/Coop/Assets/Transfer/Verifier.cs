using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Coop.Assets.Transfer;

/// <summary>
/// Checks a finished download against its hash on a background thread, and moves it to its place in
/// the cache if it matches. Results come back on the main thread through <see cref="Drain"/>.
/// </summary>
internal static class Verifier
{
    public struct Result
    {
        public Download Download;
        public bool Good;
        public string Error;
    }

    private static readonly object Lock = new object();
    private static readonly List<Result> Done = new List<Result>();

    public static void Check(Download download)
    {
        string part = download.PartPath;
        string final = FileCache.PathOf(download.Id);
        string hash = download.Id.Hash;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            var result = new Result { Download = download };
            try
            {
                string actual;
                using (var sha = SHA256.Create())
                using (var stream = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024))
                    actual = AssetId.ToHex(sha.ComputeHash(stream));
                result.Good = actual == hash;
                if (result.Good)
                {
                    if (File.Exists(final))
                        File.Delete(part);
                    else
                        File.Move(part, final);
                }
                else
                {
                    result.Error = "what arrived doesn't match its hash";
                    File.Delete(part);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                result.Error = e.Message;
            }
            lock (Lock)
                Done.Add(result);
        });
    }

    /// <summary>Main thread: the checks that finished since the last call.</summary>
    public static void Drain(List<Result> into)
    {
        lock (Lock)
        {
            if (Done.Count == 0)
                return;
            into.AddRange(Done);
            Done.Clear();
        }
    }
}
