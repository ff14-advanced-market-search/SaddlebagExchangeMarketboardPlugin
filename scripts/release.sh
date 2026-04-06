#!/usr/bin/env bash
# One-button release: bump version in csproj + repo.json, commit & push release, tag, then set manifest.toml commit and push.
# Run from repo root. Usage: bash scripts/release.sh 1.0.11

set -e
VERSION="${1:?Usage: $0 X.Y.Z (e.g. 1.0.11)}"

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

if [[ ! -d .git ]]; then
  echo "Error: not a git repo root: $REPO_ROOT" >&2
  exit 1
fi

# Validate version (e.g. 1.0.11)
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Error: version must be X.Y.Z (e.g. 1.0.11). Got: $VERSION" >&2
  exit 1
fi

CSPROJ="$REPO_ROOT/SaddlebagExchange/SaddlebagExchange.csproj"
REPO_JSON="$REPO_ROOT/repo.json"
MANIFEST="$REPO_ROOT/SaddlebagExchange/manifest.toml"

for f in "$CSPROJ" "$REPO_JSON" "$MANIFEST"; do
  if [[ ! -f "$f" ]]; then
    echo "Error: not found: $f" >&2
    exit 1
  fi
done

# 1) Bump version in csproj
if [[ "$(uname -s)" =~ ^(MINGW|MSYS|CYGWIN) ]]; then
  sed -i "s/<AssemblyVersion>[^<]*<\\/AssemblyVersion>/<AssemblyVersion>$VERSION<\\/AssemblyVersion>/" "$CSPROJ"
else
  sed -i.bak "s/<AssemblyVersion>[^<]*<\\/AssemblyVersion>/<AssemblyVersion>$VERSION<\\/AssemblyVersion>/" "$CSPROJ" && rm -f "${CSPROJ}.bak"
fi
echo "Set SaddlebagExchange.csproj AssemblyVersion to $VERSION"

# 2) Bump version and LastUpdated in repo.json
if [[ "$(uname -s)" =~ ^(MINGW|MSYS|CYGWIN) ]]; then
  sed -i "s/\"AssemblyVersion\": \"[^\"]*\"/\"AssemblyVersion\": \"$VERSION\"/" "$REPO_JSON"
  UNIX_NOW=$(date +%s 2>/dev/null || echo "0")
  sed -i "s/\"LastUpdated\": [0-9]*/\"LastUpdated\": $UNIX_NOW/" "$REPO_JSON"
else
  sed -i.bak "s/\"AssemblyVersion\": \"[^\"]*\"/\"AssemblyVersion\": \"$VERSION\"/" "$REPO_JSON" && rm -f "${REPO_JSON}.bak"
  UNIX_NOW=$(date +%s 2>/dev/null || echo "0")
  sed -i.bak "s/\"LastUpdated\": [0-9]*/\"LastUpdated\": $UNIX_NOW/" "$REPO_JSON" && rm -f "${REPO_JSON}.bak"
fi
echo "Set repo.json AssemblyVersion to $VERSION and LastUpdated to $UNIX_NOW"

# 3) Restore manifest.toml so it cannot be accidentally included
#    in the release commit with a stale commit SHA.
git restore "$MANIFEST"
echo "Restored manifest.toml to last committed state (will be updated in a separate commit)"

# 4) Git: stage only version bump files, commit, push release
git add "$CSPROJ" "$REPO_JSON"
git status
git commit -m "Release $VERSION"
git push origin main

# 5) Tag and push tag
git tag "v$VERSION"
git push origin "v$VERSION"

# 6) Capture release commit SHA (this is the code state D17 builds)
COMMIT=$(git -C "$REPO_ROOT" rev-parse HEAD)
echo "Release commit SHA: $COMMIT"

# 7) Set manifest.toml commit to release commit SHA
if ! grep -q "commit =" "$MANIFEST"; then
  echo "Error: no 'commit =' line in $MANIFEST" >&2
  exit 1
fi
if [[ "$(uname -s)" =~ ^(MINGW|MSYS|CYGWIN) ]]; then
  sed -i "s/^commit = .*/commit = \"$COMMIT\"/" "$MANIFEST"
else
  sed -i.bak "s/^commit = .*/commit = \"$COMMIT\"/" "$MANIFEST" && rm -f "${MANIFEST}.bak"
fi
if ! grep -qF "$COMMIT" "$MANIFEST"; then
  echo "Error: manifest was not updated with commit $COMMIT" >&2
  exit 1
fi
echo "Set SaddlebagExchange/manifest.toml commit to $COMMIT"

# 8) Commit and push manifest pointer update
git add "$MANIFEST"
git commit -m "Set manifest commit for $VERSION"
git push origin main

echo ""
echo "Release $VERSION done!"
echo "  Release commit (what D17 builds): $COMMIT"
echo "  Tag: v$VERSION"
echo "  manifest.toml now points to the release commit."
