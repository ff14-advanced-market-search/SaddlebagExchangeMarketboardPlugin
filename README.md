# Saddlebag Exchange

FFXIV Dalamud plugin for marketboard trading and gil making!

The [Saddlebag Exchange](https://saddlebagexchange.com/ffxiv) website but now in a plugin!

## Install (for players)

[Setup with pictures](https://github.com/ff14-advanced-market-search/SaddlebagExchangeMarketboardPlugin/wiki/Install-with-pictures)

Install via Dalamud’s custom repo using the `repo.json` feed:

1. Open **XIVLauncher** / **Dalamud** → **Settings** → **Experimental**.
2. Under **Custom plugin repositories**, add this URL and enable the repo:
   ```
   https://raw.githubusercontent.com/ff14-advanced-market-search/SaddlebagExchangeMarketboardPlugin/main/repo.json
   ```
3. Open the **Plugin Installer** (e.g. from the launcher or in-game `/xlplugins`).
4. Find **Saddlebag Exchange** in the list and click **Install**.

Slash command to open window:

```
/sbex
/saddlebagexchange
```

## Notes, logs, how to fix errors, etc...

Updates are delivered through the same repo; the installer will use the latest release from the [releases page](https://github.com/ff14-advanced-market-search/SaddlebagExchangeMarketboardPlugin/releases).

**If install fails:** Restart the game and try again. If it still fails, check the Dalamud log (XIVLauncher → Settings → open the log/data folder, or `%AppData%\XIVLauncher\log`) for the exact error (e.g. download failure, invalid zip, or missing manifest). The release zip should contain a single folder `SaddlebagExchange` with `SaddlebagExchange.dll`, `manifest.json`, and `icon.png` inside it.

## Creating a release

1. **Commit and push** all changes (workflow, repo.json, project, code).
2. **Set version** in `SaddlebagExchange/SaddlebagExchange.csproj` → `AssemblyVersion` (e.g. `1.0.0`). Optionally update `repo.json` → `AssemblyVersion` and `LastUpdated` (Unix timestamp) so the plugin list shows the right version. API level is derived from the SDK in the project; when you upgrade the Dalamud SDK (e.g. to 15.x), update `repo.json` → `DalamudApiLevel` to match the SDK major version.
3. **Create and push the tag** (use the same version number):
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
4. **GitHub Actions** runs the Release workflow. Check **Actions** on the repo; when the workflow is green, the **Releases** page will have the new release and **SaddlebagExchange.zip**. The “latest” download URL will then serve that zip for remote install.

Quick first release (from repo root):

```bash
git add -A && git commit -m "Release 1.0.0" && git push origin main
git tag v1.0.0
git push origin v1.0.0
```

(Replace `main` with your default branch if different.)

## Prerequisites

### Install .NET 10 SDK

You need the .NET 10 SDK to build. Install one of these ways:

**Option A – Download (recommended)**  
- [.NET 10 SDK – Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)  
- Run the installer, then open a **new** terminal and run: `dotnet --version` (should show 10.x.x).

**Option B – Winget**  
```bash
winget install Microsoft.DotNet.SDK.10
```

Then close and reopen your terminal so `dotnet` is on PATH.

---

## Project layout

```
ffxiv-plugin/
├── SaddlebagExchange/
│   ├── SaddlebagExchange.csproj
│   ├── Plugin.cs
│   └── manifest.json
└── README.md
```

## Build

From the repo root (`ffxiv-plugin`):

```bash
cd SaddlebagExchange
dotnet build
```

If `dotnet` is not on your PATH (e.g. in some terminals or CI), use the full path:

```bash
"C:\Program Files\dotnet\dotnet.exe" build "SaddlebagExchange\SaddlebagExchange.csproj"
```

(Run from the `ffxiv-plugin` folder, or use the full path to the `.csproj`.)

Output:

- `SaddlebagExchange/bin/Debug/net10.0/SaddlebagExchange.dll`
- On **Debug** build, the project’s post-build step **copies** the DLL (and `manifest.json`, `icon.png`) into:
  - `%AppData%\XIVLauncher\devPlugins\SaddlebagExchange`

So you don’t need to copy files manually for dev; just build and reload the plugin in-game.

## Rebuild after code changes

1. **Build** (from `ffxiv-plugin`):
   ```bash
   cd SaddlebagExchange
   dotnet build
   ```
   Or with full path to dotnet:
   ```bash
   "C:\Program Files\dotnet\dotnet.exe" build "c:\Users\cohen\AppData\Roaming\XIVLauncher\devPlugins\ffxiv-plugin\SaddlebagExchange\SaddlebagExchange.csproj"
   ```

2. **Reload the plugin in-game** so the new DLL is loaded:
   - `/xlplugins` → disable **Saddlebag Exchange**, then enable it again, or  
   - Restart the game.

No need to copy the DLL by hand; the post-build copy handles it.

## Install as dev plugin (first-time setup)

1. Create the dev plugin folder (if it doesn’t exist):
   ```
   %AppData%\XIVLauncher\devPlugins\SaddlebagExchange
   ```
   Example: `C:\Users\<You>\AppData\Roaming\XIVLauncher\devPlugins\SaddlebagExchange`

2. Either build once (post-build will copy files) or copy manually:
   - `SaddlebagExchange/bin/Debug/net10.0/SaddlebagExchange.dll`
   - `SaddlebagExchange/manifest.json`
   - `SaddlebagExchange/Assets/icon.png` (optional)

## Load in game

1. Launch FFXIV via **XIVLauncher**.
2. In-game chat: `/xlplugins`
3. Add the dev plugins folder: `%AppData%\XIVLauncher\devPlugins`
4. Enable **Saddlebag Exchange**.

---

## Roadmap

- Getting all the tools in there... wip...
