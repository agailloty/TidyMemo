#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "Usage: $0 <publish-dir> <output-dir> <version> <architecture>" >&2
  exit 2
fi

publish_dir="$(cd "$1" && pwd)"
mkdir -p "$2"
output_dir="$(cd "$2" && pwd)"
if [[ "$output_dir" == "/" ]]; then
  echo "Refusing to use the filesystem root as output directory." >&2
  exit 2
fi
version="$3"
architecture="$4"
app_name="TidyMemo"
bundle_id="com.7echnet.TidyMemo"
app_dir="$output_dir/$app_name.app"
contents_dir="$app_dir/Contents"

rm -rf "$app_dir"
mkdir -p "$contents_dir/MacOS" "$contents_dir/Resources"
cp -R "$publish_dir"/. "$contents_dir/MacOS/"
chmod +x "$contents_dir/MacOS/$app_name"

iconset_dir="$output_dir/TidyMemo.iconset"
rm -rf "$iconset_dir"
mkdir -p "$iconset_dir"
for size in 16 32 128 256 512; do
  double_size=$((size * 2))
  sips -z "$size" "$size" TidyMemo/Assets/TidyMemo.png --out "$iconset_dir/icon_${size}x${size}.png" >/dev/null
  sips -z "$double_size" "$double_size" TidyMemo/Assets/TidyMemo.png --out "$iconset_dir/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset_dir" -o "$contents_dir/Resources/TidyMemo.icns"
rm -rf "$iconset_dir"

cat > "$contents_dir/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleDisplayName</key><string>$app_name</string>
  <key>CFBundleExecutable</key><string>$app_name</string>
  <key>CFBundleIdentifier</key><string>$bundle_id</string>
  <key>CFBundleIconFile</key><string>TidyMemo.icns</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>$app_name</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>CFBundleVersion</key><string>$version</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
EOF

# An ad-hoc signature preserves bundle integrity but does not establish a trusted
# publisher identity. Developer ID signing/notarization can replace this step later.
codesign --force --deep --sign - "$app_dir"
codesign --verify --deep --strict "$app_dir"

zip_path="$output_dir/TidyMemo-macos-$architecture.zip"
dmg_path="$output_dir/TidyMemo-macos-$architecture.dmg"
ditto -c -k --sequesterRsrc --keepParent "$app_dir" "$zip_path"
dmg_root="$output_dir/dmg-root"
rm -rf "$dmg_root"
mkdir -p "$dmg_root"
cp -R "$app_dir" "$dmg_root/"
ln -s /Applications "$dmg_root/Applications"
hdiutil create -quiet -volname "$app_name" -srcfolder "$dmg_root" -ov -format UDZO "$dmg_path"
rm -rf "$dmg_root"

echo "Created $zip_path"
echo "Created $dmg_path"
