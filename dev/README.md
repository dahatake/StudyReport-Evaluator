# Developer resources

StudyReport Evaluatorの開発・保守資材です。

- [開発・保守ドキュメント](docs/README.md)
- [アプリケーション版管理手順](docs/version-management.md)
- [`version.ps1`](version.ps1): 製品版の表示・設定・bump・検証
- [`version.tests.ps1`](version.tests.ps1): 版管理toolのself-test

PowerShell scriptはPowerShell Core 7以上で実行し、Windows PowerShell 5.1へfallbackしません。
