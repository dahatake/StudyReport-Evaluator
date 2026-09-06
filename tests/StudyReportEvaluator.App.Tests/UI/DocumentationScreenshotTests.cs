using System.Security.Cryptography;
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
    private const string GenerateExecutionEnvironmentVariable =
        "STUDY_REPORT_EVALUATOR_GENERATE_EXECUTION_DOC_IMAGE";
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
        bool updateDocumentation = string.Equals(
                Environment.GetEnvironmentVariable(GenerateEnvironmentVariable),
                "1",
                StringComparison.Ordinal);
        string renderedDirectory = Path.Combine(Path.GetTempPath(), "StudyReportEvaluator-Captures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(renderedDirectory);
        try
        {
            await GenerateAsync(renderedDirectory);
            foreach (string fileName in ScreenshotFileNames)
            {
                string renderedPath = Path.Combine(renderedDirectory, fileName);
                using Bitmap rendered = new(renderedPath);
                Assert.Equal(new PixelSize(ScreenshotWidth, ScreenshotHeight), rendered.PixelSize);
                if (updateDocumentation)
                {
                    File.Copy(renderedPath, Path.Combine(imageDirectory, fileName), overwrite: true);
                }
            }
        }
        finally
        {
            Directory.Delete(renderedDirectory, recursive: true);
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

    [AvaloniaFact]
    public async Task Synthetic_screenshot_renders_are_repeatable()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StudyReportEvaluator-CaptureRepeat-" + Guid.NewGuid().ToString("N"));
        string first = Directory.CreateDirectory(Path.Combine(directory, "first")).FullName;
        string second = Directory.CreateDirectory(Path.Combine(directory, "second")).FullName;
        try
        {
            await GenerateAsync(first);
            await GenerateAsync(second);
            foreach (string fileName in ScreenshotFileNames)
            {
                string before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(first, fileName))));
                string after = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(second, fileName))));
                Assert.True(before == after, $"Synthetic screenshot is not repeatable: {fileName}");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Execution_documentation_screenshot_renders_are_repeatable()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StudyReportEvaluator-ExecutionCaptureRepeat-" + Guid.NewGuid().ToString("N"));
        string first = Directory.CreateDirectory(Path.Combine(directory, "first")).FullName;
        string second = Directory.CreateDirectory(Path.Combine(directory, "second")).FullName;
        string fileName = ScreenshotFileNames[4];
        try
        {
            await GenerateAsync(first, executionOnly: true);
            await GenerateAsync(second, executionOnly: true);
            foreach (string renderedDirectory in new[] { first, second })
            {
                string path = Assert.Single(Directory.GetFiles(renderedDirectory));
                Assert.Equal(fileName, Path.GetFileName(path));
                Assert.True(
                    new FileInfo(path).Length > 10_000,
                    $"Documentation screenshot is unexpectedly small: {fileName}");
                using Bitmap bitmap = new(path);
                Assert.Equal(new PixelSize(ScreenshotWidth, ScreenshotHeight), bitmap.PixelSize);
            }

            string before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(first, fileName))));
            string after = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(second, fileName))));
            Assert.True(before == after, $"Synthetic screenshot is not repeatable: {fileName}");

            // Retain only validated 05 renders for review; never publish to images here,
            // even when the separate seven-image generation opt-in is also set.
            if (string.Equals(
                    Environment.GetEnvironmentVariable(GenerateExecutionEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                string artifactDirectory = Path.Combine(
                    FindRepositoryRoot(), "artifacts", "test", "documentation-execution-05");
                foreach (string render in new[] { "first", "second" })
                {
                    string destination = Directory.CreateDirectory(Path.Combine(artifactDirectory, render)).FullName;
                    File.Copy(
                        Path.Combine(directory, render, fileName),
                        Path.Combine(destination, fileName),
                        overwrite: true);
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task GenerateAsync(string imageDirectory, bool executionOnly = false)
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
        QuantificationDefinition screenshotDefinition = input.DefinitionDraft with
        {
            Id = "documentation-definition",
            Questions = [.. input.DefinitionDraft.Questions.Select((question, index) => question with
            {
                Id = $"documentation-question-{index + 1}",
                Evaluators = [question.Evaluators[0] with
                {
                    Id = $"documentation-evaluator-{index + 1}",
                    Criteria = [question.Evaluators[0].Criteria[0] with { Id = $"documentation-criterion-{index + 1}" }],
                }],
            })],
        };

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
        RecordingAuthenticationBoundary authentication = new(authenticationSnapshot);
        ExecutionViewModel execution = new(
            authentication,
            runBoundary);
        ResultsOutputViewModel results = new(new RecordingOutputBoundary());
        WorkflowNavigator navigator = new();
        navigator.MoveNext();
        MainWindowViewModel viewModel = new(
            navigator,
            input,
            new QuantificationDesignViewModel(screenshotDefinition),
            execution,
            results);
        viewModel.PreviousCommand.Execute(null);
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
            if (!executionOnly)
            {
                Capture(window, imageDirectory, ScreenshotFileNames[0]);
            }

            InputView inputView = Assert.Single(
                window.GetVisualDescendants().OfType<InputView>());
            Required<ListBox>(inputView, "InputQuestionList").BringIntoView();
            Render();
            if (!executionOnly)
            {
                Capture(window, imageDirectory, ScreenshotFileNames[1]);
            }

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
            Border formulaGuide = Required<Border>(designView, "FormulaGuide");
            ListBox questionCards = Required<ListBox>(designView, "QuestionEditorList");
            TextBox knowledgePrompt = window.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(control => string.Equals(
                    AutomationProperties.GetAutomationId(control),
                    knowledgeEvaluator.KnowledgePromptPreviewAutomationId,
                    StringComparison.Ordinal));
            designView.BringIntoView();
            Render();
            questionCards.BringIntoView();
            Render();
            Assert.Equal(
                2,
                designView.GetVisualDescendants().OfType<Border>().Count(border =>
                    (AutomationProperties.GetAutomationId(border) ?? string.Empty).StartsWith(
                        "DesignQuestion-",
                        StringComparison.Ordinal)));
            AssertContainedInViewport(formulaGuide, window);
            foreach (Border card in designView.GetVisualDescendants().OfType<Border>().Where(border =>
                         (AutomationProperties.GetAutomationId(border) ?? string.Empty).StartsWith(
                             "DesignQuestion-",
                             StringComparison.Ordinal)))
            {
                AssertContainedInViewport(card, window);
            }
            if (!executionOnly)
            {
                Capture(window, imageDirectory, ScreenshotFileNames[2]);
            }

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
            designView.BringIntoView();
            Render();
            Assert.True(customPrompt.IsEffectivelyVisible);
            Assert.True(formulaGuide.IsEffectivelyVisible);
            AssertContainedInViewport(formulaGuide, window);
            AssertContainedInViewport(customPrompt, window);
            if (!executionOnly)
            {
                Capture(window, imageDirectory, ScreenshotFileNames[3]);
            }

            viewModel.NextCommand.Execute(null);
            Render();
            execution.MaxConcurrency = 2;
            Assert.Equal(402, execution.PlannedEvaluationCount);
            ExecutionView executionView = Assert.Single(
                window.GetVisualDescendants().OfType<ExecutionView>());
            Assert.True(Required<CheckBox>(executionView, "ResumeModeCheckBox").IsVisible);
            Assert.True(Required<TextBox>(executionView, "OutputDirectoryTextBox").IsVisible);
            ResetScroll(Required<ScrollViewer>(window, "ShellScrollViewer"));
            ResetScroll(Required<ScrollViewer>(executionView, "ExecutionScrollViewer"));
            Render();

            // This is the synthetic, not-yet-checked state, not evidence of live login.
            Button checkAuthentication = Required<Button>(executionView, "CheckAuthenticationButton");
            Button login = Required<Button>(executionView, "StartCopilotLogin");
            Button cancelLogin = Required<Button>(executionView, "CancelCopilotLogin");
            TextBlock loginStatus = Required<TextBlock>(executionView, "CopilotLoginStatus");
            Assert.Equal(ExecutionAuthenticationState.NotChecked, execution.AuthenticationState);
            Assert.False(execution.IsAuthenticationAvailable);
            Assert.Empty(execution.AvailableModelIds);
            Assert.Null(execution.SelectedModelId);
            Assert.False(execution.IsLoggingIn);
            Assert.Null(execution.LastLoginTask);
            Assert.Equal(0, authentication.CallCount);
            Assert.Equal(0, runBoundary.CallCount);
            Assert.Same(execution.LoginCommand, login.Command);
            Assert.Equal("GitHubにログイン", login.Content);
            Assert.True(checkAuthentication.IsEffectivelyEnabled);
            Assert.True(login.IsEffectivelyEnabled);
            Assert.False(cancelLogin.IsEffectivelyEnabled);
            Assert.False(Required<Button>(executionView, "StartRunButton").IsEffectivelyEnabled);
            Assert.Equal("GitHub へのログインは開始していません。", execution.LoginStatusText);
            Assert.Equal(execution.LoginStatusText, loginStatus.Text);
            Assert.Equal(loginStatus.Text, AutomationProperties.GetName(loginStatus));
            Assert.Empty(Required<StackPanel>(executionView, "CopilotLoginPanel")
                .GetVisualDescendants().OfType<TextBox>());
            foreach (Control control in new Control[] { checkAuthentication, login, cancelLogin, loginStatus })
            {
                Assert.True(control.IsEffectivelyVisible);
                AssertContainedInViewport(control, window);
            }

            Capture(window, imageDirectory, ScreenshotFileNames[4]);
            if (executionOnly)
            {
                return;
            }

            await execution.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Auto", execution.SelectedModelId);

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

    private static void AssertContainedInViewport(Control control, TopLevel viewport)
    {
        Point origin = control.TranslatePoint(default, viewport)
            ?? throw new InvalidOperationException("The screenshot target position is unavailable.");
        Assert.True(
            origin.X >= 0d
                && origin.Y >= 0d
                && origin.X + control.Bounds.Width <= viewport.ClientSize.Width
                && origin.Y + control.Bounds.Height <= viewport.ClientSize.Height,
            $"{control.GetType().Name} is not fully contained in the screenshot viewport.");
    }

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