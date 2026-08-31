using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;

internal static class Program
{
    private const string WorksheetContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
    private const string WorksheetRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
    private const string RunMarker = "StudyReportEvaluator.Evaluation_Run/v1";
    private static readonly int[] RoundingDigits = Enumerable.Range(-400, 801).ToArray();

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && string.Equals(args[0], "inspect", StringComparison.Ordinal))
            {
                Require(args.Length == 4, "Usage: inspect <workbook> <engine> <engine-version>");
                Console.WriteLine(JsonSerializer.Serialize(InspectEngineWorkbook(args[1], args[2], args[3]), JsonOptions));
                return 0;
            }
            if (args.Length > 0 && string.Equals(args[0], "compare", StringComparison.Ordinal))
            {
                Require(args.Length == 3, "Usage: compare <first-engine-evidence> <second-engine-evidence>");
                Console.WriteLine(JsonSerializer.Serialize(CompareEngineEvidence(args[1], args[2]), JsonOptions));
                return 0;
            }

            Require(args.Length == 3 && string.Equals(args[0], "run", StringComparison.Ordinal), "Usage: run <repository-root> <work-directory>");
            string repositoryRoot = Path.GetFullPath(args[1]);
            string workDirectory = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(workDirectory);

            LocalEvidence evidence = RunLocalProbe(repositoryRoot, workDirectory);
            string evidencePath = Path.Combine(workDirectory, "g16-local-evidence.json");
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, JsonOptions), new UTF8Encoding(false));
            Console.WriteLine(JsonSerializer.Serialize(evidence, JsonOptions));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"G16_FAIL {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    private static LocalEvidence RunLocalProbe(string repositoryRoot, string workDirectory)
    {
        string formulaSpecPath = Path.Combine(repositoryRoot, "tests", "fixtures", "specs", "formula.json");
        string unicodeSpecPath = Path.Combine(repositoryRoot, "tests", "fixtures", "specs", "unicode.json");
        Require(File.Exists(formulaSpecPath), "G-11 formula fixture is missing.");
        Require(File.Exists(unicodeSpecPath), "G-11 Unicode fixture is missing.");

        string inputPath = Path.Combine(workDirectory, "g16-input.xlsx");
        string outputPath = Path.Combine(workDirectory, "g16-output.xlsx");
        File.Delete(inputPath);
        File.Delete(outputPath);

        CreateSyntheticInput(inputPath);
        FileSnapshot before = SnapshotFile(inputPath);
        PackageFingerprint inputFingerprint = FingerprintPackage(inputPath);
        WorkbookSnapshot inputWorkbook = ReadWorkbookSnapshot(inputPath);

        File.Copy(inputPath, outputPath, overwrite: false);
        MutateWorkingCopy(outputPath, formulaSpecPath);

        FileSnapshot after = SnapshotFile(inputPath);
        Require(before == after, "Input workbook identity changed during copy-on-write mutation.");

        PackageFingerprint outputFingerprint = FingerprintPackage(outputPath);
        WorkbookSnapshot outputWorkbook = ReadWorkbookSnapshot(outputPath);
        VerifyPackageDelta(inputFingerprint, outputFingerprint, inputWorkbook, outputWorkbook);
        InlineStringEvidence inlineEvidence = VerifyInlineStrings(outputPath, formulaSpecPath, inputFingerprint, outputFingerprint);
        ValidationEvidence validationEvidence = ValidateOpenXml(outputPath);
        UnicodeEvidence unicodeEvidence = VerifyUnicode(unicodeSpecPath);
        IcuEvidence icuEvidence = ReadIcuEvidence();

        string packageVersion = typeof(SpreadsheetDocument).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(SpreadsheetDocument).Assembly.GetName().Version?.ToString()
            ?? "unknown";

        string[] nativeIcuFiles = Directory.GetFiles(AppContext.BaseDirectory, "icu*", SearchOption.AllDirectories);
        var nativeIcuHashes = nativeIcuFiles
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new FileHash(Path.GetFileName(path), Sha256(path)))
            .ToArray();

        return new LocalEvidence(
            Schema: "study-report-evaluator/g16-local-evidence/v1",
            CapturedAtUtc: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            OperatingSystem: Environment.OSVersion.VersionString,
            RuntimeIdentifier: System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            FrameworkDescription: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OpenXmlPackageVersion: packageVersion,
            AppLocalIcuPackageVersion: "72.1.0.3",
            Input: before,
            Output: SnapshotFile(outputPath),
            InputEntryCount: inputFingerprint.EntryHashes.Count,
            OutputEntryCount: outputFingerprint.EntryHashes.Count,
            AddedEntryNames: outputFingerprint.EntryHashes.Keys.Except(inputFingerprint.EntryHashes.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            ChangedExistingEntryNames: inputFingerprint.EntryHashes.Keys.Where(name => inputFingerprint.EntryHashes[name] != outputFingerprint.EntryHashes[name]).Order(StringComparer.Ordinal).ToArray(),
            ExistingRelationshipCount: inputFingerprint.Relationships.Count,
            AddedRelationshipCount: outputFingerprint.Relationships.Except(inputFingerprint.Relationships, StringComparer.Ordinal).Count(),
            SheetNames: outputWorkbook.Sheets.Select(sheet => sheet.Name).ToArray(),
            CalculationModeAuto: outputWorkbook.CalculationMode == CalculateModeValues.Auto,
            FullCalculationOnLoad: outputWorkbook.FullCalculationOnLoad,
            ForceFullCalculation: outputWorkbook.ForceFullCalculation,
            FormulaProbeMinimumDigit: RoundingDigits.Min(),
            FormulaProbeMaximumDigit: RoundingDigits.Max(),
            FormulaCellCount: outputWorkbook.FormulaCellCount,
            InlineStrings: inlineEvidence,
            Validation: validationEvidence,
            Unicode: unicodeEvidence,
            Icu: icuEvidence,
            NativeIcuFiles: nativeIcuHashes,
            InputWorkbookPath: Path.GetFileName(inputPath),
            OutputWorkbookPath: Path.GetFileName(outputPath));
    }

    private static void CreateSyntheticInput(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>("rIdOriginal");
        SheetData sheetData = new(
            new Row(
                SharedStringCell("A1", 0),
                InlineStringCell("B1", "Synthetic formula")),
            new Row(
                InlineStringCell("A2", "Synthetic value only"),
                new Cell
                {
                    CellReference = "B2",
                    CellFormula = new CellFormula("SUM(1,2)"),
                    CellValue = new CellValue("3")
                }));
        worksheetPart.Worksheet = new Worksheet(sheetData);
        worksheetPart.Worksheet.Save();

        SharedStringTablePart sharedStrings = workbookPart.AddNewPart<SharedStringTablePart>("rIdSharedStrings");
        sharedStrings.SharedStringTable = new SharedStringTable(
            new SharedStringItem(new Text("Synthetic header")))
        {
            Count = 1U,
            UniqueCount = 1U
        };
        sharedStrings.SharedStringTable.Save();

        WorkbookStylesPart styles = workbookPart.AddNewPart<WorkbookStylesPart>("rIdStyles");
        styles.Stylesheet = CreateMinimalStylesheet();
        styles.Stylesheet.Save();

        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1U,
            Name = "Original"
        });
        workbookPart.Workbook.Append(new CalculationProperties
        {
            CalculationMode = CalculateModeValues.Manual,
            FullCalculationOnLoad = false,
            ForceFullCalculation = false
        });

        CalculationChainPart chainPart = workbookPart.AddNewPart<CalculationChainPart>("rIdCalculationChain");
        chainPart.CalculationChain = new CalculationChain(
            new CalculationCell { CellReference = "B2", SheetId = 1 });
        chainPart.CalculationChain.Save();
        workbookPart.Workbook.Save();
    }

    private static Stylesheet CreateMinimalStylesheet()
    {
        return new Stylesheet(
            new Fonts(new Font()) { Count = 1U },
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2U },
            new Borders(
                new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder())) { Count = 1U },
            new CellStyleFormats(
                new CellFormat
                {
                    NumberFormatId = 0U,
                    FontId = 0U,
                    FillId = 0U,
                    BorderId = 0U
                }) { Count = 1U },
            new CellFormats(
                new CellFormat
                {
                    NumberFormatId = 0U,
                    FontId = 0U,
                    FillId = 0U,
                    BorderId = 0U,
                    FormatId = 0U
                }) { Count = 1U },
            new CellStyles(
                new CellStyle
                {
                    Name = "Normal",
                    FormatId = 0U,
                    BuiltinId = 0U
                }) { Count = 1U });
    }

    private static void MutateWorkingCopy(string path, string formulaSpecPath)
    {
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, isEditable: true))
        {
        WorkbookPart workbookPart = document.WorkbookPart ?? throw new InvalidDataException("WorkbookPart is missing.");
        Workbook workbook = workbookPart.Workbook ?? throw new InvalidDataException("Workbook is missing.");
        Sheets sheets = workbook.GetFirstChild<Sheets>() ?? throw new InvalidDataException("Sheets are missing.");

        WorksheetPart evalPart = workbookPart.AddNewPart<WorksheetPart>("rIdSreEval");
        evalPart.Worksheet = CreateEvalWorksheet(formulaSpecPath);
        SaveWorksheetPreservingCarriageReturns(evalPart);

        WorksheetPart configPart = workbookPart.AddNewPart<WorksheetPart>("rIdSreConfig");
        configPart.Worksheet = CreateConfigWorksheet();
        SaveWorksheetPreservingCarriageReturns(configPart);

        WorksheetPart runPart = workbookPart.AddNewPart<WorksheetPart>("rIdSreRun");
        runPart.Worksheet = CreateRunWorksheet();
        SaveWorksheetPreservingCarriageReturns(runPart);

        sheets.Append(
            new Sheet { Id = "rIdSreEval", SheetId = 2U, Name = "Eval" },
            new Sheet { Id = "rIdSreConfig", SheetId = 3U, Name = "Evaluation_Config" },
            new Sheet { Id = "rIdSreRun", SheetId = 4U, Name = "Evaluation_Run" });

        CalculationProperties calculation = workbook.GetFirstChild<CalculationProperties>()
            ?? workbook.AppendChild(new CalculationProperties());
        calculation.CalculationMode = CalculateModeValues.Auto;
        calculation.FullCalculationOnLoad = true;
        calculation.ForceFullCalculation = true;
        workbook.Save();
        }
        RewriteWorkbookWorksheetTargetsAsRelative(path);
    }

    private static void RewriteWorkbookWorksheetTargetsAsRelative(string path)
    {
        string relationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        string[] appRelationshipIds = ["rIdSreEval", "rIdSreConfig", "rIdSreRun"];
        using FileStream packageStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using ZipArchive archive = new(packageStream, ZipArchiveMode.Update, leaveOpen: false);
        ZipArchiveEntry entry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidDataException("Workbook relationship part is missing.");
        using Stream stream = entry.Open();
        XDocument relationships = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        XNamespace ns = relationshipNamespace;
        foreach (XElement relationship in relationships.Root?.Elements(ns + "Relationship") ?? [])
        {
            string? id = (string?)relationship.Attribute("Id");
            if (!appRelationshipIds.Contains(id, StringComparer.Ordinal))
            {
                continue;
            }
            XAttribute target = relationship.Attribute("Target") ?? throw new InvalidDataException($"Relationship {id} has no Target.");
            const string absolutePrefix = "/xl/";
            Require(target.Value.StartsWith(absolutePrefix, StringComparison.Ordinal), $"Unexpected SDK worksheet target: {target.Value}");
            target.Value = target.Value[absolutePrefix.Length..];
        }
        stream.SetLength(0);
        stream.Position = 0;
        using XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            NewLineHandling = NewLineHandling.Entitize,
            OmitXmlDeclaration = false,
            CloseOutput = false
        });
        relationships.Save(writer);
    }

    private static Worksheet CreateEvalWorksheet(string formulaSpecPath)
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllBytes(formulaSpecPath));
        JsonElement cases = fixture.RootElement.GetProperty("untrusted_string_cases");
        SheetData data = new();
        data.Append(new Row(InlineStringCell("A1", "Case"), InlineStringCell("B1", "Untrusted text")));
        uint rowIndex = 2;
        foreach (JsonElement item in cases.EnumerateArray())
        {
            string id = item.GetProperty("id").GetString() ?? throw new InvalidDataException("Formula case ID is null.");
            string value = item.GetProperty("value").GetString() ?? throw new InvalidDataException($"Formula case {id} value is null.");
            data.Append(new Row(InlineStringCell($"A{rowIndex}", id), InlineStringCell($"B{rowIndex}", value)));
            rowIndex++;
        }

        return new Worksheet(data);
    }

    private static Worksheet CreateConfigWorksheet()
    {
        SheetData data = new();
        data.Append(new Row(InlineStringCell("A1", "rounding_digits"), InlineStringCell("B1", "synthetic_value")));
        uint rowIndex = 2;
        foreach (int digit in RoundingDigits)
        {
            data.Append(new Row(NumberCell($"A{rowIndex}", digit), NumberCell($"B{rowIndex}", 1.23456789012345)));
            rowIndex++;
        }

        return new Worksheet(data);
    }

    private static void SaveWorksheetPreservingCarriageReturns(WorksheetPart part)
    {
        Worksheet worksheet = part.Worksheet ?? throw new InvalidDataException("Worksheet is missing.");
        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            NewLineHandling = NewLineHandling.Entitize,
            OmitXmlDeclaration = false
        };
        using Stream stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using XmlWriter writer = XmlWriter.Create(stream, settings);
        worksheet.WriteTo(writer);
    }

    private static Worksheet CreateRunWorksheet()
    {
        SheetData data = new();
        data.Append(new Row(
            InlineStringCell("A1", RunMarker),
            InlineStringCell("B1", "ROUND result"),
            InlineStringCell("C1", "ROUND zero sentinel")));

        uint rowIndex = 2;
        foreach (int digit in RoundingDigits)
        {
            data.Append(new Row(
                NumberCell($"A{rowIndex}", digit),
                FormulaCell($"B{rowIndex}", $"ROUND('Evaluation_Config'!B{rowIndex},'Evaluation_Config'!A{rowIndex})"),
                FormulaCell($"C{rowIndex}", $"ROUND(0,'Evaluation_Config'!A{rowIndex})=0")));
            rowIndex++;
        }

        return new Worksheet(data);
    }

    private static Cell InlineStringCell(string reference, string value)
    {
        return new Cell
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new InlineString(
                new Text(value) { Space = SpaceProcessingModeValues.Preserve })
        };
    }

    private static Cell SharedStringCell(string reference, int index)
    {
        return new Cell
        {
            CellReference = reference,
            DataType = CellValues.SharedString,
            CellValue = new CellValue(index.ToString(CultureInfo.InvariantCulture))
        };
    }

    private static Cell NumberCell(string reference, double value)
    {
        return new Cell
        {
            CellReference = reference,
            DataType = CellValues.Number,
            CellValue = new CellValue(value.ToString("R", CultureInfo.InvariantCulture))
        };
    }

    private static Cell FormulaCell(string reference, string formula)
    {
        Require(formula.Length < 8192, $"Formula at {reference} is not below the 8192-character limit.");
        return new Cell
        {
            CellReference = reference,
            CellFormula = new CellFormula(formula)
        };
    }

    private static PackageFingerprint FingerprintPackage(string path)
    {
        Dictionary<string, string> entryHashes = new(StringComparer.Ordinal);
        HashSet<string> relationships = new(StringComparer.Ordinal);
        HashSet<string> contentTypes = new(StringComparer.Ordinal);

        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            Require(entryHashes.TryAdd(entry.FullName, Sha256(entry)), $"Duplicate ZIP entry: {entry.FullName}");
            if (entry.FullName.EndsWith(".rels", StringComparison.Ordinal))
            {
                using Stream relationshipStream = entry.Open();
                XDocument document = XDocument.Load(relationshipStream, LoadOptions.PreserveWhitespace);
                XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
                string source = RelationshipSource(entry.FullName);
                foreach (XElement relationship in document.Root?.Elements(ns + "Relationship") ?? [])
                {
                    relationships.Add(string.Join('\u001F',
                        source,
                        (string?)relationship.Attribute("Id") ?? string.Empty,
                        (string?)relationship.Attribute("Type") ?? string.Empty,
                        (string?)relationship.Attribute("Target") ?? string.Empty,
                        (string?)relationship.Attribute("TargetMode") ?? string.Empty));
                }
            }
        }

        ZipArchiveEntry contentTypesEntry = archive.GetEntry("[Content_Types].xml")
            ?? throw new InvalidDataException("[Content_Types].xml is missing.");
        using (Stream contentTypeStream = contentTypesEntry.Open())
        {
            XDocument document = XDocument.Load(contentTypeStream, LoadOptions.PreserveWhitespace);
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
            foreach (XElement item in document.Root?.Elements() ?? [])
            {
                if (item.Name == ns + "Default")
                {
                    contentTypes.Add($"D\u001F{(string?)item.Attribute("Extension")}\u001F{(string?)item.Attribute("ContentType")}");
                }
                else if (item.Name == ns + "Override")
                {
                    contentTypes.Add($"O\u001F{(string?)item.Attribute("PartName")}\u001F{(string?)item.Attribute("ContentType")}");
                }
            }
        }

        return new PackageFingerprint(entryHashes, relationships, contentTypes);
    }

    private static void VerifyPackageDelta(
        PackageFingerprint input,
        PackageFingerprint output,
        WorkbookSnapshot inputWorkbook,
        WorkbookSnapshot outputWorkbook)
    {
        HashSet<string> allowedChanged = new(StringComparer.Ordinal)
        {
            "[Content_Types].xml",
            "xl/workbook.xml",
            "xl/_rels/workbook.xml.rels"
        };

        foreach ((string entry, string hash) in input.EntryHashes)
        {
            Require(output.EntryHashes.TryGetValue(entry, out string? outputHash), $"Existing entry was removed: {entry}");
            if (!string.Equals(hash, outputHash, StringComparison.Ordinal))
            {
                Require(allowedChanged.Contains(entry), $"Non-allowlisted existing entry changed: {entry}");
            }
        }

        string[] expectedSheets = ["Eval", "Evaluation_Config", "Evaluation_Run"];
        Require(outputWorkbook.Sheets.Select(sheet => sheet.Name).SequenceEqual(["Original", .. expectedSheets], StringComparer.Ordinal), "Unexpected sheet names or order.");
        Require(inputWorkbook.Sheets.Length == 1 && outputWorkbook.Sheets[0] == inputWorkbook.Sheets[0], "Original sheet identity changed.");
        Require(outputWorkbook.CalculationMode == CalculateModeValues.Auto, "calcMode is not auto.");
        Require(outputWorkbook.FullCalculationOnLoad, "fullCalcOnLoad is not enabled.");
        Require(outputWorkbook.ForceFullCalculation, "forceFullCalc is not enabled.");

        string[] addedEntries = output.EntryHashes.Keys.Except(input.EntryHashes.Keys, StringComparer.Ordinal).ToArray();
        HashSet<string> expectedAddedEntries = outputWorkbook.Sheets
            .Where(sheet => expectedSheets.Contains(sheet.Name, StringComparer.Ordinal))
            .Select(sheet => sheet.PartName.TrimStart('/'))
            .ToHashSet(StringComparer.Ordinal);
        Require(addedEntries.ToHashSet(StringComparer.Ordinal).SetEquals(expectedAddedEntries), "Unexpected new package entry.");

        Require(input.Relationships.IsSubsetOf(output.Relationships), "An existing relationship tuple changed or was removed.");
        string[] addedRelationships = output.Relationships.Except(input.Relationships, StringComparer.Ordinal).ToArray();
        Require(addedRelationships.Length == 3, "Exactly three worksheet relationships must be added.");
        HashSet<string> expectedRelationshipIds = new(["rIdSreEval", "rIdSreConfig", "rIdSreRun"], StringComparer.Ordinal);
        HashSet<string> expectedRelationshipTargets = expectedAddedEntries
            .Select(entry => entry["xl/".Length..])
            .ToHashSet(StringComparer.Ordinal);
        foreach (string relationship in addedRelationships)
        {
            string[] fields = relationship.Split('\u001F');
            Require(fields.Length == 5, "Malformed relationship tuple.");
            Require(fields[0] == "/xl/workbook.xml", "New relationship source is not workbook.xml.");
            Require(expectedRelationshipIds.Remove(fields[1]), $"Unexpected or duplicate relationship ID: {fields[1]}");
            Require(fields[2] == WorksheetRelationshipType, "New relationship type is not worksheet.");
            Require(expectedRelationshipTargets.Remove(fields[3]), $"Unexpected or duplicate worksheet target: {fields[3]}");
            Require(string.IsNullOrEmpty(fields[4]), "New worksheet relationship is external.");
        }
        Require(expectedRelationshipIds.Count == 0 && expectedRelationshipTargets.Count == 0, "Required worksheet relationship is missing.");

        Require(input.ContentTypes.IsSubsetOf(output.ContentTypes), "An existing content type changed or was removed.");
        string[] addedContentTypes = output.ContentTypes.Except(input.ContentTypes, StringComparer.Ordinal).ToArray();
        Require(addedContentTypes.Length == 3, "Exactly three worksheet content-type overrides must be added.");
        HashSet<string> expectedContentTypePartNames = expectedAddedEntries
            .Select(entry => $"/{entry}")
            .ToHashSet(StringComparer.Ordinal);
        foreach (string item in addedContentTypes)
        {
            string[] fields = item.Split('\u001F');
            Require(fields.Length == 3 && fields[0] == "O" && fields[2] == WorksheetContentType, "Unexpected content-type addition.");
            Require(expectedContentTypePartNames.Remove(fields[1]), $"Unexpected or duplicate content-type part name: {fields[1]}");
        }
        Require(expectedContentTypePartNames.Count == 0, "Required worksheet content-type override is missing.");

        const string calculationChainEntry = "xl/calcChain.xml";
        Require(input.EntryHashes.TryGetValue(calculationChainEntry, out string? inputCalculationChainHash), "Synthetic input calculation chain is missing.");
        Require(output.EntryHashes.TryGetValue(calculationChainEntry, out string? outputCalculationChainHash), "Output calculation chain is missing.");
        Require(string.Equals(inputCalculationChainHash, outputCalculationChainHash, StringComparison.Ordinal), "Calculation chain payload changed.");
    }

    private static InlineStringEvidence VerifyInlineStrings(
        string outputPath,
        string formulaSpecPath,
        PackageFingerprint input,
        PackageFingerprint output)
    {
        Dictionary<string, string> expected = LoadUntrustedStrings(formulaSpecPath);
        using SpreadsheetDocument document = SpreadsheetDocument.Open(outputPath, isEditable: false);
        WorkbookPart workbookPart = document.WorkbookPart ?? throw new InvalidDataException("WorkbookPart is missing.");
        WorksheetPart evalPart = GetWorksheetPart(workbookPart, "Eval");
        Worksheet evalWorksheet = evalPart.Worksheet ?? throw new InvalidDataException("Eval worksheet is missing.");
        Dictionary<string, Cell> cells = evalWorksheet.Descendants<Cell>()
            .Where(cell => cell.CellReference is not null)
            .ToDictionary(cell => cell.CellReference!.Value!, StringComparer.Ordinal);

        int verified = 0;
        for (int index = 0; index < expected.Count; index++)
        {
            uint row = (uint)index + 2;
            Cell idCell = cells[$"A{row}"];
            Cell valueCell = cells[$"B{row}"];
            string id = ReadInlineText(idCell);
            Require(expected.TryGetValue(id, out string? value), $"Unknown inline-string case ID: {id}");
            Require(valueCell.DataType?.Value == CellValues.InlineString, $"Case {id} is not inline string.");
            Require(valueCell.CellFormula is null, $"Case {id} contains a formula node.");
            Require(ReadInlineText(valueCell) == value, $"Case {id} text changed.");
            verified++;
        }

        Require(verified == expected.Count, "Not all untrusted-string cases were verified.");
        const string sharedStringsEntry = "xl/sharedStrings.xml";
        Require(input.EntryHashes[sharedStringsEntry] == output.EntryHashes[sharedStringsEntry], "Shared strings changed.");
        Require(!evalPart.HyperlinkRelationships.Any(), "Eval contains a hyperlink relationship.");
        Require(!evalPart.ExternalRelationships.Any(), "Eval contains an external relationship.");

        return new InlineStringEvidence(expected.Count, verified, FormulaNodeCount: 0, HyperlinkCount: 0, ExternalRelationshipCount: 0, SharedStringsUnchanged: true);
    }

    private static ValidationEvidence ValidateOpenXml(string outputPath)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(outputPath, isEditable: false);
        OpenXmlValidator validator = new(FileFormatVersions.Office2019) { MaxNumberOfErrors = 100 };
        string[] errors = validator.Validate(document)
            .Select(error => $"{error.ErrorType}:{error.Id}:{error.Path?.XPath}")
            .ToArray();
        Require(errors.Length == 0, $"Open XML validation failed with {errors.Length} error(s): {string.Join(";", errors.Take(3))}");
        return new ValidationEvidence("Office2019", errors.Length, errors);
    }

    private static UnicodeEvidence VerifyUnicode(string unicodeSpecPath)
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllBytes(unicodeSpecPath));
        int total = 0;
        int passed = 0;
        foreach (JsonElement testCase in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            total++;
            string id = testCase.GetProperty("id").GetString() ?? throw new InvalidDataException("Unicode case ID is null.");
            JsonElement expected = testCase.GetProperty("expected");
            if (testCase.TryGetProperty("question_text", out JsonElement question))
            {
                SimilarityResult actual = CalculateSimilarity(question.GetString()!, testCase.GetProperty("learner_prompt").GetString()!);
                Require(actual.Status == expected.GetProperty("status").GetString(), $"{id}: status mismatch.");
                if (expected.TryGetProperty("normalized_question", out JsonElement normalizedQuestion))
                {
                    Require(actual.NormalizedQuestion == normalizedQuestion.GetString(), $"{id}: normalized question mismatch.");
                }
                if (expected.TryGetProperty("normalized_prompt", out JsonElement normalizedPrompt))
                {
                    Require(actual.NormalizedPrompt == normalizedPrompt.GetString(), $"{id}: normalized prompt mismatch.");
                }
                if (actual.Status == "COMPUTED")
                {
                    Require(actual.Contains == expected.GetProperty("contains").GetBoolean(), $"{id}: containment mismatch.");
                    JsonElement coverage = expected.GetProperty("coverage");
                    JsonElement jaccard = expected.GetProperty("multiset_jaccard");
                    Require(actual.CoverageNumerator == coverage.GetProperty("numerator").GetInt32(), $"{id}: coverage numerator mismatch.");
                    Require(actual.CoverageDenominator == coverage.GetProperty("denominator").GetInt32(), $"{id}: coverage denominator mismatch.");
                    Require(actual.JaccardNumerator == jaccard.GetProperty("numerator").GetInt32(), $"{id}: Jaccard numerator mismatch.");
                    Require(actual.JaccardDenominator == jaccard.GetProperty("denominator").GetInt32(), $"{id}: Jaccard denominator mismatch.");
                }
                passed++;
                continue;
            }

            JsonElement representation = testCase.GetProperty("input_representation");
            int[] codeUnits = representation.TryGetProperty("value", out JsonElement valueUnits)
                ? valueUnits.EnumerateArray().Select(element => element.GetInt32()).ToArray()
                : representation.GetProperty("question").EnumerateArray().Select(element => element.GetInt32()).ToArray();
            string value = new(codeUnits.Select(unit => (char)unit).ToArray());
            if (id == "UC-014")
            {
                Require(!HasValidUtf16(value), "UC-014: lone surrogate was not rejected.");
            }
            else
            {
                Require(HasValidUtf16(value), $"{id}: valid UTF-16 was rejected.");
                Require(EnumerateScalars(value).Count == expected.GetProperty("unicode_scalar_count").GetInt32(), $"{id}: scalar count mismatch.");
                Require(value.Length == expected.GetProperty("utf16_code_unit_count").GetInt32(), $"{id}: UTF-16 count mismatch.");
            }
            passed++;
        }

        Require(total == 15 && passed == total, "Unicode fixture coverage is incomplete.");
        return new UnicodeEvidence(total, passed, "NFKC+LF+Unicode-White-Space+Rune-3-shingle");
    }

    private static SimilarityResult CalculateSimilarity(string question, string prompt)
    {
        Require(HasValidUtf16(question) && HasValidUtf16(prompt), "Similarity input contains invalid UTF-16.");
        string normalizedQuestion = NormalizeForSimilarity(question);
        string normalizedPrompt = NormalizeForSimilarity(prompt);
        string[] questionScalars = EnumerateScalars(normalizedQuestion).ToArray();
        string[] promptScalars = EnumerateScalars(normalizedPrompt).ToArray();

        if (questionScalars.Length == 0 && promptScalars.Length == 0)
        {
            return new SimilarityResult("NOT_APPLICABLE", normalizedQuestion, normalizedPrompt, null, null, null, null, null);
        }
        if (questionScalars.Length == 0 || promptScalars.Length == 0)
        {
            return new SimilarityResult("INSUFFICIENT_DATA", normalizedQuestion, normalizedPrompt, null, null, null, null, null);
        }
        if (questionScalars.Length < 3 || promptScalars.Length < 3)
        {
            return new SimilarityResult("TOO_SHORT", normalizedQuestion, normalizedPrompt, null, null, null, null, null);
        }

        Dictionary<string, int> questionShingles = BuildShingles(questionScalars);
        Dictionary<string, int> promptShingles = BuildShingles(promptScalars);
        HashSet<string> keys = new(questionShingles.Keys, StringComparer.Ordinal);
        keys.UnionWith(promptShingles.Keys);
        int intersection = keys.Sum(key => Math.Min(questionShingles.GetValueOrDefault(key), promptShingles.GetValueOrDefault(key)));
        int union = keys.Sum(key => Math.Max(questionShingles.GetValueOrDefault(key), promptShingles.GetValueOrDefault(key)));
        bool contains = ContainsSequence(promptScalars, questionScalars);
        return new SimilarityResult("COMPUTED", normalizedQuestion, normalizedPrompt, contains, intersection, questionScalars.Length - 2, intersection, union);
    }

    private static string NormalizeForSimilarity(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormKC)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        StringBuilder builder = new();
        bool pendingSpace = false;
        foreach (Rune rune in normalized.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = true;
                continue;
            }
            if (pendingSpace && builder.Length > 0)
            {
                builder.Append(' ');
            }
            builder.Append(rune.ToString());
            pendingSpace = false;
        }
        return builder.ToString();
    }

    private static Dictionary<string, int> BuildShingles(IReadOnlyList<string> scalars)
    {
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        for (int index = 0; index <= scalars.Count - 3; index++)
        {
            string shingle = string.Concat(scalars[index], scalars[index + 1], scalars[index + 2]);
            result[shingle] = result.GetValueOrDefault(shingle) + 1;
        }
        return result;
    }

    private static bool ContainsSequence(IReadOnlyList<string> haystack, IReadOnlyList<string> needle)
    {
        for (int start = 0; start <= haystack.Count - needle.Count; start++)
        {
            bool equal = true;
            for (int offset = 0; offset < needle.Count; offset++)
            {
                if (!string.Equals(haystack[start + offset], needle[offset], StringComparison.Ordinal))
                {
                    equal = false;
                    break;
                }
            }
            if (equal)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasValidUtf16(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsHighSurrogate(current))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }
                index++;
            }
            else if (char.IsLowSurrogate(current))
            {
                return false;
            }
        }
        return true;
    }

    private static List<string> EnumerateScalars(string value)
    {
        return value.EnumerateRunes().Select(rune => rune.ToString()).ToList();
    }

    private static Dictionary<string, string> LoadUntrustedStrings(string formulaSpecPath)
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllBytes(formulaSpecPath));
        return fixture.RootElement.GetProperty("untrusted_string_cases")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("id").GetString()!,
                item => item.GetProperty("value").GetString()!,
                StringComparer.Ordinal);
    }

    private static WorkbookSnapshot ReadWorkbookSnapshot(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, isEditable: false);
        WorkbookPart workbookPart = document.WorkbookPart ?? throw new InvalidDataException("WorkbookPart is missing.");
        Workbook workbook = workbookPart.Workbook ?? throw new InvalidDataException("Workbook is missing.");
        SheetInfo[] sheets = workbook.GetFirstChild<Sheets>()?.Elements<Sheet>()
            .Select(sheet =>
            {
                string relationshipId = sheet.Id?.Value ?? throw new InvalidDataException("Sheet relationship ID is missing.");
                WorksheetPart part = (WorksheetPart)workbookPart.GetPartById(relationshipId);
                return new SheetInfo(
                    sheet.Name?.Value ?? throw new InvalidDataException("Sheet name is missing."),
                    sheet.SheetId?.Value ?? throw new InvalidDataException("Sheet ID is missing."),
                    relationshipId,
                    part.Uri.ToString());
            })
            .ToArray() ?? [];
        CalculationProperties? calculation = workbook.GetFirstChild<CalculationProperties>();
        int formulaCount = workbookPart.WorksheetParts.Sum(part =>
            (part.Worksheet ?? throw new InvalidDataException($"Worksheet root is missing: {part.Uri}"))
                .Descendants<CellFormula>()
                .Count());
        return new WorkbookSnapshot(
            sheets,
            calculation?.CalculationMode?.Value,
            calculation?.FullCalculationOnLoad?.Value ?? false,
            calculation?.ForceFullCalculation?.Value ?? false,
            formulaCount);
    }

    private static WorksheetPart GetWorksheetPart(WorkbookPart workbookPart, string name)
    {
        Workbook workbook = workbookPart.Workbook ?? throw new InvalidDataException("Workbook is missing.");
        Sheet sheet = workbook.GetFirstChild<Sheets>()?.Elements<Sheet>()
            .SingleOrDefault(candidate => string.Equals(candidate.Name?.Value, name, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Sheet is missing: {name}");
        return (WorksheetPart)workbookPart.GetPartById(sheet.Id?.Value ?? throw new InvalidDataException("Sheet relationship ID is missing."));
    }

    private static EngineEvidence InspectEngineWorkbook(string path, string engine, string version)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, isEditable: false);
        WorkbookPart workbookPart = document.WorkbookPart ?? throw new InvalidDataException("WorkbookPart is missing.");
        WorksheetPart runPart = GetWorksheetPart(workbookPart, "Evaluation_Run");
        Worksheet runWorksheet = runPart.Worksheet ?? throw new InvalidDataException("Evaluation_Run worksheet is missing.");
        Dictionary<string, Cell> cells = runWorksheet.Descendants<Cell>()
            .Where(cell => cell.CellReference is not null)
            .ToDictionary(cell => cell.CellReference!.Value!, StringComparer.Ordinal);

        List<RoundResult> results = [];
        for (uint row = 2; row <= RoundingDigits.Length + 1; row++)
        {
            int digit = int.Parse(cells[$"A{row}"].CellValue!.Text, CultureInfo.InvariantCulture);
            Cell valueCell = cells[$"B{row}"];
            Cell sentinelCell = cells[$"C{row}"];
            results.Add(new RoundResult(
                digit,
                valueCell.CellValue?.Text,
                valueCell.DataType?.InnerText,
                sentinelCell.CellValue?.Text,
                sentinelCell.DataType?.InnerText));
        }

        int missing = results.Count(result => result.Value is null || result.Sentinel is null);
        int errors = results.Count(result => string.Equals(result.ValueType, "e", StringComparison.Ordinal)
            || string.Equals(result.SentinelType, "e", StringComparison.Ordinal));
        string[] validationErrors = new OpenXmlValidator(FileFormatVersions.Office2019) { MaxNumberOfErrors = 100 }
            .Validate(document)
            .Select(error => $"{error.ErrorType}:{error.Id}:{error.Path?.XPath}")
            .ToArray();
        Require(validationErrors.Length == 0, $"Engine-saved workbook has {validationErrors.Length} Open XML validation error(s).");
        string resultDigest = Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(results)));
        return new EngineEvidence(
            "study-report-evaluator/g16-engine-evidence/v1",
            engine,
            version,
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Path.GetFileName(path),
            results.Count,
            missing,
            errors,
            validationErrors.Length,
            resultDigest,
            results.ToArray());
    }

    private static CrossEngineEvidence CompareEngineEvidence(string firstPath, string secondPath)
    {
        EngineEvidence first = JsonSerializer.Deserialize<EngineEvidence>(File.ReadAllText(firstPath), JsonOptions)
            ?? throw new InvalidDataException("First engine evidence is invalid.");
        EngineEvidence second = JsonSerializer.Deserialize<EngineEvidence>(File.ReadAllText(secondPath), JsonOptions)
            ?? throw new InvalidDataException("Second engine evidence is invalid.");
        Require(first.Results.Length == second.Results.Length, "Engine evidence result counts differ.");

        Dictionary<int, RoundResult> firstByDigit = first.Results.ToDictionary(result => result.Digit);
        Dictionary<int, RoundResult> secondByDigit = second.Results.ToDictionary(result => result.Digit);
        Require(firstByDigit.Keys.ToHashSet().SetEquals(secondByDigit.Keys), "Engine evidence digit sets differ.");

        int[] firstGood = first.Results.Where(IsSuccessfulRoundResult).Select(result => result.Digit).Order().ToArray();
        int[] secondGood = second.Results.Where(IsSuccessfulRoundResult).Select(result => result.Digit).Order().ToArray();
        int[] commonGood = firstGood.Intersect(secondGood).Order().ToArray();
        string[] commonRanges = DescribeRanges(commonGood);
        Require(commonRanges.Length == 1 && commonGood.Contains(0), "Common successful range is not one contiguous range containing zero.");

        List<int> mismatches = [];
        foreach (int digit in commonGood)
        {
            RoundResult firstResult = firstByDigit[digit];
            RoundResult secondResult = secondByDigit[digit];
            double firstValue = double.Parse(firstResult.Value!, CultureInfo.InvariantCulture);
            double secondValue = double.Parse(secondResult.Value!, CultureInfo.InvariantCulture);
            if (BitConverter.DoubleToInt64Bits(firstValue) != BitConverter.DoubleToInt64Bits(secondValue)
                || !string.Equals(firstResult.Sentinel, secondResult.Sentinel, StringComparison.Ordinal))
            {
                mismatches.Add(digit);
            }
        }
        Require(mismatches.Count == 0, $"Cross-engine ROUND mismatch at {mismatches.Count} digit value(s).");

        int minimum = commonGood.Min();
        int maximum = commonGood.Max();
        int[] boundaryDigits = [minimum - 1, minimum, maximum, maximum + 1];
        BoundaryResult[] boundaries = boundaryDigits.Select(digit => new BoundaryResult(
            digit,
            IsSuccessfulRoundResult(firstByDigit[digit]),
            IsSuccessfulRoundResult(secondByDigit[digit])))
            .ToArray();

        return new CrossEngineEvidence(
            "study-report-evaluator/g16-cross-engine-evidence/v1",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            first.Engine,
            first.EngineVersion,
            first.ResultDigest,
            second.Engine,
            second.EngineVersion,
            second.ResultDigest,
            firstByDigit.Keys.Min(),
            firstByDigit.Keys.Max(),
            DescribeRanges(firstGood),
            DescribeRanges(secondGood),
            commonRanges,
            minimum,
            maximum,
            commonGood.Length,
            mismatches.ToArray(),
            boundaries);
    }

    private static bool IsSuccessfulRoundResult(RoundResult result)
    {
        return result.Value is not null
            && result.Sentinel is not null
            && !string.Equals(result.ValueType, "e", StringComparison.Ordinal)
            && !string.Equals(result.SentinelType, "e", StringComparison.Ordinal);
    }

    private static string[] DescribeRanges(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return [];
        }

        List<string> ranges = [];
        int start = values[0];
        int end = start;
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index] == end + 1)
            {
                end = values[index];
                continue;
            }
            ranges.Add($"{start}..{end}");
            start = values[index];
            end = start;
        }
        ranges.Add($"{start}..{end}");
        return ranges.ToArray();
    }

    private static IcuEvidence ReadIcuEvidence()
    {
        SortVersion version = CultureInfo.InvariantCulture.CompareInfo.Version;
        byte[] bytes = version.SortId.ToByteArray();
        int encodedVersion = bytes[3] << 24 | bytes[2] << 16 | bytes[1] << 8 | bytes[0];
        bool icuMode = encodedVersion != 0 && encodedVersion == version.FullVersion;
        Require(icuMode, "Runtime is not using ICU.");
        return new IcuEvidence(true, "72.1", version.FullVersion, version.SortId.ToString());
    }

    private static FileSnapshot SnapshotFile(string path)
    {
        FileInfo info = new(path);
        return new FileSnapshot(info.Length, info.LastWriteTimeUtc.Ticks, Sha256(path));
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Sha256(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string RelationshipSource(string entryName)
    {
        if (entryName == "_rels/.rels")
        {
            return "/";
        }
        int marker = entryName.LastIndexOf("/_rels/", StringComparison.Ordinal);
        Require(marker >= 0 && entryName.EndsWith(".rels", StringComparison.Ordinal), $"Invalid relationship part name: {entryName}");
        string directory = entryName[..marker];
        string fileName = entryName[(marker + "/_rels/".Length)..^".rels".Length];
        return $"/{directory}/{fileName}";
    }

    private static string ReadInlineText(Cell cell)
    {
        return string.Concat(cell.InlineString?.Descendants<Text>().Select(text => text.Text) ?? []);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private sealed record PackageFingerprint(
        Dictionary<string, string> EntryHashes,
        HashSet<string> Relationships,
        HashSet<string> ContentTypes);

    private sealed record WorkbookSnapshot(
        SheetInfo[] Sheets,
        CalculateModeValues? CalculationMode,
        bool FullCalculationOnLoad,
        bool ForceFullCalculation,
        int FormulaCellCount);

    private sealed record SheetInfo(string Name, uint SheetId, string RelationshipId, string PartName);
    private sealed record FileHash(string FileName, string Sha256);
    private sealed record FileSnapshot(long Bytes, long LastWriteTimeUtcTicks, string Sha256);
    private sealed record InlineStringEvidence(int ExpectedCases, int VerifiedCases, int FormulaNodeCount, int HyperlinkCount, int ExternalRelationshipCount, bool SharedStringsUnchanged);
    private sealed record ValidationEvidence(string FileFormatVersion, int ErrorCount, string[] Errors);
    private sealed record UnicodeEvidence(int ExpectedCases, int PassedCases, string Algorithm);
    private sealed record IcuEvidence(bool IcuMode, string ConfiguredVersion, int SortVersionFull, string SortId);
    private sealed record SimilarityResult(string Status, string NormalizedQuestion, string NormalizedPrompt, bool? Contains, int? CoverageNumerator, int? CoverageDenominator, int? JaccardNumerator, int? JaccardDenominator);
    private sealed record RoundResult(int Digit, string? Value, string? ValueType, string? Sentinel, string? SentinelType);
    private sealed record EngineEvidence(string Schema, string Engine, string EngineVersion, string CapturedAtUtc, string WorkbookPath, int ResultCount, int MissingCount, int ErrorCount, int OpenXmlValidationErrorCount, string ResultDigest, RoundResult[] Results);
    private sealed record BoundaryResult(int Digit, bool FirstEngineSucceeded, bool SecondEngineSucceeded);
    private sealed record CrossEngineEvidence(
        string Schema,
        string CapturedAtUtc,
        string FirstEngine,
        string FirstEngineVersion,
        string FirstResultDigest,
        string SecondEngine,
        string SecondEngineVersion,
        string SecondResultDigest,
        int ProbeMinimumDigit,
        int ProbeMaximumDigit,
        string[] FirstSuccessfulRanges,
        string[] SecondSuccessfulRanges,
        string[] CommonSuccessfulRanges,
        int SelectedCommonMinimum,
        int SelectedCommonMaximum,
        int ComparedResultCount,
        int[] BitExactMismatchDigits,
        BoundaryResult[] Boundaries);
    private sealed record LocalEvidence(
        string Schema,
        string CapturedAtUtc,
        string OperatingSystem,
        string RuntimeIdentifier,
        string FrameworkDescription,
        string OpenXmlPackageVersion,
        string AppLocalIcuPackageVersion,
        FileSnapshot Input,
        FileSnapshot Output,
        int InputEntryCount,
        int OutputEntryCount,
        string[] AddedEntryNames,
        string[] ChangedExistingEntryNames,
        int ExistingRelationshipCount,
        int AddedRelationshipCount,
        string[] SheetNames,
        bool CalculationModeAuto,
        bool FullCalculationOnLoad,
        bool ForceFullCalculation,
        int FormulaProbeMinimumDigit,
        int FormulaProbeMaximumDigit,
        int FormulaCellCount,
        InlineStringEvidence InlineStrings,
        ValidationEvidence Validation,
        UnicodeEvidence Unicode,
        IcuEvidence Icu,
        FileHash[] NativeIcuFiles,
        string InputWorkbookPath,
        string OutputWorkbookPath);
}
