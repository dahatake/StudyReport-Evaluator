using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class DocumentationScreenshotTests
{
    private const string GenerateEnvironmentVariable =
        "STUDY_REPORT_EVALUATOR_GENERATE_DOC_IMAGES";
    private const string SafeInputPath = @"C:\Synthetic\StudyReport-100x2.xlsx";
    private const int ScreenshotWidth = 1440;
    private const int ScreenshotHeight = 1050;

    private static readonly string[] ScreenshotFileNames =
    [
        "01-input-workbook.png",
        "02-input-mapping.png",
        "03-design-knowledge.png",
        "04-design-custom-prompt.png",
        "05-execution-auto.png",
        "06-results-review.png",
        "07-output-export.png",
    ];

    [AvaloniaFact]
    public async Task Documentation_screenshots_use_only_synthetic_state_and_have_expected_dimensions()
    {
        string repositoryRoot = FindRepositoryRoot();
        string imageDirectory = Path.Combine(repositoryRoot, "images");
        if (string.Equals(
                Environment.GetEnvironmentVariable(GenerateEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            Directory.CreateDirectory(imageDirectory);
            await GenerateAsync(imageDirectory);
        }

        foreach (string fileName in ScreenshotFileNames)
        {
            string path = Path.Combine(imageDirectory, fileName);
            Assert.True(File.Exists(path), $"Missing documentation screenshot: {fileName}");
            Assert.True(
                new FileInfo(path).Length > 10_000,
                $"Documentation screenshot is unexpectedly small: {fileName}");
            using Bitmap bitmap = new(path);
            Assert.Equal(
                new PixelSize(ScreenshotWidth, ScreenshotHeight),
                bitmap.PixelSize);
        }
    }

    private static async Task GenerateAsync(string imageDirectory)
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSingleSheet(
            "Original",
            headerRow: 1,
            lastRow: 101,
            lastColumn: 4,
            new X02Header(1, "回答 ID"),
            new X02Header(2, "設問 1 のレポート回答"),
            new X02Header(3, "設問 2 のレポート回答"),
            new X02Header(4, "補助情報"));
        InputWorkbookLoadResult loaded = await new InputWorkbookLoader().LoadAsync(
            workbook.Path,
            headerRow: 1,
            TestContext.Current.CancellationToken);
        InputViewModel input = new(new FixedInputWorkbookLoader(loaded));
        await input.SetFilePathAsync(
            SafeInputPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, input.Questions.Count);
        input.Questions[0].DisplayName = "設問1：機械学習の基礎";
        input.Questions[1].DisplayName = "設問2：活用方法の説明";
        input.Questions[1].SetSupportingColumn("D", selected: true);

        CopilotRuntimeIdentity runtimeIdentity = new(
            @"C:\Synthetic\StudyReportEvaluator-win-x64\runtimes\win-x64\native\copilot.exe",
            "1.0.79",
            new string('B', 64),
            "1.0.11+synthetic");
        ExecutionAuthenticationSnapshot authenticationSnapshot = new(
            ExecutionAuthenticationState.Available,
            [U04TestSupport.Model("Auto"), U04TestSupport.Model("gpt-5")],
            runtimeIdentity);
        RunSummary? preparedSummary = null;
        RecordingRunBoundary runBoundary = new((_, progress, _) =>
        {
            RunSummary summary = preparedSummary
                ?? throw new InvalidOperationException("The synthetic screenshot summary is not ready.");
            progress?.Invoke(new EvaluationProgress(
                summary.PlannedEvaluationCount,
                summary.CompletedEvaluationCount,
                0,
                EvaluationProgressStatus.Completed));
            return Task.FromResult(summary);
        });
        ExecutionViewModel execution = new(
            new RecordingAuthenticationBoundary(authenticationSnapshot),
            runBoundary);
        ResultsOutputViewModel results = new(new RecordingOutputBoundary());
        MainWindowViewModel viewModel = new(
            new WorkflowNavigator(),
            input,
            new QuantificationDesignViewModel(input.DefinitionDraft),
            execution,
            results);
        MainWindow window = new(viewModel)
        {
            Width = ScreenshotWidth,
            Height = ScreenshotHeight,
        };

        try
        {
            window.Show();
            Render();
            Assert.True(Required<Button>(Assert.Single(
                window.GetVisualDescendants().OfType<InputView>()), "PickFileButton").IsVisible);
            Capture(window, imageDirectory, ScreenshotFileNames[0]);

            InputView inputView = Assert.Single(
                window.GetVisualDescendants().OfType<InputView>());
            Required<ListBox>(inputView, "InputQuestionList").BringIntoView();
            Render();
            Capture(window, imageDirectory, ScreenshotFileNames[1]);

            viewModel.NextCommand.Execute(null);
            Render();
            QuantificationDesignViewModel design = viewModel.DesignViewModel;
            design.DefinitionName = "100名・2設問 レポート定量化";
            design.Revision = "2026-09";
            design.RoundingDigits = 1;
            design.BasePoints = 60m;
            design.SpecialPoints = 0m;
            design.SimilarityPenaltyWeight = 0.1m;
            ConfigureKnowledgeQuestion(design.Questions[0]);
            ConfigureCustomQuestion(design.Questions[1]);
            Assert.True(design.IsAllocationValid);

            QuestionDesignItemViewModel knowledgeQuestion = design.Questions[0];
            design.SelectedQuestion = knowledgeQuestion;
            EvaluatorDesignItemViewModel knowledgeEvaluator = knowledgeQuestion.Evaluators[0];
            knowledgeQuestion.SelectedEvaluator = knowledgeEvaluator;
            QuantificationDesignView designView = Assert.Single(
                window.GetVisualDescendants().OfType<QuantificationDesignView>());
            TextBox knowledgePrompt = window.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(control => string.Equals(
                    AutomationProperties.GetAutomationId(control),
                    knowledgeEvaluator.KnowledgePromptPreviewAutomationId,
                    StringComparison.Ordinal));
            knowledgePrompt.BringIntoView();
            Render();
            Capture(window, imageDirectory, ScreenshotFileNames[2]);

            QuestionDesignItemViewModel customQuestion = design.Questions[1];
            EvaluatorDesignItemViewModel customEvaluator = customQuestion.Evaluators[0];
            design.SelectedQuestion = customQuestion;
            customQuestion.SelectedEvaluator = customEvaluator;
            Render();
            TextBox customPrompt = window.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(control => string.Equals(
                    AutomationProperties.GetAutomationId(control),
                    customEvaluator.PromptAutomationId,
                    StringComparison.Ordinal));
            customPrompt.BringIntoView();
            Render();
            Capture(window, imageDirectory, ScreenshotFileNames[3]);

            viewModel.NextCommand.Execute(null);
            Render();
            await execution.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
            execution.MaxConcurrency = 2;
            Assert.Equal("Auto", execution.SelectedModelId);
            Assert.Equal(402, execution.PlannedEvaluationCount);
            ExecutionView executionView = Assert.Single(
                window.GetVisualDescendants().OfType<ExecutionView>());
            Assert.True(Required<CheckBox>(executionView, "ResumeModeCheckBox").IsVisible);
            Assert.True(Required<TextBox>(executionView, "OutputDirectoryTextBox").IsVisible);
            ResetScroll(Required<ScrollViewer>(window, "ShellScrollViewer"));
            ResetScroll(Required<ScrollViewer>(executionView, "ExecutionScrollViewer"));
            Render();
            Capture(window, imageDirectory, ScreenshotFileNames[4]);

            WorkbookMetadata metadata = input.Metadata
                ?? throw new InvalidOperationException("Synthetic workbook metadata is missing.");
            preparedSummary = await CreateDurableSummaryAsync(
                input.DefinitionDraft,
                metadata,
                runtimeIdentity);
            await execution.StartAsync(TestContext.Current.CancellationToken);
            Assert.True(results.IsLoaded);
            viewModel.NextCommand.Execute(null);
            Render();
            ResultsCriterionViewModel firstResult = results.Results[0];
            firstResult.OverrideText = "9";
            Render();

            ResultsOutputView resultsView = Assert.Single(
                window.GetVisualDescendants().OfType<ResultsOutputView>());
            Assert.True(results.IsAutomaticOutput);
            Assert.EndsWith("eval-20260902-1200.xlsx", results.FinalPath, StringComparison.Ordinal);
            ResetScroll(Required<ScrollViewer>(window, "ShellScrollViewer"));
            ResetScroll(Required<ScrollViewer>(resultsView, "ResultsOutputScrollViewer"));
            Render();
            Capture(window, imageDirectory, ScreenshotFileNames[5]);

            results.OutputPath = @"C:\Synthetic\result\eval-20260902-1200-reviewed.xlsx";
            Required<TextBox>(resultsView, "OutputPathTextBox").BringIntoView();
            Render();
            Capture(window, imageDirectory, ScreenshotFileNames[6]);
        }
        finally
        {
            window.Close();
        }
    }

    private static void ConfigureKnowledgeQuestion(QuestionDesignItemViewModel question)
    {
        question.DisplayName = "設問1：機械学習の基礎";
        question.QuestionText = "機械学習とルールベースの違いを説明してください。";
        question.Points = 20m;
        EvaluatorDesignItemViewModel evaluator = question.Evaluators[0];
        evaluator.DisplayName = "Knowledge coverage";
        evaluator.Minimum = 0m;
        evaluator.Maximum = 10m;
        CriterionDesignItemViewModel criterion = evaluator.Criteria[0];
        criterion.DisplayName = "概念の説明・関係・適用";
        criterion.Description = "主要概念を説明し、相互関係と具体的な適用に触れているか。";
        criterion.Weight = 1m;
    }

    private static void ConfigureCustomQuestion(QuestionDesignItemViewModel question)
    {
        question.DisplayName = "設問2：活用方法の説明";
        question.QuestionText = "学習した手法の活用例を説明してください。";
        question.Points = 20m;
        EvaluatorDesignItemViewModel evaluator = question.Evaluators[0];
        evaluator.Type = EvaluatorType.CustomPrompt;
        evaluator.DisplayName = "説明品質";
        evaluator.Minimum = 0m;
        evaluator.Maximum = 10m;
        evaluator.CustomPromptTemplate = """
            次の回答を、具体性と論理性の観点から評価してください。

            ### 回答
            {回答}

            ### 補助情報
            {補助情報}

            ### 評価項目
            {評価項目}
            """;
        CriterionDesignItemViewModel criterion = evaluator.Criteria[0];
        criterion.DisplayName = "具体性と論理性";
        criterion.Description = "活用例が具体的で、説明の流れが論理的か。";
        criterion.Weight = 1m;
    }

    private static async Task<RunSummary> CreateDurableSummaryAsync(
        QuantificationDefinition definition,
        WorkbookMetadata metadata,
        CopilotRuntimeIdentity runtimeIdentity)
    {
        ScriptedRowSource rows = new((request, _) =>
        {
            IEnumerable<KeyValuePair<string, string?>> cells = request.SelectedColumns.Select(
                column => new KeyValuePair<string, string?>(
                    column,
                    column switch
                    {
                        "B" => "合成回答：教師あり学習は正解例から規則性を学びます。",
                        "C" => "合成回答：需要予測へ適用し、誤差を検証して改善します。",
                        "D" => "合成補助情報：前提、手順、評価指標を明示します。",
                        _ => "合成値",
                    }));
            return Task.FromResult(new EvaluationRowData(
                request.SourceRowNumber,
                cells));
        });
        ScriptedRunner normalRunner = new((payload, _, _) => Task.FromResult(
            EvaluationRunnerResult.Succeeded(
                U01TestSupport.ValidResult(payload, _ => 8m))));
        DurableQuantificationOrchestrator orchestrator = new(
            rows,
            normalRunner,
            new ScreenshotReferenceRunner(),
            new ScreenshotSpecialRunner(),
            new ScreenshotSimilarityRunner(),
            new ScriptedInputSnapshots(U01TestSupport.InputSnapshot()),
            new ScreenshotCheckpointStore(),
            new ScreenshotPathPlanner(),
            new ScreenshotFinalizer(),
            new ScreenshotCleaner(),
            new ScreenshotTimeProvider());
        return await orchestrator.RunAsync(
            new DurableQuantificationRunRequest
            {
                Run = new QuantificationRunRequest
                {
                    DraftDefinition = definition,
                    WorkbookMetadata = metadata,
                    InputPath = SafeInputPath,
                    ModelId = "Auto",
                    MaximumPromptTokens = 64_000,
                    MaximumContextWindowTokens = 128_000,
                    MaxConcurrency = 2,
                },
                Runtime = new CheckpointRuntimeIdentity
                {
                    ApplicationIdentity = "StudyReportEvaluator.App/4.1-synthetic",
                    CliVersion = runtimeIdentity.CliVersion,
                    CliSha256 = runtimeIdentity.CliSha256,
                    SdkInformationalVersion = runtimeIdentity.SdkInformationalVersion,
                },
                OutputDirectory = @"C:\Synthetic\result",
            },
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class ScreenshotReferenceRunner : IReferenceAnswerOperationRunner
    {
        public Task<AuxiliaryOperationResult<ReferenceAnswerResult>> EvaluateAsync(
            SafeReferenceAnswerPayload payload,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AuxiliaryOperationResult<ReferenceAnswerResult>.Succeeded(
                new ReferenceAnswerResult
                {
                    QuestionId = payload.QuestionId,
                    Answer = "合成参照回答",
                }));
        }
    }

    private sealed class ScreenshotSpecialRunner : ISpecialEvaluationOperationRunner
    {
        public Task<AuxiliaryOperationResult<SpecialQuantificationResult>> EvaluateAsync(
            SafeSpecialEvaluationPayload payload,
            string modelId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AuxiliaryOperationResult<SpecialQuantificationResult>.Succeeded(
                new SpecialQuantificationResult
                {
                    SpecialEvaluationId = payload.SpecialEvaluationId,
                    Score = 0.8m,
                    Reason = "合成固有評価",
                    Evidence = string.Empty,
                    EvidenceSource = EvidenceSourceKind.None,
                    EvidenceSourceColumnId = string.Empty,
                }));
        }
    }

    private sealed class ScreenshotSimilarityRunner : ISimilarityEvaluationOperationRunner
    {
        public Task<AuxiliaryOperationResult<SimilarityQuantificationResult>> EvaluateAsync(
            SafeSimilarityPayload payload,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AuxiliaryOperationResult<SimilarityQuantificationResult>.Succeeded(
                new SimilarityQuantificationResult
                {
                    QuestionId = payload.QuestionId,
                    Similarity = 0.2m,
                    Reason = "合成類似度",
                }));
        }
    }

    private sealed class ScreenshotCheckpointStore : ICheckpointStore
    {
        private CheckpointEnvelope? current;

        public CheckpointSaveResult Create(
            CheckpointEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = envelope;
            return CheckpointSaveResult.Succeeded(createdNew: true);
        }

        public CheckpointSaveResult Update(
            CheckpointEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = envelope;
            return CheckpointSaveResult.Succeeded(createdNew: false);
        }

        public CheckpointLoadResult Load(
            string partialPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return current is null
                ? CheckpointLoadResult.Failed(CheckpointStatusCodes.Invalid)
                : CheckpointLoadResult.Succeeded(current);
        }
    }

    private sealed class ScreenshotPathPlanner : IOutputPathPlanner
    {
        public OutputPathReservation Reserve(
            string inputPath,
            DateTimeOffset localTime,
            string? outputDirectory = null) =>
            OutputPathReservation.Create(
                @"C:\Synthetic\result",
                @"C:\Synthetic\result\eval-20260902-1200.xlsx",
                @"C:\Synthetic\result\eval-20260902-1200.partial.xlsx");
    }

    private sealed class ScreenshotFinalizer : IDurableRunFinalizer
    {
        public DurableFinalizationResult Finalize(
            RunSummary summary,
            CheckpointEnvelope checkpoint,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return DurableFinalizationResult.Succeeded(checkpoint.FinalPath);
        }
    }

    private sealed class ScreenshotCleaner : IPartialCheckpointCleaner
    {
        public bool TryDelete(string partialPath) => true;
    }

    private sealed class ScreenshotTimeProvider : TimeProvider
    {
        private long ticks = new DateTimeOffset(2026, 9, 2, 3, 0, 0, TimeSpan.Zero).UtcTicks;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() =>
            new(Interlocked.Add(ref ticks, TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }

    private static void Capture(
        MainWindow window,
        string imageDirectory,
        string fileName)
    {
        using WriteableBitmap frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Avalonia did not render a screenshot frame.");
        string path = Path.Combine(imageDirectory, fileName);
        using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Flush(flushToDisk: true);
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void ResetScroll(ScrollViewer scrollViewer) =>
        scrollViewer.Offset = default;

    private static T Required<T>(Control root, string name)
        where T : Control =>
        Assert.IsType<T>(root.FindControl<T>(name));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StudyReportEvaluator.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "The repository root containing StudyReportEvaluator.slnx was not found.");
    }

    private sealed class FixedInputWorkbookLoader(InputWorkbookLoadResult result)
        : IInputWorkbookLoader
    {
        public Task<InputWorkbookLoadResult> LoadAsync(
            string filePath,
            uint headerRow,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(SafeInputPath, filePath);
            Assert.Equal(1U, headerRow);
            return Task.FromResult(result);
        }
    }
}