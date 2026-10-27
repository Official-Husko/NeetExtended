using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Economy;

/// <summary>
/// Tax bills live on the host: it sends new ones, raises their stage and calls the cops on the last.
/// Clients get the host's list (amount, stage and the bill's paper, a shared item) so the tax page
/// shows the same bills, and paying one on a client is a request the host carries out.
///
/// WorldTaxes (host to all): count, per bill: paper ref, amount, stage.
/// WorldTaxPay (client to host): paper ref.
/// </summary>
internal static class TaxSync
{
    private const float Interval = 1f;

    private static readonly List<int> SentShape = new List<int>();
    private static float _next;
    private static bool _sendAgain = true;

    public static void Init()
    {
        Messages.On(MessageIds.WorldTaxes, OnTaxes);
        Messages.On(MessageIds.WorldTaxPay, OnPay);
    }

    public static void Clear()
    {
        SentShape.Clear();
        _sendAgain = true;
    }

    public static void SendAgain() => _sendAgain = true;

    /// <summary>Host, once a second: the bill list went out again if it changed.</summary>
    public static void Tick()
    {
        float now = Time.time;
        var taxes = Handler();
        if (now < _next || taxes == null)
            return;
        _next = now + Interval;
        if (!_sendAgain && SameShape(taxes.taxBills))
            return;
        _sendAgain = false;
        Remember(taxes.taxBills);
        var w = Outbox.Begin(MessageIds.WorldTaxes);
        w.Put((byte)taxes.taxBills.Count);
        foreach (var bill in taxes.taxBills)
        {
            PaperOf(bill).Write(w);
            w.Put(bill.amount);
            w.Put((byte)bill.stage);
        }
        Outbox.ToPlayers(w, Channel.Events);
    }

    /// <summary>Client, from the tax page's pay button: false keeps the page from paying here.</summary>
    public static bool Pay(int index)
    {
        var taxes = Handler();
        if (!WorldSide.Running || !WorldSide.IsClient || taxes == null || index < 0 || index >= taxes.taxBills.Count)
            return true;
        var bill = taxes.taxBills[index];
        var paper = PaperOf(bill);
        if (!paper.IsValid || HouseManager._.mainComputer.Money < bill.amount)
            return false;
        var w = Outbox.Begin(MessageIds.WorldTaxPay);
        paper.Write(w);
        Outbox.ToHost(w, Channel.Events);
        HouseManager._.mainComputer.GetComponents<AudioSource>()[1].Play();
        return false;
    }

    private static void OnPay(in Sender from, NetDataReader reader)
    {
        var paper = NetRef.Read(reader);
        var taxes = Handler();
        if (!WorldSide.IsHost || !WorldSide.Running || taxes == null)
            return;
        for (int i = 0; i < taxes.taxBills.Count; i++)
        {
            var bill = taxes.taxBills[i];
            if (!PaperOf(bill).Equals(paper))
                continue;
            var computer = HouseManager._.mainComputer;
            // What the tax page does, on the host: the money change goes out as the new total.
            if (computer.Money >= bill.amount)
            {
                computer.Money -= bill.amount;
                taxes.taxBills.RemoveAt(i);
            }
            return;
        }
    }

    private static void OnTaxes(in Sender from, NetDataReader reader)
    {
        var taxes = Handler();
        if (WorldSide.IsHost || !WorldSide.Running || taxes == null)
            return;
        int count = reader.GetByte();
        taxes.taxBills.Clear();
        for (int i = 0; i < count; i++)
        {
            var paper = NetObjects.Find(NetRef.Read(reader));
            float amount = reader.GetFloat();
            int stage = reader.GetByte();
            // The constructor only sets the timer, which doesn't run here.
            taxes.taxBills.Add(new TaxBill(taxes, amount, paper != null ? paper.Go : null) { stage = stage });
        }
        foreach (var page in Object.FindObjectsOfType<TaxPaymentUI>())
        {
            if (page.isActiveAndEnabled)
                page.RefreshBillUI();
        }
    }

    private static TaxHandler Handler() => HouseManager._ != null ? HouseManager._.GetComponent<TaxHandler>() : null;

    private static NetRef PaperOf(TaxBill bill)
    {
        var o = bill.bill != null ? NetObjects.Find(bill.bill) : null;
        return o != null ? o.Ref : default;
    }

    private static bool SameShape(List<TaxBill> bills)
    {
        if (SentShape.Count != bills.Count * 3)
            return false;
        for (int i = 0; i < bills.Count; i++)
        {
            var bill = bills[i];
            if (SentShape[i * 3] != PaperOf(bill).Id || SentShape[i * 3 + 1] != Mathf.RoundToInt(bill.amount * 100f) || SentShape[i * 3 + 2] != bill.stage)
                return false;
        }
        return true;
    }

    private static void Remember(List<TaxBill> bills)
    {
        SentShape.Clear();
        foreach (var bill in bills)
        {
            SentShape.Add(PaperOf(bill).Id);
            SentShape.Add(Mathf.RoundToInt(bill.amount * 100f));
            SentShape.Add(bill.stage);
        }
    }
}
