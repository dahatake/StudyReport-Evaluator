using System.Collections.Immutable;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
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
                [U04TestSupport.Model("model-a"), U04TestSupport.Model("model-b"), U04TestSupport.Model("model-a")],
                identity));
        RecordingRunBoundary runner = new((request, progress, _) =>
        {
            progress?.Invoke(new EvaluationProgress(
                5,
                3,
                1,
                EvaluationProgressStatus.Running,
                DurableEvaluationStage.EvaluatingRows,
                referenceCompleted: 1,
                referenceTotal: 1,
                rowCompleted: 1,
                rowTotal: 2,
                detailStatusCode: "EVALUATING_ROW",
                finalPath: "C:\\PRIVATE\\result\\eval.xlsx",
                partialPath: "C:\\PRIVATE\\result\\eval.partial.xlsx"));
            return Task.FromResult(partial);
        });
        ExecutionViewModel viewModel = new(authentication, runner);
        string inputPath = Path.Combine(Path.GetTempPath(), "PRIVATE-U04-INPUT-CANARY.xlsx");
        viewModel.Configure(definition, metadata, inputPath);

        Assert.Equal(5, viewModel.PlannedEvaluationCount);
        Assert.Equal(15, viewModel.WorstCaseAttemptCount);
        Assert.Contains("最大 15 attempts", viewModel.PlanSummary, StringComparison.Ordinal);
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
        Assert.Equal(64_000, request.MaximumPromptTokens);
        Assert.Equal(128_000, request.MaximumContextWindowTokens);
        Assert.Equal(3, request.MaxConcurrency);
        Assert.True(request.UseDurableWorkflow);
        Assert.Same(identity, request.RuntimeIdentity);
        Assert.EndsWith("result", request.OutputDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Null(request.ResumePartialPath);
        Assert.NotSame(definition, request.DraftDefinition);
        Assert.Equal(partial.DefinitionSha256, viewModel.LastRunContext?.Summary.DefinitionSha256);
        Assert.Same(viewModel.LastRunContext, notifiedContext);
        Assert.Same(identity, viewModel.LastRunContext?.RuntimeIdentity);
        Assert.Equal("model-b", viewModel.LastRunContext?.ModelId);
        Assert.Equal(2, viewModel.ProgressTotal);
        Assert.Equal(1, viewModel.ProgressCompleted);
        Assert.Equal(0, viewModel.ProgressInFlight);
        Assert.Equal(1, viewModel.ReferenceCompleted);
        Assert.Equal(1, viewModel.ReferenceTotal);
        Assert.Equal(1, viewModel.RowCompleted);
        Assert.Equal(2, viewModel.RowTotal);
        Assert.Contains("eval.partial.xlsx", viewModel.OutputIdentityText, StringComparison.Ordinal);
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

    [Fact]
    public async Task New_and_resume_modes_bind_output_directory_or_existing_partial_exclusively()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        RecordingRunBoundary runner = new((_, _, _) => Task.FromResult(summary));
        ExecutionViewModel viewModel = U04TestSupport.ConfiguredExecutionViewModel(
            definition,
            metadata,
            runner);
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsResumeMode);
        Assert.EndsWith("result", viewModel.OutputDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.CanStart);

        viewModel.IsResumeMode = true;
        viewModel.ResumePartialPath = Path.Combine(Path.GetTempPath(), "missing.partial.xlsx");
        Assert.False(viewModel.CanStart);
        Assert.Contains(viewModel.TechnicalErrors, error => error.Code == "RESUME_PARTIAL_REQUIRED");

        string directory = Path.Combine(Path.GetTempPath(), "StudyReportEvaluator-U03-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string partialPath = Path.Combine(directory, "eval.partial.xlsx");
        await File.WriteAllBytesAsync(partialPath, [1], TestContext.Current.CancellationToken);
        try
        {
            viewModel.ResumePartialPath = partialPath;
            Assert.True(viewModel.CanStart);

            await viewModel.StartAsync(TestContext.Current.CancellationToken);

            QuantificationRunRequest request = Assert.IsType<QuantificationRunRequest>(runner.LastRequest);
            Assert.True(request.UseDurableWorkflow);
            Assert.Equal(partialPath, request.ResumePartialPath);
            Assert.Null(request.OutputDirectory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
    public void Workbook_capacity_error_blocks_start_before_authentication_or_run_boundary()
    {
        const int criterionCount = 256;
        QuantificationDefinition single = U04TestSupport.Definition(2, 2);
        ImmutableArray<CriterionDefinition> criteria = Enumerable.Range(1, criterionCount)
            .Select(index => new CriterionDefinition
            {
                Id = $"C{index.ToString(CultureInfo.InvariantCulture)}",
                DisplayName = $"Criterion {index.ToString(CultureInfo.InvariantCulture)}",
                Description = "Synthetic capacity criterion",
                Weight = 1m,
                Enabled = true,
            })
            .ToImmutableArray();
        QuantificationDefinition definition = single with
        {
            Questions =
            [
                single.Questions[0] with
                {
                    Evaluators =
                    [
                        single.Questions[0].Evaluators[0] with { Criteria = criteria },
                    ],
                },
            ],
        };
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RecordingRunBoundary runner = new((_, _, _) => throw new InvalidOperationException("must not run"));
        ExecutionViewModel viewModel = new(
            new RecordingAuthenticationBoundary(
                new ExecutionAuthenticationSnapshot(ExecutionAuthenticationState.AuthRequired)),
            runner);

        viewModel.Configure(
            definition,
            metadata,
            Path.Combine(Path.GetTempPath(), "PRIVATE-CAPACITY-INPUT-CANARY.xlsx"));

        ExecutionTechnicalError error = Assert.Single(
            viewModel.TechnicalErrors,
            item => item.Code == "FUNCTION_ARGUMENT_LIMIT_EXCEEDED");
        Assert.Equal("Evaluator_Score", error.Field);
        Assert.Contains("256", error.Message, StringComparison.Ordinal);
        Assert.Contains("255", error.Message, StringComparison.Ordinal);
        Assert.False(viewModel.CanStart);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task Model_without_sdk_prompt_limit_is_visible_but_cannot_start()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RecordingRunBoundary runner = new((_, _, _) => throw new InvalidOperationException("must not run"));
        ExecutionViewModel viewModel = new(
            new RecordingAuthenticationBoundary(
                new ExecutionAuthenticationSnapshot(
                    ExecutionAuthenticationState.Available,
                    [new CopilotModelAvailability("model-unknown", null, 128_000)],
                    U04TestSupport.RuntimeIdentity())),
            runner);
        viewModel.Configure(
            definition,
            metadata,
            Path.Combine(Path.GetTempPath(), "PRIVATE-MODEL-LIMIT-CANARY.xlsx"));

        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["model-unknown"], viewModel.AvailableModelIds);
        Assert.Equal("model-unknown", viewModel.SelectedModelId);
        Assert.Contains(
            viewModel.TechnicalErrors,
            error => error.Code == "MODEL_PROMPT_LIMIT_UNAVAILABLE");
        Assert.False(viewModel.CanStart);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public void Worst_case_retry_budget_over_twenty_thousand_blocks_start()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 3_334);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RecordingRunBoundary runner = new((_, _, _) => throw new InvalidOperationException("must not run"));
        ExecutionViewModel viewModel = new(
            new RecordingAuthenticationBoundary(
                new ExecutionAuthenticationSnapshot(ExecutionAuthenticationState.AuthRequired)),
            runner);

        viewModel.Configure(
            definition,
            metadata,
            Path.Combine(Path.GetTempPath(), "PRIVATE-ATTEMPT-BUDGET-CANARY.xlsx"));

        Assert.Equal(6_667, viewModel.PlannedEvaluationCount);
        Assert.Equal(20_001, viewModel.WorstCaseAttemptCount);
        ExecutionTechnicalError error = Assert.Single(
            viewModel.TechnicalErrors,
            item => item.Code == "ATTEMPT_BUDGET_TOO_LARGE");
        Assert.Contains("20001", error.Message, StringComparison.Ordinal);
        Assert.Contains("20000", error.Message, StringComparison.Ordinal);
        Assert.False(viewModel.CanStart);
        Assert.Equal(0, runner.CallCount);
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

    [AvaloniaFact]
    public void Login_controls_bind_accessible_commands_without_automatic_activity()
    {
        using LoginViewHarness harness = new();
        ExecutionViewModel viewModel = harness.ViewModel;
        Button check = Required<Button>(harness.View, "CheckAuthenticationButton");
        Button login = Required<Button>(harness.View, "StartCopilotLogin");
        Button cancelLogin = Required<Button>(harness.View, "CancelCopilotLogin");
        Button start = Required<Button>(harness.View, "StartRunButton");
        Button cancelRun = Required<Button>(harness.View, "CancelRunButton");
        StackPanel panel = Required<StackPanel>(harness.View, "CopilotLoginPanel");
        TextBlock instructions = Required<TextBlock>(harness.View, "CopilotLoginInstructions");

        Assert.Same(viewModel.LoginCommand, login.Command);
        Assert.Same(viewModel.CancelLoginCommand, cancelLogin.Command);
        Assert.Equal("StartCopilotLogin", AutomationProperties.GetAutomationId(login));
        Assert.Equal("CancelCopilotLogin", AutomationProperties.GetAutomationId(cancelLogin));
        Assert.Equal("GitHubにログイン", login.Content);
        Assert.Equal("ログインを取り消す", cancelLogin.Content);
        Assert.Equal("GitHubにログイン", AutomationProperties.GetName(login));
        Assert.Equal("ログインを取り消す", AutomationProperties.GetName(cancelLogin));
        Assert.All(new[] { login, cancelLogin }, button =>
        {
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(button)));
            Assert.True(button.Focusable);
            Assert.True(button.MinHeight >= 44d);
            Assert.True(button.IsEffectivelyVisible);
        });
        Assert.Same(viewModel.CheckAuthenticationCommand, check.Command);
        Assert.Same(viewModel.StartCommand, start.Command);
        Assert.Same(viewModel.CancelCommand, cancelRun.Command);
        Assert.Equal("CheckCopilotAuthentication", AutomationProperties.GetAutomationId(check));
        Assert.Equal("StartQuantification", AutomationProperties.GetAutomationId(start));
        Assert.Equal("CancelQuantification", AutomationProperties.GetAutomationId(cancelRun));
        Assert.Equal("Copilot 状態を確認", check.Content);
        Assert.Equal("定量化を開始", start.Content);
        Assert.Equal("cancel", cancelRun.Content);
        Assert.Same(check, harness.Window.FocusManager?.GetFocusedElement());
        Assert.Contains("別のブラウザー", instructions.Text, StringComparison.Ordinal);
        Assert.Contains("パスワードやトークンを取得・保存しません", instructions.Text, StringComparison.Ordinal);
        Assert.Contains("完了後は「Copilot 状態を確認」", instructions.Text, StringComparison.Ordinal);
        Assert.Contains("自動で開始しません", instructions.Text, StringComparison.Ordinal);
        Assert.Empty(panel.GetVisualDescendants().OfType<TextBox>());
        Assert.Null(harness.View.FindControl<Border>("EthicsWarningBanner"));
        AssertLoginStatus(harness);

        string[] ids = harness.View.GetVisualDescendants().OfType<Control>()
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.True(check.IsEffectivelyEnabled);
        Assert.True(login.IsEffectivelyEnabled);
        Assert.False(cancelLogin.IsEffectivelyEnabled);
        Assert.False(start.IsEffectivelyEnabled);
        Assert.False(cancelRun.IsEffectivelyEnabled);
        Assert.True(viewModel.CanLogin);
        Assert.False(viewModel.CanCancelLogin);
        Assert.False(viewModel.IsLoggingIn);
        Assert.Null(viewModel.LastLoginTask);
        Assert.Equal(0, harness.Resolver.CallCount);
        Assert.Equal(0, harness.FactoryCallCount);
        Assert.Equal(0, harness.Process.StartCount);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Login_button_click_or_keyboard_requires_explicit_authentication_recheck(bool useKeyboard)
    {
        using LoginViewHarness harness = new();
        ExecutionViewModel viewModel = harness.ViewModel;
        Button check = Required<Button>(harness.View, "CheckAuthenticationButton");
        Button login = Required<Button>(harness.View, "StartCopilotLogin");
        Button cancel = Required<Button>(harness.View, "CancelCopilotLogin");
        Button start = Required<Button>(harness.View, "StartRunButton");
        Activate(harness.Window, check, useKeyboard);
        Assert.Equal(1, harness.Authentication.CallCount);
        Assert.True(start.IsEffectivelyEnabled);

        int clicks = 0;
        login.Click += (_, _) => clicks++;
        Activate(harness.Window, login, useKeyboard);
        Task task = Assert.IsAssignableFrom<Task>(viewModel.LastLoginTask);

        Assert.Equal(1, clicks);
        Assert.Equal(1, harness.Resolver.CallCount);
        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Equal(1, harness.Process.StartCount);
        Assert.False(task.IsCompleted);
        Assert.True(viewModel.IsLoggingIn);
        Assert.False(viewModel.CanLogin);
        Assert.True(viewModel.CanCancelLogin);
        Assert.False(login.IsEffectivelyEnabled);
        Assert.True(cancel.IsEffectivelyEnabled);
        Assert.False(check.IsEffectivelyEnabled);
        Assert.False(start.IsEffectivelyEnabled);
        Assert.Equal(ExecutionAuthenticationState.NotChecked, viewModel.AuthenticationState);
        Assert.Empty(viewModel.AvailableModelIds);
        Assert.Null(viewModel.SelectedModelId);
        AssertLoginStatus(harness);

        harness.Process.Complete(0);
        await task.WaitAsync(LoginTestWait, TestContext.Current.CancellationToken);
        Render();

        Assert.False(viewModel.IsLoggingIn);
        Assert.True(login.IsEffectivelyEnabled);
        Assert.True(check.IsEffectivelyEnabled);
        Assert.False(cancel.IsEffectivelyEnabled);
        Assert.False(start.IsEffectivelyEnabled);
        Assert.Equal(ExecutionAuthenticationState.NotChecked, viewModel.AuthenticationState);
        Assert.False(viewModel.IsAuthenticationAvailable);
        Assert.Empty(viewModel.AvailableModelIds);
        Assert.Null(viewModel.SelectedModelId);
        Assert.Contains("認証状態は未確認", viewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.Contains("Copilot 状態を確認", viewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.Equal(1, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
        Assert.Null(viewModel.LastRunContext);
        AssertLoginStatus(harness);

        Activate(harness.Window, check, useKeyboard);

        Assert.Equal(2, harness.Authentication.CallCount);
        Assert.Equal(ExecutionAuthenticationState.Available, viewModel.AuthenticationState);
        Assert.Equal("model-test", viewModel.SelectedModelId);
        Assert.True(start.IsEffectivelyEnabled);
        Assert.Equal(0, harness.Runner.CallCount);
        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Same(task, viewModel.LastLoginTask);
        Assert.Empty(harness.Process.KillTreeArguments);
        Assert.Equal(1, harness.Process.DisposeCount);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_login_button_click_or_keyboard_stops_once_and_retry_is_explicit(bool useKeyboard)
    {
        using LoginViewHarness harness = new();
        Button login = Required<Button>(harness.View, "StartCopilotLogin");
        Button cancel = Required<Button>(harness.View, "CancelCopilotLogin");
        Button check = Required<Button>(harness.View, "CheckAuthenticationButton");
        Button start = Required<Button>(harness.View, "StartRunButton");
        Activate(harness.Window, login, useKeyboard);
        Task first = Assert.IsAssignableFrom<Task>(harness.ViewModel.LastLoginTask);
        LoginViewProcess cancelled = harness.Process;
        Assert.True(cancel.IsEffectivelyEnabled);

        Activate(harness.Window, cancel, useKeyboard, Key.Space);
        await first.WaitAsync(LoginTestWait, TestContext.Current.CancellationToken);
        Render();

        Assert.True(cancelled.WaitToken.IsCancellationRequested);
        Assert.Equal([false], cancelled.KillTreeArguments);
        Assert.Equal(1, cancelled.DisposeCount);
        Assert.False(harness.ViewModel.IsLoggingIn);
        Assert.True(login.IsEffectivelyEnabled);
        Assert.True(check.IsEffectivelyEnabled);
        Assert.False(cancel.IsEffectivelyEnabled);
        Assert.False(start.IsEffectivelyEnabled);
        Assert.Contains("取り消しました", harness.ViewModel.LoginStatusText, StringComparison.Ordinal);
        AssertLoginStatus(harness);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Same(first, harness.ViewModel.LastLoginTask);

        harness.Process = new LoginViewProcess();
        Render();
        Assert.Equal(0, harness.Process.StartCount);
        Assert.Equal(1, harness.FactoryCallCount);
        Activate(harness.Window, login, useKeyboard);
        Task retry = Assert.IsAssignableFrom<Task>(harness.ViewModel.LastLoginTask);
        Assert.NotSame(first, retry);
        Assert.Equal(2, harness.FactoryCallCount);
        Assert.Equal(1, harness.Process.StartCount);
        harness.Process.Complete(0);
        await retry.WaitAsync(LoginTestWait, TestContext.Current.CancellationToken);
        Render();

        AssertLoginStatus(harness);
        Assert.False(start.IsEffectivelyEnabled);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
        Assert.Equal([false], cancelled.KillTreeArguments);
        Assert.Empty(harness.Process.KillTreeArguments);
    }

    [AvaloniaFact]
    public async Task Login_status_callout_never_displays_credentials_paths_or_exception_details()
    {
        using LoginViewHarness harness = new();
        harness.Process.StartFailureMessage =
            $"{LoginTokenCanary} device-code-CANARY {harness.Resolver.CliPath} {harness.ViewModel.OutputDirectory}";
        Button login = Required<Button>(harness.View, "StartCopilotLogin");

        Activate(harness.Window, login, useKeyboard: false);
        Task task = Assert.IsAssignableFrom<Task>(harness.ViewModel.LastLoginTask);
        await task.WaitAsync(LoginTestWait, TestContext.Current.CancellationToken);
        Render();

        AssertLoginStatus(harness);
        Assert.Contains("失敗", harness.ViewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.Contains("再試行", harness.ViewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.True(login.IsEffectivelyEnabled);
        Assert.False(Required<Button>(harness.View, "CancelCopilotLogin").IsEffectivelyEnabled);
        Assert.False(Required<Button>(harness.View, "StartRunButton").IsEffectivelyEnabled);
        Assert.True(harness.ViewModel.IsConfigured);
        Assert.Equal(ExecutionAuthenticationState.NotChecked, harness.ViewModel.AuthenticationState);
        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Equal(1, harness.Process.DisposeCount);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
    }

    [AvaloniaFact]
    public async Task Login_controls_reflow_and_follow_tab_order_at_two_hundred_percent()
    {
        using LoginViewHarness harness = new();
        harness.Window.Width = 760;
        harness.Window.Height = 600;
        harness.Window.SetRenderScaling(2d);
        Render();
        StackPanel panel = Required<StackPanel>(harness.View, "CopilotLoginPanel");
        WrapPanel actions = Required<WrapPanel>(harness.View, "CopilotLoginActions");
        ScrollViewer scroll = Required<ScrollViewer>(harness.View, "ExecutionScrollViewer");
        Button check = Required<Button>(harness.View, "CheckAuthenticationButton");
        Button login = Required<Button>(harness.View, "StartCopilotLogin");
        Button cancel = Required<Button>(harness.View, "CancelCopilotLogin");
        ComboBox model = Required<ComboBox>(harness.View, "ModelComboBox");
        TextBlock instructions = Required<TextBlock>(harness.View, "CopilotLoginInstructions");
        TextBlock status = Required<TextBlock>(harness.View, "CopilotLoginStatus");

        Assert.Equal(2d, harness.Window.RenderScaling);
        Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1d);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        Assert.Equal(new Control[] { check, login, cancel }, actions.Children);
        Assert.Equal([0, 1, 2, 3], new[] { check.TabIndex, login.TabIndex, cancel.TabIndex, model.TabIndex });
        Assert.True(cancel.Bounds.Y > check.Bounds.Y, "The narrow viewport must wrap the login actions.");
        Assert.Equal(TextWrapping.Wrap, instructions.TextWrapping);
        foreach (Control control in new Control[] { actions, check, login, cancel, instructions, status })
        {
            AssertFitsLoginPanel(control, panel);
        }

        Assert.True(check.Focus(NavigationMethod.Tab, KeyModifiers.None));
        Press(harness.Window, Key.Tab);
        Assert.Same(login, harness.Window.FocusManager?.GetFocusedElement());
        Press(harness.Window, Key.Tab);
        Assert.Same(model, harness.Window.FocusManager?.GetFocusedElement());
        Press(harness.Window, Key.Tab, RawInputModifiers.Shift);
        Assert.Same(login, harness.Window.FocusManager?.GetFocusedElement());
        Assert.Equal(0, harness.FactoryCallCount);

        Activate(harness.Window, login, useKeyboard: true);
        Task task = Assert.IsAssignableFrom<Task>(harness.ViewModel.LastLoginTask);
        Assert.True(model.Focus(NavigationMethod.Tab, KeyModifiers.None));
        Press(harness.Window, Key.Tab, RawInputModifiers.Shift);
        Assert.Same(cancel, harness.Window.FocusManager?.GetFocusedElement());
        Press(harness.Window, Key.Space);
        await task.WaitAsync(LoginTestWait, TestContext.Current.CancellationToken);
        Render();

        AssertLoginStatus(harness);
        AssertFitsLoginPanel(status, panel);
        status.BringIntoView();
        Render();
        Point origin = status.TranslatePoint(default, scroll)
            ?? throw new InvalidOperationException("The login status must be attached to its scroll viewer.");
        Assert.True(origin.Y >= -1d && origin.Y + status.Bounds.Height <= scroll.Viewport.Height + 1d);
        Assert.False(cancel.IsEffectivelyEnabled);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
    }

    private const string LoginTokenCanary = "PRIVATE-A03-TOKEN-CANARY";
    private static readonly TimeSpan LoginTestWait = TimeSpan.FromSeconds(10);

    private static T Required<T>(Control root, string name)
        where T : Control => Assert.IsType<T>(root.FindControl<T>(name));

    private static void AssertLoginStatus(LoginViewHarness harness)
    {
        TextBlock status = Required<TextBlock>(harness.View, "CopilotLoginStatus");
        Assert.Equal("CopilotLoginStatus", AutomationProperties.GetAutomationId(status));
        Assert.Equal(harness.ViewModel.LoginStatusText, status.Text);
        Assert.Equal(status.Text, AutomationProperties.GetName(status));
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(status)));
        Assert.Equal(TextWrapping.Wrap, status.TextWrapping);
        Assert.True(status.IsEffectivelyVisible);
        Assert.False(status.Focusable);
        string visible = string.Join("\n", status.Text,
            AutomationProperties.GetName(status), AutomationProperties.GetHelpText(status));
        Assert.DoesNotContain(LoginTokenCanary, visible, StringComparison.Ordinal);
        Assert.DoesNotContain("device-code-CANARY", visible, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Resolver.CliPath, visible, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(harness.ViewModel.OutputDirectory, visible, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(InvalidOperationException), visible, StringComparison.Ordinal);
    }

    private static void AssertFitsLoginPanel(Control control, StackPanel panel)
    {
        Point origin = control.TranslatePoint(default, panel)
            ?? throw new InvalidOperationException("The login control must be attached to its panel.");
        Assert.True(control.Bounds.Width > 0d);
        Assert.True(origin.X >= -1d && origin.X + control.Bounds.Width <= panel.Bounds.Width + 1d,
            $"{control.Name} must fit within the login panel without horizontal clipping.");
    }

    private static void Activate(Window window, Button button, bool useKeyboard, Key key = Key.Enter)
    {
        Assert.True(button.IsEffectivelyEnabled);
        button.BringIntoView();
        Render();
        if (useKeyboard)
        {
            Assert.True(button.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Press(window, key);
            return;
        }

        Point point = button.TranslatePoint(new Point(button.Bounds.Width / 2d, button.Bounds.Height / 2d), window)
            ?? throw new InvalidOperationException("The clicked button must be attached to its window.");
        Assert.InRange(point.X, 0d, window.ClientSize.Width);
        Assert.InRange(point.Y, 0d, window.ClientSize.Height);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Render();
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        PhysicalKey physicalKey = key switch
        {
            Key.Tab => PhysicalKey.Tab,
            Key.Enter => PhysicalKey.Enter,
            Key.Space => PhysicalKey.Space,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported test key."),
        };
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
        Render();
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    // Reuse U04's configuration/auth/run fakes; A01/A02's login fakes are private to their test classes.
    private sealed class LoginViewHarness : IDisposable
    {
        public LoginViewHarness()
        {
            BundledCopilotLoginService service = new(Resolver, _ =>
            {
                FactoryCallCount++;
                return Process;
            });
            ViewModel = new ExecutionViewModel(Authentication, Runner, service);
            QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
            ViewModel.Configure(definition, U01TestSupport.ValidateMapping(definition).Metadata,
                Path.Combine(Path.GetTempPath(), "PRIVATE-A03-INPUT-CANARY.xlsx"));
            View = new ExecutionView(ViewModel);
            Window = new Window { Width = 1080, Height = 760, Content = View };
            Window.Show();
            Render();
        }

        public RecordingAuthenticationBoundary Authentication { get; } = new(
            new ExecutionAuthenticationSnapshot(ExecutionAuthenticationState.Available,
                [U04TestSupport.Model("model-test")], U04TestSupport.RuntimeIdentity()));
        public RecordingRunBoundary Runner { get; } = new((_, _, _) => throw new InvalidOperationException("must not run"));
        public LoginViewResolver Resolver { get; } = new();
        public LoginViewProcess Process { get; set; } = new();
        public int FactoryCallCount { get; private set; }
        public ExecutionViewModel ViewModel { get; }
        public ExecutionView View { get; }
        public Window Window { get; }

        public void Dispose()
        {
            ViewModel.Dispose();
            Window.Close();
        }
    }

    private sealed class LoginViewResolver : ICopilotCliPathResolver
    {
        public string CliPath { get; } = Path.Combine(Path.GetTempPath(), "PRIVATE-A03-CLI-CANARY", "copilot.exe");
        public int CallCount { get; private set; }

        public ValueTask<string?> ResolveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult<string?>(CliPath);
        }
    }

    private sealed class LoginViewProcess : ICopilotLoginProcess
    {
        private readonly TaskCompletionSource exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? StartFailureMessage { get; set; }
        public int StartCount { get; private set; }
        public int DisposeCount { get; private set; }
        public bool HasExited => exit.Task.IsCompletedSuccessfully;
        public int ExitCode { get; private set; }
        public CancellationToken WaitToken { get; private set; }
        public List<bool> KillTreeArguments { get; } = [];

        public bool Start()
        {
            StartCount++;
            if (StartFailureMessage is not null)
            {
                throw new InvalidOperationException(StartFailureMessage);
            }

            return true;
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitToken = cancellationToken;
            // Deliberately ignore cancellation: the service must still clean up its owned fake.
            return exit.Task;
        }

        public void Kill(bool entireProcessTree)
        {
            KillTreeArguments.Add(entireProcessTree);
            Complete(-1);
        }

        public bool WaitForExit(int milliseconds) => HasExited;

        public void Complete(int code)
        {
            ExitCode = code;
            exit.TrySetResult();
        }

        public void Dispose() => DisposeCount++;
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

    internal static CopilotModelAvailability Model(string id) =>
        new(id, maximumPromptTokens: 64_000, maximumContextWindowTokens: 128_000);

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
                    [Model("model-test")],
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
                    MaximumPromptTokens = 64_000,
                    MaximumContextWindowTokens = 128_000,
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