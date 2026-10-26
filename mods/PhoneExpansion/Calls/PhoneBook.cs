using System;
using System.Collections.Generic;
using System.Text;
using NeetLoader.Runtime;

namespace PhoneExpansion.Calls;

/// <summary>
/// Every modded call. Calls from files are added by the pack loader; mods can add their own:
/// <code>
/// InkScripts.Add(this, "mycall.ink", "=== MyMod_Hello ===\nHello?\n# EndPhoneCall\n-> DONE");
/// PhoneBook.Add(this, new PhoneCall { Id = "MyMod/hello", Number = "5550123", Name = "Hello Line", Knot = "MyMod_Hello" });
/// </code>
/// </summary>
public static class PhoneBook
{
    public const int MaxDigits = 7;

    private static readonly List<PhoneCall> Calls = new List<PhoneCall>();

    public static IReadOnlyList<PhoneCall> All => Calls;

    /// <summary>Raised after calls are added or removed.</summary>
    public static event Action Changed;

    public static void Add(ITracker owner, PhoneCall call)
    {
        if (string.IsNullOrEmpty(call.Id))
            throw new ArgumentException("A call needs an id.");
        if (string.IsNullOrEmpty(call.Knot))
            throw new ArgumentException($"Call '{call.Id}' has no knot to play.");
        if (Find(call.Id) != null)
            throw new ArgumentException($"There's already a call with the id '{call.Id}'.");
        if (call.Number != null)
        {
            call.Number = Digits(call.Number);
            if (call.Number.Length < 3 || call.Number.Length > MaxDigits)
                throw new ArgumentException($"Call '{call.Id}': numbers need 3 to {MaxDigits} digits (the phone's screen fits no more).");
        }
        if (call.Number == null && call.Incoming == null)
            throw new ArgumentException($"Call '{call.Id}' has no number and never rings, so it can't be reached.");

        Calls.Add(call);
        owner.Track(Disposable.Create(() =>
        {
            if (Calls.Remove(call))
                Changed?.Invoke();
        }));
        Changed?.Invoke();
    }

    /// <summary>
    /// Finds a call by id. Short ids ("pizza") are looked up in <paramref name="nearMod"/> first,
    /// then anywhere if only one call has that name.
    /// </summary>
    public static PhoneCall Find(string id, string nearMod = null)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var call in Calls)
        {
            if (string.Equals(call.Id, id, StringComparison.OrdinalIgnoreCase))
                return call;
        }
        if (id.IndexOf('/') >= 0)
            return null;

        if (nearMod != null)
        {
            var local = Find(nearMod + "/" + id);
            if (local != null)
                return local;
        }

        PhoneCall only = null;
        foreach (var call in Calls)
        {
            if (!call.Id.EndsWith("/" + id, StringComparison.OrdinalIgnoreCase))
                continue;
            if (only != null)
                return null;
            only = call;
        }
        return only;
    }

    /// <summary>Calls answering <paramref name="digits"/>, in the order they were added.</summary>
    public static IEnumerable<PhoneCall> WithNumber(string digits)
    {
        foreach (var call in Calls)
        {
            if (call.Number == digits)
                yield return call;
        }
    }

    public static string Digits(string number)
    {
        var sb = new StringBuilder(number.Length);
        foreach (char c in number)
        {
            if (c >= '0' && c <= '9')
                sb.Append(c);
        }
        return sb.ToString();
    }
}
