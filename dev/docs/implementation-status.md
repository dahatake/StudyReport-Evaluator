# Current implementation status

| 項目 | 値 |
|---|---|
| Current requirement | `docs/requirements-definition.md` v4.1 |
| Scope ADR | ADR-0012（機能）/ ADR-0013（platform） |
| Product version | `1.0.0` — `Directory.Build.props`の明示`VersionPrefix` |
| Version management | [`version-management.md`](version-management.md) / [`dev/version.ps1`](../version.ps1) / ADR-0014 |
| Source baseline | working tree based on `c9db2fc038cc09874a142d6575b47bdb49ac4b55` |
| Public release status | `IMPLEMENTATION_IN_PROGRESS` |
| Release build | PASS、warning 0 / error 0（2026-09-02実行） |
| Focused UI validation | `MainWindowTests` 8/8 PASS（2026-09-02実行） |
| Bundled CLI validation | `CopilotClientFactoryTests` 14/14 PASS（2026-09-02実行） |
| Documentation contract | 14/14 PASS（2026-09-02実行。developer docs移動・版管理契約を含む） |
| Screenshot generation | 7画像再生成、1440×1050、test 1/1 PASS、敵対review finding 0（2026-09-02実行） |
| Windows package integration | publish/package/展開/resolver/clean launch/repackage、全class 3/3 PASS（2026-09-02実行） |
| Independent adversarial review | task別reviewと最終3観点reviewを実施。再現可能な指摘を反映後、文書＋package契約16/16 PASS |
| B-07 focused validation | `SampleReport.xlsx` identity/structure/input不変 1/1 PASS、synthetic fixture isolation 1/1 PASS、敵対review finding 0 |
| Full required validation | B-07更新後のfull aggregateは本依頼範囲外のため未再実行。直前runはsample欠落で664/665 PASS |
| Optional live Copilot | `PASS` — fixed synthetic payload、required substitute false（2026-09-02実行） |
| Optional external recalculation | `FAILED_ADVISORY` — synthetic workbookは分類/schema PASS、Excel COM openが`0x800A03EC`で失敗（2026-09-02実行） |
| Version tool validation | show/verify/set/bump/dry-run/invalid rejection、11 assertions PASS（2026-09-02実行） |
| Audit date | 2026-09-02 |

現在の正式公開作業は[`20260902-readme-end-user-release-plan.md`](../../work/20260902-readme-end-user-release-plan.md)に従う。B-01〜B-04、B-06〜B-08は実装・focused test・敵対的reviewまで完了した。B-05（実在release asset）は未完了であり、正式公開済みとは扱わない。

直前のfull solution testは665件中664件成功し、当時未配置だったsample test 1件だけが失敗した。その後、ユーザーが配置した`sample/SampleReport.xlsx`を新正本としてread-only再profileし、direct sample testは1/1成功、検査前後のSHA-256、size、last-write timeも一致した。本依頼はB-07だけのためfull aggregateは再実行していない。Windows 531行synthetic read/write/final validationの最新3回中央値は4.074791秒で30秒基準内である。

## 実装済みsurface

| Surface | Production owner | Required evidence |
|---|---|---|
| `.xlsx` classification / immutable input | [`FileFormatClassifier.cs`](../../src/StudyReportEvaluator.App/Workbooks/Intake/FileFormatClassifier.cs)、[`InputSnapshotService.cs`](../../src/StudyReportEvaluator.App/Workbooks/Intake/InputSnapshotService.cs) | [`FileFormatClassifierTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Intake/FileFormatClassifierTests.cs)、[`InputSnapshotServiceTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Intake/InputSnapshotServiceTests.cs) |
| picker / mapping | [`InputView.axaml.cs`](../../src/StudyReportEvaluator.App/Views/InputView.axaml.cs)、[`ColumnMappingSuggester.cs`](../../src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingSuggester.cs)、[`ColumnMappingValidator.cs`](../../src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingValidator.cs) | [`InputViewTests.cs`](../../tests/StudyReportEvaluator.App.Tests/UI/InputViewTests.cs)、[`ColumnMappingSuggesterTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Mapping/ColumnMappingSuggesterTests.cs) |
| v4.1 definition / allocation / snapshot | [`Domain`](../../src/StudyReportEvaluator.Core/Domain/)、[`ScoringAllocationCalculator.cs`](../../src/StudyReportEvaluator.Core/Scoring/ScoringAllocationCalculator.cs)、[`QuantificationDefinitionValidator.cs`](../../src/StudyReportEvaluator.Core/Validation/QuantificationDefinitionValidator.cs) | [`Domain tests`](../../tests/StudyReportEvaluator.Core.Tests/Domain/)、[`Scoring tests`](../../tests/StudyReportEvaluator.Core.Tests/Scoring/)、[`QuantificationDefinitionValidatorTests.cs`](../../tests/StudyReportEvaluator.Core.Tests/Validation/QuantificationDefinitionValidatorTests.cs) |
| Prompt / selected-row payload | [`Prompting`](../../src/StudyReportEvaluator.Core/Prompting/) | [`Prompting tests`](../../tests/StudyReportEvaluator.Core.Tests/Prompting/) |
| reference / normal / special / similarity Copilot operations | [`Copilot`](../../src/StudyReportEvaluator.App/Copilot/) | [`Copilot fake-runtime tests`](../../tests/StudyReportEvaluator.App.Tests/Copilot/) |
| score / formula AST | [`Scoring`](../../src/StudyReportEvaluator.Core/Scoring/)、[`Formulas`](../../src/StudyReportEvaluator.Core/Formulas/) | [`Scoring tests`](../../tests/StudyReportEvaluator.Core.Tests/Scoring/)、[`Formula tests`](../../tests/StudyReportEvaluator.Core.Tests/Formulas/) |
| Config / References / Results / Run / partial checkpoint / atomic output | [`Workbooks`](../../src/StudyReportEvaluator.App/Workbooks/)、[`Workflow`](../../src/StudyReportEvaluator.App/Workflow/) | [`Workbook tests`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/)、[`Workflow tests`](../../tests/StudyReportEvaluator.App.Tests/Workflow/) |
| 4-step UI / keyboard / 200% | [`Views`](../../src/StudyReportEvaluator.App/Views/)、[`ViewModels`](../../src/StudyReportEvaluator.App/ViewModels/) | [`UI headless tests`](../../tests/StudyReportEvaluator.App.Tests/UI/) |
| Windows x64 unsigned ZIP / bundled CLI | [`publish-windows.ps1`](../../scripts/publish-windows.ps1)、[`package-windows.ps1`](../../scripts/package-windows.ps1)、[`CopilotClientFactory.cs`](../../src/StudyReportEvaluator.App/Copilot/CopilotClientFactory.cs) | [`WindowsPublishPackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs)、[`CopilotClientFactoryTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Copilot/CopilotClientFactoryTests.cs) |

詳細なAC/TR対応は[`traceability.md`](traceability.md)を参照してください。

## Historical v3 gate record

以下はv3 gate後に閉じたgapの履歴であり、現在のv4.1 release acceptanceや今回のtest結果を表さない。

### IMPL-GAP-001 — CLOSED: Custom Promptのrun開始前再検査

### Normative requirement

未知placeholder、未閉鎖brace等は実行前に拒否し、invalid definitionはrun開始前にfield errorを表示する必要があります。[要求定義書 §7.3](../../docs/requirements-definition.md#73-placeholder)、[§13.2](../../docs/requirements-definition.md#132-failure)

### Closure evidence

1. Knowledge / Custom ownership、built-in version、placeholder構文を[`QuantificationDefinitionValidator.cs`](../../src/StudyReportEvaluator.Core/Validation/QuantificationDefinitionValidator.cs)へ統合しました。
2. Design、Execution `CanStart`、`QuantificationSnapshot.Create`が同じCore validation outcomeを使用します。
3. unknown / unclosed / malformed / unmatched brace、必須placeholder欠落、Knowledge / Custom ownershipをCore testで検証します。
4. [`QuantificationOrchestratorTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workflow/QuantificationOrchestratorTests.cs)はinvalid Promptでinput capture、row read、runner callがすべて0件であることを検証します。sessionはrunnerより下流なので作成されません。

Closure commit: `69e4b992711c243fe7c70b0defff5e6abf03865c`。

### IMPL-GAP-002 — CLOSED: Excel / request capacityのrun前preflight

### Normative requirement

Results列数、formula長、function arguments、request budget等から実行可能上限をrun前に計算し、超過definitionを黙って切り詰めず拒否する必要があります。[要求定義書 §9.6](../../docs/requirements-definition.md#96-formula-rules)、[§14](../../docs/requirements-definition.md#14-performance-and-limits)

### Closure evidence

- [`WorkbookExecutionPreflight.cs`](../../src/StudyReportEvaluator.App/Workbooks/Writing/WorkbookExecutionPreflight.cs)はsnapshotとworkbook metadataからConfig address map、Results layout、最終rowの実formula ASTをI/Oなしで構築します。
- `ResultsSheetWriter.Preflight`とexportが同じlayout / `FormulaExpressions` / `FormulaPreflightValidator`を共有し、列16,384、cell 32,767、formula 8,191、function 255、reference、DAGを同じ判定にします。
- [`EvaluationRequestCapacityValidator.cs`](../../src/StudyReportEvaluator.App/Copilot/EvaluationRequestCapacityValidator.cs)は全selected rowの実payloadとclosed schemaをAI dispatch前に構築し、app-owned request 65,536 Unicode scalars、SDK model prompt/context上限の80%を保守的UTF-8境界で検査します。UTF-8 byte数をtoken実測値とは表記しません。
- evaluation units × 最大3 attemptsは20,000以下を要求します。Execution UIはfield、actual、limitだけを表示し、回答またはPrompt本文を含めません。
- inputはrequest preflight後にexact hash / size / last-write timeを再確認し、drift時はrunner call 0で停止します。

Closure commit: `69e4b992711c243fe7c70b0defff5e6abf03865c`。

### Historical evidence boundary

旧GATE-ACCEPTANCE artifactは上記2差分を検出する前に生成されたため、artifact自体を書き換えていません。2差分のcode/test closure、独立レビュー、文書同期後のlocked restore / Release build / 485 testsを評価HEAD `3f4227e`で再実行し、新しいgenerated recordは`PASS`です。optional 2件も固定合成データで`PASS`しましたがrequired判定へ算入していません。
