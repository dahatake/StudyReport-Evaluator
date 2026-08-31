using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using StudyReportEvaluator.App.Tests.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Formulas;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Workbooks.Writing;

public sealed class ConfigAndRunSheetWriterTests
{
    private const string QuestionBodyCanary = "=QUESTION-BODY-CANARY";
    private const string PromptBodyCanary = "+PROMPT-BODY-CANARY {回答} {評価項目}";
    private const string CriterionBodyCanary = "@CRITERION-BODY-CANARY";

    [Fact]
    public void Writers_preserve_original_sheets_and_reopen_with_complete_literal_metadata()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        AddSheets(
            input.Path,
            "Quantification_Config",
            "quantification_config (2)",
            "Quantification_Results",
            "QUANTIFICATION_RUN");
        IReadOnlyDictionary<string, string> originalWorksheets = ReadWorksheetXml(input.Path);
        InputSnapshotService snapshots = new();
        InputSnapshot inputIdentity = snapshots.Capture(input.Path);
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateDefinition());
        string targetDirectory = Path.Combine(input.Directory, "target");
        Directory.CreateDirectory(targetDirectory);
        string finalPath = Path.Combine(targetDirectory, "result.xlsx");
        AppOwnedSheetNames? names = null;
        ConfigCellAddressMap? addresses = null;
        string temporaryPath;

        {
            using WorkingPackage package = WorkingPackage.Create(input.Path, finalPath);
            temporaryPath = package.TemporaryPath;
            using (SpreadsheetDocument document = package.OpenForEditing())
            {
                names = new AppOwnedSheetNameResolver().Resolve(document);
                Assert.Equal("Quantification_Config (3)", names.ConfigSheetName);
                Assert.Equal("Quantification_Results (2)", names.ResultsSheetName);
                Assert.Equal("Quantification_Run (2)", names.RunSheetName);

                addresses = new ConfigSheetWriter().Write(document, snapshot, names);
                new RunSheetWriter().Write(
                    document,
                    CreateRunMetadata(inputIdentity, snapshot.Sha256, names));
                new CalculationPropertiesWriter().Write(document);
            }

            Assert.True(File.Exists(temporaryPath));
            Assert.False(File.Exists(finalPath));
            Assert.True(snapshots.Recheck(input.Path, inputIdentity).IsMatch);

            using SpreadsheetDocument reopened = SpreadsheetDocument.Open(temporaryPath, false);
            Assert.Empty(new OpenXmlValidator().Validate(reopened, TestContext.Current.CancellationToken));
            AssertOriginalWorksheetsUnchanged(reopened, originalWorksheets);
            AssertConfigSheet(reopened, snapshot, names!, addresses!);
            AssertRunSheet(reopened, inputIdentity, snapshot.Sha256, names!);
            AssertCalculationProperties(reopened);
        }

        Assert.False(File.Exists(temporaryPath));
        Assert.False(File.Exists(finalPath));
        Assert.True(snapshots.Recheck(input.Path, inputIdentity).IsMatch);
    }

    [Fact]
    public void Config_layout_is_deterministic_for_the_same_snapshot_and_binding()
    {
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateDefinition());
        using TemporaryWorkbook firstInput = X01SyntheticWorkbookFactory.Create();
        using TemporaryWorkbook secondInput = X01SyntheticWorkbookFactory.Create();
        string firstXml = WriteAndReadConfigXml(firstInput, snapshot);
        string secondXml = WriteAndReadConfigXml(secondInput, snapshot);

        Assert.Equal(firstXml, secondXml);
    }

    [Fact]
    public void Run_metadata_validation_happens_before_a_sheet_is_added_and_ToString_is_redacted()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        InputSnapshot identity = new InputSnapshotService().Capture(input.Path);
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateDefinition());
        using SpreadsheetDocument document = SpreadsheetDocument.Open(input.Path, true);
        AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
        RunSheetMetadata invalid = CreateRunMetadata(identity, snapshot.Sha256, names) with
        {
            EndedAtUtc = new DateTimeOffset(2026, 9, 1, 9, 59, 59, TimeSpan.Zero),
        };
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Synthetic workbook part is missing.");
        Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing.");
        int before = workbook.Descendants<Sheet>().Count();

        Assert.Throws<ArgumentException>(() => new RunSheetWriter().Write(document, invalid));

        Assert.Equal(before, workbook.Descendants<Sheet>().Count());
        Assert.DoesNotContain(
            workbook.Descendants<Sheet>(),
            sheet => string.Equals(sheet.Name?.Value, names.RunSheetName, StringComparison.OrdinalIgnoreCase));
        string representation = invalid.ToString();
        Assert.Contains("<redacted>", representation, StringComparison.Ordinal);
        Assert.DoesNotContain(snapshot.Sha256, representation, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-model", representation, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_writer_records_nonnegative_error_count_without_inventing_a_completed_count_relation()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        InputSnapshot identity = new InputSnapshotService().Capture(input.Path);
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateDefinition());
        using SpreadsheetDocument document = SpreadsheetDocument.Open(input.Path, true);
        AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
        RunSheetMetadata metadata = CreateRunMetadata(identity, snapshot.Sha256, names) with
        {
            PlannedEvaluationCount = 1,
            CompletedEvaluationCount = 0,
            ErrorCount = 2,
        };

        new RunSheetWriter().Write(document, metadata);

        Row errorRow = GetWorksheet(document, names.RunSheetName)
            .Descendants<Row>()
            .Single(row => Text(row, "A") == "ErrorCount");
        Assert.Equal("2", Text(errorRow, "B"));
    }

    private static void AssertConfigSheet(
        SpreadsheetDocument document,
        QuantificationSnapshot snapshot,
        AppOwnedSheetNames names,
        ConfigCellAddressMap addresses)
    {
        Worksheet worksheet = GetWorksheet(document, names.ConfigSheetName);
        Row[] rows = worksheet.Descendants<Row>().ToArray();
        Assert.Empty(worksheet.Descendants<CellFormula>());
        Assert.All(
            worksheet.Descendants<Cell>().Where(IsTextualCell),
            cell => Assert.Equal(CellValues.InlineString, cell.DataType?.Value));

        Row definition = FindRecord(rows, "DEFINITION", snapshot.Definition.Id);
        Assert.Equal(snapshot.Definition.SourceSheet, Text(definition, "K"));
        Assert.Equal("1", Text(definition, "L"));
        Assert.Equal("2", Text(definition, "M"));
        Assert.Equal("3", Text(definition, "N"));
        Assert.Equal("2", Text(definition, "U"));
        Assert.Equal("3.0", Text(definition, "V"));
        Assert.Equal(snapshot.Sha256, Text(definition, "W"));

        string reconstructedSnapshot = string.Concat(
            rows.Where(row => Text(row, "A") == "CANONICAL_JSON")
                .OrderBy(row => int.Parse(Text(row, "X"), CultureInfo.InvariantCulture))
                .Select(row => Text(row, "Y")));
        Assert.Equal(snapshot.CanonicalJson, reconstructedSnapshot);
        Assert.Contains(QuestionBodyCanary, reconstructedSnapshot, StringComparison.Ordinal);
        Assert.Contains("PROMPT-BODY-CANARY", reconstructedSnapshot, StringComparison.Ordinal);

        Row question = FindRecord(rows, "QUESTION", "Q1");
        Assert.Equal(QuestionBodyCanary, Text(question, "G"));
        Assert.Equal("G", Text(question, "O"));
        Assert.Equal("1", Text(question, "Q"));
        Assert.Equal("3", Text(question, "R"));
        Assert.Equal(["H", "K"], rows
            .Where(row => Text(row, "A") == "SUPPORTING_SOURCE" && Text(row, "D") == "Q1")
            .OrderBy(row => int.Parse(Text(row, "X"), CultureInfo.InvariantCulture))
            .Select(row => Text(row, "P")));

        Row evaluator = FindRecord(rows, "EVALUATOR", "E1");
        Assert.Equal(PromptBodyCanary, Text(evaluator, "H"));
        Assert.Equal("CUSTOM_PROMPT", Text(evaluator, "J"));
        Assert.Equal("2", Text(evaluator, "R"));
        Assert.Equal("1", Text(evaluator, "S"));
        Assert.Equal("10", Text(evaluator, "T"));

        Row inheritedCriterion = FindRecord(rows, "CRITERION", "C1");
        Assert.Equal(CriterionBodyCanary, Text(inheritedCriterion, "G"));
        Assert.Equal("4", Text(inheritedCriterion, "R"));
        Assert.Equal("1", Text(inheritedCriterion, "S"));
        Assert.Equal("10", Text(inheritedCriterion, "T"));
        Row explicitCriterion = FindRecord(rows, "CRITERION", "C2");
        Assert.Equal("0", Text(explicitCriterion, "Q"));
        Assert.Equal("-5", Text(explicitCriterion, "S"));
        Assert.Equal("5", Text(explicitCriterion, "T"));

        Assert.Equal(names.ConfigSheetName, addresses.SheetName);
        AssertAddressValue(worksheet, addresses.RoundingDigitsCell, "2");
        AssertAddressValue(worksheet, addresses.QuestionWeightCells["Q1"], "3");
        AssertAddressValue(worksheet, addresses.EvaluatorWeightCells["E1"], "2");
        AssertAddressValue(worksheet, addresses.CriterionWeightCells["C1"], "4");
        AssertAddressValue(worksheet, addresses.CriterionMinimumCells["C1"], "1");
        AssertAddressValue(worksheet, addresses.CriterionMaximumCells["C2"], "5");
        Assert.Equal(addresses.VerifiedCells.Count, addresses.VerifiedCells.Distinct().Count());
        Assert.All(addresses.VerifiedCells, address => Assert.Equal(names.ConfigSheetName, address.SheetName));
        AssertReadOnly(addresses.QuestionWeightCells, "Injected", new FormulaCellAddress("Injected", "A", 1));
        IList<FormulaCellAddress> verified = Assert.IsAssignableFrom<IList<FormulaCellAddress>>(addresses.VerifiedCells);
        Assert.True(verified.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => verified.Add(new FormulaCellAddress("Injected", "A", 1)));
        Assert.Contains("<redacted>", addresses.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(snapshot.Sha256, addresses.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(QuestionBodyCanary, addresses.ToString(), StringComparison.Ordinal);

        Cell questionCanaryCell = worksheet.Descendants<Cell>()
            .Single(cell => cell.InnerText == QuestionBodyCanary);
        Cell promptCanaryCell = worksheet.Descendants<Cell>()
            .Single(cell => cell.InnerText == PromptBodyCanary);
        Assert.Equal(CellValues.InlineString, questionCanaryCell.DataType?.Value);
        Assert.Equal(CellValues.InlineString, promptCanaryCell.DataType?.Value);
        Assert.Null(questionCanaryCell.CellFormula);
        Assert.Null(promptCanaryCell.CellFormula);
    }

    private static void AssertRunSheet(
        SpreadsheetDocument document,
        InputSnapshot inputIdentity,
        string definitionSha256,
        AppOwnedSheetNames names)
    {
        Worksheet worksheet = GetWorksheet(document, names.RunSheetName);
        Assert.Empty(worksheet.Descendants<CellFormula>());
        Dictionary<string, Cell> records = worksheet.Descendants<Row>()
            .Skip(1)
            .ToDictionary(row => Text(row, "A"), row => Cell(row, "B"), StringComparer.Ordinal);

        Assert.Equal(inputIdentity.Sha256, records["InputSha256"].InnerText);
        Assert.Equal(inputIdentity.SizeBytes.ToString(CultureInfo.InvariantCulture), records["InputSizeBytes"].InnerText);
        Assert.Equal(inputIdentity.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture), records["InputLastWriteTimeUtc"].InnerText);
        Assert.Equal(definitionSha256, records["DefinitionSha256"].InnerText);
        Assert.Equal("StudyReportEvaluator.App/3.0.0-test", records["ApplicationIdentity"].InnerText);
        Assert.Equal("GitHub.Copilot.SDK/1.0.11", records["CopilotSdkIdentity"].InnerText);
        Assert.Equal("copilot/1.0.82", records["CopilotCliIdentity"].InnerText);
        Assert.Equal("synthetic-model", records["ModelIdentity"].InnerText);
        Assert.StartsWith("DocumentFormat.OpenXml/3.5.1", records["OpenXmlSdkIdentity"].InnerText, StringComparison.Ordinal);
        Assert.Equal("6", records["PlannedEvaluationCount"].InnerText);
        Assert.Equal("5", records["CompletedEvaluationCount"].InnerText);
        Assert.Equal("1", records["ErrorCount"].InnerText);
        Assert.Equal(names.ConfigSheetName, records["ConfigSheetName"].InnerText);
        Assert.Equal(names.ResultsSheetName, records["ResultsSheetName"].InnerText);
        Assert.Equal(names.RunSheetName, records["RunSheetName"].InnerText);
        Assert.Equal(CellValues.Number, records["InputSizeBytes"].DataType?.Value);
        Assert.Equal(CellValues.Number, records["ErrorCount"].DataType?.Value);
        Assert.Equal(CellValues.InlineString, records["DefinitionSha256"].DataType?.Value);
        Assert.Equal(CellValues.InlineString, records["ModelIdentity"].DataType?.Value);

        string allRunText = worksheet.InnerText;
        Assert.DoesNotContain(QuestionBodyCanary, allRunText, StringComparison.Ordinal);
        Assert.DoesNotContain(PromptBodyCanary, allRunText, StringComparison.Ordinal);
        Assert.DoesNotContain(CriterionBodyCanary, allRunText, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic answer body", allRunText, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic reason body", allRunText, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic evidence body", allRunText, StringComparison.Ordinal);
    }

    private static void AssertCalculationProperties(SpreadsheetDocument document)
    {
        Workbook workbook = document.WorkbookPart?.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing.");
        CalculationProperties properties = Assert.IsType<CalculationProperties>(
            workbook.CalculationProperties);
        Assert.Equal(CalculateModeValues.Auto, properties.CalculationMode?.Value);
        Assert.True(properties.FullCalculationOnLoad?.Value == true);
        Assert.True(properties.ForceFullCalculation?.Value == true);
    }

    private static RunSheetMetadata CreateRunMetadata(
        InputSnapshot inputIdentity,
        string definitionSha256,
        AppOwnedSheetNames names) =>
        new()
        {
            InputIdentity = inputIdentity,
            DefinitionSha256 = definitionSha256,
            ApplicationIdentity = "StudyReportEvaluator.App/3.0.0-test",
            CopilotSdkIdentity = "GitHub.Copilot.SDK/1.0.11",
            CopilotCliIdentity = "copilot/1.0.82",
            ModelIdentity = "synthetic-model",
            StartedAtUtc = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero),
            EndedAtUtc = new DateTimeOffset(2026, 9, 1, 10, 1, 0, TimeSpan.Zero),
            PlannedEvaluationCount = 6,
            CompletedEvaluationCount = 5,
            ErrorCount = 1,
            SheetNames = names,
        };

    private static QuantificationDefinition CreateDefinition()
    {
        EvaluatorDefinition enabledEvaluator = new()
        {
            Id = "E1",
            DisplayName = "Synthetic custom evaluator",
            Type = EvaluatorType.CustomPrompt,
            Weight = 2m,
            Range = new ScoreRange(1m, 10m),
            CustomPromptTemplate = PromptBodyCanary,
            Enabled = true,
            Criteria =
            [
                new CriterionDefinition
                {
                    Id = "C1",
                    DisplayName = "Inherited range criterion",
                    Description = CriterionBodyCanary,
                    Weight = 4m,
                    Enabled = true,
                },
                new CriterionDefinition
                {
                    Id = "C2",
                    DisplayName = "Explicit range criterion",
                    Description = "Synthetic disabled criterion description",
                    Weight = 1m,
                    Range = new ScoreRange(-5m, 5m),
                    Enabled = false,
                },
            ],
        };
        QuestionDefinition enabledQuestion = new()
        {
            Id = "Q1",
            DisplayName = "Synthetic enabled question",
            QuestionText = QuestionBodyCanary,
            PrimarySourceColumn = "G",
            SupportingSourceColumns = ["H", "K"],
            Weight = 3m,
            Enabled = true,
            Evaluators = [enabledEvaluator],
        };
        EvaluatorDefinition disabledEvaluator = new()
        {
            Id = "E2",
            DisplayName = "Synthetic disabled knowledge evaluator",
            Type = EvaluatorType.KnowledgeCoverage,
            Weight = 7m,
            Range = new ScoreRange(0m, 30m),
            BuiltInTemplateVersion = "knowledge-v1",
            Enabled = false,
            Criteria =
            [
                new CriterionDefinition
                {
                    Id = "C3",
                    DisplayName = "Synthetic disabled criterion",
                    Description = "Synthetic disabled knowledge point",
                    Weight = 9m,
                    Enabled = false,
                },
            ],
        };
        QuestionDefinition disabledQuestion = new()
        {
            Id = "Q2",
            DisplayName = "Synthetic disabled question",
            QuestionText = "Synthetic disabled question text",
            PrimarySourceColumn = "C",
            SupportingSourceColumns = [],
            Weight = 5m,
            Enabled = false,
            Evaluators = [disabledEvaluator],
        };
        return new QuantificationDefinition
        {
            Id = "DEF-X03",
            Name = "Synthetic X-03 definition",
            Revision = "7",
            SourceSheet = "Original",
            HeaderRow = 1,
            FirstDataRow = 2,
            LastDataRow = 3,
            RoundingDigits = 2,
            Questions = [enabledQuestion, disabledQuestion],
        };
    }

    private static string WriteAndReadConfigXml(
        TemporaryWorkbook input,
        QuantificationSnapshot snapshot)
    {
        string targetDirectory = Path.Combine(input.Directory, "target");
        Directory.CreateDirectory(targetDirectory);
        using WorkingPackage package = WorkingPackage.Create(
            input.Path,
            Path.Combine(targetDirectory, "result.xlsx"));
        string configName;
        using (SpreadsheetDocument document = package.OpenForEditing())
        {
            AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
            configName = names.ConfigSheetName;
            _ = new ConfigSheetWriter().Write(document, snapshot, names);
        }

        using SpreadsheetDocument reopened = SpreadsheetDocument.Open(package.TemporaryPath, false);
        return GetWorksheet(reopened, configName).OuterXml;
    }

    private static void AddSheets(string path, params string[] names)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, true);
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Synthetic workbook part is missing.");
        Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing.");
        Sheets sheets = workbook.GetFirstChild<Sheets>()
            ?? throw new InvalidDataException("Synthetic sheet collection is missing.");
        uint sheetId = sheets.Elements<Sheet>()
            .Max(sheet => sheet.SheetId?.Value
                ?? throw new InvalidDataException("Synthetic sheet ID is missing.")) + 1;
        foreach (string name in names)
        {
            WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId++,
                Name = name,
            });
        }

        workbook.Save();
    }

    private static IReadOnlyDictionary<string, string> ReadWorksheetXml(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, false);
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Synthetic workbook part is missing.");
        Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing.");
        Dictionary<string, string> worksheets = new(StringComparer.Ordinal);
        foreach (Sheet sheet in workbook.Descendants<Sheet>())
        {
            string name = sheet.Name?.Value
                ?? throw new InvalidDataException("Synthetic sheet name is missing.");
            string relationshipId = sheet.Id?.Value
                ?? throw new InvalidDataException("Synthetic sheet relationship is missing.");
            Worksheet worksheet = ((WorksheetPart)workbookPart.GetPartById(relationshipId)).Worksheet
                ?? throw new InvalidDataException("Synthetic worksheet root is missing.");
            worksheets.Add(name, worksheet.OuterXml);
        }

        return worksheets;
    }

    private static void AssertOriginalWorksheetsUnchanged(
        SpreadsheetDocument document,
        IReadOnlyDictionary<string, string> expected)
    {
        Workbook workbook = document.WorkbookPart?.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing.");
        Sheet[] sheets = workbook.Descendants<Sheet>().ToArray();
        Assert.Equal(expected.Count + 2, sheets.Length);
        foreach ((string name, string worksheetXml) in expected)
        {
            Assert.Equal(worksheetXml, GetWorksheet(document, name).OuterXml);
        }
    }

    private static Worksheet GetWorksheet(SpreadsheetDocument document, string name)
    {
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Synthetic workbook part is missing.");
        Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing.");
        Sheet sheet = workbook.Descendants<Sheet>()
            .Single(candidate => string.Equals(candidate.Name?.Value, name, StringComparison.Ordinal));
        string relationshipId = sheet.Id?.Value
            ?? throw new InvalidDataException("Synthetic sheet relationship is missing.");
        return ((WorksheetPart)workbookPart.GetPartById(relationshipId)).Worksheet
            ?? throw new InvalidDataException("Synthetic worksheet root is missing.");
    }

    private static Row FindRecord(IEnumerable<Row> rows, string recordType, string nodeId) =>
        rows.Single(row => Text(row, "A") == recordType && Text(row, "C") == nodeId);

    private static void AssertAddressValue(
        Worksheet worksheet,
        FormulaCellAddress address,
        string expected)
    {
        Cell cell = worksheet.Descendants<Cell>()
            .Single(candidate => string.Equals(
                candidate.CellReference?.Value,
                address.ColumnName + address.RowNumber.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal));
        Assert.Equal(expected, cell.InnerText);
    }

    private static void AssertReadOnly(
        IReadOnlyDictionary<string, FormulaCellAddress> values,
        string attemptedKey,
        FormulaCellAddress attemptedValue)
    {
        IDictionary<string, FormulaCellAddress> dictionary =
            Assert.IsAssignableFrom<IDictionary<string, FormulaCellAddress>>(values);
        Assert.True(dictionary.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => dictionary.Add(attemptedKey, attemptedValue));
    }

    private static bool IsTextualCell(Cell cell)
    {
        CellValues? dataType = cell.DataType?.Value;
        return dataType == CellValues.InlineString
            || dataType == CellValues.String
            || dataType == CellValues.SharedString;
    }

    private static string Text(Row row, string column)
    {
        Cell? cell = row.Elements<Cell>().FirstOrDefault(candidate => string.Equals(
            Column(candidate.CellReference?.Value),
            column,
            StringComparison.Ordinal));
        return cell?.InnerText ?? string.Empty;
    }

    private static Cell Cell(Row row, string column) =>
        row.Elements<Cell>().Single(candidate => string.Equals(
            Column(candidate.CellReference?.Value),
            column,
            StringComparison.Ordinal));

    private static string Column(string? reference) =>
        new((reference ?? string.Empty).TakeWhile(char.IsAsciiLetter).ToArray());
}
