namespace Coop.Net;

/// <summary>What both sides have to agree on before they talk.</summary>
public static class Protocol
{
    /// <summary>Raised whenever a message changes shape. Games with different versions refuse each other.</summary>
    public const int Version = 1;

    /// <summary>Sent with the connection request; the host turns away anything else.</summary>
    public static string ConnectionKey => "NeetExtended-Coop/" + Version;
}
