using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.Core.Serialization;

namespace StudyReportEvaluator.App.Workbooks.Reading;

// FR-110 / AC-065〜067: ConfigSheetWriter(FR-109) が書いた設定シートから採点定義を復元する。
// 壊れた・欠けた・未知の種類名・JSONと列の食い違いは、例外ではなく検証エラーを持つ結果で返す。
public sealed class ConfigSheetReader
{
    private const string RecordTypeColumn = "A";
    private const string NodeIdColumn = "C";
    private const string EvaluatorTypeColumn = "J";
    private const string ChunkIndexColumn = "X";
    private const string SnapshotChunkColumn = "Y";

    public CanonicalDefinitionReadResult Read(SpreadsheetDocument document, AppOwnedSheetNames sheetNames)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sheetNames);
        WorkbookPart? workbookPart = document.WorkbookPart;
        return workbookPart is null
            ? CanonicalDefinitionReadResult.Failure(
                DefinitionReadErrorKind.MissingConfigSheet,
                "The workbook part is missing.")
            : Read(workbookPart, sheetNames.ConfigSheetName);
    }

    public CanonicalDefinitionReadResult Read(WorkbookPart workbookPart, string sheetName)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);

        Worksheet? worksheet = FindWorksheet(workbookPart, sheetName);
        if (worksheet is null)
        {
            return CanonicalDefinitionReadResult.Failure(
                DefinitionReadErrorKind.MissingConfigSheet,
                "The config sheet was not found.");
        }

        Row[] rows = worksheet.Descendants<Row>().ToArray();
        Row[] jsonRows = rows.Where(row => Text(workbookPart, row, RecordTypeColumn) == "CANONICAL_JSON").ToArray();
        if (jsonRows.Length == 0)
        {
            return CanonicalDefinitionReadResult.Failure(
                DefinitionReadErrorKind.MissingCanonicalJson,
                "The config sheet has no CANONICAL_JSON row.");
        }

        string json = string.Concat(jsonRows
            .OrderBy(row => int.TryParse(Text(workbookPart, row, ChunkIndexColumn), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : int.MaxValue)
            .Select(row => Text(workbookPart, row, SnapshotChunkColumn)));

        CanonicalDefinitionReadResult parsed = new CanonicalDefinitionDeserializer().Deserialize(json);
        if (!parsed.IsSuccess)
        {
            return parsed;
        }

        // JSON 内の種類と EvaluatorType 列の照合（列の未知の種類名・食い違いは拒否し、暗黙に置き換えない）
        Dictionary<string, string> columnTypes = new(StringComparer.Ordinal);
        foreach (Row row in rows.Where(r => Text(workbookPart, r, RecordTypeColumn) == "EVALUATOR"))
        {
            columnTypes[Text(workbookPart, row, NodeIdColumn)] = Text(workbookPart, row, EvaluatorTypeColumn);
        }

        List<DefinitionReadError> errors = [];
        foreach (Core.Domain.EvaluatorDefinition evaluator in parsed.Definition!.Questions.SelectMany(q => q.Evaluators))
        {
            if (!columnTypes.TryGetValue(evaluator.Id, out string? columnName) || columnName.Length == 0)
            {
                errors.Add(new DefinitionReadError(
                    DefinitionReadErrorKind.MissingRequiredField,
                    $"The EvaluatorType column is missing for evaluator '{evaluator.Id}'."));
            }
            else if (CanonicalDefinitionDeserializer.ParseType(columnName) is not Core.Domain.EvaluatorType columnType)
            {
                errors.Add(new DefinitionReadError(
                    DefinitionReadErrorKind.UnknownEvaluatorType,
                    $"Unknown evaluator type '{columnName}' in the EvaluatorType column for evaluator '{evaluator.Id}'."));
            }
            else if (columnType != evaluator.Type)
            {
                errors.Add(new DefinitionReadError(
                    DefinitionReadErrorKind.EvaluatorTypeMismatch,
                    $"The EvaluatorType column '{columnName}' differs from the CANONICAL_JSON type for evaluator '{evaluator.Id}'."));
            }
        }

        return errors.Count == 0 ? parsed : CanonicalDefinitionReadResult.Failure(errors);
    }

    private static Worksheet? FindWorksheet(WorkbookPart workbookPart, string sheetName)
    {
        Sheet? sheet = workbookPart.Workbook?.Descendants<Sheet>()
            .FirstOrDefault(s => string.Equals(s.Name?.Value, sheetName, StringComparison.Ordinal));
        string? relationshipId = sheet?.Id?.Value;
        return relationshipId is null
            ? null
            : (workbookPart.GetPartById(relationshipId) as WorksheetPart)?.Worksheet;
    }

    private static string Text(WorkbookPart workbookPart, Row row, string column)
    {
        Cell? cell = row.Elements<Cell>().FirstOrDefault(c =>
            string.Equals(
                new string((c.CellReference?.Value ?? string.Empty).TakeWhile(char.IsLetter).ToArray()),
                column,
                StringComparison.Ordinal));
        if (cell is null)
        {
            return string.Empty;
        }

        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
        {
            return workbookPart.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>()
                .ElementAtOrDefault(index)?.InnerText ?? string.Empty;
        }

        return cell.InnerText ?? string.Empty;
    }
}
