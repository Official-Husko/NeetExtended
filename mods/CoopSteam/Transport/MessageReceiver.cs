using System;
using System.Runtime.InteropServices;
using Coop.Net;
using CoopSteam.Native;
using LiteNetLib.Utils;
using Steamworks;

namespace CoopSteam.Transport;

/// <summary>
/// Reads what arrived on the poll group, undoes the framing (see <see cref="MessageSender"/>), drops old
/// Stream packets and puts big messages back together. One reader and one buffer are reused throughout.
/// </summary>
internal sealed class MessageReceiver
{
    private const int BatchSize = 64;
    private const int MaxBatches = 8;

    private readonly IntPtr[] _batch = new IntPtr[BatchSize];
    private readonly NetDataReader _reader = new NetDataReader();
    private readonly PeerTable _peers;
    private readonly Action<Peer, NetDataReader, Channel> _deliver;
    private readonly Action<Peer, string> _misbehaved;
    private readonly Func<bool> _running;
    private byte[] _buffer = new byte[64 * 1024];

    public MessageReceiver(PeerTable peers, Action<Peer, NetDataReader, Channel> deliver, Action<Peer, string> misbehaved, Func<bool> running)
    {
        _peers = peers;
        _deliver = deliver;
        _misbehaved = misbehaved;
        _running = running;
    }

    public void Pump(HSteamNetPollGroup group)
    {
        for (int pass = 0; pass < MaxBatches; pass++)
        {
            int count = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(group, _batch, BatchSize);
            for (int i = 0; i < count; i++)
                Take(_batch[i]);
            if (count < BatchSize)
                return;
        }
    }

    // Copies the message out and gives it back to Steam right away, so a handler that throws or leaves
    // the game can't leak the rest of the batch.
    private void Take(IntPtr message)
    {
        int size = Marshal.ReadInt32(message, MessageLayout.Size);
        uint conn = (uint)Marshal.ReadInt32(message, MessageLayout.Conn);
        bool wanted = size > 0 && _running() && _peers.TryGet(conn, out _);
        if (wanted)
        {
            if (size > _buffer.Length)
                _buffer = new byte[Math.Max(size, _buffer.Length * 2)];
            Marshal.Copy(Marshal.ReadIntPtr(message, MessageLayout.Data), _buffer, 0, size);
        }
        SteamNetworkingMessage_t.Release(message);
        if (!wanted || !_peers.TryGet(conn, out Peer peer))
            return;

        try
        {
            Handle(peer, size);
        }
        catch (Exception e)
        {
            CoopSteamMod.Log?.Error("A message handler failed", e);
        }
    }

    private void Handle(Peer peer, int size)
    {
        byte head = _buffer[0];
        int channelNumber = head & 0x0F;
        if (channelNumber > (int)Channel.WorldStream)
            return;
        var channel = (Channel)channelNumber;

        if ((head & MessageSender.ChunkBit) != 0)
        {
            HandlePiece(peer, channel, size);
            return;
        }

        int start = 1;
        if (channel == Channel.Stream)
        {
            if (size < 3)
                return;
            ushort seq = (ushort)(_buffer[1] | (_buffer[2] << 8));
            if (peer.HasInSeq && (short)(seq - peer.InSeq) <= 0)
                return;
            peer.InSeq = seq;
            peer.HasInSeq = true;
            start = 3;
        }
        _reader.SetSource(_buffer, start, size);
        _deliver(peer, _reader, channel);
    }

    private void HandlePiece(Peer peer, Channel channel, int size)
    {
        if (size < 2 || channel == Channel.Stream || channel == Channel.WorldStream)
            return;
        byte flags = _buffer[1];
        var assembler = peer.AssemblerFor(channel);
        if (!assembler.Append(_buffer, 2, size - 2, (flags & MessageSender.FirstPiece) != 0))
        {
            assembler.Reset();
            _misbehaved(peer, "Sent a message that was far too big.");
            return;
        }
        if ((flags & MessageSender.LastPiece) == 0)
            return;
        _reader.SetSource(assembler.Buffer, 0, assembler.Length);
        try
        {
            _deliver(peer, _reader, channel);
        }
        finally
        {
            assembler.Reset();
        }
    }
}
