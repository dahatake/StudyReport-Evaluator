#!/usr/bin/env bash
set -euo pipefail

readonly REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
readonly PROJECT_PATH="$REPOSITORY_ROOT/src/StudyReportEvaluator.App/StudyReportEvaluator.App.csproj"
readonly REGISTRY_URL="https://registry.npmjs.org"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

[[ $# -eq 2 ]] || fail "usage: $0 <osx-arm64|osx-x64> <destination-directory>"
readonly RID="$1"
case "$RID" in
  osx-arm64)
    readonly EXPECTED_PLATFORM="darwin-arm64"
    readonly EXPECTED_ARCH="arm64"
    ;;
  osx-x64)
    readonly EXPECTED_PLATFORM="darwin-x64"
    readonly EXPECTED_ARCH="x86_64"
    ;;
  *) fail "unsupported runtime identifier: $RID" ;;
esac

[[ "$(uname -s)" == "Darwin" ]] || fail "macOS is required"
for command_name in curl dotnet file openssl plutil shasum tar uuidgen; do
  command -v "$command_name" >/dev/null 2>&1 || fail "$command_name is required"
done

readonly DESTINATION_INPUT="$2"
readonly DESTINATION_PARENT="$(cd "$(dirname "$DESTINATION_INPUT")" && pwd -P)"
readonly DESTINATION="$DESTINATION_PARENT/$(basename "$DESTINATION_INPUT")"
[[ ! -e "$DESTINATION" ]] || fail "destination already exists: $DESTINATION"
readonly RUN_ID="$(uuidgen | tr '[:upper:]' '[:lower:]' | tr -d '-')"
readonly TEMP_DIR="$DESTINATION_PARENT/.copilot-acquire-$RUN_ID"
readonly METADATA_FILE="$TEMP_DIR/metadata.json"
readonly ARCHIVE_FILE="$TEMP_DIR/copilot.tgz"
readonly EXTRACT_DIR="$TEMP_DIR/extracted"
readonly RESULT_DIR="$TEMP_DIR/result"

cleanup() {
  rm -rf -- "$TEMP_DIR"
}
trap cleanup EXIT
mkdir -p -- "$EXTRACT_DIR"

readonly PROPERTY_FILE="$TEMP_DIR/msbuild-properties.json"
dotnet msbuild "$PROJECT_PATH" \
  -getProperty:CopilotCliVersion \
  -getProperty:GitHubCopilotSdkVersion \
  -getProperty:_CopilotPlatform \
  -p:RuntimeIdentifier="$RID" \
  -p:CopilotSkipCliDownload=true \
  -verbosity:quiet > "$PROPERTY_FILE"
readonly CLI_VERSION="$(plutil -extract Properties.CopilotCliVersion raw -o - "$PROPERTY_FILE")"
readonly SDK_VERSION="$(plutil -extract Properties.GitHubCopilotSdkVersion raw -o - "$PROPERTY_FILE")"
readonly ACTUAL_PLATFORM="$(plutil -extract Properties._CopilotPlatform raw -o - "$PROPERTY_FILE")"
[[ "$CLI_VERSION" =~ ^[0-9][A-Za-z0-9._+-]{0,127}$ ]] || fail "fixed Copilot CLI version is invalid"
[[ "$SDK_VERSION" =~ ^[0-9][A-Za-z0-9._+-]{0,127}$ ]] || fail "fixed Copilot SDK version is invalid"
[[ "$ACTUAL_PLATFORM" == "$EXPECTED_PLATFORM" ]] || fail "SDK RID mapping mismatch: $ACTUAL_PLATFORM"

readonly PACKAGE_NAME="@github/copilot-$EXPECTED_PLATFORM"
readonly ENCODED_PACKAGE_NAME="%40github%2Fcopilot-$EXPECTED_PLATFORM"
readonly METADATA_URL="$REGISTRY_URL/$ENCODED_PACKAGE_NAME/$CLI_VERSION"
curl \
  --proto '=https' \
  --proto-redir '=https' \
  --tlsv1.2 \
  --fail \
  --location \
  --silent \
  --show-error \
  --output "$METADATA_FILE" \
  "$METADATA_URL"

readonly METADATA_NAME="$(plutil -extract name raw -o - "$METADATA_FILE")"
readonly METADATA_VERSION="$(plutil -extract version raw -o - "$METADATA_FILE")"
readonly DIST_INTEGRITY="$(plutil -extract dist.integrity raw -o - "$METADATA_FILE")"
readonly TARBALL_URL="$(plutil -extract dist.tarball raw -o - "$METADATA_FILE")"
[[ "$METADATA_NAME" == "$PACKAGE_NAME" ]] || fail "npm package name mismatch"
[[ "$METADATA_VERSION" == "$CLI_VERSION" ]] || fail "npm package version mismatch"
[[ "$DIST_INTEGRITY" == sha512-* ]] || fail "npm package must provide SHA-512 SRI integrity"
[[ "$TARBALL_URL" == https://* ]] || fail "npm tarball URL must use HTTPS"

curl \
  --proto '=https' \
  --proto-redir '=https' \
  --tlsv1.2 \
  --fail \
  --location \
  --silent \
  --show-error \
  --output "$ARCHIVE_FILE" \
  "$TARBALL_URL"
readonly ACTUAL_INTEGRITY="sha512-$(openssl dgst -sha512 -binary "$ARCHIVE_FILE" | openssl base64 | tr -d '\r\n')"
[[ "$ACTUAL_INTEGRITY" == "$DIST_INTEGRITY" ]] || fail "npm tarball integrity mismatch"

tar -xzf "$ARCHIVE_FILE" --strip-components=1 -C "$EXTRACT_DIR"
readonly CLI_SOURCE="$EXTRACT_DIR/copilot"
[[ -s "$CLI_SOURCE" ]] || fail "Copilot CLI binary is missing or empty"
readonly FILE_DETAILS="$(file -b "$CLI_SOURCE")"
[[ "$FILE_DETAILS" == "Mach-O 64-bit $EXPECTED_ARCH executable"* ]] || fail "Copilot CLI is not a thin $EXPECTED_ARCH Mach-O executable: $FILE_DETAILS"
readonly SOURCE_CLI_SHA256="$(shasum -a 256 "$CLI_SOURCE" | awk '{print toupper($1)}')"
[[ "$SOURCE_CLI_SHA256" =~ ^[0-9A-F]{64}$ ]] || fail "Copilot CLI SHA-256 is invalid"

mkdir -p -- "$RESULT_DIR"
cp -- "$CLI_SOURCE" "$RESULT_DIR/copilot"
chmod 0755 "$RESULT_DIR/copilot"
printf '{"schemaVersion":1,"runtimeIdentifier":"%s","npmPackageName":"%s","npmPackageVersion":"%s","npmDistIntegrity":"%s","sdkPackageVersion":"%s","sourceCliSha256":"%s"}\n' \
  "$RID" \
  "$PACKAGE_NAME" \
  "$CLI_VERSION" \
  "$DIST_INTEGRITY" \
  "$SDK_VERSION" \
  "$SOURCE_CLI_SHA256" > "$RESULT_DIR/copilot-source-provenance.json"
plutil -lint "$RESULT_DIR/copilot-source-provenance.json" >/dev/null
mv -- "$RESULT_DIR" "$DESTINATION"

trap - EXIT
cleanup
printf 'Acquired verified Copilot CLI source: %s\n' "$DESTINATION/copilot"
