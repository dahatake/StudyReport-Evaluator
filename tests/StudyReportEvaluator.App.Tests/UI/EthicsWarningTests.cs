using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Composition;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.Resources;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.App.Workbooks.Mapping;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class EthicsWarningTests
{
    private const string ExpectedWarning =
        "生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません";

    [AvaloniaFact]
    public void Warning_is_visible_on_input_and_results_without_interaction_or_navigation_blocking()
    {
        MainWindow window = new ServiceRegistration().CreateMainWindow();

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Border warning = Required<Border>(window, "EthicsWarningBanner");
            TextBlock message = Required<TextBlock>(window, "EthicsWarningMessage");
            Assert.Equal(WorkflowStep.Input, window.ViewModel.CurrentStep);
            Assert.True(warning.IsVisible);
            Assert.Equal(ExpectedWarning, message.Text);

            Assert.True(window.ViewModel.NextCommand.CanExecute(null));
            window.ViewModel.NextCommand.Execute(null);
            window.ViewModel.NextCommand.Execute(null);
            window.ViewModel.NextCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WorkflowStep.Results, window.ViewModel.CurrentStep);
            Assert.True(warning.IsVisible);
            Assert.Equal(ExpectedWarning, message.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Warning_has_no_acknowledgement_controls_or_focus_target()
    {
        MainWindow window = new ServiceRegistration().CreateMainWindow();

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Border warning = Required<Border>(window, "EthicsWarningBanner");
            Control[] descendants = [.. warning.GetVisualDescendants().OfType<Control>()];

            Assert.False(warning.Focusable);
            Assert.False(warning.IsTabStop);
            Assert.False(warning.IsHitTestVisible);
            Assert.DoesNotContain(descendants, control => control is Button);
            Assert.DoesNotContain(descendants, control => control is CheckBox);
            Assert.DoesNotContain(descendants, control => control is TextBox);
            Assert.DoesNotContain(
                descendants.OfType<InputElement>(),
                input => input.Focusable);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Warning_is_one_fixed_resource_and_has_no_state_or_decision_contract()
    {
        Assert.Equal(ExpectedWarning, EthicsWarningText.Message);

        FieldInfo messageField = Assert.Single(
            typeof(EthicsWarningText).GetFields(BindingFlags.Public | BindingFlags.Static));
        Assert.Equal(nameof(EthicsWarningText.Message), messageField.Name);
        Assert.True(messageField.IsLiteral);
        Assert.Empty(typeof(EthicsWarningText).GetProperties(BindingFlags.Public | BindingFlags.Static));

        string[] prohibitedStateTerms =
        [
            "Acknowledge",
            "Acknowledgement",
            "Consent",
            "Dismiss",
            "AcceptWarning",
            "WarningAccepted",
            "EthicsAccepted",
            "WarningRole",
            "WarningExpiry",
            "WarningExpiration",
            "WarningStatus",
        ];
        MemberInfo[] shellMembers = typeof(MainWindowViewModel)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(
            shellMembers,
            member => prohibitedStateTerms.Any(term =>
                member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));

        Type[] decisionTypes =
        [
            typeof(WorkflowNavigator),
            typeof(ColumnMappingValidator),
            typeof(QuantificationOrchestrator),
            typeof(RunSummary),
            typeof(AtomicOutputCommitter),
            typeof(QuantificationDefinition),
            typeof(QuantificationSnapshot),
        ];
        MethodInfo[] decisionMethods =
        [
            .. decisionTypes.SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)),
        ];
        Assert.NotEmpty(decisionMethods);
        Assert.DoesNotContain(
            decisionMethods.SelectMany(method => method.GetParameters()),
            parameter =>
                ContainsWarningTerm(parameter.Name)
                || ContainsWarningTerm(parameter.ParameterType.Name));
        Assert.DoesNotContain(
            decisionTypes.SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)),
            property =>
                ContainsWarningTerm(property.Name)
                || ContainsWarningTerm(property.PropertyType.Name));
    }

    [AvaloniaFact]
    public void Technical_validation_placeholder_is_visually_and_semantically_distinct_from_warning()
    {
        MainWindow window = new ServiceRegistration().CreateMainWindow();

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Border warning = Required<Border>(window, "EthicsWarningBanner");
            Border technical = Required<Border>(window, "TechnicalValidationPlaceholder");
            TextBlock technicalMessage = Required<TextBlock>(window, "TechnicalValidationMessage");

            Assert.Contains("ethics-warning", warning.Classes);
            Assert.Contains("technical-validation", technical.Classes);
            Assert.DoesNotContain("technical-validation", warning.Classes);
            Assert.DoesNotContain("ethics-warning", technical.Classes);
            Assert.Contains("処理可否", technicalMessage.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(ExpectedWarning, technicalMessage.Text, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    private static bool ContainsWarningTerm(string? value) =>
        value?.Contains("warning", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("ethics", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("acknowledge", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("consent", StringComparison.OrdinalIgnoreCase) == true;

    private static T Required<T>(Control root, string name)
        where T : Control =>
        Assert.IsType<T>(root.FindControl<T>(name));
}