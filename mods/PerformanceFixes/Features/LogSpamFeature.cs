using NeetModKit.Features;
using PerformanceFixes.Logging;

namespace PerformanceFixes.Features;

/// <summary>See <see cref="Patches.KinematicVelocityPatch"/>, <see cref="Patches.CrouchPrintPatch"/> and <see cref="StackTraces"/>.</summary>
internal sealed class LogSpamFeature : ModFeature
{
    public const string FeatureId = "LogSpam";

    public override string Id => FeatureId;
    public override string Name => "Less log spam";
    public override string Description =>
        "The game writes a warning with a stack trace on every physics step while you're dead, a skeleton or hanging " +
        "(tens of thousands in a session), and a line every time anyone crouches. This stops those and leaves stack " +
        "traces out of ordinary log lines and warnings; errors keep theirs.";

    protected override void OnEnable() => StackTraces.Off();

    protected override void OnDisable() => StackTraces.Restore();
}
