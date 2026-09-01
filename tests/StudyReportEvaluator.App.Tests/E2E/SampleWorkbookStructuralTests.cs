using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Mapping;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Validation;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.E2E;

public sealed class SampleWorkbookStructuralTests
{
    private const long ExpectedSampleSizeBytes = 661_189;
    private const string ExpectedSampleSha256 =
        "446386E20BB4096561CB4AFD6D74B8EAA9D50EAE53C97F984BA7F70EBEAD0DE5";

    [Fact]
    public void Repository_sample_is_opened_read_only_and_only_structural_metadata_drives_mapping()
    {
        E02RepositoryLayout.RequireWindowsX64();
        string samplePath = E02RepositoryLayout.GetRequiredSamplePath();
        InputSnapshotService snapshots = new();
        InputSnapshot before = snapshots.Capture(samplePath);

        Assert.Equal(ExpectedSampleSha256, before.Sha256);
        Assert.Equal(ExpectedSampleSizeBytes, before.SizeBytes);

        using FileStream readOnlyLease = new(
            samplePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        Assert.True(readOnlyLease.CanRead);
        Assert.True(readOnlyLease.CanSeek);
        Assert.False(readOnlyLease.CanWrite);
        Assert.Equal(ExpectedSampleSizeBytes, readOnlyLease.Length);

        FileFormatClassificationResult classification = new FileFormatClassifier().Classify(samplePath);
        Assert.True(classification.IsAccepted);
        Assert.Equal(FileFormatClassification.StandardXlsx, classification.Classification);
        Assert.Equal(18, classification.PackagePartCount);
        Assert.Equal(14, classification.RelationshipCount);

        WorkbookMetadata metadata = new WorkbookMetadataReader().Read(samplePath);
        Assert.Equal(18, metadata.PackagePartCount);
        Assert.Equal(14, metadata.RelationshipCount);
        Assert.Collection(
            metadata.Worksheets,
            worksheet => AssertSheet(worksheet, "Old", "A1:AF531", 32),
            worksheet => AssertSheet(worksheet, "Original", "A1:L531", 12),
            worksheet => AssertSheet(worksheet, "Final", "A1:AD531", 30));

        ColumnMappingSuggestionResult suggestions = new ColumnMappingSuggester().Suggest(metadata);
        WorksheetMappingSuggestion suggestion = Assert.IsType<WorksheetMappingSuggestion>(
            suggestions.SuggestedWorksheet);
        InputSnapshot after = snapshots.Capture(samplePath);
        InputSnapshotComparison comparison = snapshots.Recheck(samplePath, before);
        Assert.Equal(before, after);
        Assert.True(comparison.Sha256Matches);
        Assert.True(comparison.SizeMatches);
        Assert.True(comparison.LastWriteTimeUtcMatches);
        Assert.True(comparison.IsMatch);

        Assert.Equal("Original", suggestion.WorksheetName);
        Assert.Equal(1U, suggestion.HeaderRow);
        Assert.Equal(2U, suggestion.FirstDataRow);
        Assert.Equal(531U, suggestion.LastDataRow);
        Assert.Equal(530U, suggestion.SuggestedDataRowCount);
        Assert.Contains("I", suggestion.InitialTargetColumns);
        Assert.Equal(["F", "G", "H", "I", "J", "K"], suggestion.InitialTargetColumns);
        Assert.Equal(["A", "B", "C", "D", "E", "L"], suggestion.InitiallyUnselectedColumns);

        IReadOnlyDictionary<string, ColumnMappingCandidate> candidates = suggestion.Candidates
            .ToDictionary(candidate => candidate.ColumnName, StringComparer.Ordinal);
        Assert.Equal(
            ["F", "G", "H", "I", "J", "K"],
            candidates.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(ColumnMappingCandidateRole.PrimaryAnswer, candidates["F"].Roles);
        Assert.Equal(
            ColumnMappingCandidateRole.PrimaryAnswer | ColumnMappingCandidateRole.StudentPromptPrimary,
            candidates["G"].Roles);
        Assert.Equal(
            ColumnMappingCandidateRole.PrimaryAnswer | ColumnMappingCandidateRole.Supporting,
            candidates["H"].Roles);
        Assert.Equal(ColumnMappingCandidateRole.PrimaryAnswer, candidates["I"].Roles);
        Assert.Equal(
            ColumnMappingCandidateRole.PrimaryAnswer | ColumnMappingCandidateRole.StudentPromptPrimary,
            candidates["J"].Roles);
        Assert.Equal(ColumnMappingCandidateRole.Supporting, candidates["K"].Roles);
        Assert.Empty(candidates["G"].SuggestedSupportingColumns);
        Assert.Equal(["K"], candidates["J"].SuggestedSupportingColumns);
    }

    private static void AssertSheet(
        WorksheetMetadata worksheet,
        string expectedName,
        string expectedDimension,
        uint expectedColumns)
    {
        Assert.Equal(expectedName, worksheet.Name);
        Assert.Equal(expectedDimension, worksheet.DimensionReference);
        Assert.Equal(1U, worksheet.FirstRowIndex);
        Assert.Equal(531U, worksheet.LastRowIndex);
        Assert.Equal(531U, worksheet.RowCount);
        Assert.Equal(1U, worksheet.FirstColumnIndex);
        Assert.Equal(expectedColumns, worksheet.LastColumnIndex);
        Assert.Equal(expectedColumns, worksheet.ColumnCount);
    }
}

public sealed class WindowsX64PerformanceEvidenceTests
{
    private const int MeasurementCount = 3;
    private const decimal ThresholdSeconds = 30m;

    [Fact]
    public void Synthetic_531_row_read_write_validation_median_is_recorded_and_within_target()
    {
        E02RepositoryLayout.RequireWindowsX64();
        decimal[] measurements = new decimal[MeasurementCount];
        for (int index = 0; index < measurements.Length; index++)
        {
            measurements[index] = MeasureSyntheticReadWriteValidation();
        }

        decimal median = measurements.Order().ElementAt(MeasurementCount / 2);
        DateTimeOffset measuredAtUtc = DateTimeOffset.UtcNow;
        string artifactPath = Path.Combine(
            E02RepositoryLayout.FindRepositoryRoot(),
            "artifacts",
            "test",
            "performance-windows-x64.json");
        WritePerformanceEvidence(artifactPath, measuredAtUtc, measurements, median);
        AssertPerformanceEvidenceSchema(artifactPath, measurements, median);

        Assert.True(
            median <= ThresholdSeconds,
            $"The measured median {median} seconds exceeded the {ThresholdSeconds} second target.");
    }

    private static decimal MeasureSyntheticReadWriteValidation()
    {
        using SyntheticWorkbook workbook = SyntheticWorkbookFactory.Create();
        Stopwatch stopwatch = Stopwatch.StartNew();
        InputSnapshotService snapshots = new();
        InputSnapshot inputBefore = snapshots.Capture(workbook.Path);

        FileFormatClassificationResult classification = new FileFormatClassifier().Classify(workbook.Path);
        Assert.True(classification.IsAccepted);
        WorkbookMetadata metadata = new WorkbookMetadataReader().Read(workbook.Path);
        WorksheetMappingSuggestion mappingSuggestion = Assert.IsType<WorksheetMappingSuggestion>(
            new ColumnMappingSuggester().Suggest(metadata).SuggestedWorksheet);
        Assert.Contains(
            mappingSuggestion.Candidates,
            candidate => candidate.ColumnName == "F"
                && candidate.IsPrimaryCandidate);

        QuantificationDefinition definition = CreatePerformanceDefinition();
        ColumnMappingValidationResult mapping = new ColumnMappingValidator().Validate(metadata, definition);
        Assert.True(mapping.IsValid);
        Assert.Equal(SyntheticWorkbookFactory.DataRowCount, mapping.Mapping!.SelectedRowCount);
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(definition);
        ImmutableArray<ResultsSheetRowInput> rows = CreatePerformanceRows();
        string outputPath = Path.Combine(workbook.RootDirectory, "performance-output.xlsx");

        using (WorkingPackage package = WorkingPackage.Create(workbook.Path, outputPath))
        {
            AppOwnedSheetNames sheetNames;
            ResultsSheetWriteResult results;
            DateTimeOffset runStartedAtUtc = DateTimeOffset.UtcNow;
            using (SpreadsheetDocument document = package.OpenForEditing())
            {
                sheetNames = new AppOwnedSheetNameResolver().Resolve(document);
                ConfigCellAddressMap config = new ConfigSheetWriter().Write(
                    document,
                    snapshot,
                    sheetNames);
                results = new ResultsSheetWriter().Write(
                    document,
                    snapshot,
                    sheetNames,
                    config,
                    rows);
                new RunSheetWriter().Write(
                    document,
                    new RunSheetMetadata
                    {
                        InputIdentity = inputBefore,
                        DefinitionSha256 = snapshot.Sha256,
                        ApplicationIdentity = "StudyReportEvaluator.App/E-02-performance",
                        CopilotSdkIdentity = "GitHub.Copilot.SDK/not-invoked",
                        CopilotCliIdentity = "copilot/not-invoked",
                        ModelIdentity = "synthetic-no-ai-wait",
                        StartedAtUtc = runStartedAtUtc,
                        EndedAtUtc = DateTimeOffset.UtcNow,
                        PlannedEvaluationCount = SyntheticWorkbookFactory.DataRowCount,
                        CompletedEvaluationCount = SyntheticWorkbookFactory.DataRowCount,
                        ErrorCount = 0,
                        SheetNames = sheetNames,
                    });
                new CalculationPropertiesWriter().Write(document);
            }

            Assert.Equal(SyntheticWorkbookFactory.DataRowCount, results.DataRowCount);
            Assert.Equal(SyntheticWorkbookFactory.DataRowCount * 5, results.FormulaCells.Length);
            ImmutableArray<ExpectedFormulaCell> expectedFormulas = results.FormulaCells
                .Select(cell => new ExpectedFormulaCell(cell.Definition, cell.CachedValue))
                .ToImmutableArray();
            OutputPackageValidationPlan validationPlan = OutputPackageValidationPlan.Capture(
                workbook.Path,
                sheetNames,
                expectedFormulas);
            OutputPackageValidationResult validation = new OutputPackageValidator().Validate(
                package.TemporaryPath,
                validationPlan,
                TestContext.Current.CancellationToken);
            Assert.True(
                validation.IsValid,
                string.Join(Environment.NewLine, validation.Errors.Select(error => error.ToString())));

            AtomicOutputCommitResult commit = new AtomicOutputCommitter().Commit(
                package,
                outputPath,
                workbook.Path,
                inputBefore,
                validationPlan,
                TestContext.Current.CancellationToken);
            Assert.True(commit.IsSuccess);
            Assert.True(File.Exists(outputPath));
            Assert.True(snapshots.Recheck(workbook.Path, inputBefore).IsMatch);
        }

        stopwatch.Stop();
        File.Delete(outputPath);
        Assert.False(File.Exists(outputPath));
        return decimal.Round(
            (decimal)stopwatch.Elapsed.TotalSeconds,
            6,
            MidpointRounding.AwayFromZero);
    }

    private static QuantificationDefinition CreatePerformanceDefinition() =>
        new()
        {
            Id = "DEF-E02-PERFORMANCE",
            Name = "E-02 synthetic performance definition",
            Revision = "1",
            SourceSheet = SyntheticWorkbookFactory.SourceSheetName,
            HeaderRow = SyntheticWorkbookFactory.HeaderRow,
            FirstDataRow = SyntheticWorkbookFactory.FirstDataRow,
            LastDataRow = SyntheticWorkbookFactory.LastDataRow,
            RoundingDigits = 2,
            Questions =
            [
                new QuestionDefinition
                {
                    Id = "Q-PERFORMANCE",
                    DisplayName = "Synthetic performance question",
                    QuestionText = "Synthetic performance question",
                    PrimarySourceColumn = "F",
                    SupportingSourceColumns = [],
                    Weight = 1m,
                    Evaluators =
                    [
                        new EvaluatorDefinition
                        {
                            Id = "E-PERFORMANCE",
                            DisplayName = "Synthetic performance evaluator",
                            Type = EvaluatorType.CustomPrompt,
                            Weight = 1m,
                            Range = new ScoreRange(0m, 10m),
                            CustomPromptTemplate = "{回答}\n{評価項目}",
                            Criteria =
                            [
                                new CriterionDefinition
                                {
                                    Id = "C-PERFORMANCE",
                                    DisplayName = "Synthetic performance criterion",
                                    Description = "Synthetic performance criterion",
                                    Weight = 1m,
                                },
                            ],
                        },
                    ],
                },
            ],
        };

    private static ImmutableArray<ResultsSheetRowInput> CreatePerformanceRows() =>
        Enumerable.Range(
                SyntheticWorkbookFactory.FirstDataRow,
                SyntheticWorkbookFactory.DataRowCount)
            .Select(sourceRow => new ResultsSheetRowInput
            {
                SourceRowNumber = sourceRow,
                Questions =
                [
                    new QuestionResultInput
                    {
                        QuestionId = "Q-PERFORMANCE",
                        Scorable = true,
                        Evaluators =
                        [
                            new EvaluatorResultInput
                            {
                                EvaluatorId = "E-PERFORMANCE",
                                Status = ResultsStatusCodes.Success,
                                AiResult = new QuantificationResult
                                {
                                    EvaluatorId = "E-PERFORMANCE",
                                    Criteria =
                                    [
                                        new CriterionQuantificationResult
                                        {
                                            CriterionId = "C-PERFORMANCE",
                                            RawScore = (sourceRow - SyntheticWorkbookFactory.FirstDataRow) % 11,
                                            Reason = "synthetic",
                                            Evidence = string.Empty,
                                            EvidenceSource = EvidenceSourceKind.None,
                                            EvidenceSourceColumnId = string.Empty,
                                        },
                                    ],
                                },
                            },
                        ],
                    },
                ],
            })
            .ToImmutableArray();

    private static void WritePerformanceEvidence(
        string artifactPath,
        DateTimeOffset measuredAtUtc,
        IReadOnlyList<decimal> measurements,
        decimal median)
    {
        string? directory = Path.GetDirectoryName(artifactPath);
        if (directory is null)
        {
            throw new InvalidOperationException("The performance artifact directory is unavailable.");
        }

        Directory.CreateDirectory(directory);
        using FileStream stream = new(
            artifactPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16 * 1024,
            FileOptions.WriteThrough);
        using Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("schema_version", "1.0");
        writer.WriteString("task", "E-02");
        writer.WriteString("measurement_date_utc", measuredAtUtc.ToString("yyyy-MM-dd"));
        writer.WriteString("measured_at_utc", measuredAtUtc.ToString("O"));
        writer.WriteStartObject("environment");
        writer.WriteString("os_description", RuntimeInformation.OSDescription);
        writer.WriteString("os_architecture", RuntimeInformation.OSArchitecture.ToString());
        writer.WriteString("process_architecture", RuntimeInformation.ProcessArchitecture.ToString());
        writer.WriteNumber("logical_processor_count", Environment.ProcessorCount);
        writer.WriteString("dotnet_version", Environment.Version.ToString());
        writer.WriteString("framework_description", RuntimeInformation.FrameworkDescription);
        writer.WriteEndObject();
        writer.WriteStartObject("scenario");
        writer.WriteString("name", "synthetic-531-row-read-write-validation");
        writer.WriteNumber("worksheet_rows", SyntheticWorkbookFactory.LastDataRow);
        writer.WriteNumber("data_rows", SyntheticWorkbookFactory.DataRowCount);
        writer.WriteNumber("run_count", MeasurementCount);
        writer.WriteBoolean("fixture_generation_included", false);
        writer.WriteBoolean("ai_wait_included", false);
        writer.WriteString(
            "operation_scope",
            "classification,metadata,mapping,copy,write,reopen-validation,atomic-commit,input-recheck");
        writer.WriteEndObject();
        writer.WriteNumber("threshold_seconds", ThresholdSeconds);
        writer.WriteStartArray("measurements_seconds");
        foreach (decimal measurement in measurements)
        {
            writer.WriteNumberValue(measurement);
        }

        writer.WriteEndArray();
        writer.WriteNumber("median_seconds", median);
        writer.WriteString("result", median <= ThresholdSeconds ? "PASS" : "FAIL");
        writer.WriteBoolean("content_data_included", false);
        writer.WriteEndObject();
        writer.Flush();
        stream.WriteByte((byte)'\n');
        stream.Flush(flushToDisk: true);
    }

    private static void AssertPerformanceEvidenceSchema(
        string artifactPath,
        IReadOnlyList<decimal> measurements,
        decimal median)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(artifactPath));
        JsonElement root = document.RootElement;
        Assert.Equal(
            [
                "schema_version",
                "task",
                "measurement_date_utc",
                "measured_at_utc",
                "environment",
                "scenario",
                "threshold_seconds",
                "measurements_seconds",
                "median_seconds",
                "result",
                "content_data_included",
            ],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("1.0", root.GetProperty("schema_version").GetString());
        Assert.Equal("E-02", root.GetProperty("task").GetString());
        Assert.Equal(MeasurementCount, root.GetProperty("measurements_seconds").GetArrayLength());
        Assert.Equal(measurements, root.GetProperty("measurements_seconds")
            .EnumerateArray()
            .Select(value => value.GetDecimal()));
        Assert.Equal(median, root.GetProperty("median_seconds").GetDecimal());
        Assert.Equal(
            median <= ThresholdSeconds ? "PASS" : "FAIL",
            root.GetProperty("result").GetString());
        Assert.False(root.GetProperty("content_data_included").GetBoolean());
    }
}

internal static class E02RepositoryLayout
{
    internal static void RequireWindowsX64()
    {
        Assert.True(
            OperatingSystem.IsWindows(),
            "E-02 is a required Windows-only acceptance test for the initial target.");
        Assert.True(
            OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22_000),
            "E-02 requires Windows 11 or later for the initial target.");
        Assert.Equal(Architecture.X64, RuntimeInformation.OSArchitecture);
        Assert.Equal(Architecture.X64, RuntimeInformation.ProcessArchitecture);
    }

    internal static string GetRequiredSamplePath()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "sample",
            "機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The required repository sample workbook is missing.");
        }

        return path;
    }


    internal static string FindRepositoryRoot()
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
}
