# カタログ

既存の機能・API・テーブル・共通部品の対応表です。1 行 1 項目の短い表にし、詳細はリンク先のファイルに任せます。
機能の表は rd-author（要求 ID・題名・決定状態）と implementer（実装ファイル・テスト・共通部品）が更新します。
未実装の要求は、実装ファイルとテストの欄を「未実装」とします。

## 機能

| 要求 ID | 題名 | 決定状態 | 実装ファイル | テスト | 使っている共通部品 |
|---|---|---|---|---|---|
| FR-001 | 採点配分と通常評価の統合画面 | 承認済み | src/StudyReportEvaluator.App/Views/QuantificationDesignView.axaml, src/StudyReportEvaluator.App/Views/QuantificationDesignView.axaml.cs | tests/system/ui/IntegratedDesignAndSettingsTests.cs | - |
| FR-002 | 共通設定と出力フォルダー選択の統一 | 承認済み | src/StudyReportEvaluator.App/Views/SettingsView.axaml, src/StudyReportEvaluator.App/Views/SettingsView.axaml.cs, src/StudyReportEvaluator.App/ViewModels/SettingsViewModel.cs, src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs, src/StudyReportEvaluator.App/ViewModels/MainWindowViewModel.cs, src/StudyReportEvaluator.App/Views/NativeOutputFolderPicker.cs | tests/system/ui/IntegratedDesignAndSettingsTests.cs, tests/system/ui/OutputFolderSelectionTests.cs, tests/system/test_fr002_output_folder.py, tests/StudyReportEvaluator.App.Tests/UI/SettingsViewTests.cs, tests/StudyReportEvaluator.App.Tests/UI/SettingsViewModelTests.cs, tests/StudyReportEvaluator.App.Tests/UI/SettingsAccessibilityTests.cs, tests/StudyReportEvaluator.App.Tests/E2E/SettingsWorkflowSystemTests.cs | デザイン基盤, 設定保存, ネイティブ出力フォルダー選択 |
| FR-003 | 削除した3タブの機能の廃止 | 承認済み | src/StudyReportEvaluator.App/Composition/ServiceRegistration.cs, src/StudyReportEvaluator.App/ViewModels/MainWindowViewModel.cs, src/StudyReportEvaluator.App/Views/SettingsView.axaml, src/StudyReportEvaluator.App/Views/QuantificationDesignView.axaml, docs/settings.md, docs/getting-started.md | tests/system/ui/IntegratedDesignAndSettingsTests.cs, tests/StudyReportEvaluator.App.Tests/Settings/RetiredEditingUiRetentionTests.cs, tests/StudyReportEvaluator.App.Tests/Launch/LaunchOptionsTests.cs, tests/StudyReportEvaluator.App.Tests/Content/DocumentationContractTests.cs | - |

## API・イベント

| 名前 | 定義ファイル | 関連する要求 ID |
|---|---|---|

## テーブル

| テーブル名 | 定義ファイル | 正本のシステム | 関連する要求 ID |
|---|---|---|---|

## 共通部品

| 部品名 | ファイル | 用途 | 使っている要求 ID |
|---|---|---|---|
| デザイン基盤 | src/StudyReportEvaluator.App/App.axaml, src/StudyReportEvaluator.App/Styles/Accessibility.axaml, src/StudyReportEvaluator.App/Resources/WorkflowIcons.axaml | 既存Avalonia FluentTheme、共通brush・フォーカス・44 DIP操作対象・アイコンを再利用。今回独自テーマは追加しない | FR-002 |
| 設定保存 | src/StudyReportEvaluator.App/Settings/SettingsFileStore.cs, src/StudyReportEvaluator.App/Settings/ApplicationSettings.cs | 既存schemaの絶対出力先/nullと次回設定を明示保存・復元。run snapshotとモデルcache自動保存契約は維持 | FR-002 |
| ネイティブ出力フォルダー選択 | src/StudyReportEvaluator.App/Views/NativeOutputFolderPicker.cs | 入力選択と同じTopLevel.StorageProviderで単一フォルダーを選択。取消はnull、非local/利用不可は理由つき失敗。IO作成なし | FR-002 |
