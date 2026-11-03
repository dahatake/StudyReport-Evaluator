# Developer resources

StudyReport Evaluatorの開発・保守資材です。

- [開発・保守ドキュメント](docs/README.md)
- [アプリケーション版管理手順](docs/version-management.md)
- [Windows単一EXEの設計決定](docs/adr/0016-windows-one-action-startup.md)
- [Windows単一EXEの方式適合記録](docs/preflight/windows-singlefile-feasibility.md)
- [`build-singlefile.ps1`](../build-singlefile.ps1): 単一EXEのビルド・パッケージ作成・起動をまとめて実行
- [`build-singlefile.tests.ps1`](build-singlefile.tests.ps1): ビルド・起動scriptの成功／失敗経路のself-test
- [`WindowsSingleFile.pubxml`](../src/StudyReportEvaluator.App/Properties/PublishProfiles/WindowsSingleFile.pubxml): App限定のWindows single-file publish profile
- [`package-windows-singlefile.ps1`](../scripts/package-windows-singlefile.ps1): 最終EXEとSHA-256 sidecarの作成
- [`test-windows-singlefile.ps1`](../scripts/test-windows-singlefile.ps1): 開発host向けpackage検証と`PASS_DEVELOPMENT` evidence生成
- [`version.ps1`](version.ps1): 製品版の表示・設定・bump・検証
- [`version.tests.ps1`](version.tests.ps1): 版管理toolのself-test

PowerShell scriptはPowerShell Core 7以上で実行し、Windows PowerShell 5.1へfallbackしません。
開発hostのsingle-file成功はfresh Windows clean-host／本人login／公開完了を意味しません。公開判定は[Traceability](docs/traceability.md)のCH-01〜CH-06とprotected publish境界に従います。

## 単一EXEをビルドして起動する

Windows 11 x64、x64 PowerShell 7以上、Git、[`global.json`](../global.json)に適合する.NET SDKが必要です。初回restoreと同梱Copilot CLIの取得にはnetworkも必要です。生成したEXEの実行にはSDKは不要です。

```powershell
# 現在の作業ツリーからビルドして起動（未commitの変更も含む）
pwsh -NoLogo -NoProfile -File .\build-singlefile.ps1 -SkipPull

# originからfast-forwardで最新化してビルド・起動（tracked fileに変更がない場合）
pwsh -NoLogo -NoProfile -File .\build-singlefile.ps1

# 起動せず、配布用のEXEとSHA-256 sidecarを作成するだけ
pwsh -NoLogo -NoProfile -File .\build-singlefile.ps1 -SkipPull -SkipLaunch

# scriptの回帰テスト（実アプリのビルドや起動は行わない）
pwsh -NoLogo -NoProfile -File .\dev\build-singlefile.tests.ps1
```

上記はrepository rootでの例です。別directoryからはscriptの絶対pathを指定してください。内部pathはscriptの場所を基準に解決し、呼び出し元の作業directoryを復元します。

既存の[`publish-windows.ps1`](../scripts/publish-windows.ps1)と[`package-windows-singlefile.ps1`](../scripts/package-windows-singlefile.ps1)を順番に呼び、`artifacts\package\StudyReportEvaluator-win-x64.exe`を起動します。既存のsingle-file profileで.NET runtime、native library、Copilot CLI、利用ガイドを同梱します。いずれかの段階が失敗した場合、古いEXEを起動せずエラーで終了します。起動要求の成功とGUIの正常表示は別の確認です。

`-Validate`を追加すると、起動前に既存の実EXE検証も行います。この検証はPowerShell 7.4以上、interactive desktopとclean checkoutを必要とし、未commitの変更がある場合は失敗して起動しません。事前に`dotnet build tests\StudyReportEvaluator.App.Tests -c Release`で検証用testをビルドしてください（検証script自体は`--no-build --no-restore`で実行します）。変更中の開発hostで実EXEを検証する場合は、ビルド後に[`test-windows-singlefile.ps1`](../scripts/test-windows-singlefile.ps1)を`-DevelopmentOnly`で別途実行します。これは公開可否やclean-host試験の代替ではありません。
