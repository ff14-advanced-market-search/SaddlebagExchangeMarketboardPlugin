#!/usr/bin/env bash
# Updates SaddlebagExchange/manifest.toml with the current git commit hash.
# Run from repo root before opening a D17 PR or when cutting a release.
set -e
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MANIFEST="$REPO_ROOT/SaddlebagExchange/manifest.toml"
COMMIT="$(git -C "$REPO_ROOT" rev-parse HEAD)"
if [[ ! -f "$MANIFEST" ]]; then
  echo "Error: $MANIFEST not found. Run from repo root or ensure SaddlebagExchange/manifest.toml exists." >&2
  exit 1
fi
if [[ "$(uname -s)" =~ ^MINGW|MSYS|CYGWIN ]]; then
  sed -i "s/^commit = .*/commit = \"$COMMIT\"/" "$MANIFEST"
else
  sed -i.bak "s/^commit = .*/commit = \"$COMMIT\"/" "$MANIFEST" && rm -f "${MANIFEST}.bak"
fi
echo "Set SaddlebagExchange/manifest.toml commit to $COMMIT"
