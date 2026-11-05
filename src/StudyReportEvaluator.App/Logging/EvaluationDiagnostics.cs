using System.Text;

namespace StudyReportEvaluator.App.Logging;

/// <summary>
/// Run-scoped diagnostic channel for failure analysis. A run installs a sink; schedulers add the row context and the
/// evaluation code reports closed codes, ids and numbers. Anything outside a small safe alphabet is replaced, so answers,
/// prompts, reasons and evidence cannot reach the log even by mistake. Reporting never throws.
/// </summary>
internal static class EvaluationDiagnostics
{
    private const int MaximumTextLength = 480;

    private static readonly AsyncLocal<Scope?> Current = new();

    private sealed record Scope(Action<string> Sink, string Context);

    private sealed class Restore(Scope? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }

    private sealed class NoOp : IDisposable
    {
        public static readonly NoOp Instance = new();

        public void Dispose()
        {
        }
    }

    internal static IDisposable BeginRun(Action<string> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        Scope? previous = Current.Value;
        Current.Value = new Scope(sink, string.Empty);
        return new Restore(previous);
    }

    internal static IDisposable BeginContext(string context)
    {
        Scope? previous = Current.Value;
        if (previous is null)
        {
            return NoOp.Instance;
        }

        Current.Value = previous with { Context = Sanitize(context) };
        return new Restore(previous);
    }

    internal static void Report(string eventName, string detail)
    {
        try
        {
            Scope? scope = Current.Value;
            if (scope is null)
            {
                return;
            }

            string context = scope.Context.Length == 0 ? string.Empty : scope.Context + " ";
            scope.Sink("DIAG " + Sanitize(eventName) + " " + context + Sanitize(detail));
        }
        catch
        {
            // Diagnostics never alter evaluation behavior.
        }
    }

    internal static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new(Math.Min(value.Length, MaximumTextLength));
        foreach (char character in value)
        {
            if (builder.Length >= MaximumTextLength)
            {
                break;
            }

            builder.Append(
                character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                    or '_' or '-' or '.' or ',' or ':' or ';' or '=' or '/' or ' '
                    ? character
                    : '?');
        }

        return builder.ToString();
    }
}
