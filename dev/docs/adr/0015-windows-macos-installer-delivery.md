# ADR-0015: Windows MSIXとmacOS DMGによるセットアップ簡素化

| 項目 | 内容 |
|---|---|
| 状態 | **承認済み・移行中** |
| 決定日 | 2026-09-03 |
| 要求正本 | `docs/requirements-definition.md` v4.2 |
| 製品版 | `1.1.0` candidate |
| Supersedes | ADR-0013の将来platform target。既存Windows ZIPの実測記録は保持 |
| Carries forward | ADR-0012の機能契約、ADR-0014のSemVer/release identity、入力不変・privacy・2-project構成 |
| 承認根拠 | 要求所有者の指示「不明点はデフォルトのプランを採用」「全てのタスクを実行」 |

## Context

現行repositoryで実装・検証済みの配布経路はWindows 11 x64向け.NET 10 self-contained unsigned ZIPとSHA-256 sidecarだけである。利用者はZIPとsidecarの取得、hash比較、展開、EXE起動を手作業で行う。macOS publish/package/sign/notary/launchのproduction証跡は存在しない。[ADR-0013](0013-windows-only-public-release.md)、[`README.md`](../../../README.md)、[`scripts/publish-windows.ps1`](../../../scripts/publish-windows.ps1)、[`WindowsPublishPackageTests.cs`](../../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs)

要求所有者はWindowsとmacOSのセットアップを、setup scriptまたはinstallerによる短い手順へ簡素化するよう指示した。現行のengineering用PowerShellはPowerShell 7+を要求するため、end-user setup scriptをprimary pathにすると新しい利用者前提が増える。OS標準UIを使うinstaller/packageをprimary pathとする。

.NET self-contained publishは対象端末への.NET runtime事前installを不要にするが、RID別artifactとOS native dependencyの検証は必要である。[Microsoft Learn: .NET application publishing](https://learn.microsoft.com/dotnet/core/deploying/)、[RID catalog](https://learn.microsoft.com/dotnet/core/rid-catalog)

Windows direct MSIXはvalidかつ端末でtrustedなcode-signing certificateを必要とする。[Microsoft Learn: Choose a distribution path](https://learn.microsoft.com/windows/apps/package-and-deploy/choose-distribution-path)、[Sign an MSIX package](https://learn.microsoft.com/windows/msix/package/signing-package-overview)

macOS直接配布は`.app` bundle、Developer ID、hardened runtime、secure timestamp、notarization、ticket stapling、Gatekeeper検証を必要とする。[Avalonia: macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos)、[Apple: Notarizing macOS software](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)、[Apple: Customizing the notarization workflow](https://developer.apple.com/documentation/security/customizing-the-notarization-workflow)

## Decision

1. 対応platformと配布形式の後方互換追加であるため、製品candidateをSemVer MINORの`1.1.0`、要求文書をv4.2とする。[ADR-0014](0014-product-versioning.md)、[`version-management.md`](../version-management.md)
2. Windowsのprimary artifactはGitHub Releasesから直接配布する`StudyReportEvaluator-win-x64.msix`とする。Microsoft Store submissionは今回追加しない。
3. Windows MSIXはproduction-trusted certificateで署名・timestampし、clean Windows 11 x64でinstall、launch、upgrade、repair、uninstallを実測した場合だけ正式配布する。self-signed certificateと、Publisher末尾に固定marker`OID.2.25.311729368913984317654407730594956997722=1`を持つWindows 11開発専用unsigned packageは`PASS_MECHANISM`に限定する。unsigned packageはsigned packageと別identityにし、管理者PowerShellの`Add-AppxPackage -AllowUnsigned`による全ユーザーinstallだけで試験し、一般利用者導線へ含めない。
4. macOSのprimary artifactは`StudyReportEvaluator-osx-arm64.dmg`と`StudyReportEvaluator-osx-x64.dmg`を別々に作る。universal binaryやRosettaをnative architecture証跡の代わりにしない。
5. 各DMGは.NET 10 self-contained `.app`、固定SDKと互換なbundled Copilot CLI、public documentationを含む。Developer ID signing、hardened runtime、notarization、app/DMGへのstapling、quarantine付きclean launchを通過した場合だけ正式配布する。
6. macOS bundle identifierの既定値を`com.github.dahatake.study-report-evaluator`とする。Apple Team ID、Developer ID identity、notarization credentialは推測せずprotected secret storeから供給する。
7. Windows package Identity Name、Publisher、DisplayNameはproduction signing identityと一致させる。Publisherを仮値のままproduction artifactへ入れない。
8. end-user primary手順はWindowsを「MSIXを開く→Install→起動」、macOSを「DMGを開く→Applicationsへdrag→起動」の各3操作以内とする。PowerShell、shell、`chmod`、`xattr`、Gatekeeper無効化を要求しない。
9. engineering用publish/package/verify scriptは維持するが、end-user primary pathとして案内しない。
10. 現行Windows ZIPは移行中のregression/fallbackとして保持できる。MSIX production gate成功後にprimaryから外し、署名済みinstallerと混同しない。
11. Linux、Windows Arm64、macOS 13以前、universal macOS artifact、Mac App Store、Microsoft Storeは今回の対応対象外とする。
12. current public claimは各artifactの`PASS_PRODUCTION`証跡が揃うまでADR-0013のWindows-only/unsigned ZIP境界を維持する。framework supportやcross-publishだけでREADMEを対応済みに変更しない。

## Artifact contract

| Platform | Artifact | Required evidence |
|---|---|---|
| Windows 11 x64 | `StudyReportEvaluator-win-x64.msix` + `.sha256` | trusted signature、timestamp、manifest/version/RID、package integrity、clean install/launch/upgrade/repair/uninstall、bundled CLI handshake |
| macOS ARM64 | `StudyReportEvaluator-osx-arm64.dmg` + `.sha256` | native ARM64、Developer ID、hardened runtime、notary log、app/DMG staple、quarantine launch、bundled CLI handshake |
| macOS x64 | `StudyReportEvaluator-osx-x64.dmg` + `.sha256` | native x64。Rosetta結果を代用しない。他はARM64と同じ |

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

## External prerequisites

| Input | Owner role | 未提供時 |
|---|---|---|
| Windows production signing identity | Security / Release owner | Windows production releaseを`BLOCKED_EXTERNAL` |
| Windows manifest Publisher / Identity | Release engineering / Signing administrator | placeholder検出でpackage failure |
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

- 利用者のprimary setupをOS標準UIだけの3操作以内へ短縮できる。
- .NET、Copilot CLI、Officeの別installを要求しない既存契約を維持できる。
- OS package trust、repair、uninstall、updateの検証をrelease gateへ統合できる。
- macOS architectureと実測OS buildを明示し、framework一般論を製品保証へ変換しない。

### Trade-offs

- trusted Windows signingとApple Developer ID/notarizationにはrepository外のidentity、credential、運用費用が必要である。
- x64/ARM64の別artifact、別test host、別証跡を保守する。
- MSIX container下のchild process、login state、user-selected workbook I/Oを本製品で実測する必要がある。成立しない場合はsigned per-user EXE installerを別ADRで選定する。
- production credentialとmacOS clean hostsがない環境では全taskを実装しても正式公開を完了できない。

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

1. v4.2 requirement、architecture、detailed design、traceability、claim ledger、system testsを先に同期する。
2. Windows MSIXとmacOS `.app`/DMG mechanismを実装し、test-only結果を`PASS_MECHANISM`へ限定する。
3. CIはsecretなしのbuild/package structureを検査し、protected release workflowだけがproduction signing/notarizationを行う。
4. claimed platform matrixのrequired行に`NOT_RUN`、`BLOCKED_EXTERNAL`、`FAIL`があれば、そのartifactとsupport claimを公開しない。
5. package-installed appでinput不変、checkpoint/resume、final output、bundled CLI、privacy regressionを再実行する。
6. public candidateをfresh downloadしてtrust/install/launch/uninstallを再確認した後だけREADME primary pathを切り替える。

## Result

**APPROVED — IMPLEMENTATION IN PROGRESS.** Windows MSIXとmacOS RID別DMGをtarget deliveryとして採用する。ただし本ADRだけではinstaller、署名、notarization、macOS supportを実装済みまたは公開済みにしない。各platformの`PASS_PRODUCTION`証跡が揃うまで、現行public claimはWindows unsigned ZIPだけである。
