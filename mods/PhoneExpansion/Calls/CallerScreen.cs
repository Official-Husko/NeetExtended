using System.Collections.Generic;

namespace PhoneExpansion.Calls;

/// <summary>
/// Caller ID on the phones' little screens. The game leaves the screen alone while a call rings,
/// and its buttons treat any text other than the idle text as a number being dialed, so the
/// patches check <see cref="IsShowing"/> before letting a button act on the screen.
/// </summary>
internal static class CallerScreen
{
    public const string Unknown = "UNKNOWN";
    private const int MaxLength = 10;

    private static readonly HashSet<Phone_Interface> Showing = new HashSet<Phone_Interface>();

    /// <summary>Switched by the Caller ID feature.</summary>
    public static bool Enabled { get; set; }

    public static void Show(Phone_Interface phone, string text)
    {
        if (!Enabled || phone == null || phone.composing || phone.calling)
            return;
        text = (text ?? Unknown).ToUpperInvariant();
        phone.screenText.text = text.Length > MaxLength ? text.Substring(0, MaxLength) : text;
        Showing.Add(phone);
    }

    public static bool IsShowing(Phone_Interface phone) => Showing.Contains(phone);

    /// <summary>Puts the idle text back, unless the phone is in a call (it resets itself when that ends).</summary>
    public static void Clear(Phone_Interface phone)
    {
        if (phone == null || !Showing.Contains(phone) || phone.talking)
            return;
        Showing.Remove(phone);
        phone.screenText.text = phone.initText;
    }

    /// <summary>The game reset the screen itself (call ended or cleared).</summary>
    public static void Forget(Phone_Interface phone) => Showing.Remove(phone);

    public static void ClearAll()
    {
        foreach (var phone in new List<Phone_Interface>(Showing))
        {
            if (phone != null && !phone.talking)
                phone.screenText.text = phone.initText;
        }
        Showing.Clear();
    }
}
