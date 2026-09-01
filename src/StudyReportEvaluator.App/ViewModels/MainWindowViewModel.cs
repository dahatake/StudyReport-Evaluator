using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StudyReportEvaluator.App.Navigation;

namespace StudyReportEvaluator.App.ViewModels;

public sealed record WorkflowStepPresentation(
    WorkflowStep Step,
    int Number,
    string NumberText,
    string Title,
    string EnglishTitle,
    string StatusIcon,
    string StatusText,
    bool IsCurrent,
    bool IsCompleted,
    bool IsUpcoming)
{
    public string AccessibleName =>
        $"ステップ {Number.ToString(CultureInfo.InvariantCulture)}、{Title}、{StatusText}";
}

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly WorkflowNavigator navigator;
    private readonly DelegateCommand navigateCommand;
    private readonly DelegateCommand nextCommand;
    private readonly DelegateCommand previousCommand;
    private readonly ExecutionViewModel executionViewModel;
    private readonly ResultsOutputViewModel resultsOutputViewModel;
    private ImmutableArray<WorkflowStepPresentation> stepPresentations;
    private QuantificationDesignViewModel designViewModel;
    private WorkflowStep previousStep;
    private bool disposed;

    public MainWindowViewModel(WorkflowNavigator navigator)
        : this(
            navigator,
            new InputViewModel(),
            new QuantificationDesignViewModel(),
            new ExecutionViewModel(),
            new ResultsOutputViewModel())
    {
    }

    public MainWindowViewModel(
        WorkflowNavigator navigator,
        InputViewModel inputViewModel,
        QuantificationDesignViewModel designViewModel)
        : this(
            navigator,
            inputViewModel,
            designViewModel,
            new ExecutionViewModel(),
            new ResultsOutputViewModel())
    {
    }

    public MainWindowViewModel(
        WorkflowNavigator navigator,
        InputViewModel inputViewModel,
        QuantificationDesignViewModel designViewModel,
        ExecutionViewModel executionViewModel,
        ResultsOutputViewModel resultsOutputViewModel)
    {
        this.navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        InputViewModel = inputViewModel ?? throw new ArgumentNullException(nameof(inputViewModel));
        this.designViewModel = designViewModel ?? throw new ArgumentNullException(nameof(designViewModel));
        this.executionViewModel = executionViewModel
            ?? throw new ArgumentNullException(nameof(executionViewModel));
        this.resultsOutputViewModel = resultsOutputViewModel
            ?? throw new ArgumentNullException(nameof(resultsOutputViewModel));
        previousStep = navigator.CurrentStep;
        stepPresentations = BuildStepPresentations();
        navigateCommand = new DelegateCommand(
            ExecuteNavigate,
            CanNavigate);
        nextCommand = new DelegateCommand(
            _ => this.navigator.MoveNext(),
            _ => this.navigator.CanMoveNext);
        previousCommand = new DelegateCommand(
            _ => this.navigator.MovePrevious(),
            _ => this.navigator.CanMovePrevious);
        this.navigator.CurrentStepChanged += HandleCurrentStepChanged;
        this.executionViewModel.RunCompleted += HandleRunCompleted;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public WorkflowNavigator Navigator => navigator;

    public InputViewModel InputViewModel { get; }

    public QuantificationDesignViewModel DesignViewModel => designViewModel;

    public ExecutionViewModel ExecutionViewModel => executionViewModel;

    public ResultsOutputViewModel ResultsOutputViewModel => resultsOutputViewModel;

    public WorkflowStep CurrentStep => navigator.CurrentStep;

    public UiObservableObject? CurrentEditorViewModel => CurrentStep switch
    {
        WorkflowStep.Input => InputViewModel,
        WorkflowStep.Design => DesignViewModel,
        WorkflowStep.Execution => ExecutionViewModel,
        WorkflowStep.Results => ResultsOutputViewModel,
        _ => throw new ArgumentOutOfRangeException(nameof(CurrentStep), CurrentStep, "Unknown workflow step."),
    };

    public bool HasEditorContent => CurrentEditorViewModel is not null;

    public bool HasPlaceholderContent => !HasEditorContent;

    public ImmutableArray<WorkflowStepPresentation> Steps => stepPresentations;

    public WorkflowStepPresentation InputStep => stepPresentations[0];

    public WorkflowStepPresentation DesignStep => stepPresentations[1];

    public WorkflowStepPresentation ExecutionStep => stepPresentations[2];

    public WorkflowStepPresentation ResultsStep => stepPresentations[3];

    public ICommand NavigateCommand => navigateCommand;

    public ICommand NextCommand => nextCommand;

    public ICommand PreviousCommand => previousCommand;

    public string CurrentStepKicker =>
        $"STEP {(navigator.CurrentIndex + 1).ToString("00", CultureInfo.InvariantCulture)} / 04 · {navigator.CurrentDefinition.EnglishTitle}";

    public string CurrentStepTitle => CurrentContent.Title;

    public string CurrentStepDescription => CurrentContent.Description;

    public string CurrentDetailOne => CurrentContent.DetailOne;

    public string CurrentDetailTwo => CurrentContent.DetailTwo;

    public string CurrentDetailThree => CurrentContent.DetailThree;

    public string CurrentStepStatusText =>
        $"{CurrentPresentation.StatusIcon} {CurrentPresentation.Title} — {CurrentPresentation.StatusText}";

    public string NavigationProgressText =>
        $"{(navigator.CurrentIndex + 1).ToString(CultureInfo.InvariantCulture)} / {WorkflowNavigator.StepCount.ToString(CultureInfo.InvariantCulture)} ステップ";

    public string NextButtonText => navigator.CanMoveNext ? "次へ  →" : "最終ステップ";

    private WorkflowStepPresentation CurrentPresentation => stepPresentations[navigator.CurrentIndex];

    private StepContent CurrentContent => ContentFor(navigator.CurrentStep);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        navigator.CurrentStepChanged -= HandleCurrentStepChanged;
        executionViewModel.RunCompleted -= HandleRunCompleted;
        executionViewModel.Dispose();
        resultsOutputViewModel.Dispose();
    }

    private bool CanNavigate(object? parameter) =>
        parameter is WorkflowStep step && navigator.CanNavigateTo(step);

    private void ExecuteNavigate(object? parameter)
    {
        if (parameter is WorkflowStep step)
        {
            navigator.NavigateTo(step);
        }
    }

    private void HandleCurrentStepChanged(object? sender, EventArgs e)
    {
        SynchronizeDraftsForTransition(previousStep, navigator.CurrentStep);
        previousStep = navigator.CurrentStep;
        stepPresentations = BuildStepPresentations();
        OnPropertyChanged(nameof(DesignViewModel));
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(CurrentEditorViewModel));
        OnPropertyChanged(nameof(HasEditorContent));
        OnPropertyChanged(nameof(HasPlaceholderContent));
        OnPropertyChanged(nameof(Steps));
        OnPropertyChanged(nameof(InputStep));
        OnPropertyChanged(nameof(DesignStep));
        OnPropertyChanged(nameof(ExecutionStep));
        OnPropertyChanged(nameof(ResultsStep));
        OnPropertyChanged(nameof(CurrentStepKicker));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));
        OnPropertyChanged(nameof(CurrentDetailOne));
        OnPropertyChanged(nameof(CurrentDetailTwo));
        OnPropertyChanged(nameof(CurrentDetailThree));
        OnPropertyChanged(nameof(CurrentStepStatusText));
        OnPropertyChanged(nameof(NavigationProgressText));
        OnPropertyChanged(nameof(NextButtonText));
        navigateCommand.RaiseCanExecuteChanged();
        nextCommand.RaiseCanExecuteChanged();
        previousCommand.RaiseCanExecuteChanged();
    }

    private void SynchronizeDraftsForTransition(WorkflowStep from, WorkflowStep to)
    {
        if (from == WorkflowStep.Design && InputViewModel.HasLoadedWorkbook)
        {
            InputViewModel.SynchronizeFromDesignDraft(DesignViewModel.Draft);
        }

        if (to == WorkflowStep.Design)
        {
            designViewModel = new QuantificationDesignViewModel(InputViewModel.DefinitionDraft);
        }

        if (to == WorkflowStep.Execution
            && from != WorkflowStep.Results
            && !executionViewModel.IsRunning)
        {
            if (InputViewModel.HasLoadedWorkbook
                && InputViewModel.Metadata is { } metadata)
            {
                executionViewModel.Configure(
                    InputViewModel.DefinitionDraft,
                    metadata,
                    InputViewModel.FilePath);
            }
            else
            {
                executionViewModel.ClearConfiguration();
            }
        }
    }

    private void HandleRunCompleted(object? sender, ExecutionRunCompletedEventArgs e) =>
        resultsOutputViewModel.Load(e.Context);

    private ImmutableArray<WorkflowStepPresentation> BuildStepPresentations() =>
        [
            .. navigator.Steps.Select(definition =>
            {
                WorkflowStepState state = navigator.GetState(definition.Step);
                return new WorkflowStepPresentation(
                    definition.Step,
                    definition.Number,
                    definition.Number.ToString("00", CultureInfo.InvariantCulture),
                    definition.Title,
                    definition.EnglishTitle,
                    state switch
                    {
                        WorkflowStepState.Completed => "✓",
                        WorkflowStepState.Current => "●",
                        WorkflowStepState.Upcoming => "○",
                        _ => throw new InvalidOperationException("Unknown workflow step state."),
                    },
                    state switch
                    {
                        WorkflowStepState.Completed => "完了",
                        WorkflowStepState.Current => "現在・選択中",
                        WorkflowStepState.Upcoming => "未着手",
                        _ => throw new InvalidOperationException("Unknown workflow step state."),
                    },
                    state == WorkflowStepState.Current,
                    state == WorkflowStepState.Completed,
                    state == WorkflowStepState.Upcoming);
            }),
        ];

    private static StepContent ContentFor(WorkflowStep step) => step switch
    {
        WorkflowStep.Input => new(
            "入力を準備する",
            "評価対象の Excel と範囲を選び、質問ごとの回答列を結び付ける入口です。U-03 で実際のファイル選択とマッピングを接続します。",
            "Excel ファイルとシートを選択",
            "見出し行・回答行を確認",
            "主回答列と補助列をマッピング"),
        WorkflowStep.Design => new(
            "定量化を設計する",
            "Knowledge / Custom の評価方法、評価項目、range、3 階層の重みを組み立てます。件数を固定しない編集画面は U-03 で接続します。",
            "質問と評価方法を構成",
            "Prompt・知識ポイントを編集",
            "range と重みを確認"),
        WorkflowStep.Execution => new(
            "定量化を実行する",
            "Copilot の状態、model、進捗、cancel、技術的エラーを扱う実行面です。snapshot に固定した処理を U-04 で接続します。",
            "ログインと model を確認",
            "評価単位と進捗を表示",
            "cancel と技術的失敗を安全に処理"),
        WorkflowStep.Results => new(
            "結果を確認して出力する",
            "AI raw、任意 override、Excel 計算 preview を確認し、入力を変えず別ファイルへ出力する画面を U-04 で接続します。",
            "raw 値と式 preview を確認",
            "必要な値だけ任意で上書き",
            "入力とは別の Excel へ出力"),
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown workflow step."),
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record StepContent(
        string Title,
        string Description,
        string DetailOne,
        string DetailTwo,
        string DetailThree);

    private sealed class DelegateCommand(
        Action<object?> execute,
        Predicate<object?> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute(parameter);

        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                execute(parameter);
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}