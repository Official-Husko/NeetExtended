using System.Collections.Generic;

namespace CoopSteam.Transport;

/// <summary>The live connections, by peer id (what Co-op sees) and by Steam's connection handle.</summary>
internal sealed class PeerTable
{
    // Per peer in the parked form: id, connection, SteamID64, connected, outSeq, inSeq, hasInSeq, ping.
    private const int ParkedFields = 8;

    private readonly Dictionary<uint, Peer> _byConn = new Dictionary<uint, Peer>();
    private readonly List<Peer> _all = new List<Peer>();
    private int _nextId;

    public int Count => _all.Count;
    public Peer this[int index] => _all[index];

    public Peer Add(uint conn, ulong steamId)
    {
        var peer = new Peer(_nextId++, conn, steamId);
        _byConn[conn] = peer;
        _all.Add(peer);
        return peer;
    }

    public bool TryGet(uint conn, out Peer peer) => _byConn.TryGetValue(conn, out peer);

    public bool TryGetById(int id, out Peer peer)
    {
        for (int i = 0; i < _all.Count; i++)
        {
            if (_all[i].Id == id)
            {
                peer = _all[i];
                return true;
            }
        }
        peer = null;
        return false;
    }

    public void Remove(Peer peer)
    {
        _byConn.Remove(peer.Conn);
        _all.Remove(peer);
    }

    public void Clear()
    {
        _byConn.Clear();
        _all.Clear();
    }

    public ulong[] Export()
    {
        var data = new ulong[2 + _all.Count * ParkedFields];
        data[0] = (ulong)_nextId;
        data[1] = (ulong)_all.Count;
        for (int i = 0; i < _all.Count; i++)
        {
            var p = _all[i];
            int at = 2 + i * ParkedFields;
            data[at] = (ulong)p.Id;
            data[at + 1] = p.Conn;
            data[at + 2] = p.SteamId;
            data[at + 3] = p.Connected ? 1UL : 0UL;
            data[at + 4] = p.OutSeq;
            data[at + 5] = p.InSeq;
            data[at + 6] = p.HasInSeq ? 1UL : 0UL;
            data[at + 7] = (ulong)(uint)p.Ping;
        }
        return data;
    }

    /// <summary>Reads what <see cref="Export"/> wrote, starting at <paramref name="start"/>.</summary>
    public void Import(ulong[] data, int start)
    {
        Clear();
        _nextId = (int)data[start];
        int count = (int)data[start + 1];
        for (int i = 0; i < count; i++)
        {
            int at = start + 2 + i * ParkedFields;
            var peer = new Peer((int)data[at], (uint)data[at + 1], data[at + 2])
            {
                Connected = data[at + 3] != 0,
                OutSeq = (ushort)data[at + 4],
                InSeq = (ushort)data[at + 5],
                HasInSeq = data[at + 6] != 0,
                Ping = (int)(uint)data[at + 7],
            };
            _byConn[peer.Conn] = peer;
            _all.Add(peer);
        }
    }

    /// <summary>The connection handles in an exported table.</summary>
    public static IEnumerable<uint> ConnectionsIn(ulong[] data, int start)
    {
        int count = (int)data[start + 1];
        for (int i = 0; i < count; i++)
            yield return (uint)data[start + 2 + i * ParkedFields + 1];
    }
}
