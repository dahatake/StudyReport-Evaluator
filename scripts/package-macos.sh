#!/usr/bin/env bash
set -euo pipefail

readonly APP_BUNDLE_NAME="StudyReportEvaluator.app"
readonly EXECUTABLE_NAME="StudyReportEvaluator.App"
readonly REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
readonly PLIST_TEMPLATE="$REPOSITORY_ROOT/eng/packaging/macos/Info.plist"
readonly ENTITLEMENTS_FILE="$REPOSITORY_ROOT/eng/packaging/macos/StudyReportEvaluator.entitlements"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

[[ $# -eq 3 ]] || fail "usage: $0 <osx-arm64|osx-x64> <product-version> <numeric-build-version>"
readonly RID="$1"
readonly PRODUCT_VERSION="$2"
readonly BUILD_VERSION="$3"
[[ "$RID" == "osx-arm64" || "$RID" == "osx-x64" ]] || fail "unsupported runtime identifier: $RID"
[[ "$PRODUCT_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "product version must be Major.Minor.Patch"
[[ "$BUILD_VERSION" =~ ^[1-9][0-9]*$ ]] || fail "build version must be a positive integer"
[[ "$(uname -s)" == "Darwin" ]] || fail "macOS is required to preserve and validate bundle metadata"
for command_name in head od plutil tr; do
  command -v "$command_name" >/dev/null 2>&1 || fail "$command_name is required"
done

: "${STUDY_REPORT_EVALUATOR_ICON_PATH:?Set STUDY_REPORT_EVALUATOR_ICON_PATH to the owner-approved .icns file}"
readonly ICON_SOURCE="$(cd "$(dirname "$STUDY_REPORT_EVALUATOR_ICON_PATH")" && pwd -P)/$(basename "$STUDY_REPORT_EVALUATOR_ICON_PATH")"
[[ -s "$ICON_SOURCE" && "$ICON_SOURCE" == *.icns ]] || fail "owner-approved .icns file is missing or empty"
[[ -s "$PLIST_TEMPLATE" ]] || fail "Info.plist template is missing"
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

readonly PUBLISH_DIR="$REPOSITORY_ROOT/artifacts/package/publish/$RID"
readonly OUTPUT_DIR="$REPOSITORY_ROOT/artifacts/package/macos/$RID"
readonly TEMP_BUNDLE="$OUTPUT_DIR/.$APP_BUNDLE_NAME.tmp"
readonly FINAL_BUNDLE="$OUTPUT_DIR/$APP_BUNDLE_NAME"
readonly MACOS_DIR="$TEMP_BUNDLE/Contents/MacOS"
readonly RESOURCES_DIR="$TEMP_BUNDLE/Contents/Resources"
[[ -s "$PUBLISH_DIR/$EXECUTABLE_NAME" ]] || fail "run publish-macos.sh for $RID first"
[[ -s "$PUBLISH_DIR/copilot-source-provenance.json" ]] || fail "verified Copilot source provenance is missing; rerun publish-macos.sh for $RID"

rm -rf -- "$TEMP_BUNDLE"
mkdir -p -- "$MACOS_DIR" "$RESOURCES_DIR"
cp -a "$PUBLISH_DIR/." "$MACOS_DIR/"
cp -- "$ICON_SOURCE" "$RESOURCES_DIR/StudyReportEvaluator.icns"
sed \
  -e "s|@PRODUCT_VERSION@|$PRODUCT_VERSION|g" \
  -e "s|@BUILD_VERSION@|$BUILD_VERSION|g" \
  "$PLIST_TEMPLATE" > "$TEMP_BUNDLE/Contents/Info.plist"
chmod 0755 "$MACOS_DIR/$EXECUTABLE_NAME" "$MACOS_DIR/runtimes/$RID/native/copilot"
[[ -x "$MACOS_DIR/$EXECUTABLE_NAME" ]] || fail "apphost execute bit was not set"
[[ -x "$MACOS_DIR/runtimes/$RID/native/copilot" ]] || fail "Copilot CLI execute bit was not set"
[[ -s "$RESOURCES_DIR/StudyReportEvaluator.icns" ]] || fail "bundle icon is missing or empty"
plutil -lint "$TEMP_BUNDLE/Contents/Info.plist" >/dev/null

[[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$TEMP_BUNDLE/Contents/Info.plist")" == "$EXECUTABLE_NAME" ]] || fail "CFBundleExecutable mismatch"
[[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$TEMP_BUNDLE/Contents/Info.plist")" == "com.github.dahatake.study-report-evaluator" ]] || fail "CFBundleIdentifier mismatch"
[[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$TEMP_BUNDLE/Contents/Info.plist")" == "$PRODUCT_VERSION" ]] || fail "CFBundleShortVersionString mismatch"
[[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$TEMP_BUNDLE/Contents/Info.plist")" == "$BUILD_VERSION" ]] || fail "CFBundleVersion mismatch"

rm -rf -- "$FINAL_BUNDLE"
mv -- "$TEMP_BUNDLE" "$FINAL_BUNDLE"
[[ -s "$FINAL_BUNDLE/Contents/Info.plist" ]] || fail "final bundle Info.plist is missing or empty"
[[ -x "$FINAL_BUNDLE/Contents/MacOS/$EXECUTABLE_NAME" ]] || fail "final bundle apphost is not executable"
[[ -x "$FINAL_BUNDLE/Contents/MacOS/runtimes/$RID/native/copilot" ]] || fail "final bundle Copilot CLI is not executable"
[[ -s "$FINAL_BUNDLE/Contents/Resources/StudyReportEvaluator.icns" ]] || fail "final bundle icon is missing or empty"
printf 'Packaged unsigned development app bundle: %s\n' "$FINAL_BUNDLE"
printf 'Production signing/notarization status: NOT_RUN\n'
