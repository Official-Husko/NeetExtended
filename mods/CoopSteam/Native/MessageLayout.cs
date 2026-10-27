using System;
using System.Runtime.InteropServices;
using Steamworks;

namespace CoopSteam.Native;

/// <summary>
/// Where the fields of Steam's message struct sit. Reading and writing them in place keeps sending
/// and receiving free of managed allocations (marshalling the whole struct would allocate its arrays
/// for every message). <see cref="Verify"/> checks the offsets against a real message before use.
/// </summary>
internal static class MessageLayout
{
    public static readonly int Data = Offset("m_pData");
    public static readonly int Size = Offset("m_cbSize");
    public static readonly int Conn = Offset("m_conn");
    public static readonly int Flags = Offset("m_nFlags");
    public static readonly int Lane = Offset("m_idxLane");

    private static int Offset(string field) => (int)Marshal.OffsetOf(typeof(SteamNetworkingMessage_t), field);

    /// <summary>Null when the offsets hold up, otherwise a reason to refuse to run.</summary>
    public static string Verify()
    {
        const int size = 40;
        IntPtr message = SteamNetworkingUtils.AllocateMessage(size);
        if (message == IntPtr.Zero)
            return "Steam wouldn't hand out a message buffer.";
        try
        {
            if (Marshal.ReadInt32(message, Size) != size || Marshal.ReadIntPtr(message, Data) == IntPtr.Zero)
                return "Steam's message layout isn't what this build expects (offsets " + Data + "/" + Size + ").";
            return null;
        }
        finally
        {
            SteamNetworkingMessage_t.Release(message);
        }
    }
}
