# Current implementation status

| 項目 | 値 |
|---|---|
| Current requirement | `docs/requirements-definition.md` v3.0 |
| Scope ADR | ADR-0011 |
| Gap closure implementation | `69e4b992711c243fe7c70b0defff5e6abf03865c` |
| Historical GATE-ACCEPTANCE record | `PASS` for `62581a3081f94ee9da7ec0585c17046cfe1223df`（generated ignored evidence） |
| Gate-time solution tests | 452 passed / 0 failed / 0 skipped |
| Documentation refresh validation | post-gateで文書契約test 2件とvisual documentation test 1件を追加後、Release build PASS、455 passed / 0 failed / 0 skipped（2026-09-01、new gateではない） |
| Gap closure non-document validation | Release build PASS、473 passed / 0 failed / 0 skipped（2026-09-01） |
| Current required validation | locked restore PASS、Release build PASS、485 passed / 0 failed / 0 skipped（2026-09-01、external opt-inなし） |
| Post-gate conformance gaps | 0 open / 2 closed |
| New GATE-ACCEPTANCE | `READY_FOR_GENERATED_RECORD` |
| Optional live Copilot | `PASS` — fixed synthetic payload、content data included false、required substitute false |
| Optional external recalculation | `PASS` — synthetic workbook / registered Microsoft Excel、required substitute false |
| Audit date | 2026-09-01 |

旧gate evidence pathは`artifacts/test/gate-acceptance.json`です。このfileはGit管理外であり、HEAD `62581a3`の履歴を保持します。新しいacceptanceはgap closure、文書同期、全required rerun、independent review後に別のgenerated evidenceとして記録します。tracked summaryは本書と[`traceability.md`](traceability.md)、永続するsource identityはcommitとtest sourceです。

## 実装済みsurface

| Surface | Production owner | Required evidence |
|---|---|---|
| `.xlsx` classification / immutable input | [`FileFormatClassifier.cs`](../src/StudyReportEvaluator.App/Workbooks/Intake/FileFormatClassifier.cs)、[`InputSnapshotService.cs`](../src/StudyReportEvaluator.App/Workbooks/Intake/InputSnapshotService.cs) | [`FileFormatClassifierTests.cs`](../tests/StudyReportEvaluator.App.Tests/Workbooks/Intake/FileFormatClassifierTests.cs)、[`InputSnapshotServiceTests.cs`](../tests/StudyReportEvaluator.App.Tests/Workbooks/Intake/InputSnapshotServiceTests.cs) |
| mapping | [`ColumnMappingSuggester.cs`](../src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingSuggester.cs)、[`ColumnMappingValidator.cs`](../src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingValidator.cs) | [`ColumnMappingSuggesterTests.cs`](../tests/StudyReportEvaluator.App.Tests/Workbooks/Mapping/ColumnMappingSuggesterTests.cs)、[`SampleWorkbookStructuralTests.cs`](../tests/StudyReportEvaluator.App.Tests/E2E/SampleWorkbookStructuralTests.cs) |
| dynamic definition / snapshot | [`Domain`](../src/StudyReportEvaluator.Core/Domain/)、[`QuantificationDefinitionValidator.cs`](../src/StudyReportEvaluator.Core/Validation/QuantificationDefinitionValidator.cs)、[`CanonicalDefinitionSerializer.cs`](../src/StudyReportEvaluator.Core/Serialization/CanonicalDefinitionSerializer.cs) | [`Domain tests`](../tests/StudyReportEvaluator.Core.Tests/Domain/)、[`QuantificationDefinitionValidatorTests.cs`](../tests/StudyReportEvaluator.Core.Tests/Validation/QuantificationDefinitionValidatorTests.cs) |
| Prompt / selected-row payload | [`Prompting`](../src/StudyReportEvaluator.Core/Prompting/) | [`Prompting tests`](../tests/StudyReportEvaluator.Core.Tests/Prompting/) |
| closed Copilot tool / retry / cleanup | [`Copilot`](../src/StudyReportEvaluator.App/Copilot/) | [`Copilot fake-runtime tests`](../tests/StudyReportEvaluator.App.Tests/Copilot/) |
| score / formula AST | [`Scoring`](../src/StudyReportEvaluator.Core/Scoring/)、[`Formulas`](../src/StudyReportEvaluator.Core/Formulas/) | [`Scoring tests`](../tests/StudyReportEvaluator.Core.Tests/Scoring/)、[`Formula tests`](../tests/StudyReportEvaluator.Core.Tests/Formulas/) |
| Config / Results / Run / atomic output | [`Workbooks`](../src/StudyReportEvaluator.App/Workbooks/) | [`Workbook tests`](../tests/StudyReportEvaluator.App.Tests/Workbooks/) |
| 4-step UI / keyboard / 200% | [`Views`](../src/StudyReportEvaluator.App/Views/)、[`ViewModels`](../src/StudyReportEvaluator.App/ViewModels/) | [`UI headless tests`](../tests/StudyReportEvaluator.App.Tests/UI/) |
| Windows x64 package | [`publish-windows.ps1`](../scripts/publish-windows.ps1)、[`package-windows.ps1`](../scripts/package-windows.ps1) | [`WindowsPublishPackageTests.cs`](../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs) |

詳細なAC/TR対応は[`traceability.md`](traceability.md)を参照してください。

## IMPL-GAP-001 — CLOSED: Custom Promptのrun開始前再検査

### Normative requirement

未知placeholder、未閉鎖brace等は実行前に拒否し、invalid definitionはrun開始前にfield errorを表示する必要があります。[要求定義書 §7.3](../docs/requirements-definition.md#73-placeholder)、[§13.2](../docs/requirements-definition.md#132-failure)

### Closure evidence

1. Knowledge / Custom ownership、built-in version、placeholder構文を[`QuantificationDefinitionValidator.cs`](../src/StudyReportEvaluator.Core/Validation/QuantificationDefinitionValidator.cs)へ統合しました。
2. Design、Execution `CanStart`、`QuantificationSnapshot.Create`が同じCore validation outcomeを使用します。
3. unknown / unclosed / malformed / unmatched brace、必須placeholder欠落、Knowledge / Custom ownershipをCore testで検証します。
4. [`QuantificationOrchestratorTests.cs`](../tests/StudyReportEvaluator.App.Tests/Workflow/QuantificationOrchestratorTests.cs)はinvalid Promptでinput capture、row read、runner callがすべて0件であることを検証します。sessionはrunnerより下流なので作成されません。

Closure commit: `69e4b992711c243fe7c70b0defff5e6abf03865c`。

## IMPL-GAP-002 — CLOSED: Excel / request capacityのrun前preflight

### Normative requirement

Results列数、formula長、function arguments、request budget等から実行可能上限をrun前に計算し、超過definitionを黙って切り詰めず拒否する必要があります。[要求定義書 §9.6](../docs/requirements-definition.md#96-formula-rules)、[§14](../docs/requirements-definition.md#14-performance-and-limits)

### Closure evidence

- [`WorkbookExecutionPreflight.cs`](../src/StudyReportEvaluator.App/Workbooks/Writing/WorkbookExecutionPreflight.cs)はsnapshotとworkbook metadataからConfig address map、Results layout、最終rowの実formula ASTをI/Oなしで構築します。
- `ResultsSheetWriter.Preflight`とexportが同じlayout / `FormulaExpressions` / `FormulaPreflightValidator`を共有し、列16,384、cell 32,767、formula 8,191、function 255、reference、DAGを同じ判定にします。
- [`EvaluationRequestCapacityValidator.cs`](../src/StudyReportEvaluator.App/Copilot/EvaluationRequestCapacityValidator.cs)は全selected rowの実payloadとclosed schemaをAI dispatch前に構築し、app-owned request 65,536 Unicode scalars、SDK model prompt/context上限の80%を保守的UTF-8境界で検査します。UTF-8 byte数をtoken実測値とは表記しません。
- evaluation units × 最大3 attemptsは20,000以下を要求します。Execution UIはfield、actual、limitだけを表示し、回答またはPrompt本文を含めません。
- inputはrequest preflight後にexact hash / size / last-write timeを再確認し、drift時はrunner call 0で停止します。

Closure commit: `69e4b992711c243fe7c70b0defff5e6abf03865c`。

## Documentation-only gaps addressed

次はproduction behaviorを変更せず、本ドキュメント更新で明確化しました。

- native file pickerはなく、入出力pathを直接入力する。
- 保存済みoutput workbookの再import reviewはない。
- Results UIとoutput workbookで表示項目が異なる。
- Copilotへ送るPromptにはselected cell values以外にdefinition/schema metadataも含まれる。
- output workbookは入力全体とPrompt/rubricを保持するため、入力と同等以上に機密である。
- standard `.xlsx`でもresource limitやexternal relationshipにより拒否される。
- Copilot CLIはpackageに含まれず、Windows `PATH`から探索する。

## Evidence boundary

旧GATE-ACCEPTANCE artifactは上記2差分を検出する前に生成されたため、artifact自体を書き換えません。2差分のcode/test closure、独立レビュー、文書同期後のlocked restore / Release build / 485 testsは完了し、現在は固定HEADに対する新しいgenerated acceptance record作成待ちです。完了までは過去のPASSを新HEADへ流用しません。
