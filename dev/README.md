# Developer resources

StudyReport Evaluatorの開発・保守資材です。

- [開発・保守ドキュメント](docs/README.md)
- [アプリケーション版管理手順](docs/version-management.md)
- [Windows単一EXEの設計決定](docs/adr/0016-windows-one-action-startup.md)
- [Windows単一EXEの方式適合記録](docs/preflight/windows-singlefile-feasibility.md)
- [`WindowsSingleFile.pubxml`](../src/StudyReportEvaluator.App/Properties/PublishProfiles/WindowsSingleFile.pubxml): App限定のWindows single-file publish profile
- [`package-windows-singlefile.ps1`](../scripts/package-windows-singlefile.ps1): 最終EXEとSHA-256 sidecarの作成
- [`test-windows-singlefile.ps1`](../scripts/test-windows-singlefile.ps1): 開発host向けpackage検証と`PASS_DEVELOPMENT` evidence生成
- [`version.ps1`](version.ps1): 製品版の表示・設定・bump・検証
- [`version.tests.ps1`](version.tests.ps1): 版管理toolのself-test

PowerShell scriptはPowerShell Core 7以上で実行し、Windows PowerShell 5.1へfallbackしません。
開発hostのsingle-file成功はfresh Windows clean-host／本人login／公開完了を意味しません。公開判定は[Traceability](docs/traceability.md)のCH-01〜CH-06とprotected publish境界に従います。
