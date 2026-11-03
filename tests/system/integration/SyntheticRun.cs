using System.Collections.Immutable;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;

namespace StudyReportEvaluator.SystemTests.Integration;

/// <summary>合成 workbook・fake AI だけで durable run を実行する System Test 用の足場（実 CLI・実 AI は使わない）。</summary>
internal sealed class SyntheticRun : IDisposable
{
    internal const string AnswerCanary = "ANSWER-CANARY-7f3a";
    internal const string SupportCanary = "SUPPORT-CANARY-5c21";
    internal const string StudentCanary = "STUDENT-CANARY-yamada-taro-90210";
    internal const string QuestionCanary = "QUESTION-CANARY-explain-recursion";
    internal const string PromptCanary = "PROMPT-CANARY-9d4e";
    internal const string ReasonCanary = "REASON-CANARY-b81c";
    internal const string EvidenceCanary = "EVIDENCE-CANARY-2e6f";
    internal const string CredentialCanary = "ghp_CANARYCREDENTIAL0123456789abcdef";

    internal SyntheticRun()
    {
        Directory = Path.Combine(Path.GetTempPath(), "SRE-ST-REALPATH-CANARY-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        InputPath = Path.Combine(Directory, "input-REALNAME-CANARY.xlsx");
        OutputDirectory = Path.Combine(Directory, "out");
        System.IO.Directory.CreateDirectory(OutputDirectory);
        CreateWorkbook(InputPath);
    }

    internal string Directory { get; }

    internal string InputPath { get; }

    internal string OutputDirectory { get; }

    internal static QuantificationDefinition Definition() =>
        new()
        {
            Id = "DEF-ST",
            Name = "Synthetic system-test definition",
            Revision = "1",
            SourceSheet = "Original",
            HeaderRow = 1,
            FirstDataRow = 2,
            LastDataRow = 2,
            BasePoints = 98m,
            RoundingDigits = 1,
            Questions =
            [
                new QuestionDefinition
                {
                    Id = "Q1",
                    DisplayName = "Question Q1",
                    QuestionText = QuestionCanary,
                    PrimarySourceColumn = "A",
                    SupportingSourceColumns = ["B"],
                    Points = 2m,
                    Enabled = true,
                    Evaluators =
                    [
                        new EvaluatorDefinition
                        {
                            Id = "E1",
                            DisplayName = "Evaluator E1",
                            Type = EvaluatorType.CustomPrompt,
                            Weight = 3m,
                            Range = new ScoreRange(0m, 10m),
                            CustomPromptTemplate = PromptCanary + " {回答} {評価項目}",
                            Enabled = true,
                            Criteria =
                            [
                                new CriterionDefinition
                                {
                                    Id = "C1",
                                    DisplayName = "Criterion C1",
                                    Description = "Description C1",
                                    Weight = 4m,
                                    Enabled = true,
                                },
                            ],
                        },
                    ],
                },
            ],
        };

    /// <summary>fake AI で durable run を実行する（outputDirectory 省略時は合成の result directory）。</summary>
    internal async Task<RunSummary> RunAsync(
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        QuantificationDefinition definition = Definition();
        WorkbookMetadata metadata = new WorkbookMetadataReader().Read(InputPath);
        DurableQuantificationOrchestrator orchestrator = new(
            new OpenXmlEvaluationRowSource(InputPath),
            new FakeNormalRunner(),
            new FakeReferenceRunner(),
            new FakeSpecialRunner());
        return await orchestrator.RunAsync(
            new DurableQuantificationRunRequest
            {
                Run = new QuantificationRunRequest
                {
                    DraftDefinition = definition,
                    WorkbookMetadata = metadata,
                    InputPath = InputPath,
                    ModelId = "model-test",
                    MaximumPromptTokens = 64_000,
                    MaximumContextWindowTokens = 128_000,
                    MaxConcurrency = 1,
                    UseDurableWorkflow = true,
                },
                Runtime = new CheckpointRuntimeIdentity
                {
                    ApplicationIdentity = "StudyReportEvaluator.App/4.0.0",
                    CliVersion = "1.0.79",
                    CliSha256 = new string('A', 64),
                    SdkInformationalVersion = "1.0.11",
                },
                OutputDirectory = outputDirectory ?? OutputDirectory,
            },
            cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                foreach (string file in System.IO.Directory.EnumerateFiles(Directory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void CreateWorkbook(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
        WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
        part.Worksheet = new Worksheet(
            new SheetDimension { Reference = "A1:C2" },
            new SheetData(
                new Row(Inline("A1", "Primary A"), Inline("B1", "Support B"), Inline("C1", "Student C")) { RowIndex = 1 },
                new Row(
                    Inline("A2", $"{AnswerCanary} {EvidenceCanary} {CredentialCanary}"),
                    Inline("B2", SupportCanary),
                    Inline("C2", StudentCanary))
                { RowIndex = 2 }));
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = 1, Name = "Original" });
        workbookPart.Workbook.Save();
    }

    private static Cell Inline(string reference, string value) =>
        new()
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(value)),
        };

    private sealed class FakeNormalRunner : IEvaluationRunner
    {
        public Task<EvaluationRunnerResult> EvaluateAsync(
            SafeEvaluationPayload payload,
            string modelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(EvaluationRunnerResult.Succeeded(
                new QuantificationResult
                {
                    EvaluatorId = payload.EvaluatorId,
                    Criteria = payload.ExpectedCriteria
                        .Select(criterion => new CriterionQuantificationResult
                        {
                            CriterionId = criterion.CriterionId,
                            RawScore = 7m,
                            Reason = ReasonCanary,
                            Evidence = EvidenceCanary,
                            EvidenceSource = EvidenceSourceKind.PrimaryAnswer,
                            EvidenceSourceColumnId = "A",
                        })
                        .ToImmutableArray(),
                }));
    }

    private sealed class FakeReferenceRunner : IReferenceAnswerOperationRunner
    {
        public Task<AuxiliaryOperationResult<ReferenceAnswerResult>> EvaluateAsync(
            SafeReferenceAnswerPayload payload,
            CancellationToken cancellationToken) =>
            Task.FromResult(AuxiliaryOperationResult<ReferenceAnswerResult>.Succeeded(
                new ReferenceAnswerResult { QuestionId = payload.QuestionId, Answer = "reference answer" }));
    }

    private sealed class FakeSpecialRunner : ISpecialEvaluationOperationRunner
    {
        public Task<AuxiliaryOperationResult<SpecialQuantificationResult>> EvaluateAsync(
            SafeSpecialEvaluationPayload payload,
            string modelId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This synthetic definition has no special evaluation.");
    }
}
