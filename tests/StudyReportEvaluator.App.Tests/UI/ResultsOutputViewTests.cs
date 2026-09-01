using System.Collections.Immutable;
using System.Reflection;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class ResultsOutputViewTests
{
    [Fact]
    public async Task Override_uses_snapshot_range_and_formula_equivalent_preview_then_blocks_invalid_export()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        RecordingOutputBoundary output = new();
        ResultsOutputViewModel viewModel = new(output, U04TestSupport.Context(summary));
        ResultsCriterionViewModel item = Assert.Single(viewModel.Results);

        Assert.Equal(5m, item.AiRawScore);
        Assert.Equal(5m, item.EffectiveRaw);
        Assert.Equal(50m, item.NormalizedScore);
        Assert.Equal(50m, item.EvaluatorScore);
        Assert.Equal(50m, item.QuestionScore);
        Assert.Equal(50m, item.OverallScore);
        Assert.True(item.CanOverride);
        Assert.True(viewModel.CanExport);

        item.OverrideText = "8";

        Assert.Null(item.OverrideError);
        Assert.Equal(8m, item.EffectiveRaw);
        Assert.Equal(80m, item.NormalizedScore);
        Assert.Equal(80m, item.EvaluatorScore);
        Assert.Equal(80m, item.QuestionScore);
        Assert.Equal(80m, item.OverallScore);
        Assert.True(viewModel.CanExport);

        item.OverrideText = "11";

        Assert.True(item.HasOverrideError);
        Assert.Contains("range", item.OverrideError, StringComparison.OrdinalIgnoreCase);
        Assert.Null(item.EffectiveRaw);
        Assert.Null(item.NormalizedScore);
        Assert.Null(item.EvaluatorScore);
        Assert.Null(item.QuestionScore);
        Assert.Null(item.OverallScore);
        Assert.True(viewModel.HasOverrideErrors);
        Assert.False(viewModel.CanExport);
        await viewModel.ExportAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, output.ExportCount);

        item.OverrideText = "8";
        await viewModel.ExportAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, output.ExportCount);
        RunCriterionOverride written = Assert.Single(output.LastRequest?.Overrides ?? []);
        Assert.Equal("8", written.Value);
        Assert.Equal(ResultsOutputStatusCodes.Success, viewModel.LastExportCode);
        Assert.DoesNotContain("8", item.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_primary_disables_override_and_keeps_raw_effective_and_ancestors_blank_not_zero()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(
            definition,
            metadata,
            primary: "   ");
        ResultsOutputViewModel viewModel = new(
            new RecordingOutputBoundary(),
            U04TestSupport.Context(summary));
        ResultsCriterionViewModel item = Assert.Single(viewModel.Results);

        Assert.Equal(ResultsStatusCodes.Empty, item.StatusCode);
        Assert.False(item.CanOverride);
        Assert.Null(item.AiRawScore);
        Assert.Null(item.EffectiveRaw);
        Assert.Null(item.NormalizedScore);
        Assert.Null(item.EvaluatorScore);
        Assert.Null(item.QuestionScore);
        Assert.Null(item.OverallScore);
        Assert.Equal("—", item.AiRawText);
        Assert.Equal("—", item.OverallScoreText);

        item.OverrideText = "5";

        Assert.Equal(string.Empty, item.OverrideText);
        Assert.False(viewModel.HasOverrideErrors);
        Assert.True(viewModel.CanExport);
        RunOutputPreparation preparation = summary.PrepareOutput();
        QuestionResultInput question = Assert.Single(Assert.Single(preparation.Rows).Questions);
        Assert.False(question.Scorable);
        Assert.Null(Assert.Single(question.Evaluators).AiResult);
    }

    [Fact]
    public async Task Cancelled_partial_run_retains_completed_raw_and_exports_unfinished_units_as_blank_cancelled()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 3);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(
            definition,
            metadata,
            cancelAfterFirst: true);
        RecordingOutputBoundary output = new();
        ExecutionRunContext context = U04TestSupport.Context(summary);
        ResultsOutputViewModel viewModel = new(output, context);

        Assert.True(viewModel.IsPartial);
        Assert.True(viewModel.IsInputUnchanged);
        Assert.Equal(2, viewModel.PlannedEvaluationCount);
        Assert.Equal(1, viewModel.CompletedEvaluationCount);
        Assert.Equal(1, viewModel.CancelledCount);
        Assert.Equal(2, viewModel.Results.Count);
        ResultsCriterionViewModel completed = viewModel.Results.Single(item => item.SourceRowNumber == 2);
        ResultsCriterionViewModel cancelled = viewModel.Results.Single(item => item.SourceRowNumber == 3);
        Assert.Equal(5m, completed.AiRawScore);
        Assert.Equal(ResultsStatusCodes.Cancelled, cancelled.StatusCode);
        Assert.Null(cancelled.AiRawScore);
        Assert.Null(cancelled.EffectiveRaw);
        Assert.Null(cancelled.NormalizedScore);
        Assert.Null(cancelled.OverallScore);
        Assert.True(viewModel.CanExport);

        await viewModel.ExportAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, output.ExportCount);
        Assert.Same(context, output.LastRequest?.Context);
        RunOutputPreparation preparation = summary.PrepareOutput(output.LastRequest?.Overrides);
        EvaluatorResultInput cancelledOutput = Assert.Single(
            Assert.Single(preparation.Rows, row => row.SourceRowNumber == 3).Questions[0].Evaluators);
        Assert.Equal(ResultsStatusCodes.Cancelled, cancelledOutput.Status);
        Assert.Null(cancelledOutput.AiResult);
        Assert.Equal(ResultsOutputStatusCodes.Success, viewModel.LastExportCode);
    }

    [Fact]
    public async Task Output_collision_is_visible_blocks_export_and_never_requests_overwrite()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        RecordingOutputBoundary output = new()
        {
            Assess = (_, path) => path.EndsWith("occupied.xlsx", StringComparison.OrdinalIgnoreCase)
                ? new ResultsOutputPathAssessment(
                    ResultsOutputStatusCodes.TargetExists,
                    isValid: false,
                    targetExists: true)
                : ResultsOutputPathAssessment.Valid,
        };
        ResultsOutputViewModel viewModel = new(output, U04TestSupport.Context(summary));

        viewModel.OutputPath = Path.Combine(Path.GetTempPath(), "occupied.xlsx");

        Assert.False(viewModel.IsOutputPathValid);
        Assert.True(viewModel.OutputTargetExists);
        Assert.False(viewModel.CanExport);
        Assert.Contains("上書き", viewModel.OutputPathStatusText, StringComparison.Ordinal);
        await viewModel.ExportAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, output.ExportCount);
    }

    [Fact]
    public async Task Loading_a_new_run_suppresses_the_late_result_of_a_cancelled_previous_export()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        TaskCompletionSource exportStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ResultsOutputResult> releaseExport = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingOutputBoundary output = new()
        {
            Export = (_, _) =>
            {
                exportStarted.TrySetResult();
                return releaseExport.Task;
            },
        };
        ResultsOutputViewModel viewModel = new(
            output,
            U04TestSupport.Context(
                summary,
                Path.Combine(Path.GetTempPath(), "first-input.xlsx")));

        Task firstExport = viewModel.ExportAsync(TestContext.Current.CancellationToken);
        await exportStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsExporting);
        ExecutionRunContext nextContext = U04TestSupport.Context(
            summary,
            Path.Combine(Path.GetTempPath(), "second-input.xlsx"));

        viewModel.Load(nextContext);

        Assert.False(viewModel.IsExporting);
        Assert.Equal(ResultsOutputStatusCodes.Ready, viewModel.LastExportCode);
        Assert.Contains("second-input_quantified_", viewModel.OutputPath, StringComparison.Ordinal);
        releaseExport.TrySetResult(new ResultsOutputResult(ResultsOutputStatusCodes.TargetExists));
        await firstExport;

        Assert.False(viewModel.IsExporting);
        Assert.Equal(ResultsOutputStatusCodes.Ready, viewModel.LastExportCode);
        Assert.Contains("second-input_quantified_", viewModel.OutputPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_output_boundary_writes_valid_separate_workbook_and_preserves_exact_input_identity()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSingleSheet(
            "Original",
            1,
            2,
            2,
            new X02Header(1, "Primary A"),
            new X02Header(2, "Support B"));
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = new WorkbookMetadataReader().Read(workbook.Path);
        ScriptedRunner runner = new((payload, _, _) => Task.FromResult(
            EvaluationRunnerResult.Succeeded(
                U01TestSupport.ValidResult(payload, _ => 5m))));
        RunSummary summary = await new QuantificationOrchestrator(
            new OpenXmlEvaluationRowSource(workbook.Path),
            runner).RunAsync(
                new QuantificationRunRequest
                {
                    DraftDefinition = definition,
                    WorkbookMetadata = metadata,
                    InputPath = workbook.Path,
                    ModelId = "model-test",
                    MaximumPromptTokens = 64_000,
                    MaximumContextWindowTokens = 128_000,
                    MaxConcurrency = 1,
                },
                cancellationToken: TestContext.Current.CancellationToken);
        string finalPath = Path.Combine(workbook.Directory, "quantified.xlsx");
        ResultsOutputBoundary output = new();
        ExecutionRunContext context = U04TestSupport.Context(summary, workbook.Path);

        Assert.True(output.AssessPath(workbook.Path, finalPath).IsValid);
        ResultsOutputResult result = await output.ExportAsync(
            new ResultsOutputRequest(context, finalPath),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal(Path.GetFullPath(finalPath), result.FinalPath);
        Assert.True(File.Exists(finalPath));
        Assert.True(new InputSnapshotService().Recheck(workbook.Path, summary.InputSnapshot).IsMatch);
        using (SpreadsheetDocument original = SpreadsheetDocument.Open(workbook.Path, false))
        {
            WorkbookPart originalPart = original.WorkbookPart
                ?? throw new InvalidDataException("The input workbook part is missing.");
            Workbook originalWorkbook = originalPart.Workbook
                ?? throw new InvalidDataException("The input workbook root is missing.");
            Assert.Single(originalWorkbook.Descendants<Sheet>());
        }

        using SpreadsheetDocument outputDocument = SpreadsheetDocument.Open(finalPath, false);
        WorkbookPart outputPart = outputDocument.WorkbookPart
            ?? throw new InvalidDataException("The output workbook part is missing.");
        Workbook outputWorkbook = outputPart.Workbook
            ?? throw new InvalidDataException("The output workbook root is missing.");
        Sheet[] sheets = outputWorkbook.Descendants<Sheet>().ToArray();
        Assert.Equal(4, sheets.Length);
        Assert.Contains(sheets, sheet => sheet.Name?.Value == AppOwnedSheetNameResolver.ConfigBaseName);
        Assert.Contains(sheets, sheet => sheet.Name?.Value == AppOwnedSheetNameResolver.ResultsBaseName);
        Assert.Contains(sheets, sheet => sheet.Name?.Value == AppOwnedSheetNameResolver.RunBaseName);
        Assert.NotEmpty(outputPart.WorksheetParts.SelectMany(part =>
            (part.Worksheet ?? throw new InvalidDataException("An output worksheet is missing."))
                .Descendants<CellFormula>()));
        Assert.DoesNotContain(workbook.Path, result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ethics_warning_acknowledgement_is_absent_from_snapshot_and_export_gate_state()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        ResultsOutputViewModel viewModel = new(
            new RecordingOutputBoundary(),
            U04TestSupport.Context(summary));
        string[] prohibited = ["Acknowledge", "Consent", "Dismiss", "WarningAccepted", "EthicsAccepted"];
        MemberInfo[] members = typeof(ResultsOutputViewModel).GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(
            members,
            member => prohibited.Any(term => member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            prohibited,
            term => summary.Snapshot.CanonicalJson.Contains(term, StringComparison.OrdinalIgnoreCase));
        Assert.True(viewModel.CanExport);
    }
}

internal sealed class RecordingOutputBoundary : IResultsOutputBoundary
{
    public Func<string, string, ResultsOutputPathAssessment> Assess { get; init; } =
        (_, _) => ResultsOutputPathAssessment.Valid;

    public Func<ResultsOutputRequest, CancellationToken, Task<ResultsOutputResult>> Export { get; init; } =
        (request, _) => Task.FromResult(new ResultsOutputResult(
            ResultsOutputStatusCodes.Success,
            request.OutputPath));

    public int ExportCount { get; private set; }

    public ResultsOutputRequest? LastRequest { get; private set; }

    public ResultsOutputPathAssessment AssessPath(string inputPath, string outputPath) =>
        Assess(inputPath, outputPath);

    public Task<ResultsOutputResult> ExportAsync(
        ResultsOutputRequest request,
        CancellationToken cancellationToken)
    {
        ExportCount++;
        LastRequest = request;
        return Export(request, cancellationToken);
    }
}