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
| FR-CD-01 | 評価項目の説明の初期値（種類別） | 承認済み | src/StudyReportEvaluator.Core/Prompting/DefaultCriterionDescriptions.cs, src/StudyReportEvaluator.Core/Domain/EvaluatorDefinition.cs, src/StudyReportEvaluator.Core/Prompting/BuiltInPromptTemplates.cs, src/StudyReportEvaluator.Core/Serialization/CanonicalDefinitionSerializer.cs, src/StudyReportEvaluator.App/ViewModels/QuantificationDesignViewModel.cs, src/StudyReportEvaluator.App/ViewModels/InputViewModel.cs, src/StudyReportEvaluator.App/Views/EvaluatorSettingsView.axaml, src/StudyReportEvaluator.App/Workbooks/Writing/ConfigSheetWriter.cs | tests/StudyReportEvaluator.App.Tests/UI/DefaultCriterionDescriptionTests.cs | - |
| FR-CD-02 | 初期値は新規作成時だけに設定 | 承認済み | src/StudyReportEvaluator.App/ViewModels/QuantificationDesignViewModel.cs, src/StudyReportEvaluator.Core/Serialization/CanonicalDefinitionSerializer.cs | tests/StudyReportEvaluator.App.Tests/UI/DefaultCriterionDescriptionTests.cs | - |
| FR-CD-03 | 名前・重み・rangeの不変と検証エラー0件 | 承認済み | src/StudyReportEvaluator.Core/Prompting/DefaultCriterionDescriptions.cs, src/StudyReportEvaluator.App/ViewModels/InputViewModel.cs | tests/StudyReportEvaluator.App.Tests/UI/DefaultCriterionDescriptionTests.cs | - |
| FR-109 | Prompt種類の組込みテンプレート評価とEvaluatorTypeの3値 | 承認済み | - | - | - |
## API・イベント

| 名前 | 定義ファイル | 関連する要求 ID |
|---|---|---|

## テーブル

| テーブル名 | 定義ファイル | 正本のシステム | 関連する要求 ID |
|---|---|---|---|

## 共通部品

| 部品名 | ファイル | 用途 | 使っている要求 ID |
|---|---|---|---|
