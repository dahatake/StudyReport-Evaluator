# Implementation task file map — plan v2.1

| 項目 | 内容 |
|---|---|
| 状態 | **v2.1 normative path map** |
| Plan | `work/20260831-implementation-plan.md` v2.1 |
| Requirement | `docs/requirements-definition.md` v1.2 |
| Rule | planの`+ tests`、shorthand、root aliasは本表のexact pathへ展開する |
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

No production/test path may be created before GATE-0. This map assigns future ownership; it is not an instruction to bypass the gate.

## Phase 0

| Task | Exact files |
|---|---|
| G-01 | `docs-dev/adr/0001-gate-semantics.md` |
| G-02 | `docs-dev/adr/0002-output-digest.md` |
| G-03 | `docs-dev/adr/0003-openxml-mutation.md` |
| G-04 | `docs-dev/adr/0004-formula-status-similarity.md` |
| G-05 | `docs-dev/adr/0005-copilot-result-protocol.md` |
| G-06 | `docs-dev/adr/0006-signed-records.md`; `eng/schemas/signed-record-v1.schema.json` |
| G-07 | `docs-dev/adr/0007-network-boundary.md`; `eng/schemas/endpoint-manifest.schema.json` |
| G-08 | `docs-dev/adr/0008-platform-print-package.md` |
| G-09 | `docs-dev/release/p1-disposition.md` |
| G-RB | `docs/requirements-definition.md`; `docs-dev/preflight/requirements-baseline.md`; `docs-dev/preflight/implementation-task-file-map.md` |
| G-10 | `docs-dev/adr/0009-runtime-report-definition.md`; `eng/schemas/report-definition-v1.schema.json`; `eng/schemas/examples/report-definition.synthetic-ja.json`; `eng/schemas/examples/report-definition.synthetic-en.json`; `docs-dev/preflight/report-definition-contract.md` |
| G-11 | `tests/fixtures/specs/workbook-clean.json`; `tests/fixtures/specs/workbook-hostile.json`; `tests/fixtures/specs/unicode.json`; `tests/fixtures/specs/formula.json`; `tests/fixtures/specs/report-definition.json`; `docs-dev/preflight/fixture-plan.md` |
| G-12 | repository-external production bundle; `tests/fixtures/policy/test-only/valid.json`; `tests/fixtures/policy/test-only/expired.json`; `tests/fixtures/policy/test-only/wrong-signature.json`; `tests/fixtures/policy/test-only/revoked-key.json` |
| G-13 | `docs-dev/preflight/governance-inputs.md` |
| G-14 | repository-external baseline; `tests/fixtures/evaluation/metric-spec-test.json` |
| G-15 | `spikes/CopilotContract/CopilotContract.csproj`; `spikes/CopilotContract/Program.cs`; `docs-dev/preflight/copilot-contract.md` |
| G-16 | `spikes/DocumentPipeline/DocumentPipeline.csproj`; `spikes/DocumentPipeline/Program.cs`; `docs-dev/preflight/document-pipeline.md` |
| G-17 | `spikes/PlatformPreflight/PlatformPreflight.csproj`; `spikes/PlatformPreflight/Program.cs`; `docs-dev/preflight/platform-matrix.md`; `eng/platform-matrix.json` |
| G-18 | `docs-dev/traceability.md` |
| GATE-0 | `docs-dev/preflight/gate-result.md` |

## Foundation

| Task | Exact files |
|---|---|
| F-01 | `global.json`; `Directory.Build.props` |
| F-02 | `Directory.Packages.props`; `NuGet.Config` |
| F-03 | `.gitignore` |
| F-04 | `src/StudyReportEvaluator.Core/StudyReportEvaluator.Core.csproj`; `Core/Ports/Ports.cs` |
| F-05 | `StudyReportEvaluator.sln`; `Workbooks/StudyReportEvaluator.Workbooks.csproj`; `Platform/StudyReportEvaluator.Platform.csproj`; `Desktop/StudyReportEvaluator.Desktop.csproj`; `Desktop/Program.cs` |
| F-06 | `Core.Tests/StudyReportEvaluator.Core.Tests.csproj`; `Workbooks.Tests/StudyReportEvaluator.Workbooks.Tests.csproj`; `Platform.Tests/StudyReportEvaluator.Platform.Tests.csproj`; `Desktop.Tests/StudyReportEvaluator.Desktop.Tests.csproj`; `E2E.Tests/StudyReportEvaluator.E2E.Tests.csproj` |
| F-07 | `Core.Tests/Architecture/DependencyRulesTests.cs` |
| F-08 | `Workbooks.Tests/Packaging/PublishAssetTests.cs` |
| F-09 | `Desktop/Views/MainWindow.axaml`; `Desktop/Views/MainWindow.axaml.cs`; `Desktop/ViewModels/MainWindowViewModel.cs`; `Desktop/Styles/Accessibility.axaml`; `Desktop.Tests/Accessibility/MainWindowAccessibilityTests.cs` |
| F-10 | all production/test `packages.lock.json`; `E2E.Tests/SupplyChain/PackageLockTests.cs` |
| F-TR | `docs-dev/traceability.md` |
| GATE-1 | `artifacts/test/gate-foundation.json` |

## Contracts and fixtures

| Task | Exact files |
|---|---|
| T-01 | `Core/Domain/StatusCodes.cs`; `Core.Tests/Domain/StatusCodesTests.cs` |
| T-02 | `Core/Domain/MappingDefinition.cs`; `Core.Tests/Domain/MappingDefinitionTests.cs` |
| T-03 | `Core/Domain/ReportDefinition.cs`; `Core/Domain/RunSnapshot.cs`; `Core.Tests/Domain/ReportDefinitionTests.cs`; `Core.Tests/Domain/RunSnapshotTests.cs` |
| T-04 | `Core/Domain/EvaluationRun.cs`; `Core.Tests/Domain/EvaluationRunTests.cs` |
| T-05 | `Core/Limits/InputAdmissionLimits.cs`; `Core.Tests/Limits/InputAdmissionLimitsTests.cs` |
| T-06 | `Core/Limits/InferenceHardLimits.cs`; `Core/Validation/ReportDefinitionBudgetValidator.cs`; `Core.Tests/Limits/InferenceHardLimitsTests.cs`; `Core.Tests/Validation/ReportDefinitionBudgetValidatorTests.cs` |
| T-07 | `Core/Hashing/CanonicalRecord.cs`; `Core.Tests/Hashing/CanonicalRecordKnownAnswerTests.cs` |
| T-08 | `Workbooks.Tests/Fixtures/WorkbookFixtureFactory.cs`; `tests/fixtures/manifest.json`; `Workbooks.Tests/Fixtures/FixtureManifestTests.cs` |
| T-09 | `Platform.Tests/TestDoubles/ScriptedHttpHandler.cs`; `Platform.Tests/TestDoubles/RetryScheduleTests.cs` |
| T-10 | `Core/Ports/IAtomicFileOps.cs`; `Workbooks.Tests/TestDoubles/FaultingAtomicFileOps.cs`; `Workbooks.Tests/Writing/AtomicFileOpsFaultInjectionTests.cs` |
| T-11 | `Core/Time/MonotonicDeadline.cs`; `Core.Tests/Time/MonotonicDeadlineTests.cs` |
| T-TR | `docs-dev/traceability.md` |
| GATE-2 | `artifacts/test/gate-contracts.json` |

## Workbook lane

| Task | Exact files |
|---|---|
| W-01 | `Workbooks/Intake/InputFileIdentity.Windows.cs`; `Workbooks.Tests/Intake/InputFileIdentityWindowsTests.cs` |
| W-02 | `Workbooks/Intake/InputFileIdentity.Unix.cs`; `Workbooks.Tests/Intake/InputFileIdentityUnixTests.cs` |
| W-03 | `Workbooks/Intake/InputSnapshotService.cs`; `Workbooks.Tests/Intake/InputSnapshotServiceTests.cs` |
| W-04 | `Workbooks/Scanning/FileFormatClassifier.cs`; `Workbooks.Tests/Scanning/FileFormatClassifierTests.cs` |
| W-05 | `Workbooks/Scanning/ZipBudgetScanner.cs`; `Workbooks.Tests/Scanning/ZipBudgetScannerTests.cs` |
| W-06 | `Workbooks/Scanning/OpcUriScanner.cs`; `Workbooks.Tests/Scanning/OpcUriScannerTests.cs` |
| W-07 | `Workbooks/Scanning/ExternalExecutionScanner.cs`; `Workbooks.Tests/Scanning/ExternalExecutionScannerTests.cs` |
| W-08 | `Workbooks/Scanning/ProtectionAndVisibilityScanner.cs`; `Workbooks.Tests/Scanning/ProtectionAndVisibilityScannerTests.cs` |
| W-09 | `Workbooks/Reading/WorkbookMetadataReader.cs`; `Workbooks.Tests/Reading/WorkbookMetadataReaderTests.cs` |
| W-10 | `Workbooks/Reading/WorksheetValueReader.cs`; `Workbooks.Tests/Reading/WorksheetValueReaderTests.cs` |
| W-11 | `Workbooks/Planning/OutputIdentity.cs`; `Workbooks.Tests/Planning/OutputIdentityTests.cs` |
| W-12 | `Workbooks/Validation/PackageFingerprint.cs`; `Workbooks.Tests/Validation/PackageFingerprintTests.cs` |

## Report, Prompt, similarity, formula lane

| Task | Exact files |
|---|---|
| L-01 | `Core/Mapping/MappingValidator.cs`; `Core.Tests/Mapping/MappingValidatorTests.cs` |
| L-02 | `Core/Prompting/TemplateParser.cs`; `Core.Tests/Prompting/TemplateParserTests.cs` |
| L-03 | `Core/Prompting/RubricValidator.cs`; `Core.Tests/Prompting/RubricValidatorTests.cs` |
| L-04 | `Core/Prompting/SafePayloadBuilder.cs`; `Core.Tests/Prompting/SafePayloadBuilderTests.cs` |
| L-05 | `Core/Privacy/PiiCandidateDetector.cs`; `Core.Tests/Privacy/PiiCandidateDetectorTests.cs` |
| L-06 | `Core/Privacy/RedactionPreview.cs`; `Core.Tests/Privacy/RedactionPreviewTests.cs` |
| L-07 | `Core/Prompting/DesignHasher.cs`; `Core.Tests/Prompting/DesignHasherTests.cs` |
| L-08 | `Core/Similarity/SimilarityNormalizer.cs`; `Core.Tests/Similarity/SimilarityNormalizerGoldenTests.cs` |
| L-09 | `Core/Similarity/ShingleSimilarity.cs`; `Core.Tests/Similarity/ShingleSimilarityGoldenTests.cs` |
| L-10 | `Core/Formulas/FormulaExpression.cs`; `Core/Formulas/FormulaSerializer.cs`; `Core.Tests/Formulas/FormulaExpressionTests.cs` |
| L-11 | `Core/Formulas/AdoptedScoreBuilder.cs`; `Core.Tests/Formulas/AdoptedScoreBuilderCrossProductTests.cs` |
| L-12 | `Core/Formulas/AggregateFormulaBuilder.cs`; `Core.Tests/Formulas/AggregateFormulaBuilderTests.cs` |
| L-13 | `Core/Formulas/SimilarityFormulaBuilder.cs`; `Core.Tests/Formulas/SimilarityFormulaBuilderTests.cs` |
| L-14 | `Core/Formulas/GeneratedFormulaValidator.cs`; `Core.Tests/Formulas/GeneratedFormulaValidatorTests.cs` |

## Policy, OAuth, Copilot lane

| Task | Exact files |
|---|---|
| A-01 | `Platform/Security/SignedRecordVerifier.cs`; `Platform.Tests/Security/SignedRecordVerifierKnownAnswerTests.cs` |
| A-02 | `Platform/Policy/TrustAndRevocationStore.cs`; `Platform.Tests/Policy/TrustAndRevocationStoreTests.cs` |
| A-03 | `Platform/Policy/OperationalPolicyReader.cs`; `Platform.Tests/Policy/OperationalPolicyReaderTests.cs` |
| A-04 | `Platform/Policy/CalibrationRecordReader.cs`; `Platform.Tests/Policy/CalibrationRecordReaderTests.cs` |
| A-05 | `Platform/Auth/GitHubDeviceFlowClient.cs`; `Platform.Tests/Auth/GitHubDeviceFlowClientTests.cs` |
| A-06 | `Platform/Auth/TokenRefreshService.cs`; `Platform.Tests/Auth/TokenRefreshServiceTests.cs` |
| A-07 | `Platform/Auth/WindowsCredentialVault.cs`; `Platform.Tests/Auth/WindowsCredentialVaultTests.cs` |
| A-08 | `Platform/Auth/MacKeychainVault.cs`; `Platform.Tests/Auth/MacKeychainVaultTests.cs` |
| A-09 | `Platform/Auth/LinuxSecretServiceVault.cs`; `Platform.Tests/Auth/LinuxSecretServiceVaultTests.cs` |
| A-10 | `Platform/Auth/GitHubAccountAuthorizationGuard.cs`; `Platform.Tests/Auth/GitHubAccountAuthorizationGuardTests.cs` |
| A-11 | `Platform/Network/EndpointManifestVerifier.cs`; `Platform.Tests/Network/EndpointManifestVerifierTests.cs` |
| A-12 | `Platform/Copilot/CopilotClientFactory.cs`; `Platform.Tests/Copilot/CopilotClientFactoryContractTests.cs` |
| A-13 | `Platform/Copilot/ApprovedModelCatalog.cs`; `Platform.Tests/Copilot/ApprovedModelCatalogTests.cs` |
| A-14 | `Core/Policy/RealDataGate.cs`; `Core.Tests/Policy/RealDataGateCrossProductTests.cs` |
| C-01 | `Platform/Copilot/SubmitEvaluationSchemaFactory.cs`; `Platform.Tests/Copilot/SubmitEvaluationSchemaFactoryTests.cs` |
| C-02 | `Core/Validation/EvaluationOutputValidator.cs`; `Core.Tests/Validation/EvaluationOutputValidatorTests.cs` |
| C-03 | `Platform/Copilot/SubmitEvaluationTool.cs`; `Platform.Tests/Copilot/SubmitEvaluationToolTests.cs` |
| C-04 | `Platform/Copilot/EphemeralSessionRunner.cs`; `Platform.Tests/Copilot/EphemeralSessionRunnerTests.cs` |
| C-05 | `Platform/Copilot/SchemaRetryCoordinator.cs`; `Platform.Tests/Copilot/SchemaRetryCoordinatorTests.cs` |
| C-06 | `Platform/Copilot/TransportRetryCoordinator.cs`; `Platform.Tests/Copilot/TransportRetryCoordinatorTests.cs` |
| C-07 | `Platform/Copilot/SessionCleanupService.cs`; `Platform.Tests/Copilot/SessionCleanupServiceTests.cs` |
| C-08 | `Platform/Copilot/StartupSessionScavenger.cs`; `Platform.Tests/Copilot/StartupSessionScavengerTests.cs` |
| C-09 | `Platform/Copilot/UsageCollector.cs`; `Platform.Tests/Copilot/UsageCollectorTests.cs` |
| C-10 | `Platform.Tests/Copilot/CapabilityFirewallTests.cs` |
| C-11 | `Platform.Tests/Copilot/ProtocolFailureTests.cs` |
| C-12 | `Platform.Tests/Copilot/TelemetryContractTests.cs` |
| C-13 | `Core/Runs/AnonymousRunMetrics.cs`; `Core.Tests/Runs/AnonymousRunMetricsTests.cs` |
| P4-TR | `docs-dev/traceability.md` |

## Output lane

| Task | Exact files |
|---|---|
| O-01 | `Workbooks/Writing/WorkingPackage.cs`; `Workbooks.Tests/Writing/WorkingPackagePreservationTests.cs` |
| O-02 | `Workbooks/Writing/MinimalOutputWorkbook.cs`; `Workbooks.Tests/Writing/MinimalOutputWorkbookTests.cs` |
| O-03 | `Workbooks/Writing/EvalProjectionWriter.cs`; `Workbooks/Writing/UntrustedStringCellWriter.cs`; `Workbooks.Tests/Writing/EvalProjectionFormulaInjectionTests.cs` |
| O-04 | `Workbooks/Writing/ControlSheetWriter.cs`; `Workbooks.Tests/Writing/ControlSheetWriterTests.cs` |
| O-05 | `Workbooks/Writing/EvaluationResultWriter.cs`; `Workbooks.Tests/Writing/EvaluationResultWriterTests.cs` |
| O-06 | `Workbooks/Writing/WorksheetPresentationWriter.cs`; `Workbooks.Tests/Writing/WorksheetPresentationWriterTests.cs` |
| O-07 | `Workbooks/Writing/WorkbookExplanationsWriter.cs`; `Workbooks.Tests/Writing/WorkbookExplanationsWriterTests.cs` |
| O-08 | `Workbooks/Validation/OutputPackageValidator.cs`; `Workbooks.Tests/Validation/OutputPackageValidatorGoldenTests.cs` |
| O-09 | `Workbooks/Writing/PreparedOutputWriter.cs`; `Workbooks.Tests/Writing/PreparedOutputWriterFaultTests.cs` |
| O-10 | `Platform/State/CompletionRecordWriter.cs`; `Platform.Tests/State/CompletionRecordWriterTests.cs` |
| O-11 | `Workbooks/Writing/AtomicOutputCommitter.cs`; `Workbooks.Tests/Writing/AtomicOutputCommitterFaultTests.cs` |
| O-TR | `docs-dev/traceability.md` |
| GATE-OUTPUT | `artifacts/test/gate-output.json` |

## Run, recovery, review, validation

| Task | Exact files |
|---|---|
| R-01 | `Platform/State/CheckpointCipher.cs`; `Platform.Tests/State/CheckpointCipherTests.cs` |
| R-02 | `Platform/State/CheckpointStore.cs`; `Platform.Tests/State/CheckpointStoreFaultTests.cs` |
| R-03 | `Core/Runs/RunStateMachine.cs`; `Core.Tests/Runs/RunStateMachineTransitionTests.cs` |
| R-04 | `Core/Runs/RequestAttemptBudget.cs`; `Core.Tests/Runs/RequestAttemptBudgetConcurrencyTests.cs` |
| R-05 | `Core/Runs/EvaluationScheduler.cs`; `Core.Tests/Runs/EvaluationSchedulerTests.cs` |
| R-06 | `Core/Runs/ResumePlanner.cs`; `Core.Tests/Runs/ResumePlannerTests.cs` |
| R-07 | `Core/Runs/OfflineRunPlanner.cs`; `Core.Tests/Runs/OfflineRunPlannerTests.cs` |
| R-08 | `Platform/Logging/SafeLogger.cs`; `Platform.Tests/Logging/SafeLoggerCanaryTests.cs` |
| R-09 | `Core/Review/ReviewQueue.cs`; `Core.Tests/Review/ReviewQueueTests.cs` |
| R-10 | `Core/Review/ReviewDecisionService.cs`; `Core.Tests/Review/ReviewDecisionServiceTests.cs` |
| R-11 | `Core/Review/RowExplanation.cs`; `Core.Tests/Review/RowExplanationTests.cs` |
| R-12 | `Core/Runs/EvaluationOrchestrator.cs`; `Core.Tests/Runs/EvaluationOrchestratorTransitionTests.cs` |
| V-01 | `Core/Validation/EducationalMetrics.cs`; `Core.Tests/Validation/EducationalMetricsHandCalculatedTests.cs` |
| V-02 | `Core/Validation/SubgroupAndCounterexampleReport.cs`; `Core.Tests/Validation/SubgroupAndCounterexampleReportTests.cs` |
| V-03 | `Core/Validation/ProductionReadinessGate.cs`; `Core.Tests/Validation/ProductionReadinessGateTests.cs` |
| V-04 | `E2E.Tests/Validation/RepeatedEvaluationHarness.cs`; `E2E.Tests/Validation/RepeatedEvaluationHarnessTests.cs` |
| V-05 | `E2E.Tests/Validation/NondeterminismReportTests.cs` |
| V-06 | `E2E.Tests/Validation/EducationalGateTests.cs` |
| R-TR | `docs-dev/traceability.md` |
| GATE-RUN | `artifacts/test/gate-run.json` |

## UI

| Task | Exact files |
|---|---|
| UI-01 | `Desktop/Views/WelcomeView.axaml`; `Desktop/Views/WelcomeView.axaml.cs`; `Desktop/ViewModels/WelcomeViewModel.cs`; `Desktop.Tests/Views/WelcomeViewTests.cs` |
| UI-02 | `Desktop/Views/AuthenticationView.axaml`; `Desktop/Views/AuthenticationView.axaml.cs`; `Desktop/ViewModels/AuthenticationViewModel.cs`; `Desktop.Tests/Views/AuthenticationViewTests.cs` |
| UI-03 | `Desktop/Views/FileSelectionView.axaml`; `Desktop/Views/FileSelectionView.axaml.cs`; `Desktop/ViewModels/FileSelectionViewModel.cs`; `Desktop.Tests/Views/FileSelectionViewTests.cs` |
| UI-04 | `Desktop/Views/QuestionMappingView.axaml`; `Desktop/Views/QuestionMappingView.axaml.cs`; `Desktop/ViewModels/QuestionMappingViewModel.cs`; `Desktop.Tests/Views/QuestionMappingViewTests.cs` |
| UI-05 | `Desktop/Views/EvaluationDesignView.axaml`; `Desktop/Views/EvaluationDesignView.axaml.cs`; `Desktop/ViewModels/EvaluationDesignViewModel.cs`; `Desktop.Tests/Views/EvaluationDesignViewTests.cs` |
| UI-06 | `Desktop/Views/TrialValidationView.axaml`; `Desktop/Views/TrialValidationView.axaml.cs`; `Desktop/ViewModels/TrialValidationViewModel.cs`; `Desktop.Tests/Views/TrialValidationViewTests.cs` |
| UI-07 | `Desktop/Views/RunConfirmationView.axaml`; `Desktop/Views/RunConfirmationView.axaml.cs`; `Desktop/ViewModels/RunConfirmationViewModel.cs`; `Desktop.Tests/Views/RunConfirmationViewTests.cs` |
| UI-08 | `Desktop/Views/ExecutionView.axaml`; `Desktop/Views/ExecutionView.axaml.cs`; `Desktop/ViewModels/ExecutionViewModel.cs`; `Desktop.Tests/Views/ExecutionViewTests.cs` |
| UI-09 | `Desktop/Views/ReviewView.axaml`; `Desktop/Views/ReviewView.axaml.cs`; `Desktop/ViewModels/ReviewViewModel.cs`; `Desktop.Tests/Views/ReviewViewTests.cs` |
| UI-10 | `Desktop/Views/RowExplanationView.axaml`; `Desktop/Views/RowExplanationView.axaml.cs`; `Desktop/ViewModels/RowExplanationViewModel.cs`; `Desktop.Tests/Views/RowExplanationViewTests.cs` |
| UI-11 | `Desktop/Views/CompletionView.axaml`; `Desktop/Views/CompletionView.axaml.cs`; `Desktop/ViewModels/CompletionViewModel.cs`; `Desktop.Tests/Views/CompletionViewTests.cs` |
| UI-12 | `Desktop/Views/SettingsDiagnosticsView.axaml`; `Desktop/Views/SettingsDiagnosticsView.axaml.cs`; `Desktop/ViewModels/SettingsDiagnosticsViewModel.cs`; `Desktop.Tests/Views/SettingsDiagnosticsViewTests.cs` |
| UI-13 | `Desktop.Tests/Scenarios/SyntheticJourney.cs`; `Desktop.Tests/Scenarios/SyntheticJourneyAccessibilityTests.cs` |
| UI-14 | `Desktop/Composition/ServiceRegistration.cs`; `Desktop/Navigation/WorkflowNavigator.cs`; `Desktop.Tests/Navigation/WorkflowNavigatorTests.cs` |
| UI-TR | `docs-dev/traceability.md` |
| GATE-UI | `artifacts/test/gate-ui.json` |

## Packaging

| Task | Exact files |
|---|---|
| P-01 | `scripts/publish.ps1`; `eng/publish-matrix.json`; `E2E.Tests/Packaging/PublishLayoutTests.cs` |
| P-02 | `scripts/setup-windows.cmd`; `scripts/setup-windows.ps1`; `E2E.Tests/Packaging/WindowsSetupTests.cs` |
| P-03 | `scripts/setup-macos.sh`; `E2E.Tests/Packaging/MacOsSetupTests.cs` |
| P-04 | `scripts/setup-linux.sh`; `E2E.Tests/Packaging/LinuxSetupTests.cs` |
| P-05 | `scripts/uninstall-windows.ps1`; `E2E.Tests/Packaging/WindowsUninstallTests.cs` |
| P-06 | `scripts/uninstall-macos.sh`; `E2E.Tests/Packaging/MacOsUninstallTests.cs` |
| P-07 | `scripts/uninstall-linux.sh`; `E2E.Tests/Packaging/LinuxUninstallTests.cs` |
| P-08 | `scripts/create-release-evidence.ps1`; `eng/sbom-config.json`; `E2E.Tests/SupplyChain/SupplyChainEvidenceTests.cs` |
| P-09 | `eng/rc-manifest.schema.json`; `E2E.Tests/Packaging/RcManifestTests.cs`; `artifacts/rc/rc-manifest.json` |
| GATE-RC | `artifacts/rc/gate-result.json` |

## E2E and acceptance

| Task | Exact files |
|---|---|
| E-01 | `E2E.Tests/Workbook/PreservationAndImmutabilityTests.cs` |
| E-02 | `E2E.Tests/Workbook/ProtectedSampleOfflineEvidenceTests.cs`; `docs-dev/evidence/protected-sample-record.schema.json` |
| E-03 | `E2E.Tests/Security/PackageMappingPromptTests.cs` |
| E-04 | `E2E.Tests/Copilot/PolicyOAuthCopilotTests.cs` |
| E-05 | `E2E.Tests/Security/SessionNetworkLogTests.cs` |
| E-06 | `E2E.Tests/Recovery/CheckpointRecoveryTests.cs` |
| E-07 | `E2E.Tests/Office/FormulaSimilarityOfficeTests.cs`; `eng/office-matrix.json` |
| E-08 | `E2E.Tests/Accessibility/PrimaryJourneyAccessibilityTests.cs`; `docs-dev/evidence/accessibility-manual-matrix.md` |
| E-09 | `E2E.Tests/CrossPlatform/SetupToUninstallTests.cs` |
| E-10 | `E2E.Tests/Performance/LocalPipelineBenchmarks.cs`; `eng/schemas/performance-result.schema.json` |
| E-11 | `E2E.Tests/Performance/LiveCopilotMetricsTests.cs`; `docs-dev/evidence/live-copilot-environment-record.schema.json` |
| E-12 | `E2E.Tests/Validation/RealDataReleaseGateTests.cs` |
| E-13 | `E2E.Tests/Content/SafetyNoticeUiWorkbookTests.cs` |
| E-14 | `E2E.Tests/Flows/SyntheticHappyPathTests.cs` |
| E-TR | `docs-dev/traceability.md` |
| GATE-ACCEPTANCE | `artifacts/test/gate-acceptance.json` |

## Docs and release

| Task | Exact files |
|---|---|
| D-01 | `docs-dev/architecture.md`; `docs-dev/images/architecture.svg` |
| D-02 | `docs-dev/components.md`; `docs-dev/images/components.svg` |
| D-03 | `docs-dev/excel-contract.md` |
| D-04 | `docs-dev/prompt-authoring.md` |
| D-05 | `docs-dev/evaluation.md` |
| D-06 | `docs-dev/security-privacy.md` |
| D-07 | `docs-dev/build-test-release.md` |
| D-08 | `SECURITY.md` |
| D-09 | `README.md` |
| D-10 | `images/01-welcome.png`; `images/02-authentication.png`; `images/03-file-selection.png`; `images/04-question-mapping.png`; `images/05-evaluation-design.png`; `images/06-trial-validation.png`; `images/07-run-confirmation.png`; `images/08-execution.png`; `images/09-review.png`; `images/10-completion.png`; `images/11-settings-diagnostics.png` |
| D-11 | `docs-dev/customization.md` |
| D-12 | `E2E.Tests/Content/DocumentationContractTests.cs` |
| REL-01 | `artifacts/release/supply-chain-index.json`; SBOM/license/SHA/signature/security report |
| REL-02 | `artifacts/release/acceptance-report.json` |
| REL-03 | `artifacts/release/operations-approval-record.jws` |
| REL-04 | `artifacts/release/independent-review.jws` |
| REL-05 | `docs-dev/traceability.md` completeness check only |
| REL-06 | `artifacts/release/release-manifest.jws` |

## P1 exact files

| Task | Exact files |
|---|---|
| P1-01 | `Core/Mapping/MappingProfile.cs`; `Platform/State/MappingProfileStore.cs`; `Core.Tests/Mapping/MappingProfileTests.cs`; `Platform.Tests/State/MappingProfileStoreTests.cs` |
| P1-02 | `Core/Mapping/FormsManagementColumnSuggester.cs`; `Core.Tests/Mapping/FormsManagementColumnSuggesterTests.cs` |
| P1-03 | `Core/Prompting/FewShotExampleSet.cs`; `Core.Tests/Prompting/FewShotExampleSetTests.cs` |
| P1-04 | `Core/Runs/RepeatedEvaluationPlan.cs`; `Core.Tests/Runs/RepeatedEvaluationPlanTests.cs` |
| P1-05 | `Core/Policy/RetentionResponsibility.cs`; `Core.Tests/Policy/RetentionResponsibilityTests.cs` |
| P1-06 | `Core/Validation/ThresholdCalibrationMetrics.cs`; `Core.Tests/Validation/ThresholdCalibrationMetricsTests.cs` |
| P1-07 | `Workbooks/Writing/ConfigNamedRangeWriter.cs`; `Workbooks.Tests/Writing/ConfigNamedRangeWriterTests.cs` |
| P1-08 | `Workbooks/Writing/MinimizedReviewCopyWriter.cs`; `Workbooks.Tests/Writing/MinimizedReviewCopyWriterTests.cs` |
| P1-09 | `Platform/Storage/SharedFolderRiskDetector.cs`; `Platform.Tests/Storage/SharedFolderRiskDetectorTests.cs` |
| P1-10 | `docs-dev/accessibility-conformance.md`; `E2E.Tests/Accessibility/ConformanceChecklistTests.cs` |

## Ownership rule

If this map conflicts with an older v2.0 path, v2.1 runtime ReportDefinition names win only for these intentional renames/additions:

- `EvaluationDesign.cs` → `ReportDefinition.cs` + `RunSnapshot.cs`.
- `InferenceLimits.cs` → `InferenceHardLimits.cs` + `ReportDefinitionBudgetValidator.cs`.
- `GitHubIdentityGuard.cs` → `GitHubAccountAuthorizationGuard.cs`.
- `SubmitEvaluationSchema.cs` → `SubmitEvaluationSchemaFactory.cs`.

All other path changes require an explicit plan revision; implementers must not invent alternate folders or duplicate owners.
