using System.Collections.Generic;
using Unity.Collections;

namespace PerformanceFixes.Painting;

/// <summary>
/// Reusable unmanaged buffers for paint pixels. They live outside the Mono heap, so a save of
/// many surfaces doesn't grow the heap (which would make every later garbage collection slower).
/// Rented on the main thread, returned by the writer thread.
/// </summary>
internal static class PixelBuffers
{
    private const int Keep = 4;
    private static readonly object Lock = new object();
    private static readonly List<NativeArray<byte>> Free = new List<NativeArray<byte>>();

    public static NativeArray<byte> Rent(int length)
    {
        lock (Lock)
        {
            for (int i = Free.Count - 1; i >= 0; i--)
            {
                if (Free[i].Length != length)
                    continue;
                var buffer = Free[i];
                Free.RemoveAt(i);
                return buffer;
            }
        }
        return new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    public static void Return(NativeArray<byte> buffer)
    {
        lock (Lock)
        {
            if (Free.Count < Keep)
            {
                Free.Add(buffer);
                return;
            }
        }
        buffer.Dispose();
    }

    public static void Clear()
    {
        lock (Lock)
        {
            foreach (var buffer in Free)
                buffer.Dispose();
            Free.Clear();
        }
    }
}
