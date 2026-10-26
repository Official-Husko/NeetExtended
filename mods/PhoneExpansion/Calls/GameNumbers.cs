using System;

namespace PhoneExpansion.Calls;

/// <summary>
/// The numbers the game itself answers (Phone_Interface.Start). Each plays the knot "Phone.p" + digits
/// in the game's story; random incoming calls play "Phone.pPrank1" to "Phone.pPrank11".
/// </summary>
public static class GameNumbers
{
    public const string KnotPrefix = "Phone.p";
    public const string PrankKnotPrefix = "Phone.pPrank";

    public static readonly string[] All =
    {
        "411", "911", "999", "4206969", "1111111", "6666666", "3333333", "3141592", "5552368", "5555555",
        "5551234", "4279683", "9829643", "6686262", "5551337",
    };

    public static bool Contains(string digits) => Array.IndexOf(All, digits) >= 0;
}
