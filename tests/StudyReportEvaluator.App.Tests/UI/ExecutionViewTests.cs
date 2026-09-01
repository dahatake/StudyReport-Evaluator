using System.Collections.Immutable;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class ExecutionViewTests
{
    [Fact]
    public async Task Available_authentication_and_run_bind_model_runtime_snapshot_progress_and_partial_result()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 3);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary partial = await U04TestSupport.CreateSummaryAsync(
            definition,
            metadata,
            cancelAfterFirst: true);
        CopilotRuntimeIdentity identity = U04TestSupport.RuntimeIdentity();
        RecordingAuthenticationBoundary authentication = new(
            new ExecutionAuthenticationSnapshot(
                ExecutionAuthenticationState.Available,
                ["model-a", "model-b", "model-a", " invalid "],
                identity));
        RecordingRunBoundary runner = new((request, progress, _) =>
        {
            progress?.Invoke(new EvaluationProgress(2, 1, 1, EvaluationProgressStatus.Running));
            return Task.FromResult(partial);
        });
        ExecutionViewModel viewModel = new(authentication, runner);
        string inputPath = Path.Combine(Path.GetTempPath(), "PRIVATE-U04-INPUT-CANARY.xlsx");
        viewModel.Configure(definition, metadata, inputPath);

        Assert.Equal(2, viewModel.PlannedEvaluationCount);
        Assert.False(viewModel.CanStart);
        Assert.Contains(viewModel.TechnicalErrors, error => error.Code == "AUTH_CHECK_REQUIRED");

        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionAuthenticationState.Available, viewModel.AuthenticationState);
        Assert.Equal(["model-a", "model-b"], viewModel.AvailableModelIds);
        Assert.Equal("model-a", viewModel.SelectedModelId);
        Assert.Contains("1.0.82", viewModel.RuntimeIdentityText, StringComparison.Ordinal);
        Assert.Contains(identity.CliSha256[..12], viewModel.RuntimeIdentityText, StringComparison.Ordinal);
        Assert.DoesNotContain(identity.CliPath, viewModel.RuntimeIdentityText, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.CanStart);
        viewModel.SelectedModelId = "model-b";
        viewModel.MaxConcurrency = 3;
        ExecutionRunContext? notifiedContext = null;
        viewModel.RunCompleted += (_, eventArgs) => notifiedContext = eventArgs.Context;
        viewModel.RunCompleted += (_, _) => throw new InvalidOperationException("PRIVATE-OBSERVER-CANARY");

        await viewModel.StartAsync(TestContext.Current.CancellationToken);

        QuantificationRunRequest request = Assert.IsType<QuantificationRunRequest>(runner.LastRequest);
        Assert.Equal("model-b", request.ModelId);
        Assert.Equal(3, request.MaxConcurrency);
        Assert.NotSame(definition, request.DraftDefinition);
        Assert.Equal(partial.DefinitionSha256, viewModel.LastRunContext?.Summary.DefinitionSha256);
        Assert.Same(viewModel.LastRunContext, notifiedContext);
        Assert.Same(identity, viewModel.LastRunContext?.RuntimeIdentity);
        Assert.Equal("model-b", viewModel.LastRunContext?.ModelId);
        Assert.Equal(2, viewModel.ProgressTotal);
        Assert.Equal(1, viewModel.ProgressCompleted);
        Assert.Equal(0, viewModel.ProgressInFlight);
        Assert.True(viewModel.LastRunContext?.Summary.IsPartial);
        Assert.Contains("部分結果", viewModel.RunStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain(viewModel.TechnicalErrors, error => error.Code == "RUN_FAILED");
        Assert.DoesNotContain(inputPath, viewModel.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE-OBSERVER-CANARY", viewModel.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancel_routes_the_token_once_and_retains_the_boundary_partial_summary()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 3);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary partial = await U04TestSupport.CreateSummaryAsync(
            definition,
            metadata,
            cancelAfterFirst: true);
        ControlledRunBoundary runner = new(partial);
        ExecutionViewModel viewModel = U04TestSupport.ConfiguredExecutionViewModel(
            definition,
            metadata,
            runner);
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        Task run = viewModel.StartAsync(TestContext.Current.CancellationToken);
        await runner.Started.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsRunning);
        Assert.True(viewModel.CanCancel);

        viewModel.Cancel();
        await runner.CancellationObserved.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsCancelling);
        Assert.False(viewModel.CanCancel);
        Assert.Contains("cancel", viewModel.RunStatusText, StringComparison.OrdinalIgnoreCase);
        runner.Release();
        await run;

        Assert.Equal(1, runner.CallCount);
        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.IsCancelling);
        Assert.Same(partial, viewModel.LastRunContext?.Summary);
        Assert.True(viewModel.LastRunContext?.Summary.IsExportReady);
        Assert.Equal(1, viewModel.ProgressCompleted);
        Assert.Contains("部分結果", viewModel.RunStatusText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ExecutionAuthenticationState.AuthRequired, ResultsStatusCodes.AuthRequired)]
    [InlineData(ExecutionAuthenticationState.CliUnavailable, "COPILOT_CLI_UNAVAILABLE")]
    [InlineData(ExecutionAuthenticationState.RuntimeFailed, "COPILOT_RUNTIME_FAILED")]
    [InlineData(ExecutionAuthenticationState.Cancelled, null)]
    public async Task Finite_authentication_states_never_enable_run(
        ExecutionAuthenticationState state,
        string? expectedErrorCode)
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        ExecutionViewModel viewModel = new(
            new RecordingAuthenticationBoundary(new ExecutionAuthenticationSnapshot(state)),
            new RecordingRunBoundary((_, _, _) => throw new InvalidOperationException("must not run")));
        viewModel.Configure(
            definition,
            metadata,
            Path.Combine(Path.GetTempPath(), "PRIVATE-AUTH-INPUT-CANARY.xlsx"));

        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        Assert.Equal(state, viewModel.AuthenticationState);
        Assert.False(viewModel.CanStart);
        Assert.Empty(viewModel.AvailableModelIds);
        if (expectedErrorCode is not null)
        {
            Assert.Contains(viewModel.TechnicalErrors, error => error.Code == expectedErrorCode);
        }
    }

    [Fact]
    public async Task Invalid_concurrency_and_runtime_failure_are_blocking_and_content_redacted()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        const string exceptionCanary = "PRIVATE-ANSWER-AND-PATH-CANARY";
        ExecutionViewModel viewModel = U04TestSupport.ConfiguredExecutionViewModel(
            definition,
            metadata,
            new RecordingRunBoundary((_, _, _) => throw new IOException(exceptionCanary)));
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        viewModel.MaxConcurrency = 4;

        Assert.False(viewModel.CanStart);
        Assert.Contains(viewModel.TechnicalErrors, error => error.Code == "CONCURRENCY_OUT_OF_RANGE");
        viewModel.MaxConcurrency = 1;
        Assert.True(viewModel.CanStart);

        await viewModel.StartAsync(TestContext.Current.CancellationToken);

        ExecutionTechnicalError failure = Assert.Single(
            viewModel.TechnicalErrors,
            error => error.Code == "RUN_FAILED");
        Assert.DoesNotContain(exceptionCanary, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(exceptionCanary, failure.ToString(), StringComparison.Ordinal);
        Assert.Null(viewModel.LastRunContext);
        Assert.False(viewModel.CanStart);
    }

    [Fact]
    public void Public_execution_contract_has_no_secret_or_warning_gate_input()
    {
        string[] prohibited =
        [
            "Password",
            "PersonalAccessToken",
            "ClientSecret",
            "ApiKey",
            "Acknowledge",
            "Consent",
            "Dismiss",
        ];

        Assert.DoesNotContain(
            typeof(ExecutionViewModel).GetMembers(),
            member => prohibited.Any(term => member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            typeof(ExecutionRunContext).GetMembers(),
            member => prohibited.Any(term => member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }
}

internal static class U04TestSupport
{
    internal static QuantificationDefinition Definition(int firstDataRow, int lastDataRow) =>
        U01TestSupport.Definition(
            firstDataRow,
            lastDataRow,
            U01TestSupport.Question(
                "Q1",
                "A",
                ["B"],
                true,
                U01TestSupport.Evaluator("E1", "C1")));

    internal static CopilotRuntimeIdentity RuntimeIdentity() =>
        new(
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "copilot-u04-test.exe")),
            "1.0.82",
            new string('B', 64),
            "1.0.11");

    internal static ExecutionRunContext Context(RunSummary summary, string? inputPath = null) =>
        new(
            summary,
            inputPath ?? Path.Combine(Path.GetTempPath(), "PRIVATE-U04-CONTEXT-CANARY.xlsx"),
            "model-test",
            RuntimeIdentity());

    internal static ExecutionViewModel ConfiguredExecutionViewModel(
        QuantificationDefinition definition,
        WorkbookMetadata metadata,
        IQuantificationRunBoundary runBoundary)
    {
        ExecutionViewModel viewModel = new(
            new RecordingAuthenticationBoundary(
                new ExecutionAuthenticationSnapshot(
                    ExecutionAuthenticationState.Available,
                    ["model-test"],
                    RuntimeIdentity())),
            runBoundary);
        viewModel.Configure(
            definition,
            metadata,
            Path.Combine(Path.GetTempPath(), "PRIVATE-U04-RUN-CANARY.xlsx"));
        return viewModel;
    }

    internal static async Task<RunSummary> CreateSummaryAsync(
        QuantificationDefinition definition,
        WorkbookMetadata metadata,
        string primary = "synthetic answer",
        bool cancelAfterFirst = false)
    {
        using CancellationTokenSource cancellation = new();
        ScriptedRowSource rows = new((request, _) => Task.FromResult(new EvaluationRowData(
            request.SourceRowNumber,
            new Dictionary<string, string?>
            {
                ["A"] = primary,
                ["B"] = "synthetic support",
            })));
        int invocation = 0;
        ScriptedRunner runner = new((payload, _, _) =>
        {
            EvaluationRunnerResult result = EvaluationRunnerResult.Succeeded(
                U01TestSupport.ValidResult(payload, _ => 5m));
            if (cancelAfterFirst && Interlocked.Increment(ref invocation) == 1)
            {
                cancellation.Cancel();
            }

            return Task.FromResult(result);
        });
        return await new QuantificationOrchestrator(
            rows,
            runner,
            new ScriptedInputSnapshots(U01TestSupport.InputSnapshot())).RunAsync(
                new QuantificationRunRequest
                {
                    DraftDefinition = definition,
                    WorkbookMetadata = metadata,
                    InputPath = Path.Combine(Path.GetTempPath(), "PRIVATE-U04-SUMMARY-CANARY.xlsx"),
                    ModelId = "model-test",
                    MaxConcurrency = 1,
                },
                cancellationToken: cancellation.Token);
    }
}

internal sealed class RecordingAuthenticationBoundary(
    ExecutionAuthenticationSnapshot snapshot) : IExecutionAuthenticationBoundary
{
    public int CallCount { get; private set; }

    public Task<ExecutionAuthenticationSnapshot> CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        return Task.FromResult(snapshot);
    }
}

internal sealed class RecordingRunBoundary(
    Func<QuantificationRunRequest, Action<EvaluationProgress>?, CancellationToken, Task<RunSummary>> handler)
    : IQuantificationRunBoundary
{
    public int CallCount { get; private set; }

    public QuantificationRunRequest? LastRequest { get; private set; }

    public Task<RunSummary> RunAsync(
        QuantificationRunRequest request,
        Action<EvaluationProgress>? progress,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        return handler(request, progress, cancellationToken);
    }
}

internal sealed class ControlledRunBoundary(RunSummary summary) : IQuantificationRunBoundary
{
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => started.Task;

    public Task CancellationObserved => cancellationObserved.Task;

    public int CallCount { get; private set; }

    public void Release() => release.TrySetResult();

    public async Task<RunSummary> RunAsync(
        QuantificationRunRequest request,
        Action<EvaluationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        CallCount++;
        progress?.Invoke(new EvaluationProgress(
            summary.PlannedEvaluationCount,
            0,
            1,
            EvaluationProgressStatus.Running));
        started.TrySetResult();
        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => cancellationObserved.TrySetResult());
        await release.Task;
        progress?.Invoke(new EvaluationProgress(
            summary.PlannedEvaluationCount,
            summary.CompletedEvaluationCount,
            0,
            EvaluationProgressStatus.Cancelled));
        return summary;
    }
}