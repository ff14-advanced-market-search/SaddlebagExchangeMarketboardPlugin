# Saddlebag Exchange

FFXIV Dalamud plugin for marketboard analytics and cross-world arbitrage (TSM-style for FFXIV).

## Prerequisites

### Install .NET 8 SDK

You need the .NET 8 SDK to build. Install one of these ways:

**Option A – Download (recommended)**  
- [.NET 8 SDK – Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)  
- Run the installer, then open a **new** terminal and run: `dotnet --version` (should show 8.x.x).

**Option B – Winget**  
```bash
winget install Microsoft.DotNet.SDK.8
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

From the repo root:

```bash
cd SaddlebagExchange
dotnet build
```

Output:

- `SaddlebagExchange/bin/Debug/net8.0/SaddlebagExchange.dll`
- (Manifest is already in `SaddlebagExchange/manifest.json`.)

## Install as dev plugin

1. Create the dev plugin folder (if it doesn’t exist):
   ```
   %AppData%\XIVLauncher\devPlugins\SaddlebagExchange
   ```
   Example: `C:\Users\<You>\AppData\Roaming\XIVLauncher\devPlugins\SaddlebagExchange`

2. Copy into that folder:
   - `SaddlebagExchange/bin/Debug/net8.0/SaddlebagExchange.dll`
   - `SaddlebagExchange/manifest.json`

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
