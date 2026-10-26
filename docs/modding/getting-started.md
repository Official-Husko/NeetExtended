# Getting started with NEET Demo mods

This guide takes you from a clean checkout to a mod that loads in the game. It covers building the repo, installing the loader, the edit-build-reload loop, and a first mod. For the full reference (manifest fields, lifecycle, hot reload, logging) see [loader.md](loader.md).

## Prerequisites

- **.NET 10 SDK.** The tool (`tools/NeetTool`) targets `net10.0`. Mods target `net472`, which is restored from NuGet, so the first build needs network access.
- **NEET Demo installed** through Steam (app 2054460). The game runs under Proton on Linux; no launch options are needed.
- **The game folder.** On Linux the default is `$HOME/.local/share/Steam/steamapps/common/NEET Demo`. If your library is somewhere else, point the build at it in one of two ways:

  Environment variable:

  ```sh
  export NEET_GAME_DIR="/mnt/games/SteamLibrary/steamapps/common/NEET Demo"
  ```

  Or a per-machine file, `build/Local.props` (gitignored, imported by the root `Directory.Build.props` before the default path is chosen):

  ```xml
  <Project>
    <PropertyGroup>
      <NeetGameDir>/mnt/games/SteamLibrary/steamapps/common/NEET Demo</NeetGameDir>
    </PropertyGroup>
  </Project>
  ```

## Build

From the repo root:

```sh
dotnet build NeetMods.sln -c Release
```

The build deploys straight into the game folder when `<game>/NEET_Data/Managed` exists:

- `NeetLoader.dll`, `0Harmony.dll` and `Mono.Cecil.dll` go to `<game>/NEET_Data/Managed/`.
- Each mod project copies its output and `mod.json` to `<game>/Mods/<ModId>/`. `assets/**` is included in the output too.
- The framework, `NeetModKit`, is a mod like any other and lands in `<game>/Mods/NeetModKit/`.

If the game folder is missing, nothing is deployed and the build still succeeds.

## Install the loader

The loader is not a proxy DLL. `neet install` copies the loader into the game's managed folder and adds it to two JSON files that Unity reads at startup, so Unity calls `NeetLoader.Entry.Init` before the first scene.

```sh
dotnet run --project tools/NeetTool -- install
# or, after a build:
tools/NeetTool/bin/Release/net10.0/neet install
```

The install:

- copies `NeetLoader.dll`, `NeetLoader.pdb`, `0Harmony.dll` and `Mono.Cecil.dll` from the tool's payload into `NEET_Data/Managed`;
- adds `NeetLoader.dll` to `NEET_Data/ScriptingAssemblies.json` and an `Entry.Init` call to `NEET_Data/RuntimeInitializeOnLoads.json`. The first time, the originals are saved next to them as `*.neetbak`;
- creates `<game>/Mods`.

Use `--game DIR` if the tool can't find the game. If the game is running, the changes take effect after a restart. `neet uninstall` reverses the install; mods stay unless you pass `--remove-mods`.

Mod builds don't need `neet install` again. Every solution build updates `NeetLoader.dll` in `NEET_Data/Managed`, but a changed loader only takes effect after a restart.

## Launch and check

```sh
neet launch              # starts the game through Steam; windowed by default
neet launch --direct     # starts the executable without the Steam client
neet status              # install state, installed mods, whether the game is up
neet logs                # end of <game>/NeetLoader/logs/latest.log
```

## The in-game mod menu

Press **F1** in game (the key is `MenuKey` in NeetModKit's settings). The same menu is also reachable from **MODS** in the pause menu, which takes the place of the feedback form.

The Mods page lists every mod the loader knows about, with its state and error text, and offers:

| Control | What it does |
| --- | --- |
| `LOAD NOW` / `UNLOAD` / `RELOAD` | Act on the selected mod immediately. `RELOAD` appears only for hot-reload mods. |
| Load when the game starts | Saves the mod's on/off switch. It applies on the next launch. |
| `RELOAD ALL` | Reloads every loaded hot-reload mod. |
| `FIND NEW MODS` | Looks for mod folders added since startup. They are listed but not loaded. |
| Reload mods when their files change | The auto-reload switch. Saved to `settings.json`. |

## The edit, build, reload loop

Once a mod has `"hotReload": true` in its `mod.json`, you can iterate without restarting the game:

1. Launch the game with the mod loaded.
2. Change code and build: `dotnet build mods/MyFirstMod/MyFirstMod.csproj -c Release`.
3. The new DLL lands in `<game>/Mods/MyFirstMod/`. The loader polls DLL timestamps once a second and reloads the mod after its files have stopped changing for one poll, so expect a delay of one to two seconds.
4. Check the Mods page or `neet logs`.

Things that do not reload and need a restart: the loader itself (`NeetLoader.dll`, Harmony and Cecil in `NEET_Data/Managed`) and any mod without `hotReload`. Editing `mod.json` by itself does not trigger a reload; the watcher looks only at DLL timestamps. The new manifest is read on the next reload, whatever triggered it.

## Your first mod

This walks through a mod called `MyFirstMod`. It logs a line when the pause menu's feedback form is sent. Each step uses the same layout as `mods/NoFeedback/`, so you can compare the files with it.

### 1. Create the folder and project

```text
mods/MyFirstMod/
  MyFirstMod.csproj
  mod.json
  MyFirstModEntry.cs
  Patches/
    SendFeedbackLogPatch.cs
```

Copy `mods/NoFeedback/NoFeedback.csproj` to `mods/MyFirstMod/MyFirstMod.csproj` and change `RootNamespace` and `Description`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>MyFirstMod</RootNamespace>
    <Description>Logs when the feedback form is sent.</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/NeetLoader/NeetLoader.csproj" />
    <ProjectReference Include="../../src/NeetModKit/NeetModKit.csproj" />
  </ItemGroup>
</Project>
```

What the project gets for free:

- `mods/Directory.Build.props` imports `build/Game.props`, which sets `net472`, adds Harmony as a compile-only reference, and references the game's assemblies (`Assembly-CSharp`, `UnityEngine`, and so on) with `Private="false"`, so none of them are copied.
- `mods/Directory.Build.targets` imports `build/Mod.targets`, which copies `mod.json` and `assets/**` to the output and deploys the build to `<game>/Mods/<AssemblyName>/`.
- `ProjectReference` to the framework is also `Private="false"`. The framework is loaded from its own folder at runtime, so only your DLL goes into the mod folder.

If your mod uses only `NeetLoader`, drop the `NeetModKit` reference and the matching dependency below.

Add the project to the solution:

```sh
dotnet sln NeetMods.sln add mods/MyFirstMod/MyFirstMod.csproj -s mods
```

### 2. Write the manifest

`mods/MyFirstMod/mod.json`:

```json
{
  "id": "MyFirstMod",
  "name": "My First Mod",
  "version": "0.1.0",
  "author": "your name",
  "description": "Logs when the feedback form is sent.",
  "dependencies": { "NeetModKit": "0.1.0" },
  "hotReload": true
}
```

Keep the `id`, the project's `AssemblyName` and the folder name the same. The build deploys to `<game>/Mods/<AssemblyName>/`, and other mods depend on the `id`. `hotReload` is optional and defaults to `false`; setting it to `true` lets you rebuild without restarting. The full field list is in [loader.md](loader.md#modjson-fields).

### 3. Write the entry class

`mods/MyFirstMod/MyFirstModEntry.cs`:

```csharp
using NeetLoader;

namespace MyFirstMod;

public sealed class MyFirstModEntry : NeetMod
{
    internal static MyFirstModEntry Instance { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Log.Info("Loaded.");
    }

    public override void OnUnload() => Instance = null;
}
```

The loader creates one instance of the `NeetMod` subclass per load. `Log` is a `ModLogger` tagged with the mod id. `OnUnload` runs before the instance is discarded, which is where you clear anything the mod exposes.

### 4. Add a Harmony patch

`mods/MyFirstMod/Patches/SendFeedbackLogPatch.cs`:

```csharp
using HarmonyLib;

namespace MyFirstMod.Patches;

[HarmonyPatch(typeof(Feedback_Sender), nameof(Feedback_Sender.SendFeedback))]
internal static class SendFeedbackLogPatch
{
    private static void Postfix() => MyFirstModEntry.Instance?.Log.Info("SendFeedback ran.");
}
```

The loader patches every class in the mod's assemblies that carries a `[HarmonyPatch]` attribute, so there is nothing to call. `Feedback_Sender` is a game type; any public method in `decompiled/Assembly-CSharp/` works as a target. Check the signature there before you write the patch. The attribute and the `Postfix` signature must match it.

### 5. Build

```sh
dotnet build mods/MyFirstMod/MyFirstMod.csproj -c Release
```

The output appears in `<game>/Mods/MyFirstMod/`:

```text
<game>/Mods/MyFirstMod/
  MyFirstMod.dll
  MyFirstMod.pdb
  mod.json
```

If the folder doesn't appear, check that `<game>/NEET_Data/Managed` exists. Without it the build skips deployment silently.

### 6. See it in game

- **Game closed:** `neet launch`. Press F1 and look for "My First Mod" on the Mods page, marked as loaded. `neet logs` shows a line ending in `[Info] [MyFirstMod] Loaded.`.
- **Game running:** a folder added while the game runs is not loaded automatically. Press `FIND NEW MODS` on the Mods page. It is listed as off, so select it and press `LOAD NOW`.

After that, edit `SendFeedbackLogPatch.cs` or `MyFirstModEntry.cs`, rebuild, and watch the log line change. Open the pause menu and submit the feedback form to see the patch fire.

Next: [loader.md](loader.md) covers the rest of the mod API, including `Track`, `LoadConfig`, `ModRegistry`, and how hot reload handles state.
