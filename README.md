# Saddlebag Exchange

FFXIV Dalamud plugin for marketboard analytics and cross-world arbitrage (TSM-style for FFXIV).

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
```

You should see a window: **Saddlebag Exchange** with “Market tools coming soon.”

---

## Roadmap

- Universalis API integration  
- Item search  
- Cross-world price comparison  
- Flip scanner (buy world / price vs sell world / profit)
