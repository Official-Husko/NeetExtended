using System;
using System.Runtime.InteropServices;
using Coop.Net;
using CoopSteam.Native;
using Steamworks;

namespace CoopSteam.Transport;

/// <summary>
/// Frames and sends. Each message starts with a channel byte (bit 6 set: one piece of a bigger message,
/// followed by a flags byte, 1 = first, 2 = last); Stream adds a 16-bit counter. Messages are built in
/// Steam's own buffer, so nothing is allocated per send.
/// </summary>
internal sealed class MessageSender
{
    /// <summary>Steam sends at most 512 KB in one message; bigger ones go in pieces of this size.</summary>
    public const int ChunkSize = 256 * 1024;

    public const byte ChunkBit = 0x40;
    public const byte FirstPiece = 1;
    public const byte LastPiece = 2;

    private readonly IntPtr[] _message = new IntPtr[1];
    private readonly long[] _result = new long[1];

    /// <summary>Zero when sent, otherwise the negative EResult Steam gave back.</summary>
    public int Send(Peer peer, Channel channel, byte[] data, int length)
    {
        switch (channel)
        {
            case Channel.Stream:
                ushort seq = peer.OutSeq++;
                return Check(Put(peer.Conn, Constants.k_nSteamNetworkingSend_UnreliableNoNagle, ConnectionOptions.NormalLane,
                    3, (byte)channel, (byte)seq, (byte)(seq >> 8), data, 0, length));
            case Channel.WorldStream:
                return Check(Put(peer.Conn, Constants.k_nSteamNetworkingSend_UnreliableNoNagle, ConnectionOptions.NormalLane,
                    1, (byte)channel, 0, 0, data, 0, length));
            default:
                return SendReliable(peer.Conn, channel, data, length);
        }
    }

    private int SendReliable(uint conn, Channel channel, byte[] data, int length)
    {
        bool bulk = channel == Channel.Bulk;
        int flags = bulk ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_ReliableNoNagle;
        ushort lane = bulk ? ConnectionOptions.BulkLane : ConnectionOptions.NormalLane;

        if (length <= ChunkSize)
            return Check(Put(conn, flags, lane, 1, (byte)channel, 0, 0, data, 0, length));

        for (int offset = 0; offset < length; offset += ChunkSize)
        {
            int count = Math.Min(ChunkSize, length - offset);
            byte piece = (byte)((offset == 0 ? FirstPiece : 0) | (offset + count >= length ? LastPiece : 0));
            int result = Check(Put(conn, flags, lane, 2, (byte)((byte)channel | ChunkBit), piece, 0, data, offset, count));
            if (result != 0)
                return result;
        }
        return 0;
    }

    private long Put(uint conn, int sendFlags, ushort lane, int headerLength, byte h0, byte h1, byte h2, byte[] data, int offset, int count)
    {
        IntPtr message = SteamNetworkingUtils.AllocateMessage(headerLength + count);
        if (message == IntPtr.Zero)
            return -(long)EResult.k_EResultFail;
        IntPtr body = Marshal.ReadIntPtr(message, MessageLayout.Data);
        Marshal.WriteByte(body, 0, h0);
        if (headerLength > 1)
            Marshal.WriteByte(body, 1, h1);
        if (headerLength > 2)
            Marshal.WriteByte(body, 2, h2);
        if (count > 0)
            Marshal.Copy(data, offset, IntPtr.Add(body, headerLength), count);
        Marshal.WriteInt32(message, MessageLayout.Conn, (int)conn);
        Marshal.WriteInt32(message, MessageLayout.Flags, sendFlags);
        Marshal.WriteInt16(message, MessageLayout.Lane, (short)lane);

        _message[0] = message;
        _result[0] = 0;
        SteamNetworkingSockets.SendMessages(1, _message, _result);
        return _result[0];
    }

    // A positive number is the message number; a negative one is -EResult.
    private static int Check(long result) => result > 0 ? 0 : (int)(result == 0 ? -(long)EResult.k_EResultFail : result);
}
