# Implementation task file map — plan v3.0

| 項目 | 内容 |
|---|---|
| 状態 | **CURRENT NORMATIVE PATH MAP** |
| Requirement | `docs/requirements-definition.md` v2.0 |
| Plan | `work/20260831-implementation-plan.md` v3.0 |
| Initial target | Windows 11 x64 |
| Rule | planのshorthandと`tests`を本表のexact pathへ展開する |
| 記録日 | 2026-08-31 |

## Root aliases

| Alias | Exact root |
|---|---|
| `Core/` | `src/StudyReportEvaluator.Core/` |
| `Workbooks/` | `src/StudyReportEvaluator.Workbooks/` |
| `Platform/` | `src/StudyReportEvaluator.Platform/` |
| `Desktop/` | `src/StudyReportEvaluator.Desktop/` |
| `Core.Tests/` | `tests/StudyReportEvaluator.Core.Tests/` |
| `Workbooks.Tests/` | `tests/StudyReportEvaluator.Workbooks.Tests/` |
| `Platform.Tests/` | `tests/StudyReportEvaluator.Platform.Tests/` |
| `Desktop.Tests/` | `tests/StudyReportEvaluator.Desktop.Tests/` |
| `E2E.Tests/` | `tests/StudyReportEvaluator.E2E.Tests/` |

No production or production-test path may be created before revised GATE-0 PASS. The map assigns future ownership and does not itself authorize implementation.

## Phase 0

| Task | Exact files |
|---|---|
| G0-01 | `docs/requirements-definition.md`; `work/20260831-implementation-plan.md` |
| G0-02 | `docs-dev/adr/0010-single-input-local-scope.md` |
| G0-03 | `docs-dev/preflight/requirements-baseline.md`; `docs-dev/preflight/implementation-task-file-map.md` |
| G0-04 | `docs-dev/traceability.md` |
| GATE-0 | `docs-dev/preflight/gate-result.md` |

## Foundation

| Task | Exact files |
|---|---|
| F-01 | `global.json`; `Directory.Build.props`; `Directory.Packages.props`; `NuGet.Config` |
| F-02 | `.gitignore` |
| F-03 | `StudyReportEvaluator.sln`; `Core/StudyReportEvaluator.Core.csproj`; `Workbooks/StudyReportEvaluator.Workbooks.csproj`; `Platform/StudyReportEvaluator.Platform.csproj`; `Desktop/StudyReportEvaluator.Desktop.csproj`; `Desktop/Program.cs`; `Desktop/App.axaml`; `Desktop/App.axaml.cs`; `Core.Tests/StudyReportEvaluator.Core.Tests.csproj`; `Workbooks.Tests/StudyReportEvaluator.Workbooks.Tests.csproj`; `Platform.Tests/StudyReportEvaluator.Platform.Tests.csproj`; `Desktop.Tests/StudyReportEvaluator.Desktop.Tests.csproj`; `E2E.Tests/StudyReportEvaluator.E2E.Tests.csproj` |
| F-04 | `Core/Ports/Ports.cs`; `Core.Tests/Architecture/DependencyRulesTests.cs` |
| F-05 | `Core/packages.lock.json`; `Workbooks/packages.lock.json`; `Platform/packages.lock.json`; `Desktop/packages.lock.json`; `Core.Tests/packages.lock.json`; `Workbooks.Tests/packages.lock.json`; `Platform.Tests/packages.lock.json`; `Desktop.Tests/packages.lock.json`; `E2E.Tests/packages.lock.json`; `E2E.Tests/SupplyChain/PackageLockTests.cs` |
| F-TR | `docs-dev/traceability.md` |
| GATE-1 | `artifacts/test/gate-foundation.json` |

## Domain and preset contracts

| Task | Exact files |
|---|---|
| T-01 | `Core/Domain/StatusCodes.cs`; `Core.Tests/Domain/StatusCodesTests.cs` |
| T-02 | `Core/Domain/QuestionColumnMapping.cs`; `Core.Tests/Domain/QuestionColumnMappingTests.cs` |
| T-03 | `Core/Domain/EvaluationDefinition.cs`; `Core/Domain/RunSnapshot.cs`; `Core.Tests/Domain/EvaluationDefinitionTests.cs`; `Core.Tests/Domain/RunSnapshotTests.cs` |
| T-04 | `Core/Presets/BuiltInEvaluationPresets.cs`; `Core.Tests/Presets/BuiltInEvaluationPresetsTests.cs` |
| T-05 | `Core/Prompting/PromptTemplateRenderer.cs`; `Core.Tests/Prompting/PromptTemplateRendererTests.cs` |
| T-06 | `Core/Privacy/PiiCandidateDetector.cs`; `Core/Prompting/SafeEvaluationPayloadBuilder.cs`; `Core.Tests/Privacy/PiiCandidateDetectorTests.cs`; `Core.Tests/Prompting/SafeEvaluationPayloadBuilderTests.cs` |
| T-07 | `Core/Domain/EvaluationResult.cs`; `Core.Tests/Domain/EvaluationResultTests.cs` |
| T-08 | `Core/Validation/EvaluationResultValidator.cs`; `Core.Tests/Validation/EvaluationResultValidatorTests.cs` |
| T-09 | `Core/Review/ReviewDecisionService.cs`; `Core.Tests/Review/ReviewDecisionServiceTests.cs` |
| T-10 | `tests/fixtures/v2/mapping.json`; `tests/fixtures/v2/evaluation-definitions.json`; `tests/fixtures/v2/evaluation-results.json`; `tests/fixtures/v2/security.json`; `tests/fixtures/v2/manifest.json`; `Core.Tests/Fixtures/SyntheticFixtureContractTests.cs` |
| T-TR | `docs-dev/traceability.md` |
| GATE-2 | `artifacts/test/gate-contracts.json` |

## Workbook lane

| Task | Exact files |
|---|---|
| W-01 | `Workbooks/Intake/FileFormatClassifier.cs`; `Workbooks.Tests/Intake/FileFormatClassifierTests.cs` |
| W-02 | `Workbooks/Intake/InputSnapshotService.cs`; `Workbooks.Tests/Intake/InputSnapshotServiceTests.cs` |
| W-03 | `Workbooks/Reading/WorkbookMetadataReader.cs`; `Workbooks.Tests/Reading/WorkbookMetadataReaderTests.cs` |
| W-04 | `Workbooks/Mapping/ColumnMappingValidator.cs`; `Workbooks.Tests/Mapping/ColumnMappingValidatorTests.cs` |
| W-05 | `Workbooks/Writing/WorkingPackage.cs`; `Workbooks.Tests/Writing/WorkingPackagePreservationTests.cs` |
| W-06 | `Workbooks/Writing/ControlSheetWriter.cs`; `Workbooks.Tests/Writing/ControlSheetWriterTests.cs` |
| W-07 | `Workbooks/Writing/EvaluationResultsWriter.cs`; `Workbooks/Writing/UntrustedStringCellWriter.cs`; `Workbooks.Tests/Writing/EvaluationResultsWriterTests.cs`; `Workbooks.Tests/Writing/UntrustedStringCellWriterTests.cs` |
| W-08 | `Workbooks/Validation/OutputPackageValidator.cs`; `Workbooks.Tests/Validation/OutputPackageValidatorTests.cs` |
| W-09 | `Workbooks/Writing/AtomicOutputCommitter.cs`; `Workbooks.Tests/Writing/AtomicOutputCommitterFaultTests.cs` |
| W-TR | `docs-dev/traceability.md` |
| GATE-W | `artifacts/test/gate-workbook.json` |

## Copilot lane

| Task | Exact files |
|---|---|
| A-01 | `Platform/Copilot/CopilotClientFactory.cs`; `Platform.Tests/Copilot/CopilotClientFactoryTests.cs` |
| A-02 | `Platform/Copilot/EvaluationSchemaFactory.cs`; `Platform.Tests/Copilot/EvaluationSchemaFactoryTests.cs` |
| A-03 | `Platform/Copilot/SubmitEvaluationTool.cs`; `Platform.Tests/Copilot/SubmitEvaluationToolTests.cs` |
| A-04 | `Platform/Copilot/EphemeralEvaluationRunner.cs`; `Platform.Tests/Copilot/EphemeralEvaluationRunnerTests.cs` |
| A-05 | `Platform/Copilot/RetryAndCleanupCoordinator.cs`; `Platform.Tests/Copilot/RetryAndCleanupCoordinatorTests.cs` |
| A-06 | `Platform/Logging/SafeLogger.cs`; `Platform.Tests/Logging/SafeLoggerCanaryTests.cs` |
| A-07 | `Platform.Tests/Copilot/CapabilityBoundaryTests.cs` |
| A-08 | `Platform.Tests/Copilot/AuthenticatedSyntheticSmokeTests.cs` |
| A-TR | `docs-dev/traceability.md` |
| GATE-AI | `artifacts/test/gate-copilot.json` |

## Run orchestration

| Task | Exact files |
|---|---|
| R-01 | `Core/Runs/EvaluationPlanBuilder.cs`; `Core.Tests/Runs/EvaluationPlanBuilderTests.cs` |
| R-02 | `Core/Runs/EvaluationScheduler.cs`; `Core.Tests/Runs/EvaluationSchedulerTests.cs` |
| R-03 | `Core/Runs/EvaluationOrchestrator.cs`; `Core.Tests/Runs/EvaluationOrchestratorTests.cs` |
| R-04 | `Core/Runs/RunSummary.cs`; `Core.Tests/Runs/RunSummaryTests.cs` |
| R-TR | `docs-dev/traceability.md` |
| GATE-RUN | `artifacts/test/gate-run.json` |

## Avalonia UI

| Task | Exact files |
|---|---|
| U-01 | `Desktop/Views/MainWindow.axaml`; `Desktop/Views/MainWindow.axaml.cs`; `Desktop/ViewModels/MainWindowViewModel.cs`; `Desktop/Navigation/WorkflowNavigator.cs`; `Desktop/Styles/Accessibility.axaml`; `Desktop.Tests/Views/MainWindowTests.cs` |
| U-02 | `Desktop/Views/FileSelectionView.axaml`; `Desktop/Views/FileSelectionView.axaml.cs`; `Desktop/ViewModels/FileSelectionViewModel.cs`; `Desktop.Tests/Views/FileSelectionViewTests.cs` |
| U-03 | `Desktop/Views/ColumnMappingView.axaml`; `Desktop/Views/ColumnMappingView.axaml.cs`; `Desktop/ViewModels/ColumnMappingViewModel.cs`; `Desktop.Tests/Views/ColumnMappingViewTests.cs` |
| U-04 | `Desktop/Views/EvaluationSettingsView.axaml`; `Desktop/Views/EvaluationSettingsView.axaml.cs`; `Desktop/ViewModels/EvaluationSettingsViewModel.cs`; `Desktop.Tests/Views/EvaluationSettingsViewTests.cs` |
| U-05 | `Desktop/Views/SendPreviewView.axaml`; `Desktop/Views/SendPreviewView.axaml.cs`; `Desktop/ViewModels/SendPreviewViewModel.cs`; `Desktop.Tests/Views/SendPreviewViewTests.cs` |
| U-06 | `Desktop/Views/ExecutionReviewView.axaml`; `Desktop/Views/ExecutionReviewView.axaml.cs`; `Desktop/ViewModels/ExecutionReviewViewModel.cs`; `Desktop.Tests/Views/ExecutionReviewViewTests.cs` |
| U-07 | `Desktop/Views/CompletionView.axaml`; `Desktop/Views/CompletionView.axaml.cs`; `Desktop/ViewModels/CompletionViewModel.cs`; `Desktop/Composition/ServiceRegistration.cs`; `Desktop.Tests/Views/CompletionViewTests.cs`; `Desktop.Tests/Composition/ServiceRegistrationTests.cs` |
| U-08 | `Desktop.Tests/Accessibility/PrimaryJourneyAccessibilityTests.cs` |
| U-TR | `docs-dev/traceability.md` |
| GATE-UI | `artifacts/test/gate-ui.json` |

## End-to-end, packaging, and docs

| Task | Exact files |
|---|---|
| E-01 | `E2E.Tests/Flows/SyntheticEvaluationJourneyTests.cs`; generated fixed-seed workbooks under ignored `artifacts/test/fixtures/` |
| E-02 | `E2E.Tests/Security/HostileInputAndFailureTests.cs` |
| E-03 | `E2E.Tests/Windows/WindowsLocalApplicationTests.cs` |
| E-04 | `E2E.Tests/Performance/LocalPipelineBenchmarks.cs`; `artifacts/test/performance-windows-x64.json` |
| P-01 | `scripts/publish-windows.ps1`; `E2E.Tests/Packaging/WindowsPublishLayoutTests.cs` |
| P-02 | `scripts/package-windows.ps1`; `E2E.Tests/Packaging/WindowsPackageTests.cs`; generated ignored `artifacts/package/StudyReportEvaluator-win-x64.zip`; generated ignored `artifacts/package/StudyReportEvaluator-win-x64.zip.sha256` |
| D-01 | `README.md` |
| D-02 | `docs-dev/architecture.md`; `docs-dev/excel-contract.md`; `docs-dev/customization.md` |
| D-03 | `E2E.Tests/Content/DocumentationContractTests.cs` |
| E-TR | `docs-dev/traceability.md` |
| GATE-ACCEPTANCE | `artifacts/test/gate-acceptance.json` |

## Shared-file and generated-artifact rules

1. `.gitignore` has existing uncommitted changes. F-02 must read and merge them; it must not reset or overwrite them.
2. `README.md` has existing uncommitted changes. D-01 must read and merge them; it must not reset or overwrite them.
3. `.vscode/settings.json` is untracked and has no owner in plan v3.0. Do not stage, modify, or delete it.
4. Gate JSON and package/performance output under `artifacts/` are generated evidence. They are ignored unless a later task explicitly changes the release policy.
5. Actual Forms workbooks are never committed. Test workbooks are generated from fixed-seed JSON specifications.
6. `docs-dev/traceability.md` is edited only by the listed `*-TR` owner tasks.
7. A task must not invent alternate production paths. A necessary path change requires a plan/map update before implementation.

## Superseded map

The previous map for requirement v1.2 / plan v2.1 is historical. Paths for signed policy, platform matrix, cross-platform packages, row printing, educational validation, and release signing are not part of current initial implementation. Their files may remain as historical design artifacts but are not task dependencies.
