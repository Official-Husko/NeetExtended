using System;

namespace Coop;

/// <summary>
/// Frame hooks for the parts of the mod, called by <see cref="CoopMod"/> after the network has been
/// polled, so handlers see this frame's messages. <see cref="Unloading"/> is the moment to destroy
/// anything left in the scene (the session may carry on in the next copy of the mod).
/// </summary>
internal static class CoopLoop
{
    public static event Action Update;
    public static event Action LateUpdate;
    public static event Action FixedUpdate;
    public static event Action Unloading;

    internal static void RunUpdate() => Update?.Invoke();
    internal static void RunLateUpdate() => LateUpdate?.Invoke();
    internal static void RunFixedUpdate() => FixedUpdate?.Invoke();

    internal static void RunUnloading()
    {
        try
        {
            Unloading?.Invoke();
        }
        finally
        {
            Update = null;
            LateUpdate = null;
            FixedUpdate = null;
            Unloading = null;
        }
    }
}
