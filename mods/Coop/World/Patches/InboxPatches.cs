using Coop.World.Economy;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>Every email the host's inbox gets goes to the others; see <see cref="InboxSync"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch(typeof(EmailHandler), nameof(EmailHandler.SendEmail))]
internal static class InboxPatches
{
    private static void Postfix(KnotList.Knot mail) => InboxSync.Sent(mail);
}
