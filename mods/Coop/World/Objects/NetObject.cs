using Coop.World.Npcs;
using Coop.World.Streaming;
using UnityEngine;

namespace Coop.World.Objects;

/// <summary>One networked thing in this game: what it is, who simulates it and its stream state.</summary>
internal sealed class NetObject
{
    public NetRef Ref;
    public GameObject Go;
    public Transform Tf;

    /// <summary>The body that moves it; null for things moved by their NavMeshAgent only.</summary>
    public Rigidbody Body;

    public Usable Usable;
    public Brain Brain;
    public Door Door;

    /// <summary>The PickupBank or BrainBank name it can be spawned from; empty for scene objects.</summary>
    public string PrefabName = "";

    /// <summary>Set for NPCs built on the player rig; they travel as NPC poses, not bodies.</summary>
    public NpcRig Rig;

    // Ownership
    public byte Owner = WorldSide.HostSlot;
    public byte Holder = WorldSide.None;

    /// <summary>The isKinematic the game wants, as its owner last reported it.</summary>
    public bool GameKinematic;

    /// <summary>Kinematic here and moved by the owner's stream.</summary>
    public bool IsReplica;

    // Owner side: what was sent last and when it last changed.
    public Vector3 SentPosition;
    public Quaternion SentRotation;
    public Vector3 SentScale;
    public byte SentFlags;
    public float LastChange;
    public bool Moving;

    /// <summary>When the local player last touched it (no handing back while touching).</summary>
    public float LastTouched = float.NegativeInfinity;

    // Replica side
    public readonly BodyBuffer Buffer = new BodyBuffer();
    public bool Driving;

    internal int Index = -1;
    internal int GoId;
    internal int BodyId;

    public bool Alive => Go != null;

    public bool IsSpawnable => Ref.IsDynamic && PrefabName.Length > 0;

    public bool IsHeld => Holder != WorldSide.None;

    public bool HeldByOther => Holder != WorldSide.None && Holder != WorldSide.LocalSlot;

    public bool OwnedHere => Owner == WorldSide.LocalSlot;

    public override string ToString() => $"{Ref} {(PrefabName.Length > 0 ? PrefabName : Go != null ? Go.name : "gone")}";
}
