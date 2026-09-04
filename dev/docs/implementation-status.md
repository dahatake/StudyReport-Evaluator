# Current implementation status

| 項目 | 値 |
|---|---|
| Current requirement | `docs/requirements-definition.md` v4.3 |
| Scope ADR | ADR-0012（機能）/ ADR-0015（Windows ZIP public / development MSIX）/ ADR-0013（Windows public evidence） |
| Product version | `0.8.1` candidate — `Directory.Build.props`の明示`VersionPrefix` |
| Version management | [`version-management.md`](version-management.md) / [`dev/version.ps1`](../version.ps1) / ADR-0014 |
| Source baseline | `main`、release commit `d0b03b9201d397b6c3333dafbb816b13b4dc003c`、annotated tag `v0.8.1` |
| Public release status | `DRAFT_CREATED_UNPUBLISHED — v0.8.1 draftはZIPとsidecarの2 asset、公開未実行` |
| Release build | PASS、warning 0 / error 0（2026-09-04 V2-01実行） |
| Full required gate | 696/696 PASS（Core 190/190、App 506/506、failed 0、skipped 0、2026-09-04 V2-01実行） |
| Baseline focused validation | locked restore、version self-test 15 assertions、DocumentationContractTests 14/14、WindowsInstallerPackageTests 6/6、MacOsPublishPackageTests 2/2、`git diff --check`が全てPASS（2026-09-04実行） |
| Previous focused UI baseline | `MainWindowTests` 8/8 PASS（2026-09-02実行。current candidate full gateへ未算入） |
| Previous bundled CLI baseline | `CopilotClientFactoryTests` 14/14 PASS（2026-09-02実行。current candidate full gateへ未算入） |
| Documentation contract | 14/14 PASS（2026-09-04実行。root `SystemTest-prompt.md`、25 scenario、TR-01〜TR-29、v4.3 canonical sample契約を含む） |
| Previous screenshot baseline | 7画像再生成、1440×1050、test 1/1 PASS、敵対review finding 0（2026-09-02実行。current candidate full gateへ未算入） |
| Previous Windows ZIP integration baseline | publish/package/展開/resolver/clean launch/repackage、全class 3/3 PASS（2026-09-02実行。current candidate gateへ流用しない） |
| B1-16 adversarial review | RealData evidence contractの曖昧性を計画へ反映。source identity assertionはPASS、opt-in E2E／Live AIは非実行。未解決の再現可能なblocker/high finding 0 |
| Previous canonical sample baseline | canonical `sample/SampleReport.xlsx` exact path・identity・structure・input不変 1/1 PASS、synthetic fixture isolation 1/1 PASS（2026-09-03実行。current candidate full gateへ未算入） |
| Previous canonical technical E2E baseline | 530行、2,650 normal evaluations、5 references、535 checkpoint updates、no-network primary application path 1/1 PASS（2026-09-03実行。current candidate full gateへ未算入） |
| Previous 10-person system smoke baseline | fixed 10-person fixtureのidentity/formula canaryとuninterrupted/interruption-resume同値性 2/2 PASS（2026-09-03 full run。current candidate full gateへ未算入） |
| Previous synthetic 531-row journey baseline | fixed-seed local atomic journeyとdurable new/resume同値性 2/2 PASS（2026-09-03 full run。current candidate full gateへ未算入） |
| Previous functional baseline | Release 670/670 PASS、Core 190/190・App 480/480、failed/error/not-executed 0（2026-09-03、v1.0.1 recovery tree。現在の0.8.0 delivery証跡へ流用しない） |
| Previous optional live Copilot evidence | `PASS` — fixed synthetic payload、required substitute false（2026-09-02実行。current required gateへ算入しない） |
| Previous optional external recalculation evidence | `MIXED_ADVISORY` — syntheticはMicrosoft Excel 16.0でopen/recalculate/save・process cleanupまで`PASS`。canonical output copyは20,672 formula error 0だが、Excel保存時に生成されたoptional `/xl/calcChain.xml`だけに`Sem_MissingIndexedElement`が発生して`FAILED_ADVISORY`（2026-09-03実行。current required gateへ算入しない） |
| Version tool validation | show/verify/set/bump/dry-run/invalid rejection、複数Git pathでのtag/clean検証、15 assertions PASS（2026-09-04実行） |
| Development MSIX mechanism | `PASS_MECHANISM` — package/unpack/hash/policy/cleanup、0.8.1.0、install未実行・非required（2026-09-04実行） |
| macOS static source contract | 2/2 PASS — unsigned bundle最小contractとproduction trust順序のsource検証。production artifact／署名／公証の実測ではない（2026-09-04 B1-16実行） |
| Audit date | 2026-09-04 |

現在のdelivery実装は[`20260904-publication-remediation-plan.md`](../../work/20260904-publication-remediation-plan.md)と[ADR-0015](adr/0015-windows-macos-installer-delivery.md)に従う。未公開`v1.0.1` recoveryと`1.1.0` delivery candidateは`0.8.0`へ再baselineし、過去の1.x候補を公開版として扱わない。Windows public artifactはunsigned ZIP、development MSIXはnon-public `PASS_MECHANISM`、macOS production deliveryは現版scope外である。

2026-09-03のdelivery変更前working treeを再buildしたfull solution testは670件中670件成功した。ユーザーが配置した`sample/SampleReport.xlsx`だけをcanonical sampleとし、exact path、470,806 bytes、SHA-256 `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA`、read-only検査前後のidentity不変を確認した。opt-in technical E2Eはnetwork/live AIを使わず530行を完走した。これらは機能regressionの比較baselineであり、0.8.0のMSIX／macOS／signing／notarization／quarantine結果ではない。delivery変更後は全required regressionを再実行する。

2026-09-04のV2-01はrelease commitのclean treeでlocked restore、Release build、full required deterministic gateを実行し、696件中696件が成功した。Windows ZIP regressionはhosted CIのclean checkoutとcandidate workflowでも`PASS_REQUIRED` evidenceを生成している。optional Live Copilot、external recalculation、RealData system smokeはrequired gateへ算入していない。

## v4.3 delivery status

| Surface | Current status | Completion evidence |
|---|---|---|
| Windows public ZIP regression | `PASS_REQUIRED` | clean checkoutでpublish/package/hash/safe layout/bundled CLI/clean launch/入力不変を検証し、closed evidenceを生成（hosted CIとcandidate workflow） |
| Full required regression | `PASS_REQUIRED` | Core 190/190、App 506/506、合計696/696（2026-09-04 V2-01） |
| Canonical technical E2E | `NOT_RUN_CURRENT_CANDIDATE` | opt-in no-network E2Eはrequired gate外。current candidateでは実行していない |
| Windows development MSIX mechanism | `PASS_MECHANISM` | manifest/version/RID、unpack、block map、payload、CLI、sidecar、policy negative、cleanup |
| Development MSIX install/launch | `NOT_RUN_NOT_REQUIRED` | 現版のrequired gateではなく、一般配布しない |
| macOS source foundation static contract | `PASS_REQUIRED` | `MacOsPublishPackageTests` 2/2。production signing／notary evidenceではない |
| macOS production delivery | `EXCLUDED_CURRENT_SCOPE` | public artifact／support claimなし |
| Windows ZIP public setup docs | `PASS_REQUIRED` | 14/14 documentation contract（公開URLはRelease後に追加） |
| Release matrix/workflow | `NOT_RUN` | schema、validator、workflow contract、candidate re-download |

## 実装済みsurface

| Surface | Production owner | Required evidence |
|---|---|---|
| `.xlsx` classification / immutable input | [`FileFormatClassifier.cs`](../../src/StudyReportEvaluator.App/Workbooks/Intake/FileFormatClassifier.cs)、[`InputSnapshotService.cs`](../../src/StudyReportEvaluator.App/Workbooks/Intake/InputSnapshotService.cs) | [`FileFormatClassifierTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Intake/FileFormatClassifierTests.cs)、[`InputSnapshotServiceTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Intake/InputSnapshotServiceTests.cs) |
| picker / mapping | [`InputView.axaml.cs`](../../src/StudyReportEvaluator.App/Views/InputView.axaml.cs)、[`ColumnMappingSuggester.cs`](../../src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingSuggester.cs)、[`ColumnMappingValidator.cs`](../../src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingValidator.cs) | [`InputViewTests.cs`](../../tests/StudyReportEvaluator.App.Tests/UI/InputViewTests.cs)、[`ColumnMappingSuggesterTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/Mapping/ColumnMappingSuggesterTests.cs) |
| v4 domain / allocation / snapshot | [`Domain`](../../src/StudyReportEvaluator.Core/Domain/)、[`ScoringAllocationCalculator.cs`](../../src/StudyReportEvaluator.Core/Scoring/ScoringAllocationCalculator.cs)、[`QuantificationDefinitionValidator.cs`](../../src/StudyReportEvaluator.Core/Validation/QuantificationDefinitionValidator.cs) | [`Domain tests`](../../tests/StudyReportEvaluator.Core.Tests/Domain/)、[`Scoring tests`](../../tests/StudyReportEvaluator.Core.Tests/Scoring/)、[`QuantificationDefinitionValidatorTests.cs`](../../tests/StudyReportEvaluator.Core.Tests/Validation/QuantificationDefinitionValidatorTests.cs) |
| Prompt / selected-row payload | [`Prompting`](../../src/StudyReportEvaluator.Core/Prompting/) | [`Prompting tests`](../../tests/StudyReportEvaluator.Core.Tests/Prompting/) |
| reference / normal / special / similarity Copilot operations | [`Copilot`](../../src/StudyReportEvaluator.App/Copilot/) | [`Copilot fake-runtime tests`](../../tests/StudyReportEvaluator.App.Tests/Copilot/) |
| score / formula AST | [`Scoring`](../../src/StudyReportEvaluator.Core/Scoring/)、[`Formulas`](../../src/StudyReportEvaluator.Core/Formulas/) | [`Scoring tests`](../../tests/StudyReportEvaluator.Core.Tests/Scoring/)、[`Formula tests`](../../tests/StudyReportEvaluator.Core.Tests/Formulas/) |
| Config / References / Results / Run / partial checkpoint / atomic output | [`Workbooks`](../../src/StudyReportEvaluator.App/Workbooks/)、[`Workflow`](../../src/StudyReportEvaluator.App/Workflow/) | [`Workbook tests`](../../tests/StudyReportEvaluator.App.Tests/Workbooks/)、[`Workflow tests`](../../tests/StudyReportEvaluator.App.Tests/Workflow/) |
| 4-step UI / keyboard / 200% | [`Views`](../../src/StudyReportEvaluator.App/Views/)、[`ViewModels`](../../src/StudyReportEvaluator.App/ViewModels/) | [`UI headless tests`](../../tests/StudyReportEvaluator.App.Tests/UI/) |
| Windows x64 unsigned ZIP / bundled CLI regression | [`publish-windows.ps1`](../../scripts/publish-windows.ps1)、[`package-windows.ps1`](../../scripts/package-windows.ps1)、[`CopilotClientFactory.cs`](../../src/StudyReportEvaluator.App/Copilot/CopilotClientFactory.cs) | [`WindowsPublishPackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs)、[`CopilotClientFactoryTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Copilot/CopilotClientFactoryTests.cs) |
| Windows development MSIX | [`eng/packaging/windows`](../../eng/packaging/windows/)、[`package-windows-msix.ps1`](../../scripts/package-windows-msix.ps1)、[`test-windows-msix-unsigned.ps1`](../../scripts/test-windows-msix-unsigned.ps1) | [`WindowsInstallerPackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/WindowsInstallerPackageTests.cs)、mechanism evidence |
| macOS source foundation | [`eng/packaging/macos`](../../eng/packaging/macos/)、macOS publish/package/sign/notary scripts | [`MacOsPublishPackageTests.cs`](../../tests/StudyReportEvaluator.App.Tests/Packaging/MacOsPublishPackageTests.cs) static contract |

詳細なAC/TR対応は[`traceability.md`](traceability.md)を参照してください。

## Historical v3 gate record

以下はv3 gate後に閉じたgapの履歴であり、現在のv4.3 release acceptanceや今回のtest結果を表さない。

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
