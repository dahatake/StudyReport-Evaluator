using System.Globalization;
using System.Text;
using StudyReportEvaluator.App.Workflow;

namespace StudyReportEvaluator.App.Logging;

/// <summary>
/// FR-108: job progress log saved as <c>{yyyyMMddHHmm}-log.txt</c> (UTF-8 plain text) in the automatic-output result directory.
/// Only numbers, closed codes and stage names are recorded (same secrecy rule as the §11.9 JSONL log): never answers,
/// prompts, reasons, evidence, student identifiers, question text, real paths or credentials.
/// Saving never throws; failure is reported as a closed code.
/// </summary>
internal sealed class RunTextLog
{
    internal const string Saved = "RUN_LOG_SAVED";
    internal const string WriteFailed = "RUN_LOG_WRITE_FAILED";
    internal const string NotAttempted = "RUN_LOG_NOT_ATTEMPTED";
    internal const int MaximumLines = 5000;
    internal const int MaximumDiagnosticLines = 4000;

    private readonly TimeProvider timeProvider;
    private readonly DateTimeOffset startedLocal;
    private readonly List<string> lines = [];
    private readonly List<string> diagnostics = [];
    private readonly object gate = new();
    private string? lastProgressLine;
    private int dropped;
    private int diagnosticsDropped;

    internal RunTextLog(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
        startedLocal = timeProvider.GetLocalNow();
        Add("JOB_START");
    }

    /// <summary>Result directory; set once known. Null means the log cannot be saved.</summary>
    internal string? Directory { get; set; }

    internal Action<DurableEvaluationProgress> Wrap(Action<DurableEvaluationProgress>? inner) =>
        value =>
        {
            AddProgress(value);
            inner?.Invoke(value);
        };

    internal void Add(string closedText)
    {
        lock (gate)
        {
            if (lines.Count >= MaximumLines)
            {
                dropped++;
                return;
            }

            lines.Add(Stamp() + closedText);
        }
    }

    /// <summary>Failure diagnostics have their own budget so that progress lines can never crowd them out.</summary>
    internal void AddDiagnostic(string closedText)
    {
        lock (gate)
        {
            if (diagnostics.Count >= MaximumDiagnosticLines)
            {
                diagnosticsDropped++;
                return;
            }

            diagnostics.Add(Stamp() + closedText);
        }
    }

    internal void AddProgress(DurableEvaluationProgress value)
    {
        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"PROGRESS stage={value.Stage} status={value.StatusCode} reference={value.ReferenceCompleted}/{value.ReferenceTotal} row={value.RowCompleted}/{value.RowTotal} operation={value.OperationCompleted}/{value.OperationTotal} inFlight={value.InFlight}");
        lock (gate)
        {
            if (string.Equals(text, lastProgressLine, StringComparison.Ordinal))
            {
                return;
            }

            lastProgressLine = text;
        }

        Add(text);
    }

    /// <summary>Writes the log without overwriting; returns a closed status code.</summary>
    internal string Save()
    {
        try
        {
            string? directory = Directory;
            if (string.IsNullOrWhiteSpace(directory))
            {
                return NotAttempted;
            }

            string text;
            lock (gate)
            {
                if (dropped > 0)
                {
                    lines.Add(Stamp() + "LINES_DROPPED count=" + dropped.ToString(CultureInfo.InvariantCulture));
                }

                lines.AddRange(diagnostics);
                if (diagnosticsDropped > 0)
                {
                    lines.Add(Stamp() + "DIAG_LINES_DROPPED count=" + diagnosticsDropped.ToString(CultureInfo.InvariantCulture));
                }

                text = string.Join("\n", lines) + "\n";
            }

            System.IO.Directory.CreateDirectory(directory);
            string stamp = startedLocal.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
            for (int sequence = 1; sequence < 1000; sequence++)
            {
                string suffix = sequence == 1
                    ? string.Empty
                    : "-" + sequence.ToString("D2", CultureInfo.InvariantCulture);
                string path = Path.Combine(directory, stamp + suffix + "-log.txt");
                if (File.Exists(path) || System.IO.Directory.Exists(path))
                {
                    continue;
                }

                try
                {
                    using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                    stream.Write(bytes, 0, bytes.Length);
                    return Saved;
                }
                catch (IOException) when (File.Exists(path))
                {
                    // Lost a race for this name; take the next unused one.
                }
            }

            return WriteFailed;
        }
        catch
        {
            return WriteFailed;
        }
    }

    private string Stamp() =>
        timeProvider.GetLocalNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture) + " ";
}
