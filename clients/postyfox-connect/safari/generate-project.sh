#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"
extension_dir="$script_dir/../browser-extension"
project_dir="$script_dir/generated"

xcrun safari-web-extension-converter "$extension_dir" \
  --project-location "$project_dir" \
  --app-name "PostyFox Connect" \
  --bundle-identifier "net.postyfox.connect" \
  --swift \
  --copy-resources \
  --no-open \
  --force

echo "Generated the Safari containing-app project under $project_dir"
