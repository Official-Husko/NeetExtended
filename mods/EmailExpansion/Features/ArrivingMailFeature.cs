using EmailExpansion.Mail;
using NeetModKit.Features;

namespace EmailExpansion.Features;

/// <summary>Modded emails arriving by themselves.</summary>
internal sealed class ArrivingMailFeature : ModFeature
{
    public override string Id => "ArrivingMail";
    public override string Name => "New emails arrive";
    public override string Description =>
        "New emails show up in the computer's inbox every few minutes, after store orders, when you get home or wake up. " +
        "How often is set in the settings. Emails something else set up (a call, another email) arrive either way.";

    protected override void OnEnable() => Mailer.Enabled = true;

    protected override void OnDisable() => Mailer.Enabled = false;
}
