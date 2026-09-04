# ADR-0015: Platform delivery foundationと初回公開境界

| 項目 | 内容 |
|---|---|
| 状態 | **承認済み・移行中** |
| 決定日 | 2026-09-03 |
| 要求正本 | `docs/requirements-definition.md` v4.3 |
| 製品版 | `0.8.0` candidate（2026-09-04、初回公開前に再baseline） |
| Supersedes | ADR-0013の将来platform target。既存Windows ZIPの実測記録は保持 |
| Carries forward | ADR-0012の機能契約、ADR-0014のSemVer/release identity、入力不変・privacy・2-project構成 |
| 承認根拠 | 要求所有者の指示「不明点はデフォルトのプランを採用」「全てのタスクを実行」 |

> 2026-09-04更新: 公開済み製品版がないため、製品所有者の指示で候補版を`1.1.0`から`0.8.0`へ再baselineした。続く指示「開発用のMSIXでOK」「外部ブロッカーの情報はない」により、初回公開はWindows unsigned ZIP、MSIXはnon-public development mechanism、macOS production deliveryは現版scope外へ改版した。

## Context

現行repositoryで実装・検証済みの配布経路はWindows 11 x64向け.NET 10 self-contained unsigned ZIPとSHA-256 sidecarだけである。利用者はZIPとsidecarの取得、hash比較、展開、EXE起動を手作業で行う。macOS publish/package/sign/notary/launchのproduction証跡は存在しない。[ADR-0013](0013-windows-only-public-release.md)、[`README.md`](../../../README.md)、[`scripts/publish-windows.ps1`](../../../scripts/publish-windows.ps1)、[`WindowsPublishPackageTests.cs`](../../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs)

要求所有者は当初WindowsとmacOSのsetup簡素化を指示した。その後、2026-09-04にdevelopment MSIXまでを採用し、外部production入力なしで初回公開を進めるようscopeを改版した。現行のengineering用PowerShellはPowerShell 7+を要求するため、development MSIXをend-user primary pathにはしない。

.NET self-contained publishは対象端末への.NET runtime事前installを不要にするが、RID別artifactとOS native dependencyの検証は必要である。[Microsoft Learn: .NET application publishing](https://learn.microsoft.com/dotnet/core/deploying/)、[RID catalog](https://learn.microsoft.com/dotnet/core/rid-catalog)

Windows direct MSIXはvalidかつ端末でtrustedなcode-signing certificateを必要とする。[Microsoft Learn: Choose a distribution path](https://learn.microsoft.com/windows/apps/package-and-deploy/choose-distribution-path)、[Sign an MSIX package](https://learn.microsoft.com/windows/msix/package/signing-package-overview)

macOS直接配布は`.app` bundle、Developer ID、hardened runtime、secure timestamp、notarization、ticket stapling、Gatekeeper検証を必要とする。[Avalonia: macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos)、[Apple: Notarizing macOS software](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)、[Apple: Customizing the notarization workflow](https://developer.apple.com/documentation/security/customizing-the-notarization-workflow)

## Decision

1. 初回公開前の製品candidateを`0.8.0`、要求文書をv4.3とする。初回公開後に対応platformや配布形式を後方互換追加する場合はSemVer MINORを適用する。[ADR-0014](0014-product-versioning.md)、[`version-management.md`](../version-management.md)
2. Windowsの初回public artifactはGitHub Releasesから直接配布する`StudyReportEvaluator-win-x64.zip`とSHA-256 sidecarとする。Microsoft Store submissionは追加しない。
3. Windows MSIXはPublisher末尾に固定marker`OID.2.25.311729368913984317654407730594956997722=1`を持つWindows 11開発専用unsigned packageだけを`PASS_MECHANISM`として検証する。signed packageと別identityにし、一般利用者導線とGitHub Release assetへ含めない。installを追加検証する場合は使い捨て環境の管理者PowerShellから`Add-AppxPackage -AllowUnsigned`を使い、初回公開のrequired gateにはしない。
4. macOSのpublish／bundle／sign／notary script foundationは保持するが、`StudyReportEvaluator-osx-arm64.dmg`と`StudyReportEvaluator-osx-x64.dmg`を現版のpublic artifactまたはrequired acceptanceとしない。universal binaryやRosettaをnative architecture証跡の代わりにしない。
5. 将来macOSを公開scopeへ追加する場合だけ、.NET 10 self-contained `.app`、固定SDKと互換なbundled Copilot CLI、Developer ID signing、hardened runtime、notarization、app/DMGへのstapling、quarantine付きclean launchをrequired gateとする。
6. macOS bundle identifierの既定値を`com.github.dahatake.study-report-evaluator`とする。Apple Team ID、Developer ID identity、notarization credentialは推測せずprotected secret storeから供給する。
7. Windows development MSIXのIdentity Name、Publisher、DisplayNameはtest-only値とし、production identityと表示しない。
8. end-user primary手順はWindows ZIPとsidecarの取得、SHA-256確認、展開、apphost起動とする。development MSIX、shell setup、未実測macOS手順を一般利用者へ案内しない。
9. engineering用publish/package/verify scriptは維持するが、development MSIX scriptをend-user primary pathとして案内しない。
10. Windows ZIPは初回public primary artifactであり、unsigned、non-installer、SmartScreen reputation非保証を明示する。
11. Linux、Windows Arm64、macOS 13以前、universal macOS artifact、Mac App Store、Microsoft Storeは今回の対応対象外とする。
12. current public claimはADR-0013のWindows-only/unsigned ZIP境界とする。development MSIX、framework support、macOS cross-publishを対応済み表示またはpublic assetへ変換しない。

## Artifact contract

| Platform | Artifact | Required evidence |
|---|---|---|
| Windows 11 x64 public | `StudyReportEvaluator-win-x64.zip` + `.sha256` | `PASS_REQUIRED`。self-contained、safe layout、version、package hash、clean extract/launch、bundled CLI identity |
| Windows 11 x64 development | `StudyReportEvaluator-win-x64.unsigned.test.msix` + `.sha256` | `PASS_MECHANISM`のみ。manifest/version/RID、block map、package integrity、bundled CLI、policy negative、cleanup。public assetにしない |
| macOS source foundation | public artifactなし | static contractのみ。将来のnative/sign/notary evidenceなしに対応表示しない |

macOS 14、15、26はvendor support matrixから得た試験候補であり、本製品の対応表示ではない。exact OS build × architectureの実測行だけを公開support matrixへ載せる。[Avalonia: Supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)

## macOS signing order

Copilot CLIを含むnested Mach-Oへの署名はfile bytesを変更し得るため、runtime manifestを次の順で確定する。

1. 固定SDKが指定するRID別CLI package/version/integrityを検証する。
2. unsigned source CLIのSHA-256をprovenanceとして記録する。
3. `.app`の全resourceを配置し、nested Mach-O、CLI、apphostを内側から署名する。outer `.app`はまだ署名しない。
4. shipped signed CLIのSHA-256を`copilot-runtime.json`へ記録する。
5. resource不変を確認してouter `.app`を最後に署名する。
6. outer署名後はbundleを変更せず、strict signature、runtime manifest、CLI handshakeを検証する。

Appleが署名後のbundle変更をinvalid signatureとして扱うため、manifest更新後にouter bundleを署名する。[Apple: Resolving common notarization issues](https://developer.apple.com/documentation/security/resolving-common-notarization-issues)

## Future macOS external prerequisites（現版required release blockerではない）

| Input | Owner role | 未提供時 |
|---|---|---|
| Developer ID Application identity | Apple Account Holder / Release engineering | macOS signingを`BLOCKED_EXTERNAL` |
| Notarization credential | Apple Account Holder / Release engineering | notarizationを`BLOCKED_EXTERNAL` |
| approved visual assets | Product / Design owner | production packageを`BLOCKED_EXTERNAL` |
| macOS ARM64/x64 clean test hosts | QA / Release engineering | 未実測architectureを公開しない |

秘密値、certificate private key、password、token、device codeはrepository、release evidence、通常log、process argumentへ保存しない。

## Acceptance vocabulary

| Status | 意味 |
|---|---|
| `PASS_REQUIRED` | OS非依存の決定的code/testが成功 |
| `PASS_MECHANISM` | test certificateまたはunsigned packageで仕組みだけが成功 |
| `PASS_PRODUCTION` | production trust pathとclean target OSで成功 |
| `BLOCKED_EXTERNAL` | signing identity、credential、実機等の外部入力不足 |
| `NOT_RUN` | 未実行。PASSへ変換しない |
| `FAIL` | 必須結果と不一致 |

## Consequences

### Positive

- 外部production signing入力なしで、検証済みWindows ZIPを初回公開できる。
- .NET、Copilot CLI、Officeの別installを要求しない既存契約を維持できる。
- development MSIX mechanismをproduction trustや一般配布と混同しない。
- macOS framework一般論を製品保証へ変換しない。

### Trade-offs

- Windows ZIPはinstaller、Publisher identity、SmartScreen reputationを提供しない。
- 利用者がZIPとsidecarを取得し、hash確認と展開を行う。
- development MSIXの標準App Installer、non-admin install、production trustを保証しない。
- macOS packageは現版で提供しない。

## Rejected alternatives

### End-user `setup.ps1`だけを提供する

PowerShell 7+導入、execution policy、terminal操作が追加され、非技術利用者のsetup簡素化にならないためprimary pathには採用しない。

### unsigned MSIXまたはunnotarized `.app`を正式配布する

OS trust UIを悪化させ、MSIX installまたはGatekeeperのproduction acceptanceを満たさないため採用しない。

Windows 11のunsigned MSIXは、本製品固有のpackage container互換性を証明書なしで先行確認する開発試験に限って採用する。専用Publisher identity、明示的なunsigned artifact名、`PASS_MECHANISM` statusを必須とし、production artifact、標準UI setup、signature trustの証拠には使用しない。

### frameworkのcross-platform supportだけでmacOS対応と表示する

本製品のbundle、CLI、filesystem、signing、notarization、quarantine launchを証明しないため採用しない。

### x64/ARM64 universal artifactを最初から作る

apphost、native dependency、bundled CLI、署名後manifestの統合証跡がなく、architecture別failureを隠すため採用しない。

## Transition and validation

1. v4.3 requirement、architecture、detailed design、traceability、claim ledger、system testsを先に同期する。
2. Windows ZIP regressionとdevelopment MSIX mechanismを実行し、後者を`PASS_MECHANISM`へ限定する。
3. CIはsecretなしでbuild、test、ZIP、development MSIXを検査する。
4. release matrixのWindows ZIP `publish=true`行が`PASS_REQUIRED`でなければ公開しない。development MSIXは`publish=false`とする。
5. public ZIPをfresh downloadしてhash、layout、version、bundled CLI、clean launchを再確認した後だけ実在URLを文書へ記載する。

## Result

**APPROVED — V4.3 SCOPE.** 初回public artifactはWindows 11 x64 unsigned ZIPとsidecar。Windows MSIXはnon-public development `PASS_MECHANISM`、macOSはsource/static contractのみとする。development packageや未実測platformを公開済み・対応済みと表示しない。
