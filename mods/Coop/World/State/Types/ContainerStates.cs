using Coop.World.Objects;
using HarmonyLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// A dispenser item (egg carton, pizza box, tissue-like things that hand out copies): how many are
/// left and whether it is open. What it hands out is made, and shared, where it is used; this keeps
/// the count the same everywhere, so whoever picks it up next carries on from it, and shows the
/// open lid and the missing eggs.
///
/// Layout: count (short, -1 for endless), enabled (bool).
/// </summary>
internal sealed class PrefabContainerState : StateType<PrefabContainer>
{
    private static readonly AccessTools.FieldRef<PrefabContainer, int> Start = AccessTools.FieldRefAccess<PrefabContainer, int>("initObjCount");

    public override bool OnItems => true;

    protected override void Write(PrefabContainer box, NetDataWriter w)
    {
        w.Put((short)box.objCount);
        w.Put(box.enabled);
    }

    protected override void Apply(PrefabContainer box, NetDataReader r)
    {
        int count = r.GetShort();
        bool enabled = r.GetBool();
        box.objCount = count;
        if (box.enabled != enabled)
            box.enabled = enabled;
        // Taking the first one makes it free to sell.
        if (count != Start(box) && Start(box) != -1)
            box.GetComponent<Usable>().price = 0f;
        Show(box, count, enabled);
    }

    private static void Show(PrefabContainer box, int count, bool enabled)
    {
        var eggs = box.GetComponent<Egg_Carton>();
        if (eggs != null)
        {
            if (enabled)
                eggs.lid.transform.localRotation = Quaternion.Euler(-210f, 0f, 0f);
            for (int i = 0; i < eggs.eggsVisuals.Length; i++)
                eggs.eggsVisuals[i].SetActive(i < count);
        }
        var pizza = box.GetComponent<PizzaBox>();
        if (pizza != null && count != Start(box))
        {
            var filter = box.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != pizza.openedBox)
                filter.mesh = pizza.openedBox;
        }
    }
}

/// <summary>A toilet paper roll: sheets left, and the roll shrinking with them.</summary>
internal sealed class PaperRollState : StateType<Toilet_Paper_Roll>
{
    public override bool OnItems => true;

    protected override void Write(Toilet_Paper_Roll roll, NetDataWriter w) => w.Put((short)roll.sheetsCount);

    protected override void Apply(Toilet_Paper_Roll roll, NetDataReader r)
    {
        int sheets = r.GetShort();
        if (roll.sheetsCount == sheets)
            return;
        roll.sheetsCount = sheets;
        var meshes = roll.smallerMeshes;
        if (meshes == null || meshes.Length == 0 || roll.maxSheets <= 0)
            return;
        // The game's own pick of the smaller mesh.
        int step = sheets != 0 ? Mathf.FloorToInt(sheets / (float)roll.maxSheets * 3f + 1f) : 0;
        roll.GetComponent<MeshFilter>().mesh = meshes[Mathf.Clamp(step, 0, meshes.Length - 1)];
    }
}

/// <summary>A cereal box: opened or not (the cereal that spills out is each game's own particles).</summary>
internal sealed class CerealBoxState : StateType<CerealBox>
{
    public override bool OnItems => true;

    protected override void Write(CerealBox box, NetDataWriter w) => w.Put(box.opened);

    protected override void Apply(CerealBox box, NetDataReader r)
    {
        if (r.GetBool() && !box.opened)
            box.Open();
    }
}

/// <summary>A vacuum cleaner: its battery, whether it runs and how much dust it holds.</summary>
internal sealed class VacuumState : StateType<VacuumCleaner>
{
    public override bool OnItems => true;

    protected override void Write(VacuumCleaner vacuum, NetDataWriter w)
    {
        ItemRefs.Of(vacuum.Fuse != null ? vacuum.Fuse.gameObject : null).Write(w);
        w.Put(vacuum.on);
        w.Put((ushort)Mathf.Clamp(vacuum.containedDust, 0, ushort.MaxValue));
    }

    protected override void Apply(VacuumCleaner vacuum, NetDataReader r)
    {
        var fuse = ItemRefs.Find<Fuse>(NetRef.Read(r), out bool known);
        bool on = r.GetBool();
        vacuum.containedDust = r.GetUShort();
        if (known && vacuum.Fuse != fuse)
            vacuum.Fuse = fuse;
        if (vacuum.on != on)
            vacuum.SetUse(on);
    }
}
