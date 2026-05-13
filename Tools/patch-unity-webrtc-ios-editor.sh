#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_CACHE="$ROOT_DIR/Library/PackageCache"

if [[ ! -d "$PACKAGE_CACHE" ]]; then
  echo "Unity package cache not found: $PACKAGE_CACHE"
  exit 1
fi

WEBRTC_FILES=()
while IFS= read -r file; do
  WEBRTC_FILES+=("$file")
done < <(find "$PACKAGE_CACHE" -path "*/com.unity.webrtc@*/Runtime/Scripts/WebRTC.cs" -type f)

if [[ "${#WEBRTC_FILES[@]}" -eq 0 ]]; then
  echo "Unity WebRTC package cache file not found. Open the project in Unity first so Package Manager resolves com.unity.webrtc."
  exit 1
fi

for file in "${WEBRTC_FILES[@]}"; do
  if grep -q "#if UNITY_IOS && !UNITY_EDITOR" "$file"; then
    echo "Already patched: $file"
    continue
  fi

  if ! grep -q "#if UNITY_IOS" "$file"; then
    echo "Could not find expected UNITY_IOS block in: $file"
    exit 1
  fi

  perl -0pi -e 's/#if UNITY_IOS\n        internal const string Lib = "__Internal";/#if UNITY_IOS \&\& !UNITY_EDITOR\n        internal const string Lib = "__Internal";/' "$file"
  echo "Patched: $file"
done
