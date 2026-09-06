using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Reflection;
using System.Windows.Input;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class CopilotLoginCommandTests
{
    private const string SensitiveFailure =
        "PRIVATE-LOGIN-TOKEN-CANARY device-code C:\\private\\copilot.exe";
    private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(10);

    [Fact]
    public void Default_and_existing_two_argument_constructors_leave_login_unstarted()
    {
        RecordingAuthenticationBoundary authentication = new(AvailableSnapshot());
        RecordingRunBoundary runner = new((_, _, _) => throw new InvalidOperationException("must not run"));
        using ExecutionViewModel defaultViewModel = new();
        using ExecutionViewModel legacyViewModel = new(authentication, runner);
        using ExecutionViewModel nullServiceViewModel = new(authentication, runner, loginService: null);
        FieldInfo serviceField = typeof(ExecutionViewModel).GetField(
            "loginService", BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo ownedProcess = typeof(BundledCopilotLoginService).GetField(
            "_ownedProcess", BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo activeCancellation = typeof(BundledCopilotLoginService).GetField(
            "_activeCancellation", BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (ExecutionViewModel viewModel in new[] { defaultViewModel, legacyViewModel, nullServiceViewModel })
        {
            BundledCopilotLoginService service = Assert.IsType<BundledCopilotLoginService>(serviceField.GetValue(viewModel));
            Assert.Null(ownedProcess.GetValue(service));
            Assert.Null(activeCancellation.GetValue(service));
            Assert.Null(viewModel.LastLoginTask);
            Assert.False(viewModel.IsLoggingIn);
            Assert.True(viewModel.CanLogin);
            Assert.True(viewModel.LoginCommand.CanExecute(null));
            Assert.False(viewModel.CanCancelLogin);
            Assert.False(viewModel.CancelLoginCommand.CanExecute(null));
            Assert.Equal(ExecutionAuthenticationState.NotChecked, viewModel.AuthenticationState);
            Assert.Contains("開始していません", viewModel.LoginStatusText, StringComparison.Ordinal);
        }

        Assert.Equal(0, authentication.CallCount);
        Assert.Equal(0, runner.CallCount);
        ParameterInfo lastParameter = typeof(ExecutionViewModel).GetConstructor(
            [typeof(IExecutionAuthenticationBoundary), typeof(IQuantificationRunBoundary), typeof(BundledCopilotLoginService)])!
            .GetParameters()[^1];
        Assert.True(lastParameter.IsOptional);
        Assert.Null(lastParameter.DefaultValue);
    }

    [Fact]
    public void Configuration_and_shell_navigation_do_not_start_login_authentication_or_evaluation()
    {
        using LoginHarness harness = new();
        ExecutionViewModel viewModel = harness.ViewModel;
        using MainWindowViewModel shell = new(
            new WorkflowNavigator(),
            new InputViewModel(),
            new QuantificationDesignViewModel(),
            viewModel,
            new ResultsOutputViewModel());

        viewModel.MaxConcurrency = 2;
        shell.NextCommand.Execute(null);
        shell.NextCommand.Execute(null);
        Assert.Equal(WorkflowStep.Execution, shell.CurrentStep);
        shell.PreviousCommand.Execute(null);
        shell.NextCommand.Execute(null);

        Assert.Null(viewModel.LastLoginTask);
        Assert.False(viewModel.IsLoggingIn);
        Assert.True(viewModel.CanLogin);
        Assert.Equal(0, harness.Resolver.CallCount);
        Assert.Equal(0, harness.FactoryCallCount);
        Assert.Equal(0, harness.Process.StartCount);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
    }

    [Fact]
    public async Task Login_invalidates_old_identity_and_exit_zero_requires_explicit_authentication_check()
    {
        using LoginHarness harness = new();
        ExecutionViewModel viewModel = harness.ViewModel;
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.CanStart);
        Assert.Equal("model-before-login", viewModel.SelectedModelId);
        string previousIdentity = viewModel.RuntimeIdentityText;
        harness.Authentication.Snapshot = AvailableSnapshot("model-after-login");

        viewModel.LoginCommand.Execute(null);
        Task login = LoginTask(viewModel);
        await harness.Process.Waiting.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsLoggingIn);
        Assert.False(viewModel.CanLogin);
        Assert.True(viewModel.CanCancelLogin);
        Assert.False(viewModel.CanCheckAuthentication);
        Assert.False(viewModel.CanStart);
        Assert.Equal(ExecutionAuthenticationState.NotChecked, viewModel.AuthenticationState);
        Assert.False(viewModel.IsAuthenticationAvailable);
        Assert.Empty(viewModel.AvailableModelIds);
        Assert.Null(viewModel.SelectedModelId);
        Assert.NotEqual(previousIdentity, viewModel.RuntimeIdentityText);
        Assert.Contains(viewModel.TechnicalErrors, error => error.Code == "AUTH_CHECK_REQUIRED");

        viewModel.CheckAuthenticationCommand.Execute(null);
        viewModel.StartCommand.Execute(null);
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        await viewModel.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);

        harness.Process.Complete(0);
        await login.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsLoggingIn);
        Assert.True(viewModel.CanLogin);
        Assert.True(viewModel.CanCheckAuthentication);
        Assert.False(viewModel.CanCancelLogin);
        Assert.False(viewModel.CanStart);
        Assert.False(viewModel.IsAuthenticationAvailable);
        Assert.Empty(viewModel.AvailableModelIds);
        Assert.Null(viewModel.SelectedModelId);
        Assert.Contains("認証状態は未確認", viewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.Contains("Copilot 状態を確認", viewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.Equal(1, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
        Assert.Null(viewModel.LastRunContext);

        // This boundary completes synchronously: only this explicit command restores availability.
        viewModel.CheckAuthenticationCommand.Execute(null);

        Assert.Equal(2, harness.Authentication.CallCount);
        Assert.Equal("model-after-login", viewModel.SelectedModelId);
        Assert.True(viewModel.CanStart);
        Assert.Equal(0, harness.Runner.CallCount);
        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Same(login, viewModel.LastLoginTask);
        AssertSafe(viewModel);
    }

    [Fact]
    public async Task Repeated_commands_and_direct_calls_are_ignored_during_resolution_and_login()
    {
        using LoginHarness harness = new();
        TaskCompletionSource<string?> resolution = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Resolver.ResolveOverride = _ => new(resolution.Task);
        ExecutionViewModel viewModel = harness.ViewModel;

        viewModel.LoginCommand.Execute(null);
        Task first = LoginTask(viewModel);
        await harness.Resolver.Called.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        viewModel.LoginCommand.Execute(null);
        await viewModel.LoginAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, viewModel.LastLoginTask);
        Assert.Equal(1, harness.Resolver.CallCount);
        Assert.Equal(0, harness.FactoryCallCount);
        Assert.False(first.IsCompleted);

        resolution.SetResult(CliPath());
        await harness.Process.Waiting.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        viewModel.LoginCommand.Execute(null);
        await viewModel.LoginAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, viewModel.LastLoginTask);
        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Equal(1, harness.Process.StartCount);
        harness.Process.Complete(0);
        await first.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
    }

    [Fact]
    public async Task Cancel_command_stops_only_owned_login_once_and_retry_remains_explicit()
    {
        FakeLoginProcess unrelated = new();
        using LoginHarness harness = new();
        ExecutionViewModel viewModel = harness.ViewModel;
        viewModel.LoginCommand.Execute(null);
        Task first = LoginTask(viewModel);
        FakeLoginProcess cancelled = harness.Process;
        await cancelled.Waiting.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        viewModel.CancelLoginCommand.Execute(null);
        viewModel.CancelLoginCommand.Execute(null);
        viewModel.CancelLogin();
        Assert.False(viewModel.CanCancelLogin);
        await first.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.True(cancelled.WaitToken.IsCancellationRequested);
        Assert.Equal([false], cancelled.KillTreeArguments);
        Assert.Equal([5_000], cancelled.CleanupWaits);
        Assert.Equal(1, cancelled.DisposeCount);
        Assert.Empty(unrelated.KillTreeArguments);
        Assert.Equal(0, unrelated.DisposeCount);
        Assert.False(viewModel.IsLoggingIn);
        Assert.True(viewModel.CanLogin);
        Assert.True(viewModel.CanCheckAuthentication);
        Assert.False(viewModel.CanStart);
        Assert.Contains("取り消しました", viewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.Contains("再試行", viewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);

        harness.Process = new FakeLoginProcess { ExitOnStart = 0 };
        viewModel.LoginCommand.Execute(null);
        await LoginTask(viewModel).WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.Equal(2, harness.FactoryCallCount);
        Assert.Equal(1, harness.Process.StartCount);
        Assert.Empty(harness.Process.KillTreeArguments);
        Assert.False(viewModel.IsAuthenticationAvailable);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
    }

    [Fact]
    public async Task Caller_cancellation_during_resolution_prevents_late_start_and_allows_retry()
    {
        using LoginHarness harness = new();
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource<string?> resolution = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Resolver.ResolveOverride = _ => new(resolution.Task);
        Task login = harness.ViewModel.LoginAsync(cancellation.Token);
        Assert.Same(login, harness.ViewModel.LastLoginTask);
        await harness.Resolver.Called.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        cancellation.Cancel();
        await login.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        resolution.SetResult(CliPath());

        Assert.Equal(0, harness.FactoryCallCount);
        Assert.True(harness.ViewModel.CanLogin);
        Assert.False(harness.ViewModel.IsLoggingIn);
        Assert.Contains("取り消しました", harness.ViewModel.LoginStatusText, StringComparison.Ordinal);
        harness.Resolver.ResolveOverride = null;
        harness.Process = new FakeLoginProcess { ExitOnStart = 0 };
        harness.ViewModel.LoginCommand.Execute(null);
        await LoginTask(harness.ViewModel).WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Equal(2, harness.Resolver.CallCount);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
    }

    [Fact]
    public async Task Pre_cancelled_login_never_resolves_or_starts_a_process()
    {
        using LoginHarness harness = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Task login = harness.ViewModel.LoginAsync(cancellation.Token);
        await login;

        Assert.Same(login, harness.ViewModel.LastLoginTask);
        Assert.Equal(0, harness.Resolver.CallCount);
        Assert.Equal(0, harness.FactoryCallCount);
        Assert.False(harness.ViewModel.IsLoggingIn);
        Assert.True(harness.ViewModel.CanLogin);
        Assert.True(harness.ViewModel.CanCheckAuthentication);
        Assert.False(harness.ViewModel.CanCancelLogin);
        Assert.False(harness.ViewModel.CanStart);
        Assert.Contains("取り消しました", harness.ViewModel.LoginStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelling_an_old_caller_token_does_not_cancel_the_next_login()
    {
        using LoginHarness harness = new(new FakeLoginProcess { ExitOnStart = 0 });
        using CancellationTokenSource oldCancellation = new();
        await harness.ViewModel.LoginAsync(oldCancellation.Token);
        FakeLoginProcess next = new();
        harness.Process = next;
        harness.ViewModel.LoginCommand.Execute(null);
        Task login = LoginTask(harness.ViewModel);
        await next.Waiting.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        oldCancellation.Cancel();

        Assert.False(next.WaitToken.IsCancellationRequested);
        Assert.False(login.IsCompleted);
        Assert.True(harness.ViewModel.IsLoggingIn);
        Assert.Empty(next.KillTreeArguments);
        next.Complete(0);
        await login.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Assert.Equal(2, harness.FactoryCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authentication_check_excludes_login_until_completion_or_cancellation(bool cancelCheck)
    {
        using LoginHarness harness = new(new FakeLoginProcess { ExitOnStart = 0 });
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource<ExecutionAuthenticationSnapshot> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Authentication.CheckOverride = token => completion.Task.WaitAsync(token);
        ExecutionViewModel viewModel = harness.ViewModel;
        Task checking = viewModel.CheckAuthenticationAsync(cancellation.Token);

        Assert.True(viewModel.IsCheckingAuthentication);
        Assert.False(viewModel.CanLogin);
        Assert.False(viewModel.LoginCommand.CanExecute(null));
        viewModel.LoginCommand.Execute(null);
        await viewModel.LoginAsync(TestContext.Current.CancellationToken);
        Assert.Null(viewModel.LastLoginTask);
        Assert.Equal(0, harness.Resolver.CallCount);
        Assert.Equal(0, harness.FactoryCallCount);

        if (cancelCheck)
        {
            cancellation.Cancel();
        }
        else
        {
            completion.SetResult(new ExecutionAuthenticationSnapshot(ExecutionAuthenticationState.AuthRequired));
        }

        await checking.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Assert.False(viewModel.IsCheckingAuthentication);
        Assert.True(viewModel.CanLogin);
        Assert.Equal(0, harness.FactoryCallCount);
        viewModel.LoginCommand.Execute(null);
        await LoginTask(viewModel).WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Equal(1, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
    }

    [Fact]
    public async Task Run_and_run_cancellation_exclude_login_and_login_preserves_completed_run_state()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 3);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata, cancelAfterFirst: true);
        ControlledRunBoundary runner = new(summary);
        using LoginHarness harness = new(new FakeLoginProcess { ExitOnStart = 0 }, runner);
        ExecutionViewModel viewModel = harness.ViewModel;
        viewModel.Configure(definition, metadata, InputPath());
        int completions = 0;
        viewModel.RunCompleted += (_, _) => completions++;
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        Task run = viewModel.StartAsync(TestContext.Current.CancellationToken);
        await runner.Started.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.CanLogin);
        viewModel.LoginCommand.Execute(null);
        await viewModel.LoginAsync(TestContext.Current.CancellationToken);
        viewModel.CancelCommand.Execute(null);
        await runner.CancellationObserved.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsCancelling);
        Assert.False(viewModel.CanLogin);
        viewModel.LoginCommand.Execute(null);
        await viewModel.LoginAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, harness.FactoryCallCount);
        Assert.Null(viewModel.LastLoginTask);

        runner.Release();
        await run.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Assert.True(viewModel.CanLogin);
        Assert.False(viewModel.IsCancelling);
        Assert.Same(summary, viewModel.LastRunContext?.Summary);
        ExecutionRunContext? context = viewModel.LastRunContext;
        string runStatus = viewModel.RunStatusText;
        string progress = viewModel.ProgressText;
        string stage = viewModel.StageText;
        string durableProgress = viewModel.DurableProgressText;
        string outputIdentity = viewModel.OutputIdentityText;
        string outputDirectory = viewModel.OutputDirectory;
        viewModel.IsResumeMode = true;
        viewModel.ResumePartialPath = Path.Combine(Path.GetTempPath(), "a02-preserved.partial.xlsx");
        string resumePath = viewModel.ResumePartialPath;
        string outputMode = viewModel.OutputModeText;

        viewModel.LoginCommand.Execute(null);
        await LoginTask(viewModel).WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.Same(context, viewModel.LastRunContext);
        Assert.Equal(runStatus, viewModel.RunStatusText);
        Assert.Equal(progress, viewModel.ProgressText);
        Assert.Equal(stage, viewModel.StageText);
        Assert.Equal(durableProgress, viewModel.DurableProgressText);
        Assert.Equal(outputIdentity, viewModel.OutputIdentityText);
        Assert.Equal(outputDirectory, viewModel.OutputDirectory);
        Assert.True(viewModel.IsResumeMode);
        Assert.Equal(resumePath, viewModel.ResumePartialPath);
        Assert.Equal(outputMode, viewModel.OutputModeText);
        Assert.Equal(1, completions);
        Assert.Equal(1, runner.CallCount);
        Assert.Equal(1, harness.Authentication.CallCount);
        Assert.False(viewModel.CanStart);
    }

    [Fact]
    public async Task Login_does_not_clear_existing_run_failure_or_rewrite_its_safe_message()
    {
        using LoginHarness harness = new(new FakeLoginProcess { ExitOnStart = 0 });
        ExecutionViewModel viewModel = harness.ViewModel;
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        await viewModel.StartAsync(TestContext.Current.CancellationToken);
        ExecutionTechnicalError previous = Assert.Single(viewModel.TechnicalErrors, error => error.Code == "RUN_FAILED");
        string status = viewModel.RunStatusText;
        // Model-list bindings may clear the selected value during a collection reset.
        ((INotifyCollectionChanged)viewModel.AvailableModelIds).CollectionChanged +=
            (_, _) => viewModel.SelectedModelId = null;

        await viewModel.LoginAsync(TestContext.Current.CancellationToken);

        ExecutionTechnicalError retained = Assert.Single(viewModel.TechnicalErrors, error => error.Code == "RUN_FAILED");
        Assert.Equal(previous.Message, retained.Message);
        Assert.Equal(status, viewModel.RunStatusText);
        Assert.Equal(1, harness.Runner.CallCount);
        Assert.Equal(1, harness.Authentication.CallCount);
        Assert.False(viewModel.CanStart);
        AssertSafe(viewModel);
    }

    [Theory]
    [InlineData(FailurePoint.MissingCli)]
    [InlineData(FailurePoint.Resolver)]
    [InlineData(FailurePoint.Factory)]
    [InlineData(FailurePoint.StartFalse)]
    [InlineData(FailurePoint.Start)]
    [InlineData(FailurePoint.NonzeroExit)]
    public async Task Login_failures_are_safe_leave_gui_configured_and_allow_explicit_retry(FailurePoint failure)
    {
        using LoginHarness harness = new(new FakeLoginProcess
        {
            Failure = failure,
            ExitOnStart = failure == FailurePoint.NonzeroExit ? 1 : 0,
        });
        harness.Resolver.Path = failure == FailurePoint.MissingCli ? null : CliPath();
        harness.Resolver.ThrowOnResolve = failure == FailurePoint.Resolver;
        harness.ThrowOnFactory = failure == FailurePoint.Factory;
        ExecutionViewModel viewModel = harness.ViewModel;
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        string plan = viewModel.PlanSummary;

        viewModel.LoginCommand.Execute(null);
        await LoginTask(viewModel).WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsConfigured);
        Assert.Equal(plan, viewModel.PlanSummary);
        Assert.False(viewModel.IsLoggingIn);
        Assert.True(viewModel.CanLogin);
        Assert.True(viewModel.CanCheckAuthentication);
        Assert.False(viewModel.CanStart);
        Assert.False(viewModel.CanCancelLogin);
        Assert.Equal(ExecutionAuthenticationState.NotChecked, viewModel.AuthenticationState);
        Assert.Empty(viewModel.AvailableModelIds);
        Assert.Null(viewModel.SelectedModelId);
        Assert.Equal(1, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
        AssertSafe(viewModel);
        if (failure is FailurePoint.MissingCli or FailurePoint.Resolver)
        {
            Assert.Contains("同梱", viewModel.LoginStatusText, StringComparison.Ordinal);
            Assert.Contains("再取得", viewModel.LoginStatusText, StringComparison.Ordinal);
            Assert.Contains("再展開", viewModel.LoginStatusText, StringComparison.Ordinal);
            Assert.Equal(0, harness.FactoryCallCount);
        }
        else
        {
            Assert.Contains("失敗", viewModel.LoginStatusText, StringComparison.Ordinal);
            Assert.Contains("再試行", viewModel.LoginStatusText, StringComparison.Ordinal);
        }

        harness.Resolver.Path = CliPath();
        harness.Resolver.ThrowOnResolve = false;
        harness.ThrowOnFactory = false;
        harness.Process = new FakeLoginProcess { ExitOnStart = 0 };
        viewModel.LoginCommand.Execute(null);
        await LoginTask(viewModel).WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Process.StartCount);
        Assert.Contains("認証状態は未確認", viewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.False(viewModel.IsAuthenticationAvailable);
        Assert.Equal(1, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
    }

    [Theory]
    [InlineData(FailurePoint.Wait)]
    [InlineData(FailurePoint.PrematureWait)]
    [InlineData(FailurePoint.UnconfirmedExit)]
    public async Task Unconfirmed_exit_and_already_running_block_auth_and_run_without_starting_another_process(FailurePoint failure)
    {
        FakeLoginProcess first = new() { Failure = failure };
        using LoginHarness harness = new(first);
        ExecutionViewModel viewModel = harness.ViewModel;
        viewModel.LoginCommand.Execute(null);
        if (failure == FailurePoint.UnconfirmedExit)
        {
            await first.Waiting.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);
            viewModel.CancelLoginCommand.Execute(null);
        }

        await LoginTask(viewModel).WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Assert.False(viewModel.IsLoggingIn);
        Assert.True(viewModel.CanLogin);
        Assert.False(viewModel.CanCheckAuthentication);
        Assert.False(viewModel.CanStart);
        Assert.Contains("終了を確認できません", viewModel.LoginStatusText, StringComparison.Ordinal);
        AssertSafe(viewModel);
        FakeLoginProcess retry = new() { ExitOnStart = 0 };
        harness.Process = retry;

        viewModel.LoginCommand.Execute(null);
        await LoginTask(viewModel).WaitAsync(TestWait, TestContext.Current.CancellationToken);
        viewModel.CheckAuthenticationCommand.Execute(null);
        viewModel.StartCommand.Execute(null);
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        await viewModel.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Equal(0, retry.StartCount);
        Assert.Equal(0, first.DisposeCount);
        Assert.Equal(failure == FailurePoint.UnconfirmedExit ? new[] { false } : [], first.KillTreeArguments);
        Assert.False(viewModel.CanCheckAuthentication);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);

        first.Complete(-1);
        viewModel.LoginCommand.Execute(null);
        await LoginTask(viewModel).WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Assert.Equal(2, harness.FactoryCallCount);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, retry.StartCount);
        Assert.True(viewModel.CanCheckAuthentication);
        Assert.False(viewModel.CanStart);
    }

    [Fact]
    public async Task Pre_cancelled_retry_cannot_unlock_authentication_while_a_previous_process_is_retained()
    {
        FakeLoginProcess retained = new() { Failure = FailurePoint.Wait };
        using LoginHarness harness = new(retained);
        await harness.ViewModel.LoginAsync(TestContext.Current.CancellationToken);
        Assert.False(harness.ViewModel.CanCheckAuthentication);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await harness.ViewModel.LoginAsync(cancellation.Token);
        harness.ViewModel.CheckAuthenticationCommand.Execute(null);
        await harness.ViewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        Assert.False(harness.ViewModel.IsLoggingIn);
        Assert.True(harness.ViewModel.CanLogin);
        Assert.False(harness.ViewModel.CanCheckAuthentication);
        Assert.False(harness.ViewModel.CanStart);
        Assert.Equal(1, harness.FactoryCallCount);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
        Assert.Contains("終了を確認できません", harness.ViewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.Empty(retained.KillTreeArguments);
        retained.Complete(-1);
        harness.Process = new FakeLoginProcess { ExitOnStart = 0 };
        await harness.ViewModel.LoginAsync(TestContext.Current.CancellationToken);
        Assert.True(harness.ViewModel.CanCheckAuthentication);
        Assert.False(harness.ViewModel.CanStart);
    }

    [Fact]
    public async Task An_already_disposed_service_never_claims_success_or_offers_an_unusable_login_retry()
    {
        using LoginHarness harness = new();
        harness.Service.Dispose();

        await harness.ViewModel.LoginAsync(TestContext.Current.CancellationToken);

        Assert.False(harness.ViewModel.IsLoggingIn);
        Assert.False(harness.ViewModel.CanLogin);
        Assert.False(harness.ViewModel.CanCancelLogin);
        Assert.False(harness.ViewModel.CanCheckAuthentication);
        Assert.False(harness.ViewModel.CanStart);
        Assert.Contains("開き直してください", harness.ViewModel.LoginStatusText, StringComparison.Ordinal);
        Assert.Equal(0, harness.Resolver.CallCount);
        Assert.Equal(0, harness.FactoryCallCount);
        AssertSafe(harness.ViewModel);
    }

    [Fact]
    public async Task Dispose_before_login_is_idempotent_and_commands_and_direct_login_are_safe_no_ops()
    {
        using LoginHarness harness = new();
        ExecutionViewModel viewModel = harness.ViewModel;
        viewModel.Dispose();
        viewModel.Dispose();

        viewModel.LoginCommand.Execute(null);
        viewModel.CancelLoginCommand.Execute(null);
        viewModel.CheckAuthenticationCommand.Execute(null);
        viewModel.StartCommand.Execute(null);
        await viewModel.LoginAsync(TestContext.Current.CancellationToken);
        viewModel.CancelLogin();

        Assert.Null(viewModel.LastLoginTask);
        Assert.False(viewModel.IsLoggingIn);
        Assert.False(viewModel.CanLogin);
        Assert.False(viewModel.CanCancelLogin);
        Assert.False(viewModel.CanCheckAuthentication);
        Assert.False(viewModel.CanStart);
        Assert.Equal(0, harness.Resolver.CallCount);
        Assert.Equal(0, harness.FactoryCallCount);
        Assert.Equal(0, harness.Authentication.CallCount);
        Assert.Equal(0, harness.Runner.CallCount);
        Assert.Equal(CopilotLoginStatus.Disposed,
            (await harness.Service.LoginAsync(TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Dispose_while_resolving_prevents_late_start_and_late_ui_updates()
    {
        using LoginHarness harness = new();
        TaskCompletionSource<string?> resolution = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Resolver.ResolveOverride = _ => new(resolution.Task);
        ExecutionViewModel viewModel = harness.ViewModel;
        viewModel.LoginCommand.Execute(null);
        Task login = LoginTask(viewModel);
        await harness.Resolver.Called.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        viewModel.Dispose();
        string disposedStatus = viewModel.LoginStatusText;
        int lateNotifications = 0;
        viewModel.PropertyChanged += (_, _) => lateNotifications++;
        resolution.SetResult(CliPath());
        await login.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.FactoryCallCount);
        Assert.Equal(0, harness.Process.StartCount);
        Assert.Equal(0, lateNotifications);
        Assert.Equal(disposedStatus, viewModel.LoginStatusText);
        Assert.False(viewModel.IsLoggingIn);
        Assert.False(viewModel.CanLogin);
        Assert.False(viewModel.CanCheckAuthentication);
    }

    [Fact]
    public async Task MainWindow_dispose_cleans_up_synchronously_without_waiting_for_the_login_ui_continuation()
    {
        using LoginHarness harness = new();
        using MainWindowViewModel shell = new(
            new WorkflowNavigator(), new InputViewModel(), new QuantificationDesignViewModel(),
            harness.ViewModel, new ResultsOutputViewModel());
        QueuedSynchronizationContext ui = new();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task login;
        SynchronizationContext.SetSynchronizationContext(ui);
        try
        {
            harness.ViewModel.LoginCommand.Execute(null);
            login = LoginTask(harness.ViewModel);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await harness.Process.Waiting.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        // The UI queue is deliberately not pumped until Dispose has returned.
        Task closing = Task.Run(shell.Dispose, TestContext.Current.CancellationToken);
        try
        {
            await closing.WaitAsync(TestWait, TestContext.Current.CancellationToken);
            Assert.True(harness.Process.HasExited);
            Assert.Equal([false], harness.Process.KillTreeArguments);
            Assert.Equal(1, harness.Process.DisposeCount);
            Assert.False(login.IsCompleted);
            Assert.False(harness.ViewModel.IsLoggingIn);
            Assert.False(harness.ViewModel.CanLogin);
            Assert.False(harness.ViewModel.CanCancelLogin);
            Assert.False(harness.ViewModel.CanCheckAuthentication);
            string disposedStatus = harness.ViewModel.LoginStatusText;
            int lateNotifications = 0;
            harness.ViewModel.PropertyChanged += (_, _) => lateNotifications++;

            await ui.Posted.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);
            ui.Drain();
            await login.WaitAsync(TestWait, TestContext.Current.CancellationToken);

            Assert.Equal(disposedStatus, harness.ViewModel.LoginStatusText);
            Assert.Equal(0, lateNotifications);
            shell.Dispose();
            harness.ViewModel.CancelLoginCommand.Execute(null);
            harness.ViewModel.LoginCommand.Execute(null);
            Assert.Equal(1, harness.FactoryCallCount);
            Assert.Equal(1, harness.Process.DisposeCount);
            Assert.Equal(0, harness.Authentication.CallCount);
            Assert.Equal(0, harness.Runner.CallCount);
        }
        finally
        {
            await ui.Posted.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);
            ui.Drain();
            await closing.WaitAsync(TestWait, TestContext.Current.CancellationToken);
            await login.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Throwing_cancellation_callbacks_do_not_escape_or_skip_owned_process_cleanup(bool dispose)
    {
        using LoginHarness harness = new();
        harness.ViewModel.LoginCommand.Execute(null);
        Task login = LoginTask(harness.ViewModel);
        await harness.Process.Waiting.Task.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        using CancellationTokenRegistration registration = harness.Process.WaitToken.Register(
            () => throw new InvalidOperationException(SensitiveFailure));

        if (dispose)
        {
            harness.ViewModel.Dispose();
        }
        else
        {
            harness.ViewModel.CancelLoginCommand.Execute(null);
        }

        await login.WaitAsync(TestWait, TestContext.Current.CancellationToken);
        Assert.True(harness.Process.HasExited);
        Assert.Equal([false], harness.Process.KillTreeArguments);
        Assert.Equal(1, harness.Process.DisposeCount);
        Assert.False(harness.ViewModel.IsLoggingIn);
        Assert.False(harness.ViewModel.CanCancelLogin);
        AssertSafe(harness.ViewModel);
    }

    [Fact]
    public async Task Login_state_changes_notify_bindings_and_all_execution_commands()
    {
        using LoginHarness harness = new();
        ExecutionViewModel viewModel = harness.ViewModel;
        List<string?> properties = [];
        viewModel.PropertyChanged += (_, eventArgs) => properties.Add(eventArgs.PropertyName);
        ICommand[] commands =
        [
            viewModel.LoginCommand, viewModel.CancelLoginCommand,
            viewModel.CheckAuthenticationCommand, viewModel.StartCommand, viewModel.CancelCommand,
        ];
        int[] commandNotifications = new int[commands.Length];
        for (int index = 0; index < commands.Length; index++)
        {
            int observedIndex = index;
            commands[index].CanExecuteChanged += (_, _) => commandNotifications[observedIndex]++;
        }

        viewModel.LoginCommand.Execute(null);
        Task login = LoginTask(viewModel);
        Assert.False(viewModel.LoginCommand.CanExecute(null));
        Assert.True(viewModel.CancelLoginCommand.CanExecute(null));
        Assert.False(viewModel.CheckAuthenticationCommand.CanExecute(null));
        Assert.False(viewModel.StartCommand.CanExecute(null));
        int[] atStart = [.. commandNotifications];
        viewModel.CancelLoginCommand.Execute(null);
        await login.WaitAsync(TestWait, TestContext.Current.CancellationToken);

        Assert.True(viewModel.LoginCommand.CanExecute(null));
        Assert.False(viewModel.CancelLoginCommand.CanExecute(null));
        Assert.True(viewModel.CheckAuthenticationCommand.CanExecute(null));
        Assert.False(viewModel.StartCommand.CanExecute(null));
        foreach (string name in new[]
        {
            nameof(ExecutionViewModel.IsLoggingIn), nameof(ExecutionViewModel.LoginStatusText),
            nameof(ExecutionViewModel.LastLoginTask), nameof(ExecutionViewModel.CanLogin),
            nameof(ExecutionViewModel.CanCancelLogin), nameof(ExecutionViewModel.CanCheckAuthentication),
            nameof(ExecutionViewModel.CanStart), nameof(ExecutionViewModel.AvailableModelIds),
            nameof(ExecutionViewModel.SelectedModelId), nameof(ExecutionViewModel.RuntimeIdentityText),
        })
        {
            Assert.Contains(name, properties);
        }

        for (int index = 0; index < commands.Length; index++)
        {
            Assert.True(atStart[index] > 0);
            Assert.True(commandNotifications[index] > atStart[index]);
        }
    }

    [Fact]
    public async Task Both_existing_cli_unavailable_messages_recommend_bundled_distribution_recovery_not_PATH()
    {
        using LoginHarness harness = new();
        harness.Authentication.Snapshot = new ExecutionAuthenticationSnapshot(ExecutionAuthenticationState.CliUnavailable);

        await harness.ViewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        ExecutionTechnicalError error = Assert.Single(
            harness.ViewModel.TechnicalErrors, item => item.Code == "COPILOT_CLI_UNAVAILABLE");
        foreach (string message in new[] { harness.ViewModel.AuthenticationStatusText, error.Message })
        {
            Assert.Contains("同梱", message, StringComparison.Ordinal);
            Assert.Contains("再取得", message, StringComparison.Ordinal);
            Assert.Contains("再展開", message, StringComparison.Ordinal);
            Assert.DoesNotContain("PATH", message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(0, harness.FactoryCallCount);
    }

    private static Task LoginTask(ExecutionViewModel viewModel) =>
        Assert.IsAssignableFrom<Task>(viewModel.LastLoginTask);

    private static string CliPath() => Path.Combine(Path.GetTempPath(), "copilot-a02-private", "copilot.exe");

    private static string InputPath() => Path.Combine(Path.GetTempPath(), "a02-synthetic-input.xlsx");

    private static ExecutionAuthenticationSnapshot AvailableSnapshot(string modelId = "model-before-login") =>
        new(ExecutionAuthenticationState.Available, [U04TestSupport.Model(modelId)], U04TestSupport.RuntimeIdentity());

    private static void AssertSafe(ExecutionViewModel viewModel)
    {
        string text = string.Join("\n",
            new[] { viewModel.LoginStatusText, viewModel.AuthenticationStatusText, viewModel.RunStatusText, viewModel.ToString() }
                .Concat(viewModel.TechnicalErrors.Select(error => error.Message)));
        Assert.DoesNotContain("PRIVATE-LOGIN-TOKEN-CANARY", text, StringComparison.Ordinal);
        Assert.DoesNotContain("device-code", text, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\private", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("copilot-a02-private", text, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PATH", viewModel.LoginStatusText, StringComparison.OrdinalIgnoreCase);
    }

    public enum FailurePoint { None, MissingCli, Resolver, Factory, StartFalse, Start, NonzeroExit, Wait, PrematureWait, UnconfirmedExit }

    private sealed class LoginHarness : IDisposable
    {
        public LoginHarness(FakeLoginProcess? process = null, IQuantificationRunBoundary? runBoundary = null)
        {
            Process = process ?? new FakeLoginProcess();
            Service = new BundledCopilotLoginService(Resolver, _ =>
            {
                FactoryCallCount++;
                if (ThrowOnFactory)
                {
                    throw new InvalidOperationException(SensitiveFailure);
                }

                return Process;
            });
            ViewModel = new ExecutionViewModel(Authentication, runBoundary ?? Runner, Service);
            QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
            ViewModel.Configure(definition, U01TestSupport.ValidateMapping(definition).Metadata, InputPath());
        }

        public StubResolver Resolver { get; } = new();
        public LoginAuthenticationBoundary Authentication { get; } = new();
        public RecordingRunBoundary Runner { get; } = new((_, _, _) => throw new InvalidOperationException(SensitiveFailure));
        public FakeLoginProcess Process { get; set; }
        public BundledCopilotLoginService Service { get; }
        public ExecutionViewModel ViewModel { get; }
        public int FactoryCallCount { get; private set; }
        public bool ThrowOnFactory { get; set; }

        public void Dispose() => ViewModel.Dispose();
    }

    private sealed class LoginAuthenticationBoundary : IExecutionAuthenticationBoundary
    {
        public ExecutionAuthenticationSnapshot Snapshot { get; set; } = AvailableSnapshot();
        public Func<CancellationToken, Task<ExecutionAuthenticationSnapshot>>? CheckOverride { get; set; }
        public int CallCount { get; private set; }

        public Task<ExecutionAuthenticationSnapshot> CheckAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return CheckOverride?.Invoke(cancellationToken) ?? Task.FromResult(Snapshot);
        }
    }

    private sealed class StubResolver : ICopilotCliPathResolver
    {
        public string? Path { get; set; } = CliPath();
        public bool ThrowOnResolve { get; set; }
        public int CallCount { get; private set; }
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<CancellationToken, ValueTask<string?>>? ResolveOverride { get; set; }

        public ValueTask<string?> ResolveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Called.TrySetResult();
            if (ThrowOnResolve)
            {
                throw new InvalidOperationException(SensitiveFailure);
            }

            return ResolveOverride?.Invoke(cancellationToken) ?? ValueTask.FromResult(Path);
        }
    }

    private sealed class FakeLoginProcess : ICopilotLoginProcess
    {
        private readonly TaskCompletionSource exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public FailurePoint Failure { get; init; }
        public int? ExitOnStart { get; init; }
        public int StartCount { get; private set; }
        public int DisposeCount { get; private set; }
        public bool HasExited => exit.Task.IsCompletedSuccessfully;
        public int ExitCode { get; private set; }
        public CancellationToken WaitToken { get; private set; }
        public List<bool> KillTreeArguments { get; } = [];
        public List<int> CleanupWaits { get; } = [];

        public bool Start()
        {
            StartCount++;
            if (Failure == FailurePoint.Start)
            {
                throw new InvalidOperationException(SensitiveFailure);
            }

            if (Failure == FailurePoint.StartFalse)
            {
                return false;
            }

            if (ExitOnStart is int code)
            {
                Complete(code);
            }

            return true;
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitToken = cancellationToken;
            Waiting.TrySetResult();
            if (Failure == FailurePoint.Wait)
            {
                throw new InvalidOperationException(SensitiveFailure);
            }

            // Deliberately ignore cancellation; the real service must still stop this fake.
            return Failure == FailurePoint.PrematureWait ? Task.CompletedTask : exit.Task;
        }

        public void Kill(bool entireProcessTree)
        {
            KillTreeArguments.Add(entireProcessTree);
            if (Failure != FailurePoint.UnconfirmedExit)
            {
                Complete(-1);
            }
        }

        public bool WaitForExit(int milliseconds)
        {
            CleanupWaits.Add(milliseconds);
            return HasExited;
        }

        public void Complete(int code)
        {
            ExitCode = code;
            exit.TrySetResult();
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> callbacks = new();
        public TaskCompletionSource Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            callbacks.Enqueue((callback, state));
            Posted.TrySetResult();
        }

        public void Drain()
        {
            while (callbacks.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}