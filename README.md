# Saddlebag Exchange

FFXIV Dalamud plugin for marketboard analytics and cross-world arbitrage (TSM-style for FFXIV).

## Install (for players)

Install via Dalamud’s custom repo using the `repo.json` feed:

1. Open **XIVLauncher** / **Dalamud** → **Settings** → **Experimental**.
2. Under **Custom plugin repositories**, add this URL and enable the repo:
   ```
   https://raw.githubusercontent.com/ff14-advanced-market-search/SaddlebagExchangeMarketboardPlugin/main/repo.json
   ```
3. Open the **Plugin Installer** (e.g. from the launcher or in-game `/xlplugins`).
4. Find **Saddlebag Exchange** in the list and click **Install**.

Updates are delivered through the same repo; the installer will use the latest release from the [releases page](https://github.com/ff14-advanced-market-search/SaddlebagExchangeMarketboardPlugin/releases).

## Creating a release

1. **Commit and push** all changes (workflow, repo.json, manifest, code).
2. **Set version** in `SaddlebagExchange/manifest.json` → `AssemblyVersion` (e.g. `"1.0.0"`). Optionally update `repo.json` → `AssemblyVersion` and `LastUpdated` (Unix timestamp) so the plugin list shows the right version.
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

## Test

In-game chat:

```
/saddlebag
/sbex
/saddlebagexchange
```

You should see a window: **Saddlebag Exchange** with “Market tools coming soon.”

---

## Roadmap

- Universalis API integration  
- Item search  
- Cross-world price comparison  
- Flip scanner (buy world / price vs sell world / profit)
