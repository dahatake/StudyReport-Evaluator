#!/usr/bin/env bash
set -euo pipefail

readonly APP_BUNDLE_NAME="StudyReportEvaluator.app"
readonly REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

[[ $# -eq 1 ]] || fail "usage: $0 <osx-arm64|osx-x64>"
readonly RID="$1"
case "$RID" in
  osx-arm64) readonly EXPECTED_ARCH="arm64" ;;
  osx-x64) readonly EXPECTED_ARCH="x86_64" ;;
  *) fail "unsupported runtime identifier: $RID" ;;
esac
[[ "$(uname -s)" == "Darwin" ]] || fail "macOS is required"
readonly HOST_ARCH="$(uname -m)"
[[ "$HOST_ARCH" == "$EXPECTED_ARCH" ]] || fail "native $EXPECTED_ARCH host required for $RID; actual host is $HOST_ARCH"
: "${MACOS_DEVELOPER_ID_APPLICATION:?Set MACOS_DEVELOPER_ID_APPLICATION to the approved Developer ID Application identity}"
: "${MACOS_NOTARY_KEYCHAIN_PROFILE:?Set MACOS_NOTARY_KEYCHAIN_PROFILE to an approved notarytool keychain profile name}"
for command_name in codesign ditto file hdiutil plutil shasum spctl stat uuidgen xcrun; do
  command -v "$command_name" >/dev/null 2>&1 || fail "$command_name is required"
done

readonly IDENTITY="$MACOS_DEVELOPER_ID_APPLICATION"
readonly NOTARY_PROFILE="$MACOS_NOTARY_KEYCHAIN_PROFILE"
readonly SOURCE_BUNDLE="$REPOSITORY_ROOT/artifacts/package/macos-signed/$RID/$APP_BUNDLE_NAME"
readonly OUTPUT_ROOT="$REPOSITORY_ROOT/artifacts/package/macos-release/$RID"
readonly RUN_ID="$(uuidgen | tr '[:upper:]' '[:lower:]' | tr -d '-')"
readonly TEMP_ROOT="$OUTPUT_ROOT/.notary-$RUN_ID"
readonly TEMP_BUNDLE="$TEMP_ROOT/$APP_BUNDLE_NAME"
readonly APP_ZIP="$TEMP_ROOT/StudyReportEvaluator-$RID-notary.zip"
readonly APP_SUBMISSION="$TEMP_ROOT/app-notary-submission.json"
readonly APP_LOG="$TEMP_ROOT/app-notary-log.json"
readonly DMG_ROOT="$TEMP_ROOT/dmg-root"
readonly TEMP_DMG="$TEMP_ROOT/StudyReportEvaluator-$RID.dmg"
readonly DMG_SUBMISSION="$TEMP_ROOT/dmg-notary-submission.json"
readonly DMG_LOG="$TEMP_ROOT/dmg-notary-log.json"

[[ -d "$SOURCE_BUNDLE/Contents/MacOS" ]] || fail "signed app bundle is missing; run sign-macos.sh first"
[[ -s "$SOURCE_BUNDLE/Contents/Info.plist" ]] || fail "signed app Info.plist is missing or empty"
codesign --verify --deep --strict --verbose=2 "$SOURCE_BUNDLE"
readonly PRODUCT_VERSION="$(plutil -extract CFBundleShortVersionString raw -o - "$SOURCE_BUNDLE/Contents/Info.plist")"
readonly BUILD_VERSION="$(plutil -extract CFBundleVersion raw -o - "$SOURCE_BUNDLE/Contents/Info.plist")"
[[ "$PRODUCT_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "signed app product version is invalid"
[[ "$BUILD_VERSION" =~ ^[1-9][0-9]*$ ]] || fail "signed app build version is invalid"
readonly FINAL_DMG="$OUTPUT_ROOT/StudyReportEvaluator-$PRODUCT_VERSION-$RID.dmg"
readonly FINAL_SHA256="$FINAL_DMG.sha256"
readonly FINAL_EVIDENCE="$OUTPUT_ROOT/StudyReportEvaluator-$PRODUCT_VERSION-$RID.notary-evidence.json"
readonly FINAL_APP_LOG="$OUTPUT_ROOT/StudyReportEvaluator-$PRODUCT_VERSION-$RID.app-notary-log.json"
readonly FINAL_DMG_LOG="$OUTPUT_ROOT/StudyReportEvaluator-$PRODUCT_VERSION-$RID.dmg-notary-log.json"

cleanup() {
  rm -rf -- "$TEMP_ROOT"
}
trap cleanup EXIT
mkdir -p -- "$TEMP_ROOT" "$DMG_ROOT" "$OUTPUT_ROOT"
cp -a "$SOURCE_BUNDLE" "$TEMP_BUNDLE"

validate_submission() {
  local result_path="$1"
  local log_path="$2"
  local description="$3"
  local submission_id
  local status
  status="$(plutil -extract status raw -o - "$result_path")"
  submission_id="$(plutil -extract id raw -o - "$result_path")"
  [[ "$status" == "Accepted" ]] || fail "$description notarization was not accepted: $status"
  [[ "$submission_id" =~ ^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$ ]] || fail "$description notarization returned an invalid submission id"
  xcrun notarytool log "$submission_id" --keychain-profile "$NOTARY_PROFILE" "$log_path"
  plutil -lint "$log_path" >/dev/null
  [[ "$(plutil -extract status raw -o - "$log_path")" == "Accepted" ]] || fail "$description notary log status is not Accepted"
  [[ "$(plutil -extract issues json -o - "$log_path")" == "[]" ]] || fail "$description notary log contains issues"
}

ditto -c -k --sequesterRsrc --keepParent "$TEMP_BUNDLE" "$APP_ZIP"
xcrun notarytool submit "$APP_ZIP" \
  --keychain-profile "$NOTARY_PROFILE" \
  --wait \
  --output-format json > "$APP_SUBMISSION"
validate_submission "$APP_SUBMISSION" "$APP_LOG" "app"
xcrun stapler staple "$TEMP_BUNDLE"
xcrun stapler validate "$TEMP_BUNDLE"
codesign --verify --deep --strict --verbose=2 "$TEMP_BUNDLE"
spctl --assess --type execute --verbose=4 "$TEMP_BUNDLE"

cp -a "$TEMP_BUNDLE" "$DMG_ROOT/$APP_BUNDLE_NAME"
ln -s /Applications "$DMG_ROOT/Applications"
hdiutil create \
  -volname "Study Report Evaluator $PRODUCT_VERSION" \
  -srcfolder "$DMG_ROOT" \
  -format UDZO \
  -ov \
  "$TEMP_DMG"
hdiutil verify "$TEMP_DMG"
codesign --force --timestamp --sign "$IDENTITY" "$TEMP_DMG"
codesign --verify --strict --verbose=2 "$TEMP_DMG"
xcrun notarytool submit "$TEMP_DMG" \
  --keychain-profile "$NOTARY_PROFILE" \
  --wait \
  --output-format json > "$DMG_SUBMISSION"
validate_submission "$DMG_SUBMISSION" "$DMG_LOG" "DMG"
xcrun stapler staple "$TEMP_DMG"
xcrun stapler validate "$TEMP_DMG"
hdiutil verify "$TEMP_DMG"
codesign --verify --strict --verbose=2 "$TEMP_DMG"
spctl --assess --type open --context context:primary-signature --verbose=4 "$TEMP_DMG"

readonly DMG_SHA256="$(shasum -a 256 "$TEMP_DMG" | awk '{print toupper($1)}')"
readonly DMG_BYTES="$(stat -f '%z' "$TEMP_DMG")"
readonly APP_SUBMISSION_ID="$(plutil -extract id raw -o - "$APP_SUBMISSION")"
readonly APP_NOTARY_STATUS="$(plutil -extract status raw -o - "$APP_SUBMISSION")"
readonly DMG_SUBMISSION_ID="$(plutil -extract id raw -o - "$DMG_SUBMISSION")"
readonly DMG_NOTARY_STATUS="$(plutil -extract status raw -o - "$DMG_SUBMISSION")"
[[ "$DMG_SHA256" =~ ^[0-9A-F]{64}$ ]] || fail "final DMG SHA-256 is invalid"
[[ "$DMG_BYTES" =~ ^[1-9][0-9]*$ ]] || fail "final DMG byte count is invalid"

cat > "$TEMP_ROOT/notary-evidence.json" <<EOF
{
  "schemaVersion": 1,
  "runtimeIdentifier": "$RID",
  "productVersion": "$PRODUCT_VERSION",
  "buildVersion": "$BUILD_VERSION",
  "dmgFileName": "$(basename "$FINAL_DMG")",
  "dmgBytes": $DMG_BYTES,
  "dmgSha256": "$DMG_SHA256",
  "appNotarySubmissionId": "$APP_SUBMISSION_ID",
  "appNotaryStatus": "$APP_NOTARY_STATUS",
  "dmgNotarySubmissionId": "$DMG_SUBMISSION_ID",
  "dmgNotaryStatus": "$DMG_NOTARY_STATUS",
  "staplerValidation": "PASS",
  "gatekeeperAssessment": "PASS"
}
EOF
plutil -lint "$TEMP_ROOT/notary-evidence.json" >/dev/null
printf '%s  %s\n' "$DMG_SHA256" "$(basename "$FINAL_DMG")" > "$TEMP_ROOT/final.sha256"

rm -f -- "$FINAL_DMG" "$FINAL_SHA256" "$FINAL_EVIDENCE" "$FINAL_APP_LOG" "$FINAL_DMG_LOG"
mv -- "$TEMP_ROOT/final.sha256" "$FINAL_SHA256"
mv -- "$TEMP_ROOT/notary-evidence.json" "$FINAL_EVIDENCE"
mv -- "$APP_LOG" "$FINAL_APP_LOG"
mv -- "$DMG_LOG" "$FINAL_DMG_LOG"
mv -- "$TEMP_DMG" "$FINAL_DMG"
trap - EXIT
cleanup
printf 'Created notarized and stapled DMG: %s\n' "$FINAL_DMG"
printf 'SHA-256: %s\n' "$DMG_SHA256"
