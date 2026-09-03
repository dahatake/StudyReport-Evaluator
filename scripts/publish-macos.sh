#!/usr/bin/env bash
set -euo pipefail

readonly TARGET_FRAMEWORK="net10.0"
readonly APP_NAME="StudyReportEvaluator.App"
readonly REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
readonly PROJECT_PATH="$REPOSITORY_ROOT/src/StudyReportEvaluator.App/StudyReportEvaluator.App.csproj"
readonly PUBLISH_ROOT="$REPOSITORY_ROOT/artifacts/package/publish"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

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

[[ $# -eq 1 ]] || fail "usage: $0 <osx-arm64|osx-x64>"
readonly RID="$1"
case "$RID" in
  osx-arm64)
    readonly EXPECTED_ARCH="arm64"
    readonly EXPECTED_NPM_PACKAGE="@github/copilot-darwin-arm64"
    ;;
  osx-x64)
    readonly EXPECTED_ARCH="x86_64"
    readonly EXPECTED_NPM_PACKAGE="@github/copilot-darwin-x64"
    ;;
  *) fail "unsupported runtime identifier: $RID" ;;
esac

[[ "$(uname -s)" == "Darwin" ]] || fail "macOS is required; cross-publish is not accepted as launch evidence"
readonly HOST_ARCH="$(uname -m)"
[[ "$HOST_ARCH" == "$EXPECTED_ARCH" ]] || fail "native $EXPECTED_ARCH host required for $RID; actual host is $HOST_ARCH"
command -v dotnet >/dev/null 2>&1 || fail "dotnet selected by global.json is required"
command -v shasum >/dev/null 2>&1 || fail "shasum is required"
command -v file >/dev/null 2>&1 || fail "file is required"
command -v plutil >/dev/null 2>&1 || fail "plutil is required"

readonly SDK_VERSION="$(dotnet --version)"
readonly REQUIRED_SDK="$(plutil -extract sdk.version raw -o - "$REPOSITORY_ROOT/global.json")"
readonly ROLL_FORWARD="$(plutil -extract sdk.rollForward raw -o - "$REPOSITORY_ROOT/global.json")"
readonly ALLOW_PRERELEASE="$(plutil -extract sdk.allowPrerelease raw -o - "$REPOSITORY_ROOT/global.json")"
[[ "$ROLL_FORWARD" == "latestPatch" && "$ALLOW_PRERELEASE" == "false" ]] || fail "global.json must select non-prerelease latestPatch roll-forward"
[[ "$REQUIRED_SDK" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]] || fail "global.json SDK version is invalid"
readonly REQUIRED_MAJOR="${BASH_REMATCH[1]}"
readonly REQUIRED_MINOR="${BASH_REMATCH[2]}"
readonly REQUIRED_PATCH="${BASH_REMATCH[3]}"
readonly REQUIRED_BAND="$((REQUIRED_PATCH - REQUIRED_PATCH % 100))"
[[ "$SDK_VERSION" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]] || fail "selected SDK version is invalid: $SDK_VERSION"
readonly ACTUAL_MAJOR="${BASH_REMATCH[1]}"
readonly ACTUAL_MINOR="${BASH_REMATCH[2]}"
readonly ACTUAL_PATCH="${BASH_REMATCH[3]}"
readonly ACTUAL_BAND="$((ACTUAL_PATCH - ACTUAL_PATCH % 100))"
[[ "$ACTUAL_MAJOR" == "$REQUIRED_MAJOR" && "$ACTUAL_MINOR" == "$REQUIRED_MINOR" && "$ACTUAL_BAND" == "$REQUIRED_BAND" && "$ACTUAL_PATCH" -ge "$REQUIRED_PATCH" ]] || fail "selected SDK $SDK_VERSION is incompatible with global.json $REQUIRED_SDK latestPatch"

readonly RUN_ID="$(uuidgen | tr '[:upper:]' '[:lower:]' | tr -d '-')"
readonly TEMP_DIR="$PUBLISH_ROOT/.$RID-$RUN_ID"
readonly FINAL_DIR="$PUBLISH_ROOT/$RID"
readonly TEMP_LOCK_RELATIVE="obj/P102/$RUN_ID/packages.$RID.lock.json"
readonly CORE_LOCK="$REPOSITORY_ROOT/src/StudyReportEvaluator.Core/packages.lock.json"
readonly APP_LOCK="$REPOSITORY_ROOT/src/StudyReportEvaluator.App/packages.lock.json"
readonly CORE_RID_LOCK="$REPOSITORY_ROOT/src/StudyReportEvaluator.Core/$TEMP_LOCK_RELATIVE"
readonly APP_RID_LOCK="$REPOSITORY_ROOT/src/StudyReportEvaluator.App/$TEMP_LOCK_RELATIVE"
readonly LOCK_VALIDATOR="$REPOSITORY_ROOT/scripts/validate-rid-lock.cs"
readonly CLI_ACQUIRER="$REPOSITORY_ROOT/scripts/acquire-copilot-cli-macos.sh"
readonly CLI_SOURCE_DIR="$PUBLISH_ROOT/.$RID-cli-$RUN_ID"
readonly CORE_LOCK_HASH="$(shasum -a 256 "$CORE_LOCK" | awk '{print toupper($1)}')"
readonly APP_LOCK_HASH="$(shasum -a 256 "$APP_LOCK" | awk '{print toupper($1)}')"

cleanup() {
  rm -rf -- "$TEMP_DIR"
  rm -rf -- "$CLI_SOURCE_DIR"
  rm -rf -- "$REPOSITORY_ROOT/src/StudyReportEvaluator.Core/obj/P102/$RUN_ID"
  rm -rf -- "$REPOSITORY_ROOT/src/StudyReportEvaluator.App/obj/P102/$RUN_ID"
  [[ "$(shasum -a 256 "$CORE_LOCK" | awk '{print toupper($1)}')" == "$CORE_LOCK_HASH" ]] || fail "canonical Core lock changed"
  [[ "$(shasum -a 256 "$APP_LOCK" | awk '{print toupper($1)}')" == "$APP_LOCK_HASH" ]] || fail "canonical App lock changed"
}
trap cleanup EXIT
mkdir -p -- "$PUBLISH_ROOT"
rm -rf -- "$TEMP_DIR"

dotnet restore "$PROJECT_PATH" --locked-mode -p:CopilotSkipCliDownload=true --verbosity minimal
bash "$CLI_ACQUIRER" "$RID" "$CLI_SOURCE_DIR"
dotnet restore "$PROJECT_PATH" \
  --runtime "$RID" \
  --force-evaluate \
  --lock-file-path "$TEMP_LOCK_RELATIVE" \
  -p:CopilotSkipCliDownload=true \
  -p:SelfContained=true \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false \
  -p:PublishReadyToRun=false \
  --verbosity minimal
dotnet run "$LOCK_VALIDATOR" -- "$CORE_LOCK" "$CORE_RID_LOCK" "$TARGET_FRAMEWORK" "$RID"
dotnet run "$LOCK_VALIDATOR" -- "$APP_LOCK" "$APP_RID_LOCK" "$TARGET_FRAMEWORK" "$RID"
dotnet publish "$PROJECT_PATH" \
  --configuration Release \
  --framework "$TARGET_FRAMEWORK" \
  --runtime "$RID" \
  --self-contained true \
  --no-restore \
  --output "$TEMP_DIR" \
  -p:CopilotCliBinaryPath="$CLI_SOURCE_DIR/copilot" \
  -p:CopilotSkipCliDownload=false \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false \
  -p:PublishReadyToRun=false \
  -p:UseAppHost=true \
  -p:DebugSymbols=false \
  -p:DebugType=None \
  --verbosity minimal
cp -- "$CLI_SOURCE_DIR/copilot-source-provenance.json" "$TEMP_DIR/copilot-source-provenance.json"

find "$TEMP_DIR" -type f -name '*.pdb' -delete
readonly APPHOST="$TEMP_DIR/$APP_NAME"
readonly CLI_RELATIVE="runtimes/$RID/native/copilot"
readonly CLI="$TEMP_DIR/$CLI_RELATIVE"
readonly MANIFEST="$TEMP_DIR/copilot-runtime.json"
readonly PROVENANCE="$TEMP_DIR/copilot-source-provenance.json"
for required in \
  "$APPHOST" \
  "$TEMP_DIR/$APP_NAME.dll" \
  "$TEMP_DIR/$APP_NAME.runtimeconfig.json" \
  "$TEMP_DIR/$APP_NAME.deps.json" \
  "$TEMP_DIR/StudyReportEvaluator.Core.dll" \
  "$TEMP_DIR/DocumentFormat.OpenXml.dll" \
  "$TEMP_DIR/GitHub.Copilot.SDK.dll" \
  "$TEMP_DIR/Avalonia.dll" \
  "$TEMP_DIR/libAvaloniaNative.dylib" \
  "$TEMP_DIR/libHarfBuzzSharp.dylib" \
  "$TEMP_DIR/libSkiaSharp.dylib" \
  "$CLI" \
  "$MANIFEST" \
  "$PROVENANCE"; do
  [[ -s "$required" ]] || fail "required publish file is missing or empty: $required"
done

chmod 0755 "$APPHOST" "$CLI"
assert_thin_macho_executable "$APPHOST" "apphost"
assert_thin_macho_executable "$CLI" "Copilot CLI"
assert_thin_macho_library "$TEMP_DIR/libAvaloniaNative.dylib" "Avalonia native library"
assert_thin_macho_library "$TEMP_DIR/libHarfBuzzSharp.dylib" "HarfBuzz native library"
assert_thin_macho_library "$TEMP_DIR/libSkiaSharp.dylib" "Skia native library"

readonly MANIFEST_SCHEMA="$(plutil -extract schemaVersion raw -o - "$MANIFEST")"
readonly MANIFEST_RID="$(plutil -extract runtimeIdentifier raw -o - "$MANIFEST")"
readonly MANIFEST_CLI_PATH="$(plutil -extract cliRelativePath raw -o - "$MANIFEST")"
readonly MANIFEST_CLI_HASH="$(plutil -extract cliSha256 raw -o - "$MANIFEST")"
readonly MANIFEST_CLI_VERSION="$(plutil -extract cliVersion raw -o - "$MANIFEST")"
readonly MANIFEST_SDK_VERSION="$(plutil -extract sdkVersion raw -o - "$MANIFEST")"
readonly PROVENANCE_SCHEMA="$(plutil -extract schemaVersion raw -o - "$PROVENANCE")"
readonly PROVENANCE_RID="$(plutil -extract runtimeIdentifier raw -o - "$PROVENANCE")"
readonly PROVENANCE_PACKAGE_NAME="$(plutil -extract npmPackageName raw -o - "$PROVENANCE")"
readonly PROVENANCE_PACKAGE_VERSION="$(plutil -extract npmPackageVersion raw -o - "$PROVENANCE")"
readonly PROVENANCE_INTEGRITY="$(plutil -extract npmDistIntegrity raw -o - "$PROVENANCE")"
readonly PROVENANCE_SDK_VERSION="$(plutil -extract sdkPackageVersion raw -o - "$PROVENANCE")"
readonly PROVENANCE_CLI_HASH="$(plutil -extract sourceCliSha256 raw -o - "$PROVENANCE")"
[[ "$MANIFEST_SCHEMA" == "1" && "$MANIFEST_RID" == "$RID" && "$MANIFEST_CLI_PATH" == "$CLI_RELATIVE" ]] || fail "bundled Copilot manifest schema/RID/path is invalid"
[[ "$MANIFEST_CLI_HASH" =~ ^[0-9A-Fa-f]{64}$ ]] || fail "bundled Copilot manifest SHA-256 is invalid"
[[ "$MANIFEST_CLI_VERSION" =~ ^[0-9][A-Za-z0-9._+-]{0,127}$ && "$MANIFEST_SDK_VERSION" =~ ^[0-9][A-Za-z0-9._+-]{0,127}$ ]] || fail "bundled Copilot manifest version is invalid"
[[ "$PROVENANCE_SCHEMA" == "1" && "$PROVENANCE_RID" == "$RID" && "$PROVENANCE_PACKAGE_NAME" == "$EXPECTED_NPM_PACKAGE" ]] || fail "Copilot source provenance schema/RID/package is invalid"
[[ "$PROVENANCE_PACKAGE_VERSION" == "$MANIFEST_CLI_VERSION" && "$PROVENANCE_SDK_VERSION" == "$MANIFEST_SDK_VERSION" ]] || fail "Copilot source provenance version mismatch"
[[ "$PROVENANCE_INTEGRITY" == sha512-* ]] || fail "Copilot source provenance must contain SHA-512 SRI integrity"
readonly ACTUAL_CLI_HASH="$(shasum -a 256 "$CLI" | awk '{print toupper($1)}')"
readonly NORMALIZED_MANIFEST_CLI_HASH="$(printf '%s' "$MANIFEST_CLI_HASH" | tr '[:lower:]' '[:upper:]')"
[[ "$NORMALIZED_MANIFEST_CLI_HASH" == "$ACTUAL_CLI_HASH" ]] || fail "bundled Copilot CLI hash does not match its manifest"
[[ "$PROVENANCE_CLI_HASH" == "$ACTUAL_CLI_HASH" ]] || fail "bundled Copilot CLI hash does not match source provenance"

"$APPHOST" >/dev/null 2>&1 &
readonly APP_PID=$!
sleep 2
kill -0 "$APP_PID" 2>/dev/null || fail "self-contained app exited during startup"
kill "$APP_PID" 2>/dev/null || true
wait "$APP_PID" 2>/dev/null || true

rm -rf -- "$FINAL_DIR"
mv -- "$TEMP_DIR" "$FINAL_DIR"
trap - EXIT
cleanup
printf 'Published native self-contained unsigned folder: %s\n' "$FINAL_DIR"
