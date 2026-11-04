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
| FR-107 | 別名workbook出力欄の既定名は未使用名 | 承認済み | src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs, src/StudyReportEvaluator.App/Workbooks/Writing/OutputPathPlanner.cs | tests/system/integration/OutputNamingAndRunLogTests.cs, tests/StudyReportEvaluator.App.Tests/Logging/RunTextLogTests.cs, tests/StudyReportEvaluator.App.Tests/E2E/SettingsWorkflowSystemTests.cs | OutputPathPlanner |
| FR-111 | [実行]画面の同一情報は1か所だけ表示する | 承認済み | src/StudyReportEvaluator.App/Views/ExecutionView.axaml, src/StudyReportEvaluator.App/Views/ExecutionView.axaml.cs, src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs, src/StudyReportEvaluator.App/Views/MainWindow.axaml | tests/StudyReportEvaluator.App.Tests/UI/ExecutionViewTests.cs, tests/StudyReportEvaluator.App.Tests/UI/MainWindowSettingsTests.cs | なし |
| FR-108 | ジョブ実行ログのテキスト保存 | 承認済み | src/StudyReportEvaluator.App/Logging/RunTextLog.cs, src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs, src/StudyReportEvaluator.App/Workflow/RunSummary.cs, src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs | tests/system/integration/OutputNamingAndRunLogTests.cs, tests/StudyReportEvaluator.App.Tests/Logging/RunTextLogTests.cs | RunTextLog |
| FR-109 | Prompt種類の組込みテンプレート評価とEvaluatorTypeの3値 | 承認済み | 未実装 | tests/StudyReportEvaluator.App.Tests/Workbooks/Writing/Fr109EvaluatorTypeConfigSheetTests.cs, tests/StudyReportEvaluator.Core.Tests/Prompting/Fr109PromptKindTemplateTests.cs | - |
| FR-110 | 設定シートからの採点定義の読み戻し（ConfigSheetReader） | 承認済み | src/StudyReportEvaluator.App/Workbooks/Reading/ConfigSheetReader.cs, src/StudyReportEvaluator.Core/Serialization/CanonicalDefinitionDeserializer.cs | tests/StudyReportEvaluator.App.Tests/Workbooks/Reading/Fr110ConfigSheetReaderTests.cs, tests/StudyReportEvaluator.App.Tests/Workbooks/Writing/Fr109EvaluatorTypeConfigSheetTests.cs | - |
| FR-CD-01 | 評価項目の説明の初期値（種類別） | 承認済み | src/StudyReportEvaluator.Core/Prompting/DefaultCriterionDescriptions.cs, src/StudyReportEvaluator.Core/Domain/EvaluatorDefinition.cs, src/StudyReportEvaluator.Core/Prompting/BuiltInPromptTemplates.cs, src/StudyReportEvaluator.Core/Serialization/CanonicalDefinitionSerializer.cs, src/StudyReportEvaluator.App/ViewModels/QuantificationDesignViewModel.cs, src/StudyReportEvaluator.App/ViewModels/InputViewModel.cs, src/StudyReportEvaluator.App/Views/EvaluatorSettingsView.axaml, src/StudyReportEvaluator.App/Workbooks/Writing/ConfigSheetWriter.cs | tests/StudyReportEvaluator.App.Tests/UI/DefaultCriterionDescriptionTests.cs | - |
| FR-CD-02 | 初期値は新規作成時だけに設定 | 承認済み | src/StudyReportEvaluator.App/ViewModels/QuantificationDesignViewModel.cs, src/StudyReportEvaluator.Core/Serialization/CanonicalDefinitionSerializer.cs | tests/StudyReportEvaluator.App.Tests/UI/DefaultCriterionDescriptionTests.cs | - |
| FR-CD-03 | 名前・重み・rangeの不変と検証エラー0件 | 承認済み | src/StudyReportEvaluator.Core/Prompting/DefaultCriterionDescriptions.cs, src/StudyReportEvaluator.App/ViewModels/InputViewModel.cs | tests/StudyReportEvaluator.App.Tests/UI/DefaultCriterionDescriptionTests.cs | - |
| FR-112 | 結果画面で選んだ行だけの再実行 | 承認済み | src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs, src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.Rerun.cs, src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.Rerun.cs, src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs, src/StudyReportEvaluator.App/ViewModels/MainWindowViewModel.cs, src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs, src/StudyReportEvaluator.App/Workflow/QuantificationOrchestrator.cs, src/StudyReportEvaluator.App/Views/ResultsOutputView.axaml, src/StudyReportEvaluator.App/Views/ExecutionView.axaml, docs/settings.md | tests/system/integration/SelectedRowRerunTests.cs, tests/StudyReportEvaluator.App.Tests/UI/SelectedRowRerunTests.cs | デザイン基盤, OutputPathPlanner |
| FR-113 | 結果workbookへの「採点結果」シート（評価点と総合点だけの一覧） | 承認済み | src/StudyReportEvaluator.App/Workbooks/Writing/ScoreSummarySheetWriter.cs, src/StudyReportEvaluator.App/Workbooks/Writing/ResultsSheetWriter.cs, src/StudyReportEvaluator.App/Workbooks/Writing/AppOwnedSheetNameResolver.cs, src/StudyReportEvaluator.App/Workbooks/Validation/OutputPackageValidator.cs, src/StudyReportEvaluator.App/Workflow/DurableRunFinalizer.cs | tests/system/integration/ScoreSummarySheetTests.cs, tests/StudyReportEvaluator.App.Tests/Workbooks/Writing/ScoreSummarySheetWriterTests.cs | AppOwnedSheetNameResolver |
| FR-114 | 未検証類似度の確認指標化と採点保護 | 承認済み | src/StudyReportEvaluator.Core/Similarity/SurfaceTextSimilarityCalculator.cs, src/StudyReportEvaluator.Core/Similarity/SimilarityDiagnostics.cs, src/StudyReportEvaluator.Core/Scoring/WeightedScoreCalculator.cs, src/StudyReportEvaluator.Core/Formulas/FormulaExpression.cs, src/StudyReportEvaluator.Core/Domain/QuantificationDefinition.cs, src/StudyReportEvaluator.Core/Serialization/CanonicalDefinitionDeserializer.cs, src/StudyReportEvaluator.App/ViewModels/QuantificationDesignViewModel.cs, src/StudyReportEvaluator.App/Workbooks/Writing/ResultsSheetWriter.cs, src/StudyReportEvaluator.App/Workbooks/Checkpoint/CheckpointEnvelope.cs, src/StudyReportEvaluator.App/Workbooks/Checkpoint/CheckpointPayloadCodec.cs, src/StudyReportEvaluator.App/Workflow/DurableEvaluationScheduler.cs, src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs, src/StudyReportEvaluator.App/Workflow/RunSummary.cs, src/StudyReportEvaluator.App/Workflow/LivePreviewTracker.cs, src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs, src/StudyReportEvaluator.App/Views/ResultsOutputView.axaml | tests/system/integration/SimilarityIndicatorTests.cs, tests/StudyReportEvaluator.Core.Tests/Similarity/SimilarityDiagnosticsTests.cs, tests/StudyReportEvaluator.Core.Tests/Formulas/FormulaSerializerTests.cs, tests/StudyReportEvaluator.App.Tests/Workbooks/Writing/ResultsSheetWriterTests.cs, tests/StudyReportEvaluator.App.Tests/UI/QuantificationDesignViewTests.cs, tests/StudyReportEvaluator.App.Tests/Content/ResultExcelDescriptionTests.cs | 類似度確認指標, デザイン基盤 |
| FR-115 | 質問文字列による複数参照と条件・互換性の一貫性 | 承認済み | 未実装 | 未実装 | - |
| FR-116 | ラベル付きローカル対照データによる再現評価 | 承認済み | 未実装 | 未実装 | - |

## API・イベント

| 名前 | 定義ファイル | 関連する要求 ID |
|---|---|---|
| DurableQuantificationOrchestrator.RerunRowsAsync（選んだ行だけの再実行） | src/StudyReportEvaluator.App/Workflow/DurableQuantificationOrchestrator.cs | FR-112 |
| ResultsOutputViewModel.RerunRequested（対象行の確定イベント） | src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.Rerun.cs | FR-112 |

## テーブル

| テーブル名 | 定義ファイル | 正本のシステム | 関連する要求 ID |
|---|---|---|---|

## 共通部品

| 部品名 | ファイル | 用途 | 使っている要求 ID |
|---|---|---|---|
| デザイン基盤 | src/StudyReportEvaluator.App/App.axaml, src/StudyReportEvaluator.App/Styles/Accessibility.axaml, src/StudyReportEvaluator.App/Resources/WorkflowIcons.axaml | 既存Avalonia FluentTheme、共通brush・フォーカス・44 DIP操作対象・アイコンを再利用。今回独自テーマは追加しない | FR-002, FR-112, FR-114 |
| 設定保存 | src/StudyReportEvaluator.App/Settings/SettingsFileStore.cs, src/StudyReportEvaluator.App/Settings/ApplicationSettings.cs | 既存schemaの絶対出力先/nullと次回設定を明示保存・復元。run snapshotとモデルcache自動保存契約は維持 | FR-002 |
| ネイティブ出力フォルダー選択 | src/StudyReportEvaluator.App/Views/NativeOutputFolderPicker.cs | 入力選択と同じTopLevel.StorageProviderで単一フォルダーを選択。取消はnull、非local/利用不可は理由つき失敗。IO作成なし | FR-002 |
| OutputPathPlanner | src/StudyReportEvaluator.App/Workbooks/Writing/OutputPathPlanner.cs | `eval-yyyyMMdd-HHmm[-NN]` の未使用名の採番（出力予約と別名既定名） | FR-107, FR-112 |
| RunTextLog | src/StudyReportEvaluator.App/Logging/RunTextLog.cs | ジョブ実行ログの `{yyyyMMddHHmm}-log.txt` 保存（閉じたコードと数値のみ） | FR-108 |
| AppOwnedSheetNameResolver | src/StudyReportEvaluator.App/Workbooks/Writing/AppOwnedSheetNameResolver.cs | app-owned シート名（Quantification_*、完成workbookの `採点結果`）の衝突回避と実際の名前の束縛 | FR-113 |
| 類似度確認指標 | src/StudyReportEvaluator.Core/Similarity/SimilarityDiagnostics.cs | 参照別の確認指標（未検証）の詳細（全文一致・最長一致・双方被覆率・正規化後位置・短文注意 PARAM-004）、参照別最大値の選択、「確認指標・未検証」の表示文言 | FR-114 |
