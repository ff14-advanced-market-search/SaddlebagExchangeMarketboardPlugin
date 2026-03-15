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

**If install fails:** Restart the game and try again. If it still fails, check the Dalamud log (XIVLauncher → Settings → open the log/data folder, or `%AppData%\XIVLauncher\log`) for the exact error (e.g. download failure, invalid zip, or missing manifest). The release zip has **SaddlebagExchange.dll**, **manifest.json**, and **icon.png** at the archive root (no nested folder).

## One-button release

From repo root, run one of (replace `1.0.11` with your version):

```bash
bash scripts/release.sh 1.0.11
```

Or PowerShell: `.\scripts\release.ps1 1.0.11`

The script will:

1. Bump **version** in `SaddlebagExchange/SaddlebagExchange.csproj` (`AssemblyVersion`) and `repo.json` (`AssemblyVersion` + `LastUpdated` timestamp).
2. **Git:** `git add -A`, `git status`, `git commit -m "Release X.Y.Z"`, `git push origin main`.
3. **Tag:** `git tag vX.Y.Z`, `git push origin vX.Y.Z`.
4. Set **`SaddlebagExchange/manifest.toml`** `commit` to the release commit hash, then commit and push that change.

After the script finishes, **GitHub Actions** runs the Release workflow. Check **Actions** on the repo; when the workflow is green, the **Releases** page will have the new release and **SaddlebagExchange.zip**.

## Creating a release (manual)

For reference, the manual steps the one-button script performs:

1. **Commit and push** all changes (workflow, repo.json, project, code).
2. **Set version** in `SaddlebagExchange/SaddlebagExchange.csproj` → `AssemblyVersion` (e.g. `1.0.0`). Optionally update `repo.json` → `AssemblyVersion` and `LastUpdated` (Unix timestamp) so the plugin list shows the right version. API level is derived from the SDK in the project; when you upgrade the Dalamud SDK (e.g. to 15.x), update `repo.json` → `DalamudApiLevel` to match the SDK major version.

   **When bumping version, update:**
   - `SaddlebagExchange/SaddlebagExchange.csproj` → `<AssemblyVersion>X.Y.Z</AssemblyVersion>` (source of truth; manifest is generated from this).
   - `repo.json` → `"AssemblyVersion": "X.Y.Z"` (and optionally `"LastUpdated": <Unix timestamp>` so the plugin list shows the new version).
3. **Create and push the tag** (use the same version number):
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
4. **GitHub Actions** runs the Release workflow. Check **Actions** on the repo; when the workflow is green, the **Releases** page will have the new release and **SaddlebagExchange.zip**. The “latest” download URL will then serve that zip for remote install.

**Commands to run for a version bump release** (from repo root; replace `1.0.7` and `main` as needed):

```bash
git add -A
git status
git commit -m "Release 1.0.7"
git push origin main
git tag v1.0.7
git push origin v1.0.7
```

5. Set **manifest.toml** `commit` to the release commit hash: run `scripts/update-manifest-commit.ps1` or `scripts/update-manifest-commit.sh` (after the release commit is pushed, so HEAD is the release), then commit and push the manifest change.

## D17 submission (manifest.toml)

To submit the plugin to the **official Dalamud plugin repo** ([DalamudPluginsD17](https://github.com/GoatCorp/DalamudPluginsD17)), you use the **manifest.toml** file.

- **Location:** `SaddlebagExchange/manifest.toml`
- **Purpose:** Tells the D17 repo where the plugin lives, who maintains it, and which commit to build. One PR = one plugin; new plugins go to the **testing/live** track (not stable).
- **Before opening your PR:**
  1. Set **`commit`** to the **exact full commit hash** of the version you are submitting. You can run the helper script from repo root:

     ```bash
     bash scripts/update-manifest-commit.sh
     ```

     Or PowerShell: `.\scripts\update-manifest-commit.ps1`  
     This writes the current `git rev-parse HEAD` into `SaddlebagExchange/manifest.toml`. Leave `commit` empty only while developing; the D17 build will fail without a valid commit.
  2. Update **`changelog`** if you’re submitting a new version.
  3. Keep **`owners`** as the list of GitHub usernames that maintain the plugin (e.g. `["cohenaj194"]`).
- **What to include in the PR:** The folder you add must follow the required layout. Add the **`SaddlebagExchange/`** folder with:
  - `manifest.toml` (with **`commit`** set to the full SHA — D17 build fails if empty).
  - **`images/icon.png`** — required; the icon lives at `SaddlebagExchange/images/icon.png` in this repo (same file used for the plugin in `/xlplugins`). Icon must be square 64×64–512×512 px (recommended 512×512). Optional: `images/image1.png`…`image5.png` for screenshots.
  The D17 layout is `testing/live/SaddlebagExchange/manifest.toml` and `testing/live/SaddlebagExchange/images/icon.png`.

The **repo.json** at the repo root is only for **custom plugin repos** (e.g. the install URL in “Install (for players)”). The D17 build system does **not** use repo.json; it uses **manifest.toml** and builds from the GitHub repo and commit you specify there.

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

**Why .NET 10?** The project targets `net10.0` to match your XIVLauncher/Dalamud dev hooks (Dalamud 14 is built for .NET 10). Targeting `net9.0` would require a .NET 9–compatible Dalamud (e.g. in CI or when the ecosystem moves).

---

## Project layout

```
ffxiv-plugin/
├── SaddlebagExchange/
│   ├── SaddlebagExchange.csproj
│   ├── Plugin.cs
│   ├── manifest.toml          ← for D17 official repo submission (see "D17 submission" above)
│   └── ... (manifest.json is generated to bin/<Configuration>/net10.0/)
├── repo.json                  ← for custom repo install (players); not used by D17
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

2. Either build once (post-build will copy files) or copy manually from `SaddlebagExchange/bin/Debug/net10.0/`:
   - `SaddlebagExchange.dll`
   - `manifest.json`
   - `icon.png` (optional)

## Load in game

1. Launch FFXIV via **XIVLauncher**.
2. In-game chat: `/xlplugins`
3. Add the dev plugins folder: `%AppData%\XIVLauncher\devPlugins`
4. Enable **Saddlebag Exchange**.

---

## Roadmap

- Getting all the tools in there... wip...
