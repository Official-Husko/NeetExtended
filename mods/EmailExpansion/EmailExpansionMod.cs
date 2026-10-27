using EmailExpansion.Features;
using EmailExpansion.Mail;
using EmailExpansion.Menu;
using EmailExpansion.Packs;
using EmailExpansion.Reader;
using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Events;
using NeetModKit.Features;
using NeetModKit.Menu;
using NeetModKit.Settings;

namespace EmailExpansion;

/// <summary>
/// New emails in the computer's inbox, with replies. Emails come from the "email" folder of any
/// mod (JSON or Ink), or from code through <see cref="MailBook"/>.
/// </summary>
public sealed class EmailExpansionMod : NeetMod
{
    internal static EmailExpansionMod Instance { get; private set; }
    internal static ConfigFile<EmailConfig> Config { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<EmailConfig>();
        Config.Save();
        ModSettings.Register(this, Config, "Email Expansion");

        MailMemory.Init(this);
        MailRules.Register(this);
        MailReader.Init(this);
        MailPacks.Init(this);
        EmailCommands.Register(this);

        ModFeatures.Register(this, new ArrivingMailFeature());
        ModMenu.AddPage(this, new EmailPage());

        GameEvents.HouseReady.Subscribe(this, Mailer.OnHouseReady);
        GameEvents.WokeUp.Subscribe(this, Mailer.OnWake);
        GameEvents.ItemOrdered.Subscribe(this, Mailer.OnOrder);
    }

    // After every mod has loaded, so their email folders are there too.
    public override void OnAllLoaded() => MailPacks.Reload();

    public override void OnUpdate() => Mailer.Tick();

    public override void OnUnload() => Instance = null;
}
