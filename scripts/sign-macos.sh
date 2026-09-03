#!/usr/bin/env bash
set -euo pipefail

readonly APP_BUNDLE_NAME="StudyReportEvaluator.app"
readonly EXECUTABLE_NAME="StudyReportEvaluator.App"
readonly REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
readonly ENTITLEMENTS_FILE="$REPOSITORY_ROOT/eng/packaging/macos/StudyReportEvaluator.entitlements"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

[[ $# -eq 1 ]] || fail "usage: $0 <osx-arm64|osx-x64>"
readonly RID="$1"
[[ "$RID" == "osx-arm64" || "$RID" == "osx-x64" ]] || fail "unsupported runtime identifier: $RID"
[[ "$(uname -s)" == "Darwin" ]] || fail "macOS is required"
case "$RID" in
  osx-arm64) readonly EXPECTED_ARCH="arm64" ;;
  osx-x64) readonly EXPECTED_ARCH="x86_64" ;;
esac
readonly HOST_ARCH="$(uname -m)"
[[ "$HOST_ARCH" == "$EXPECTED_ARCH" ]] || fail "native $EXPECTED_ARCH host required for $RID; actual host is $HOST_ARCH"
: "${MACOS_DEVELOPER_ID_APPLICATION:?Set MACOS_DEVELOPER_ID_APPLICATION to the approved Developer ID Application identity}"
for command_name in basename codesign file find head od plutil shasum spctl tr uuidgen; do
  command -v "$command_name" >/dev/null 2>&1 || fail "$command_name is required"
done

readonly IDENTITY="$MACOS_DEVELOPER_ID_APPLICATION"
readonly SOURCE_BUNDLE="$REPOSITORY_ROOT/artifacts/package/macos/$RID/$APP_BUNDLE_NAME"
readonly OUTPUT_ROOT="$REPOSITORY_ROOT/artifacts/package/macos-signed/$RID"
readonly FINAL_BUNDLE="$OUTPUT_ROOT/$APP_BUNDLE_NAME"
readonly RUN_ID="$(uuidgen | tr '[:upper:]' '[:lower:]' | tr -d '-')"
readonly TEMP_BUNDLE="$OUTPUT_ROOT/.$APP_BUNDLE_NAME-$RUN_ID"
readonly MACOS_DIR="$TEMP_BUNDLE/Contents/MacOS"
readonly APPHOST="$MACOS_DIR/$EXECUTABLE_NAME"
readonly CLI="$MACOS_DIR/runtimes/$RID/native/copilot"
readonly MANIFEST="$MACOS_DIR/copilot-runtime.json"
readonly PROVENANCE="$MACOS_DIR/copilot-source-provenance.json"

[[ -d "$SOURCE_BUNDLE/Contents/MacOS" ]] || fail "unsigned app bundle is missing; run package-macos.sh first"
[[ -s "$SOURCE_BUNDLE/Contents/Info.plist" ]] || fail "unsigned app bundle Info.plist is missing or empty"
[[ -s "$SOURCE_BUNDLE/Contents/Resources/StudyReportEvaluator.icns" ]] || fail "unsigned app bundle icon is missing or empty"
[[ -s "$ENTITLEMENTS_FILE" ]] || fail "entitlements plist is missing or empty"
readonly ENTITLEMENTS_PREFIX="$(LC_ALL=C head -c 3 "$ENTITLEMENTS_FILE" | od -An -tx1 | tr -d '[:space:]')"
[[ "$ENTITLEMENTS_PREFIX" != "efbbbf" ]] || fail "entitlements plist must not contain a UTF-8 BOM"
plutil -lint "$ENTITLEMENTS_FILE" >/dev/null
for forbidden_entitlement in \
  com.apple.security.get-task-allow \
  com.apple.security.cs.allow-unsigned-executable-memory \
  com.apple.security.cs.disable-library-validation; do
  if /usr/libexec/PlistBuddy -c "Print :$forbidden_entitlement" "$ENTITLEMENTS_FILE" >/dev/null 2>&1; then
    fail "unapproved entitlement is forbidden: $forbidden_entitlement"
  fi
done
mkdir -p -- "$OUTPUT_ROOT"
rm -rf -- "$TEMP_BUNDLE"
cleanup() {
  rm -rf -- "$TEMP_BUNDLE"
}
trap cleanup EXIT
cp -a "$SOURCE_BUNDLE" "$TEMP_BUNDLE"

assert_thin_macho_executable() {
  local path="$1"
  local description="$2"
  local details
  details="$(file -b "$path")"
  [[ "$details" == "Mach-O 64-bit $EXPECTED_ARCH executable"* ]] || fail "$description is not a thin $EXPECTED_ARCH Mach-O executable: $details"
}

assert_thin_macho_library() {
  local path="$1"
  local description="$2"
  local details
  details="$(file -b "$path")"
  [[ "$details" == "Mach-O 64-bit"* &&
     "$details" == *"$EXPECTED_ARCH"* &&
     "$details" == *"dynamically linked shared library"* &&
     "$details" != *"universal binary"* ]] || fail "$description is not a thin $EXPECTED_ARCH Mach-O shared library: $details"
}

for required in "$APPHOST" "$CLI" "$MANIFEST" "$PROVENANCE"; do
  [[ -s "$required" ]] || fail "required signing input is missing or empty: $required"
done
[[ -s "$TEMP_BUNDLE/Contents/Info.plist" ]] || fail "signing copy Info.plist is missing or empty"
[[ -s "$TEMP_BUNDLE/Contents/Resources/StudyReportEvaluator.icns" ]] || fail "signing copy icon is missing or empty"
plutil -lint "$TEMP_BUNDLE/Contents/Info.plist" >/dev/null
assert_thin_macho_executable "$APPHOST" "apphost"
assert_thin_macho_executable "$CLI" "Copilot CLI"
for native_library in libAvaloniaNative.dylib libHarfBuzzSharp.dylib libSkiaSharp.dylib; do
  [[ -s "$MACOS_DIR/$native_library" ]] || fail "required native library is missing or empty: $native_library"
done
readonly SOURCE_HASH="$(plutil -extract sourceCliSha256 raw -o - "$PROVENANCE")"
readonly PROVENANCE_RID="$(plutil -extract runtimeIdentifier raw -o - "$PROVENANCE")"
readonly PROVENANCE_INTEGRITY="$(plutil -extract npmDistIntegrity raw -o - "$PROVENANCE")"
readonly MANIFEST_RID="$(plutil -extract runtimeIdentifier raw -o - "$MANIFEST")"
readonly MANIFEST_HASH="$(plutil -extract cliSha256 raw -o - "$MANIFEST")"
readonly UNSIGNED_HASH="$(shasum -a 256 "$CLI" | awk '{print toupper($1)}')"
[[ "$PROVENANCE_RID" == "$RID" && "$MANIFEST_RID" == "$RID" ]] || fail "CLI RID metadata mismatch"
[[ "$PROVENANCE_INTEGRITY" == sha512-* ]] || fail "CLI source provenance must contain SHA-512 SRI integrity"
[[ "$SOURCE_HASH" == "$UNSIGNED_HASH" && "$MANIFEST_HASH" == "$UNSIGNED_HASH" ]] || fail "unsigned CLI does not match source provenance and runtime manifest"

while IFS= read -r -d '' dylib; do
  assert_thin_macho_library "$dylib" "$(basename "$dylib")"
  # Runtime entitlements belong to executable code; libraries are signed without them.
  codesign --force --timestamp --options runtime --sign "$IDENTITY" "$dylib"
done < <(find "$MACOS_DIR" -type f -name '*.dylib' -print0)

codesign \
  --force \
  --timestamp \
  --options runtime \
  --entitlements "$ENTITLEMENTS_FILE" \
  --sign "$IDENTITY" \
  "$CLI"
readonly SIGNED_CLI_HASH="$(shasum -a 256 "$CLI" | awk '{print toupper($1)}')"
[[ "$SIGNED_CLI_HASH" =~ ^[0-9A-F]{64}$ ]] || fail "signed CLI SHA-256 is invalid"
plutil -replace cliSha256 -string "$SIGNED_CLI_HASH" "$MANIFEST"
[[ "$(plutil -extract cliSha256 raw -o - "$MANIFEST")" == "$SIGNED_CLI_HASH" ]] || fail "signed CLI hash was not written to the runtime manifest"

codesign \
  --force \
  --timestamp \
  --options runtime \
  --entitlements "$ENTITLEMENTS_FILE" \
  --sign "$IDENTITY" \
  "$APPHOST"
readonly FINAL_MANIFEST_SHA256="$(shasum -a 256 "$MANIFEST" | awk '{print toupper($1)}')"
codesign \
  --force \
  --timestamp \
  --options runtime \
  --entitlements "$ENTITLEMENTS_FILE" \
  --sign "$IDENTITY" \
  "$TEMP_BUNDLE"

codesign --verify --deep --strict --verbose=2 "$TEMP_BUNDLE"
codesign --verify --strict --verbose=2 "$CLI"
codesign --verify --strict --verbose=2 "$APPHOST"
assert_thin_macho_executable "$CLI" "signed Copilot CLI"
assert_thin_macho_executable "$APPHOST" "signed apphost"
[[ -x "$CLI" ]] || fail "signed Copilot CLI is not executable"
[[ -x "$APPHOST" ]] || fail "signed apphost is not executable"
[[ "$(shasum -a 256 "$CLI" | awk '{print toupper($1)}')" == "$SIGNED_CLI_HASH" ]] || fail "CLI changed after outer bundle signing"
[[ "$(shasum -a 256 "$MANIFEST" | awk '{print toupper($1)}')" == "$FINAL_MANIFEST_SHA256" ]] || fail "runtime manifest changed after outer bundle signing"
[[ "$(plutil -extract cliSha256 raw -o - "$MANIFEST")" == "$SIGNED_CLI_HASH" ]] || fail "runtime manifest contains the wrong signed CLI hash"
if spctl --assess --type execute --verbose=4 "$TEMP_BUNDLE"; then
  printf 'Pre-notarization Gatekeeper assessment: PASS\n'
else
  printf 'Pre-notarization Gatekeeper assessment: REJECTED (final acceptance requires P3-04 notarization)\n'
fi

rm -rf -- "$FINAL_BUNDLE"
mv -- "$TEMP_BUNDLE" "$FINAL_BUNDLE"
trap - EXIT
cleanup
printf 'Signed app bundle with finalized CLI manifest: %s\n' "$FINAL_BUNDLE"
printf 'Notarization status: NOT_RUN\n'
