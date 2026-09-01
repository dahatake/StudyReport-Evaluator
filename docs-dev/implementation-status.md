# Current implementation status

| 項目 | 値 |
|---|---|
| Current requirement | `docs/requirements-definition.md` v3.0 |
| Scope ADR | ADR-0011 |
| Audited HEAD | `62581a3081f94ee9da7ec0585c17046cfe1223df` |
| GATE-ACCEPTANCE record | `PASS`（generated ignored evidence） |
| Gate-time solution tests | 452 passed / 0 failed / 0 skipped |
| Documentation refresh validation | post-gateで文書契約test 2件とvisual documentation test 1件を追加後、Release build PASS、455 passed / 0 failed / 0 skipped（2026-09-01、new gateではない） |
| Post-gate conformance gaps | 2 open |
| Optional live Copilot | `NOT_RUN` |
| Optional external recalculation | `NOT_RUN` |
| Audit date | 2026-09-01 |

Gate evidence path: `artifacts/test/gate-acceptance.json`。このfileはGit管理外であり、再実行可能なsession evidenceです。tracked summaryは本書と[`traceability.md`](traceability.md)、永続するsource identityはcommitとtest sourceです。

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

## IMPL-GAP-001 — Custom Promptをrun開始前に再検査しない

### Normative requirement

未知placeholder、未閉鎖brace等は実行前に拒否し、invalid definitionはrun開始前にfield errorを表示する必要があります。[要求定義書 §7.3](../docs/requirements-definition.md#73-placeholder)、[§13.2](../docs/requirements-definition.md#132-failure)

### Current behavior

1. Design ViewModelはPromptをrenderしてerrorを表示します。[`QuantificationDesignViewModel.cs`](../src/StudyReportEvaluator.App/ViewModels/QuantificationDesignViewModel.cs#L1187-L1261)
2. workflow navigationはDesign validityを参照しません。[`WorkflowNavigator.cs`](../src/StudyReportEvaluator.App/Navigation/WorkflowNavigator.cs#L53-L89)
3. Execution preflightはdefinition構造とmappingだけを検査し、Prompt rendererを呼びません。[`ExecutionViewModel.cs`](../src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs#L844-L903)
4. run内でpayload buildが失敗するとAIへdispatchはしませんが、unitを`AI_RUNTIME_FAILED`にします。[`EvaluationScheduler.cs`](../src/StudyReportEvaluator.App/Workflow/EvaluationScheduler.cs#L452-L472)

### Impact

利用者はDesignに明示されたerrorを残したままExecutionへ進み、run開始後にunit-level runtime failureを見る可能性があります。AIへ不正Promptを送る漏えいではありませんが、要求するfail-fast timingを満たしません。

### Required closure

- Prompt validationをCoreのsnapshot/run preflightへ統合する。
- Execution `CanStart`を同じvalidation outcomeへ接続する。
- invalid Promptでrow read、session create、runner callがすべて0件となるintegration testを追加する。

## IMPL-GAP-002 — Excel / request capacityの完全なpreflightがrun前ではない

### Normative requirement

Results列数、formula長、function arguments、request budget等から実行可能上限をrun前に計算し、超過definitionを黙って切り詰めず拒否する必要があります。[要求定義書 §9.6](../docs/requirements-definition.md#96-formula-rules)、[§14](../docs/requirements-definition.md#14-performance-and-limits)

### Current behavior

- Execution preflightはselected rows、mapping、definition、`PlannedEvaluationCount > int.MaxValue`、concurrency、authを確認します。[`ExecutionViewModel.cs`](../src/StudyReportEvaluator.App/ViewModels/ExecutionViewModel.cs#L844-L944)
- Results列16,384上限はexport時のlayout作成で確認します。[`ResultsSheetWriter.cs`](../src/StudyReportEvaluator.App/Workbooks/Writing/ResultsSheetWriter.cs#L324-L358)
- formula 8,191文字とfunction 255 argumentsはexport時に生成したASTへ確認します。[`FormulaPreflightValidator.cs`](../src/StudyReportEvaluator.Core/Formulas/FormulaPreflightValidator.cs#L61-L69)、[`FormulaPreflightValidator.cs`](../src/StudyReportEvaluator.Core/Formulas/FormulaPreflightValidator.cs#L125-L183)
- Prompt/request全体のUnicode scalar数、model context割合、worst-case retry budgetをrun admissionへ結合するproduction validatorはありません。

### Impact

AI evaluation完了後に、output columnまたはformula capacityでexport不能になる可能性があります。definition textがExcel cell上限を超える場合も、現在は主にexport時に失敗します。

### Required closure

- snapshotからResults layoutとformula shapeをI/Oなしで構築するpreflightを追加する。
- Prompt/schema最大instanceとmodel context budgetを測定する。
- worst-case unit × retryをrun admissionへ含める。
- Execution UIへsafe ID、field、actual、limitだけを表示する。

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

GATE-ACCEPTANCE artifactは上記2差分を検出する前に生成されたため、artifact自体を書き換えません。post-gate auditとして本書とtraceabilityへ追記します。新しいfinal acceptanceを主張するには、2差分をcloseし、全required checksとindependent reviewを再実行する必要があります。
