using System.Collections.Immutable;
using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using StudyReportEvaluator.App.Tests.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Formulas;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Workbooks.Writing;

public sealed class ResultsSheetWriterTests
{
    private const string ReasonA = "=REASON-A-CANARY";
    private const string EvidenceA = "+EVIDENCE-A-CANARY";
    private const string ReasonB = "-REASON-B-CANARY";
    private const string EvidenceB = "@EVIDENCE-B-CANARY";

    [Fact]
    public void Reopened_results_use_enabled_stable_id_columns_safe_literals_golden_formulas_and_rounded_cached_oracle()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        AddSheets(
            input.Path,
            AppOwnedSheetNameResolver.ConfigBaseName,
            AppOwnedSheetNameResolver.ResultsBaseName,
            AppOwnedSheetNameResolver.RunBaseName);
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateOracleDefinition(2, 2));
        AppOwnedSheetNames names;
        ConfigCellAddressMap config;
        ResultsSheetWriteResult writeResult;

        using (SpreadsheetDocument document = SpreadsheetDocument.Open(input.Path, true))
        {
            names = new AppOwnedSheetNameResolver().Resolve(document);
            Assert.Equal("Quantification_Config (2)", names.ConfigSheetName);
            Assert.Equal("Quantification_Results (2)", names.ResultsSheetName);
            config = new ConfigSheetWriter().Write(document, snapshot, names);
            writeResult = new ResultsSheetWriter().Write(
                document,
                snapshot,
                names,
                config,
                [OracleRow(2)]);
        }

        using SpreadsheetDocument reopened = SpreadsheetDocument.Open(input.Path, false);
        Assert.Empty(new OpenXmlValidator().Validate(reopened, TestContext.Current.CancellationToken));
        Worksheet worksheet = GetWorksheet(reopened, names.ResultsSheetName);
        Assert.Equal(names.ResultsSheetName, writeResult.SheetName);
        Assert.Equal(1, writeResult.HeaderRow);
        Assert.Equal(1, writeResult.DataRowCount);
        Assert.Equal(23, writeResult.ColumnCount);
        Assert.Equal(7, writeResult.FormulaCells.Length);
        Assert.Equal(
            writeResult.FormulaCells.Length,
            writeResult.FormulaCells.Select(item => item.Definition.Target).Distinct().Count());
        Assert.Equal(
            CachedDecimal(Cell(worksheet, "W2")),
            Assert.Single(
                writeResult.FormulaCells,
                item => item.Definition.Identity.Field == ResultsSheetWriter.OverallScoreHeader).CachedValue);
        Assert.All(
            writeResult.FormulaCells,
            item => Assert.Contains("<redacted>", item.ToString(), StringComparison.Ordinal));
        Assert.Contains("<redacted>", writeResult.ToString(), StringComparison.Ordinal);

        string[] expectedHeaders =
        [
            "Q1.E1.C-A.Scorable",
            "Q1.E1.C-A.AI_Raw",
            "Q1.E1.C-A.Override",
            "Q1.E1.C-A.Effective_Raw",
            "Q1.E1.C-A.Normalized",
            "Q1.E1.C-A.Reason",
            "Q1.E1.C-A.Evidence",
            "Q1.E1.C-A.Evidence_Source",
            "Q1.E1.C-A.Evidence_SourceColumn",
            "Q1.E1.C-A.Status",
            "Q1.E1.C-B.Scorable",
            "Q1.E1.C-B.AI_Raw",
            "Q1.E1.C-B.Override",
            "Q1.E1.C-B.Effective_Raw",
            "Q1.E1.C-B.Normalized",
            "Q1.E1.C-B.Reason",
            "Q1.E1.C-B.Evidence",
            "Q1.E1.C-B.Evidence_Source",
            "Q1.E1.C-B.Evidence_SourceColumn",
            "Q1.E1.C-B.Status",
            "Q1.E1.Evaluator_Score",
            "Q1.Question_Score",
            "Overall_Score",
        ];
        Row header = worksheet.Descendants<Row>().Single(row => row.RowIndex?.Value == 1);
        Assert.Equal(expectedHeaders, header.Elements<Cell>().Select(cell => cell.InnerText));
        Assert.All(header.Elements<Cell>(), cell => Assert.Equal(CellValues.InlineString, cell.DataType?.Value));
        Assert.DoesNotContain("C-DISABLED", header.InnerText, StringComparison.Ordinal);
        Assert.DoesNotContain("E-DISABLED", header.InnerText, StringComparison.Ordinal);
        Assert.DoesNotContain("Q-DISABLED", header.InnerText, StringComparison.Ordinal);

        AssertNumber(Cell(worksheet, "A2"), "1");
        AssertNumber(Cell(worksheet, "B2"), "25");
        AssertTrulyBlank(Cell(worksheet, "C2"));
        AssertNumber(Cell(worksheet, "K2"), "1");
        AssertNumber(Cell(worksheet, "L2"), "8");
        AssertTrulyBlank(Cell(worksheet, "M2"));
        AssertInline(Cell(worksheet, "F2"), ReasonA);
        AssertInline(Cell(worksheet, "G2"), EvidenceA);
        AssertInline(Cell(worksheet, "H2"), "PRIMARY_ANSWER");
        AssertInline(Cell(worksheet, "I2"), "G");
        AssertInline(Cell(worksheet, "J2"), ResultsStatusCodes.Success);
        AssertInline(Cell(worksheet, "P2"), ReasonB);
        AssertInline(Cell(worksheet, "Q2"), EvidenceB);
        AssertInline(Cell(worksheet, "R2"), "SUPPORTING_COLUMN");
        AssertInline(Cell(worksheet, "S2"), "H");

        string resultsName = Quote(names.ResultsSheetName);
        string aMin = ConfigReference(config.CriterionMinimumCells["C-A"]);
        string aMax = ConfigReference(config.CriterionMaximumCells["C-A"]);
        string bMin = ConfigReference(config.CriterionMinimumCells["C-B"]);
        string bMax = ConfigReference(config.CriterionMaximumCells["C-B"]);
        string rounding = ConfigReference(config.RoundingDigitsCell);
        string weightA = ConfigReference(config.CriterionWeightCells["C-A"]);
        string weightB = ConfigReference(config.CriterionWeightCells["C-B"]);
        string evaluatorWeight = ConfigReference(config.EvaluatorWeightCells["E1"]);
        string questionWeight = ConfigReference(config.QuestionWeightCells["Q1"]);
        AssertFormula(
            Cell(worksheet, "D2"),
            $"IF(({resultsName}!A2<>1),\"\",IF(({resultsName}!C2=\"\"),IF(ISNUMBER({resultsName}!B2),IF(({resultsName}!B2<{aMin}),\"\",IF(({resultsName}!B2>{aMax}),\"\",{resultsName}!B2)),\"\"),IF(ISNUMBER({resultsName}!C2),IF(({resultsName}!C2<{aMin}),\"\",IF(({resultsName}!C2>{aMax}),\"\",{resultsName}!C2)),\"\")))",
            "25");
        AssertFormula(
            Cell(worksheet, "E2"),
            $"IF(ISNUMBER({resultsName}!D2),IFERROR(ROUND(((({resultsName}!D2-{aMin})/({aMax}-{aMin}))*100),{rounding}),\"\"),\"\")",
            "83.3");
        AssertFormula(
            Cell(worksheet, "O2"),
            $"IF(ISNUMBER({resultsName}!N2),IFERROR(ROUND(((({resultsName}!N2-{bMin})/({bMax}-{bMin}))*100),{rounding}),\"\"),\"\")",
            "77.8");
        string evaluatorFormula =
            $"IF((COUNT({resultsName}!E2,{resultsName}!O2)=2),IFERROR(ROUND((SUM(({resultsName}!E2*{weightA}),({resultsName}!O2*{weightB}))/SUM({weightA},{weightB})),{rounding}),\"\"),\"\")";
        AssertFormula(Cell(worksheet, "U2"), evaluatorFormula, "81.5");
        Assert.DoesNotContain("E2:O2", evaluatorFormula, StringComparison.Ordinal);
        Assert.DoesNotContain("C-DISABLED", string.Concat(worksheet.Descendants<CellFormula>().Select(formula => formula.Text)), StringComparison.Ordinal);
        Assert.DoesNotContain("83.3", evaluatorFormula, StringComparison.Ordinal);
        Assert.Contains(weightA, evaluatorFormula, StringComparison.Ordinal);
        Assert.Contains(weightB, evaluatorFormula, StringComparison.Ordinal);
        AssertFormula(
            Cell(worksheet, "V2"),
            $"IF((COUNT({resultsName}!U2)=1),IFERROR(ROUND((SUM(({resultsName}!U2*{evaluatorWeight}))/SUM({evaluatorWeight})),{rounding}),\"\"),\"\")",
            "81.5");
        AssertFormula(
            Cell(worksheet, "W2"),
            $"IF((COUNT({resultsName}!V2)=1),IFERROR(ROUND((SUM(({resultsName}!V2*{questionWeight}))/SUM({questionWeight})),{rounding}),\"\"),\"\")",
            "81.5");
        Assert.All(
            worksheet.Descendants<CellFormula>(),
            formula => Assert.False(formula.Text.StartsWith("=", StringComparison.Ordinal)));

        decimal oracleA = decimal.Round(((25m - 0m) / (30m - 0m)) * 100m, 1, MidpointRounding.AwayFromZero);
        decimal oracleB = decimal.Round(((8m - 1m) / (10m - 1m)) * 100m, 1, MidpointRounding.AwayFromZero);
        decimal oracleEvaluator = decimal.Round(((oracleA * 2m) + (oracleB * 1m)) / 3m, 1, MidpointRounding.AwayFromZero);
        Assert.Equal(83.3m, oracleA);
        Assert.Equal(77.8m, oracleB);
        Assert.Equal(81.5m, oracleEvaluator);
        Assert.Equal(oracleA, CachedDecimal(Cell(worksheet, "E2")));
        Assert.Equal(oracleB, CachedDecimal(Cell(worksheet, "O2")));
        Assert.Equal(oracleEvaluator, CachedDecimal(Cell(worksheet, "U2")));
        Assert.Equal(oracleEvaluator, CachedDecimal(Cell(worksheet, "V2")));
        Assert.Equal(oracleEvaluator, CachedDecimal(Cell(worksheet, "W2")));

        DataValidation[] validations = worksheet.Descendants<DataValidation>().ToArray();
        Assert.Equal(2, validations.Length);
        AssertDecimalValidation(validations.Single(item => item.SequenceOfReferences?.InnerText == "C2"), aMin, aMax);
        AssertDecimalValidation(validations.Single(item => item.SequenceOfReferences?.InnerText == "M2"), bMin, bMax);
        Assert.NotNull(GetWorksheet(reopened, AppOwnedSheetNameResolver.ResultsBaseName));
    }

    [Fact]
    public void Empty_failed_cancelled_and_valid_override_rows_keep_blank_semantics_and_custom_empty_validation()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateSingleCriterionDefinition(2, 5));
        AppOwnedSheetNames names;
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(input.Path, true))
        {
            names = new AppOwnedSheetNameResolver().Resolve(document);
            ConfigCellAddressMap config = new ConfigSheetWriter().Write(document, snapshot, names);
            new ResultsSheetWriter().Write(
                document,
                snapshot,
                names,
                config,
                [
                    SingleCriterionRow(2, scorable: false, ResultsStatusCodes.Empty),
                    SingleCriterionRow(3, scorable: true, ResultsStatusCodes.Cancelled),
                    SingleCriterionRow(4, scorable: true, ResultsStatusCodes.AiOutputInvalid, overrideValue: "5"),
                    SingleCriterionRow(5, scorable: true, ResultsStatusCodes.Success, overrideValue: "4", aiRaw: 11m),
                ]);
        }

        using SpreadsheetDocument reopened = SpreadsheetDocument.Open(input.Path, false);
        Worksheet worksheet = GetWorksheet(reopened, names.ResultsSheetName);
        Assert.Empty(new OpenXmlValidator().Validate(reopened, TestContext.Current.CancellationToken));

        AssertNumber(Cell(worksheet, "A2"), "0");
        AssertTrulyBlank(Cell(worksheet, "B2"));
        AssertTrulyBlank(Cell(worksheet, "C2"));
        AssertFormulaWithoutCache(Cell(worksheet, "D2"));
        AssertFormulaWithoutCache(Cell(worksheet, "E2"));
        AssertFormulaWithoutCache(Cell(worksheet, "K2"));
        AssertFormulaWithoutCache(Cell(worksheet, "L2"));
        AssertFormulaWithoutCache(Cell(worksheet, "M2"));
        AssertInline(Cell(worksheet, "J2"), ResultsStatusCodes.Empty);

        AssertNumber(Cell(worksheet, "A3"), "1");
        AssertTrulyBlank(Cell(worksheet, "B3"));
        AssertTrulyBlank(Cell(worksheet, "C3"));
        AssertFormulaWithoutCache(Cell(worksheet, "D3"));
        AssertFormulaWithoutCache(Cell(worksheet, "M3"));
        AssertInline(Cell(worksheet, "J3"), ResultsStatusCodes.Cancelled);

        AssertTrulyBlank(Cell(worksheet, "B4"));
        AssertNumber(Cell(worksheet, "C4"), "5");
        AssertFormula(Cell(worksheet, "D4"), Cell(worksheet, "D4").CellFormula?.Text ?? string.Empty, "5");
        AssertFormula(Cell(worksheet, "E4"), Cell(worksheet, "E4").CellFormula?.Text ?? string.Empty, "50");
        AssertFormula(Cell(worksheet, "M4"), Cell(worksheet, "M4").CellFormula?.Text ?? string.Empty, "50");
        AssertInline(Cell(worksheet, "J4"), ResultsStatusCodes.AiOutputInvalid);

        AssertNumber(Cell(worksheet, "B5"), "11");
        AssertNumber(Cell(worksheet, "C5"), "4");
        Assert.Equal("4", Cell(worksheet, "D5").CellValue?.Text);
        Assert.Equal("40", Cell(worksheet, "E5").CellValue?.Text);
        string effectiveFormula = Cell(worksheet, "D5").CellFormula?.Text ?? string.Empty;
        Assert.Contains("C5=\"\"", effectiveFormula, StringComparison.Ordinal);
        Assert.Contains("ISNUMBER", effectiveFormula, StringComparison.Ordinal);
        Assert.DoesNotContain("MIN(", effectiveFormula, StringComparison.Ordinal);
        Assert.DoesNotContain("MAX(", effectiveFormula, StringComparison.Ordinal);

        DataValidation[] validations = worksheet.Descendants<DataValidation>().ToArray();
        Assert.Equal(4, validations.Length);
        DataValidation emptyValidation = validations.Single(item => item.SequenceOfReferences?.InnerText == "C2");
        Assert.Equal(DataValidationValues.Custom, emptyValidation.Type?.Value);
        Assert.True(emptyValidation.AllowBlank?.Value == true);
        Assert.Contains("C2=\"\"", emptyValidation.Formula1?.Text ?? string.Empty, StringComparison.Ordinal);
        Assert.All(
            validations.Where(item => item.SequenceOfReferences?.InnerText != "C2"),
            item => Assert.Equal(DataValidationValues.Decimal, item.Type?.Value));
    }

    [Theory]
    [InlineData("PRIVATE-NONNUMERIC-CANARY", "OVERRIDE_NOT_NUMERIC")]
    [InlineData("-0.1", "OVERRIDE_OUT_OF_RANGE")]
    [InlineData("10.1", "OVERRIDE_OUT_OF_RANGE")]
    public void Invalid_app_side_override_returns_a_field_error_and_refuses_before_results_mutation(
        string overrideValue,
        string expectedCode)
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateSingleCriterionDefinition(2, 2));
        using SpreadsheetDocument document = SpreadsheetDocument.Open(input.Path, true);
        AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
        ConfigCellAddressMap config = new ConfigSheetWriter().Write(document, snapshot, names);
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Synthetic workbook part is missing.");
        Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing.");
        int sheetCount = workbook.Descendants<Sheet>().Count();
        int worksheetPartCount = workbookPart.WorksheetParts.Count();
        ResultsSheetRowInput row = SingleCriterionRow(
            2,
            scorable: true,
            ResultsStatusCodes.AiOutputInvalid,
            overrideValue: overrideValue);

        ResultsSheetValidationException exception = Assert.Throws<ResultsSheetValidationException>(
            () => new ResultsSheetWriter().Write(document, snapshot, names, config, [row]));

        ResultsSheetValidationError error = Assert.Single(exception.Errors, item => item.Code == expectedCode);
        Assert.Equal("Override", error.Field);
        Assert.Equal("C1", error.NodeId);
        Assert.Equal(sheetCount, workbook.Descendants<Sheet>().Count());
        Assert.Equal(worksheetPartCount, workbookPart.WorksheetParts.Count());
        Assert.DoesNotContain(workbook.Descendants<Sheet>(), sheet => sheet.Name?.Value == names.ResultsSheetName);
        Assert.DoesNotContain(overrideValue, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(overrideValue, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(overrideValue, row.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Override_for_scorable_zero_is_rejected_before_mutation()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateSingleCriterionDefinition(2, 2));
        using SpreadsheetDocument document = SpreadsheetDocument.Open(input.Path, true);
        AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
        ConfigCellAddressMap config = new ConfigSheetWriter().Write(document, snapshot, names);
        int before = document.WorkbookPart?.WorksheetParts.Count() ?? 0;

        ResultsSheetValidationException exception = Assert.Throws<ResultsSheetValidationException>(
            () => new ResultsSheetWriter().Write(
                document,
                snapshot,
                names,
                config,
                [SingleCriterionRow(2, scorable: false, ResultsStatusCodes.Empty, overrideValue: "5")]));

        Assert.Contains(exception.Errors, error => error.Code == "OVERRIDE_NOT_ALLOWED" && error.Field == "Override");
        Assert.Equal(before, document.WorkbookPart?.WorksheetParts.Count() ?? 0);
    }

    [Fact]
    public void Reserved_results_name_must_still_be_absent_when_write_starts()
    {
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(CreateSingleCriterionDefinition(2, 2));
        using SpreadsheetDocument document = SpreadsheetDocument.Open(input.Path, true);
        AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
        ConfigCellAddressMap config = new ConfigSheetWriter().Write(document, snapshot, names);
        AddSheet(document, names.ResultsSheetName);
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Synthetic workbook part is missing.");
        Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing.");
        int sheetCount = workbook.Descendants<Sheet>().Count();
        int partCount = workbookPart.WorksheetParts.Count();
        string sentinelXml = GetWorksheet(document, names.ResultsSheetName).OuterXml;

        ResultsSheetValidationException exception = Assert.Throws<ResultsSheetValidationException>(
            () => new ResultsSheetWriter().Write(
                document,
                snapshot,
                names,
                config,
                [SingleCriterionRow(2, true, ResultsStatusCodes.Cancelled)]));

        Assert.Contains(exception.Errors, error => error.Code == "RESULTS_SHEET_ALREADY_EXISTS");
        Assert.Equal(sheetCount, workbook.Descendants<Sheet>().Count());
        Assert.Equal(partCount, workbookPart.WorksheetParts.Count());
        Assert.Equal(sentinelXml, GetWorksheet(document, names.ResultsSheetName).OuterXml);
    }

    [Fact]
    public void Excel_column_limit_is_rejected_before_results_mutation()
    {
        const int criterionCount = 1_639;
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(
            CreateManyCriteriaDefinition(criterionCount));
        using SpreadsheetDocument document = SpreadsheetDocument.Open(input.Path, true);
        AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
        ConfigCellAddressMap config = new ConfigSheetWriter().Write(document, snapshot, names);
        int before = document.WorkbookPart?.WorksheetParts.Count() ?? 0;

        ResultsSheetValidationException exception = Assert.Throws<ResultsSheetValidationException>(
            () => new ResultsSheetWriter().Write(document, snapshot, names, config, []));

        ResultsSheetValidationError error = Assert.Single(exception.Errors, item => item.Code == "COLUMN_LIMIT_EXCEEDED");
        Assert.Equal((criterionCount * 10 + 3).ToString(CultureInfo.InvariantCulture), error.SafeOffendingValue);
        Assert.Equal(before, document.WorkbookPart?.WorksheetParts.Count() ?? 0);
        Assert.DoesNotContain(Workbook(document).Descendants<Sheet>(), sheet => sheet.Name?.Value == names.ResultsSheetName);
    }

    [Fact]
    public void Formula_function_argument_preflight_rejects_before_results_mutation()
    {
        const int criterionCount = 256;
        using TemporaryWorkbook input = X01SyntheticWorkbookFactory.Create();
        QuantificationSnapshot snapshot = QuantificationSnapshot.Create(
            CreateManyCriteriaDefinition(criterionCount));
        using SpreadsheetDocument document = SpreadsheetDocument.Open(input.Path, true);
        AppOwnedSheetNames names = new AppOwnedSheetNameResolver().Resolve(document);
        ConfigCellAddressMap config = new ConfigSheetWriter().Write(document, snapshot, names);
        int before = document.WorkbookPart?.WorksheetParts.Count() ?? 0;

        ResultsSheetValidationException exception = Assert.Throws<ResultsSheetValidationException>(
            () => new ResultsSheetWriter().Write(
                document,
                snapshot,
                names,
                config,
                [SingleEvaluatorRow(2, ResultsStatusCodes.Cancelled)]));

        Assert.Contains(
            exception.Errors,
            error => error.Code == "FUNCTION_ARGUMENT_LIMIT_EXCEEDED"
                && error.NodeKind == "Evaluator"
                && error.NodeId == "E1"
                && error.SafeOffendingValue == "256");
        Assert.Equal(before, document.WorkbookPart?.WorksheetParts.Count() ?? 0);
            Assert.DoesNotContain(Workbook(document).Descendants<Sheet>(), sheet => sheet.Name?.Value == names.ResultsSheetName);
    }

    private static QuantificationDefinition CreateOracleDefinition(int firstRow, int lastRow)
    {
        EvaluatorDefinition enabledEvaluator = new()
        {
            Id = "E1",
            DisplayName = "Enabled evaluator",
            Type = EvaluatorType.CustomPrompt,
            Weight = 7m,
            Range = new ScoreRange(1m, 10m),
            CustomPromptTemplate = "{回答} {評価項目}",
            Enabled = true,
            Criteria =
            [
                new CriterionDefinition
                {
                    Id = "C-A",
                    DisplayName = "Criterion A",
                    Description = "Synthetic criterion A",
                    Weight = 2m,
                    Range = new ScoreRange(0m, 30m),
                    Enabled = true,
                },
                new CriterionDefinition
                {
                    Id = "C-DISABLED",
                    DisplayName = "Disabled criterion",
                    Description = "Synthetic disabled criterion",
                    Weight = 999m,
                    Enabled = false,
                },
                new CriterionDefinition
                {
                    Id = "C-B",
                    DisplayName = "Criterion B",
                    Description = "Synthetic criterion B",
                    Weight = 1m,
                    Enabled = true,
                },
            ],
        };
        EvaluatorDefinition disabledEvaluator = new()
        {
            Id = "E-DISABLED",
            DisplayName = "Disabled evaluator",
            Type = EvaluatorType.CustomPrompt,
            Weight = 999m,
            Range = new ScoreRange(0m, 100m),
            CustomPromptTemplate = "{回答} {評価項目}",
            Enabled = false,
            Criteria =
            [
                new CriterionDefinition
                {
                    Id = "C-DESCENDANT",
                    DisplayName = "Disabled descendant",
                    Description = "Synthetic disabled descendant",
                    Weight = 999m,
                    Enabled = true,
                },
            ],
        };
        QuestionDefinition enabledQuestion = new()
        {
            Id = "Q1",
            DisplayName = "Enabled question",
            QuestionText = "Synthetic question",
            PrimarySourceColumn = "G",
            SupportingSourceColumns = ["H"],
            Weight = 3m,
            Enabled = true,
            Evaluators = [enabledEvaluator, disabledEvaluator],
        };
        QuestionDefinition disabledQuestion = new()
        {
            Id = "Q-DISABLED",
            DisplayName = "Disabled question",
            QuestionText = "Synthetic disabled question",
            PrimarySourceColumn = "K",
            Weight = 999m,
            Enabled = false,
            Evaluators =
            [
                new EvaluatorDefinition
                {
                    Id = "E-DESCENDANT",
                    DisplayName = "Disabled question evaluator",
                    Type = EvaluatorType.CustomPrompt,
                    Weight = 999m,
                    Range = new ScoreRange(0m, 100m),
                    CustomPromptTemplate = "{回答} {評価項目}",
                    Enabled = true,
                    Criteria =
                    [
                        new CriterionDefinition
                        {
                            Id = "C-Q-DESCENDANT",
                            DisplayName = "Disabled question criterion",
                            Description = "Synthetic disabled question criterion",
                            Weight = 999m,
                            Enabled = true,
                        },
                    ],
                },
            ],
        };
        return new QuantificationDefinition
        {
            Id = "DEF-X04-ORACLE",
            Name = "X-04 oracle definition",
            Revision = "1",
            SourceSheet = "Original",
            HeaderRow = 1,
            FirstDataRow = firstRow,
            LastDataRow = lastRow,
            RoundingDigits = 1,
            Questions = [enabledQuestion, disabledQuestion],
        };
    }

    private static QuantificationDefinition CreateSingleCriterionDefinition(int firstRow, int lastRow) =>
        new()
        {
            Id = "DEF-X04-SINGLE",
            Name = "X-04 single criterion definition",
            Revision = "1",
            SourceSheet = "Original",
            HeaderRow = 1,
            FirstDataRow = firstRow,
            LastDataRow = lastRow,
            RoundingDigits = 1,
            Questions =
            [
                new QuestionDefinition
                {
                    Id = "Q1",
                    DisplayName = "Question",
                    QuestionText = "Synthetic question",
                    PrimarySourceColumn = "G",
                    Weight = 1m,
                    Evaluators =
                    [
                        new EvaluatorDefinition
                        {
                            Id = "E1",
                            DisplayName = "Evaluator",
                            Type = EvaluatorType.CustomPrompt,
                            Weight = 1m,
                            Range = new ScoreRange(0m, 10m),
                            CustomPromptTemplate = "{回答} {評価項目}",
                            Criteria =
                            [
                                new CriterionDefinition
                                {
                                    Id = "C1",
                                    DisplayName = "Criterion",
                                    Description = "Synthetic criterion",
                                    Weight = 1m,
                                },
                            ],
                        },
                    ],
                },
            ],
        };

    private static QuantificationDefinition CreateManyCriteriaDefinition(int criterionCount)
    {
        ImmutableArray<CriterionDefinition> criteria = Enumerable.Range(1, criterionCount)
            .Select(index => new CriterionDefinition
            {
                Id = $"C{index.ToString(CultureInfo.InvariantCulture)}",
                DisplayName = $"Criterion {index.ToString(CultureInfo.InvariantCulture)}",
                Description = "Synthetic criterion",
                Weight = 1m,
            })
            .ToImmutableArray();
        QuantificationDefinition single = CreateSingleCriterionDefinition(2, 2);
        EvaluatorDefinition evaluator = single.Questions[0].Evaluators[0] with { Criteria = criteria };
        QuestionDefinition question = single.Questions[0] with { Evaluators = [evaluator] };
        return single with
        {
            Id = $"DEF-X04-MANY-{criterionCount.ToString(CultureInfo.InvariantCulture)}",
            Questions = [question],
        };
    }

    private static ResultsSheetRowInput OracleRow(int sourceRow) =>
        new()
        {
            SourceRowNumber = sourceRow,
            Questions =
            [
                new QuestionResultInput
                {
                    QuestionId = "Q1",
                    Scorable = true,
                    Evaluators =
                    [
                        new EvaluatorResultInput
                        {
                            EvaluatorId = "E1",
                            Status = ResultsStatusCodes.Success,
                            AiResult = new QuantificationResult
                            {
                                EvaluatorId = "E1",
                                Criteria =
                                [
                                    CriterionResult("C-B", 8m, ReasonB, EvidenceB, EvidenceSourceKind.SupportingColumn, "H"),
                                    CriterionResult("C-A", 25m, ReasonA, EvidenceA, EvidenceSourceKind.PrimaryAnswer, "G"),
                                ],
                            },
                        },
                    ],
                },
            ],
        };

    private static ResultsSheetRowInput SingleCriterionRow(
        int sourceRow,
        bool scorable,
        string status,
        string? overrideValue = null,
        decimal? aiRaw = null) =>
        new()
        {
            SourceRowNumber = sourceRow,
            Questions =
            [
                new QuestionResultInput
                {
                    QuestionId = "Q1",
                    Scorable = scorable,
                    Evaluators =
                    [
                        new EvaluatorResultInput
                        {
                            EvaluatorId = "E1",
                            Status = status,
                            AiResult = aiRaw is decimal raw
                                ? new QuantificationResult
                                {
                                    EvaluatorId = "E1",
                                    Criteria =
                                    [
                                        CriterionResult("C1", raw, "synthetic reason", "", EvidenceSourceKind.None, ""),
                                    ],
                                }
                                : null,
                            Overrides = overrideValue is null
                                ? []
                                : [new CriterionOverrideInput { CriterionId = "C1", Value = overrideValue }],
                        },
                    ],
                },
            ],
        };

    private static ResultsSheetRowInput SingleEvaluatorRow(int sourceRow, string status) =>
        new()
        {
            SourceRowNumber = sourceRow,
            Questions =
            [
                new QuestionResultInput
                {
                    QuestionId = "Q1",
                    Scorable = true,
                    Evaluators = [new EvaluatorResultInput { EvaluatorId = "E1", Status = status }],
                },
            ],
        };

    private static CriterionQuantificationResult CriterionResult(
        string id,
        decimal score,
        string reason,
        string evidence,
        EvidenceSourceKind source,
        string sourceColumn) =>
        new()
        {
            CriterionId = id,
            RawScore = score,
            Reason = reason,
            Evidence = evidence,
            EvidenceSource = source,
            EvidenceSourceColumnId = sourceColumn,
        };

    private static void AssertDecimalValidation(DataValidation validation, string minimum, string maximum)
    {
        Assert.Equal(DataValidationValues.Decimal, validation.Type?.Value);
        Assert.Equal(DataValidationOperatorValues.Between, validation.Operator?.Value);
        Assert.True(validation.AllowBlank?.Value == true);
        Assert.True(validation.ShowErrorMessage?.Value == true);
        Assert.Equal(minimum, validation.Formula1?.Text);
        Assert.Equal(maximum, validation.Formula2?.Text);
    }

    private static void AssertFormula(Cell cell, string expectedFormula, string expectedCache)
    {
        Assert.Equal(expectedFormula, cell.CellFormula?.Text);
        Assert.False((cell.CellFormula?.Text ?? string.Empty).StartsWith("=", StringComparison.Ordinal));
        Assert.Equal(CellValues.Number, cell.DataType?.Value);
        Assert.Equal(expectedCache, cell.CellValue?.Text);
    }

    private static void AssertFormulaWithoutCache(Cell cell)
    {
        Assert.NotNull(cell.CellFormula);
        Assert.Null(cell.CellValue);
        Assert.Null(cell.DataType);
    }

    private static void AssertNumber(Cell cell, string expected)
    {
        Assert.Equal(CellValues.Number, cell.DataType?.Value);
        Assert.Equal(expected, cell.CellValue?.Text);
        Assert.Null(cell.CellFormula);
    }

    private static void AssertTrulyBlank(Cell cell)
    {
        Assert.Null(cell.DataType);
        Assert.Null(cell.CellValue);
        Assert.Null(cell.CellFormula);
        Assert.Null(cell.InlineString);
        Assert.Equal(string.Empty, cell.InnerText);
    }

    private static void AssertInline(Cell cell, string expected)
    {
        Assert.Equal(CellValues.InlineString, cell.DataType?.Value);
        Assert.Equal(expected, cell.InlineString?.Text?.Text);
        Assert.Null(cell.CellFormula);
    }

    private static decimal CachedDecimal(Cell cell) =>
        decimal.Parse(cell.CellValue?.Text ?? throw new InvalidDataException("Cached value is missing."), CultureInfo.InvariantCulture);

    private static string ConfigReference(FormulaCellAddress address) =>
        $"{Quote(address.SheetName)}!${address.NormalizedColumnName}${address.RowNumber.ToString(CultureInfo.InvariantCulture)}";

    private static string Quote(string sheetName) =>
        $"'{sheetName.Replace("'", "''", StringComparison.Ordinal)}'";

    private static Cell Cell(Worksheet worksheet, string reference) =>
        worksheet.Descendants<Cell>().Single(cell => string.Equals(cell.CellReference?.Value, reference, StringComparison.Ordinal));

    private static Worksheet GetWorksheet(SpreadsheetDocument document, string name)
    {
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Synthetic workbook part is missing.");
        Sheet sheet = (workbookPart.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing."))
            .Descendants<Sheet>()
            .Single(candidate => string.Equals(candidate.Name?.Value, name, StringComparison.Ordinal));
        string relationshipId = sheet.Id?.Value
            ?? throw new InvalidDataException("Synthetic worksheet relationship is missing.");
        return ((WorksheetPart)workbookPart.GetPartById(relationshipId)).Worksheet
            ?? throw new InvalidDataException("Synthetic worksheet root is missing.");
    }

    private static void AddSheets(string path, params string[] names)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, true);
        foreach (string name in names)
        {
            AddSheet(document, name);
        }
    }

    private static void AddSheet(SpreadsheetDocument document, string name)
    {
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Synthetic workbook part is missing.");
        Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Synthetic workbook root is missing.");
        Sheets sheets = workbook.GetFirstChild<Sheets>()
            ?? throw new InvalidDataException("Synthetic sheet collection is missing.");
        uint nextId = sheets.Elements<Sheet>().Max(sheet => sheet.SheetId?.Value ?? 0) + 1;
        WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
        part.Worksheet = new Worksheet(new SheetData());
        part.Worksheet.Save();
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(part),
            SheetId = nextId,
            Name = name,
        });
        workbook.Save();
    }

    private static Workbook Workbook(SpreadsheetDocument document) =>
        document.WorkbookPart?.Workbook
        ?? throw new InvalidDataException("Synthetic workbook root is missing.");
}
