# Current implementation status

| 項目 | 値 |
|---|---|
| Current requirement | `docs/requirements-definition.md` v4.2 |
| Scope ADR | ADR-0012（機能）/ ADR-0015（target delivery）/ ADR-0013（current public evidence） |
| Product version | `1.1.0` candidate — `Directory.Build.props`の明示`VersionPrefix` |
| Version management | [`version-management.md`](version-management.md) / [`dev/version.ps1`](../version.ps1) / ADR-0014 |
| Source baseline | `feature/setup-simplification-20260903`、base `5a1d1ebe5e6dcc5d934f959e58ac2582f9d9c326`。exact release commitはtag作成時に確定する |
| Public release status | `IMPLEMENTATION_IN_PROGRESS — Windows/macOS delivery expansion` |
| Release build | PASS、warning 0 / error 0（2026-09-03実行） |
| Focused UI validation | `MainWindowTests` 8/8 PASS（2026-09-02実行） |
| Bundled CLI validation | `CopilotClientFactoryTests` 14/14 PASS（2026-09-02実行） |
| Documentation contract | 14/14 PASS（2026-09-03実行。root `SystemTest-prompt.md`、21 scenario、TR-01〜TR-24、canonical sample契約を含む） |
| Screenshot generation | 7画像再生成、1440×1050、test 1/1 PASS、敵対review finding 0（2026-09-02実行） |
| Windows package integration | publish/package/展開/resolver/clean launch/repackage、全class 3/3 PASS（2026-09-02実行） |
| Independent adversarial review | task別reviewと最終3観点reviewを実施。再現可能な指摘を反映後、文書＋package契約16/16 PASS |
| B-07 focused validation | canonical `sample/SampleReport.xlsx` exact path・identity・structure・input不変 1/1 PASS、synthetic fixture isolation 1/1 PASS（2026-09-03実行） |
| Canonical technical E2E | 530行、2,650 normal evaluations、5 references、535 checkpoint updates、no-network primary application path 1/1 PASS（2026-09-03実行） |
| 10-person system smoke | fixed 10-person fixtureのidentity/formula canaryとuninterrupted/interruption-resume同値性 2/2 PASS（2026-09-03 full run） |
| Synthetic 531-row journey | fixed-seed local atomic journeyとdurable new/resume同値性 2/2 PASS（2026-09-03 full run） |
| Previous functional baseline | Release 670/670 PASS、Core 190/190・App 480/480、failed/error/not-executed 0（2026-09-03、v1.0.1 recovery tree。1.1.0 delivery証跡へ流用しない） |
| Optional live Copilot | `PASS` — fixed synthetic payload、required substitute false（2026-09-02実行） |
| Optional external recalculation | `MIXED_ADVISORY` — syntheticはMicrosoft Excel 16.0でopen/recalculate/save・process cleanupまで`PASS`。canonical output copyは20,672 formula error 0だが、Excel保存時に生成されたoptional `/xl/calcChain.xml`だけに`Sem_MissingIndexedElement`が発生して`FAILED_ADVISORY`（2026-09-03実行） |
| Version tool validation | show/verify/set/bump/dry-run/invalid rejection、複数Git pathでのtag/clean検証、14 assertions PASS（2026-09-03実行） |
| Audit date | 2026-09-03 |

現在のdelivery実装は[`20260903-0923-windows-macos-setup-simplification-plan.md`](../../work/20260903-0923-windows-macos-setup-simplification-plan.md)と[ADR-0015](adr/0015-windows-macos-installer-delivery.md)に従う。未公開`v1.0.1` recoveryは`1.1.0`へcarry forwardし、`v1.0.1` tag／Releaseを作成しない。Windows MSIX、macOS package、production signing/notarization、clean-machine証跡は本節の過去test結果から導出せず、完了までは`NOT_RUN`または`BLOCKED_EXTERNAL`である。

2026-09-03のdelivery変更前working treeを再buildしたfull solution testは670件中670件成功した。ユーザーが配置した`sample/SampleReport.xlsx`だけをcanonical sampleとし、exact path、470,806 bytes、SHA-256 `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA`、read-only検査前後のidentity不変を確認した。opt-in technical E2Eはnetwork/live AIを使わず530行を完走した。これらは機能regressionの比較baselineであり、1.1.0のMSIX／macOS／signing／notarization／quarantine結果ではない。delivery変更後は全required regressionを再実行する。

## v4.2 delivery transition

| Surface | Current status | Completion evidence |
|---|---|---|
| Windows unsigned ZIP regression | `PASS_REQUIRED`（delivery変更前baseline。再実行予定） | publish/package/hash/extract/clean launch |
| Windows MSIX mechanism | `NOT_RUN` | manifest/layout/test-sign/install/launch/repair/uninstall |
| Windows production trust | `BLOCKED_EXTERNAL` | trusted signature/timestamp + clean target verification |
| macOS `osx-arm64` publish/bundle | `NOT_RUN` | native bundle/Info.plist/mode/CLI manifest |
| macOS `osx-x64` publish/bundle | `NOT_RUN` | native bundle/Info.plist/mode/CLI manifest |
| macOS Developer ID/notarization | `BLOCKED_EXTERNAL` | nested/outer sign, notary log, app/DMG staple |
| macOS clean-machine matrix | `BLOCKED_EXTERNAL` | exact OS build × native architecture quarantine launch |
| 3-operation public setup docs | `NOT_RUN` | public artifact re-download後のdocumentation contract |

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
| Windows x64 unsigned ZIP / bundled CLI regression | [`publish-windows.ps1`](../../scripts/publish-windows.ps1)、[`package-windows.ps1`](../../scripts/package-windows.ps1)、[`CopilotClientFactory.cs`](../../src/StudyReportEvaluator.App/Copilot/CopilotClientFactory.cs) | [`WindowsPublishPackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs)、[`CopilotClientFactoryTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Copilot/CopilotClientFactoryTests.cs) |
| Windows MSIX | planned `eng/packaging/windows` + `scripts/package-windows-msix.ps1` | planned installer mechanism/production tests |
| macOS RID別DMG | planned `eng/packaging/macos` + macOS publish/package scripts | planned bundle/sign/notary/quarantine tests |

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
