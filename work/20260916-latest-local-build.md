# 最新ローカルビルド（2026-09-16）

目的は現在ソースの起動可能なWindowsビルドを揃えること。公開・署名・バージョン番号変更・依存ロック更新は行わない。

## 成果物

- 製品: 0.8.6（未公開候補）
- HEAD: ea91d9f4a532d836a1c57d61369d6523081627e3。作業ツリーの未コミット修正を含む開発ビルド。
- 単一EXE: `artifacts/package/StudyReportEvaluator-win-x64.exe`
- ZIP: `artifacts/package/StudyReportEvaluator-win-x64.zip`
- フォルダー版: `artifacts/package/publish/win-x64/StudyReportEvaluator.App.exe`
- EXE／ZIPの各`.sha256`は実ファイルと一致確認済み。

## 実施内容

1. 直前のApp.Tests全体は1781 Passed、0 Failed、8 opt-in Skipped（`TestResults/app-fixes-20260916/acceptance/`）。
2. 単一EXEのpublishで、SDK 10.0.401が暗黙ILLink 10.0.12を選び、専用lockの10.0.11と不一致になった。
3. `WindowsSingleFile.pubxml`のApp限定groupにRuntimeFrameworkVersion 10.0.11と、net10.0のKnownILLinkPackメタデータ10.0.11を明示。通常ビルド・Core・canonical lockは変更しない。PublishTrimmed=falseを維持。
4. `publish-windows.ps1 -SingleFile`成功。専用lock照合、同梱CLI hash、展開内容、runtime10.0.11、製品版、起動の検査を通過。
5. `package-windows-singlefile.ps1`成功。EXE／sidecar生成。
6. 旧0.8.0のZIP検証記録は現在ZIPのhashと不一致のため、`artifacts/test/archived-evidence/20260916/StudyReportEvaluator-win-x64.v0.8.0.evidence.json`へ退避。現行の公開検証記録として流用しない。
7. プロファイル固定値とApp限定条件の回帰テストを追加し成功。MSBuild評価でも固定が単一EXE Appだけに適用されることを確認。

## 検証状態

- 単一EXE publish・package: 成功。
- EXE／ZIP checksum: 一致。
- WindowsSingleFilePublishTests／WindowsSingleFileArtifactTests（実成果物opt-inを含む）: 100 Passed、0 Failed、0 Skipped。`TestResults/latest-build-20260916/singlefile-contract/dahatake_DAHATAKE-OFFICE_2026-09-16_15_38_44_net10.0.trx`。
- 新規profile回帰テスト: 1 Passed。`TestResults/latest-build-20260916/profile-regression/dahatake_DAHATAKE-OFFICE_2026-09-16_15_47_34_net10.0.trx`。
- GUI起動・再起動・cache復旧等のP06七シナリオ: 7 Passed、0 Failed、0 Skipped。`artifacts/test/singlefile/StudyReportEvaluator-win-x64.exe.evidence.json`はPASS_DEVELOPMENT。
- 上記検証記録のEXE hashと現在EXEの一致、ZIP内App/Coreとフォルダー版のバイト一致・製品版0.8.6を最終確認済み。

ローカルWindowsビルドの生成・検証は完了。前回全体実行でスキップした実成果物8件も今回別実行で成功した（全体テストを再度まとめて実行したという意味ではない）。

### 最終チェックサム

- EXE（283496648 bytes）: `11B2FACB33F4CDA3AA89AC83066363463E126151B24A947B475AC8FFCDC49A83`
- ZIP（156603464 bytes）: `4BA8DF00C3C11AEF19F3B5295468E3A2FF26CF9EB5193264727449A2F07099E9`

起動可能なローカル開発ビルドの検証であり、clean-host／実ログイン／実AI／公開準備完了を意味しない。署名なし。未コミット変更を含むためP07はDevelopmentOnlyで検証した。記録のsourceContentSha256は検証実行時点のもの（本完了記録更新前）である。