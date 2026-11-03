# カタログ

既存の機能・API・テーブル・共通部品の対応表です。1 行 1 項目の短い表にし、詳細はリンク先のファイルに任せます。
機能の表は rd-author（要求 ID・題名・決定状態）と implementer（実装ファイル・テスト・共通部品）が更新します。
未実装の要求は、実装ファイルとテストの欄を「未実装」とします。

## 機能

| 要求 ID | 題名 | 決定状態 | 実装ファイル | テスト | 使っている共通部品 |
|---|---|---|---|---|---|
| FR-001 | 採点配分と通常評価の統合画面 | 承認済み | src/StudyReportEvaluator.App/Views/QuantificationDesignView.axaml, src/StudyReportEvaluator.App/Views/QuantificationDesignView.axaml.cs | tests/system/ui/IntegratedDesignAndSettingsTests.cs | - |
| FR-002 | 設定は共通のみ | 承認済み | src/StudyReportEvaluator.App/Views/SettingsView.axaml, src/StudyReportEvaluator.App/Views/SettingsView.axaml.cs, src/StudyReportEvaluator.App/ViewModels/MainWindowViewModel.cs | tests/system/ui/IntegratedDesignAndSettingsTests.cs | - |
| FR-003 | 削除した3タブの機能の廃止 | 承認済み | src/StudyReportEvaluator.App/Composition/ServiceRegistration.cs, src/StudyReportEvaluator.App/ViewModels/MainWindowViewModel.cs, src/StudyReportEvaluator.App/Views/SettingsView.axaml, src/StudyReportEvaluator.App/Views/QuantificationDesignView.axaml, docs/settings.md, docs/getting-started.md | tests/system/ui/IntegratedDesignAndSettingsTests.cs, tests/StudyReportEvaluator.App.Tests/Settings/RetiredEditingUiRetentionTests.cs, tests/StudyReportEvaluator.App.Tests/Launch/LaunchOptionsTests.cs, tests/StudyReportEvaluator.App.Tests/Content/DocumentationContractTests.cs | - |
| FR-107 | 別名workbook出力欄の既定名は未使用名 | 承認済み | 未実装 | 未実装 | - |
| FR-108 | ジョブ実行ログのテキスト保存 | 承認済み | 未実装 | 未実装 | - |

## API・イベント

| 名前 | 定義ファイル | 関連する要求 ID |
|---|---|---|

## テーブル

| テーブル名 | 定義ファイル | 正本のシステム | 関連する要求 ID |
|---|---|---|---|

## 共通部品

| 部品名 | ファイル | 用途 | 使っている要求 ID |
|---|---|---|---|
