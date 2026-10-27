namespace CoopSteam;

/// <summary>What a player types to join: a SteamID64, optionally written "steam:7656...".</summary>
internal static class SteamAddress
{
    private const string Prefix = "steam:";

    public static bool TryParse(string text, out ulong steamId)
    {
        steamId = 0;
        if (text == null)
            return false;
        text = text.Trim();
        if (text.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase))
            text = text.Substring(Prefix.Length).Trim();
        if (text.Length != 17)
            return false;
        foreach (char c in text)
        {
            if (c < '0' || c > '9')
                return false;
        }
        // Public universe (1), account type "individual" (1) in the top twelve bits.
        return ulong.TryParse(text, out steamId) && (steamId >> 52) == 0x011;
    }

    public static bool Handles(string text) => TryParse(text, out _);
}
