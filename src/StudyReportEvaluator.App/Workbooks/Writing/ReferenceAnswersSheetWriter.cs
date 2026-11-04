using System.Collections.Immutable;
using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Similarity;

namespace StudyReportEvaluator.App.Workbooks.Writing;

public sealed record ReferenceAnswerSheetRow
{
    public required string QuestionId { get; init; }

    public required string ModelId { get; init; }

    public string? ReasoningEffort { get; init; }

    public string? Answer { get; init; }

    public required string StatusCode { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>FR-115: 設問内の slot 番号（1 起点）。0 は旧単独参照（旧形式の単独集合）。</summary>
    public int SlotIndex { get; init; }

    /// <summary>FR-115: 設問ごとの予定 slot 数。0 は旧形式（予定数 1・条件は未取得）。</summary>
    public int SlotCount { get; init; }

    public string? ContextTier { get; init; }

    /// <summary>FR-115 AC-080: 集合に結び付けた system/template 版・比較方式版・正規化条件。null は未取得（旧形式）。</summary>
    public string? TemplateVersion { get; init; }

    public string? ComparisonVersion { get; init; }

    public string? NormalizationVersion { get; init; }

    public override string ToString() =>
        $"{nameof(ReferenceAnswerSheetRow)} {{ QuestionId = {QuestionId}, StatusCode = {StatusCode}, Content = <redacted> }}";
}

public sealed record ReferenceAnswersSheetWriteResult(
    string SheetName,
    int RowCount);

public sealed class ReferenceAnswersSheetWriter
{
    public ReferenceAnswersSheetWriteResult Write(
        SpreadsheetDocument document,
        QuantificationSnapshot snapshot,
        AppOwnedSheetNames sheetNames,
        IEnumerable<ReferenceAnswerSheetRow> rows)
    {
        ArgumentNullException.ThrowIfNull(document);
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The workbook part is missing.");
        return Write(workbookPart, snapshot, sheetNames.ReferencesSheetName, rows);
    }

    public ReferenceAnswersSheetWriteResult Write(
        WorkbookPart workbookPart,
        QuantificationSnapshot snapshot,
        string sheetName,
        IEnumerable<ReferenceAnswerSheetRow> rows)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);
        ArgumentNullException.ThrowIfNull(rows);
        if (!snapshot.HasValidHash())
        {
            throw new InvalidDataException("The quantification snapshot hash is invalid.");
        }

        ImmutableArray<ReferenceAnswerSheetRow> materialized = rows.ToImmutableArray();
        ImmutableArray<QuestionDefinition> expected = snapshot.Definition.Questions
            .Where(question => question.Enabled)
            .ToImmutableArray();
        Validate(expected, materialized);

        SheetData data = new();
        data.Append(Header());
        uint rowNumber = 2;
        foreach (QuestionDefinition question in expected)
        {
            foreach (ReferenceAnswerSheetRow source in materialized
                         .Where(item => string.Equals(item.QuestionId, question.Id, StringComparison.Ordinal))
                         .OrderBy(item => item.SlotIndex))
            {
                string rowText = rowNumber.ToString(CultureInfo.InvariantCulture);
                Row row = new() { RowIndex = rowNumber };
                int slotsPlanned = ReferenceSetPolicy.PlannedSlots(source.SlotCount);
                row.Append(
                    SpreadsheetLiteral.CreateInlineStringCell("A" + rowText, question.Id),
                    SpreadsheetLiteral.CreateInlineStringCell("B" + rowText, question.DisplayName),
                    SpreadsheetLiteral.CreateInlineStringCell("C" + rowText, question.QuestionText),
                    SpreadsheetLiteral.CreateInlineStringCell("D" + rowText, source.ModelId),
                    SpreadsheetLiteral.CreateInlineStringCell("E" + rowText, source.Answer ?? string.Empty),
                    SpreadsheetLiteral.CreateInlineStringCell("F" + rowText, source.StatusCode),
                    SpreadsheetLiteral.CreateInlineStringCell(
                        "G" + rowText,
                        source.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                    SpreadsheetLiteral.CreateInlineStringCell("H" + rowText, source.ReasoningEffort ?? "未指定"),
                    // FR-115 AC-080: slot 識別・予定数・指定条件と、観測できなかった条件（実 model・temperature・seed 等）。
                    SpreadsheetLiteral.CreateInlineStringCell(
                        "I" + rowText,
                        Math.Max(1, source.SlotIndex).ToString(CultureInfo.InvariantCulture)),
                    SpreadsheetLiteral.CreateInlineStringCell(
                        "J" + rowText,
                        slotsPlanned.ToString(CultureInfo.InvariantCulture)),
                    SpreadsheetLiteral.CreateInlineStringCell("K" + rowText, source.ContextTier ?? "未指定"),
                    SpreadsheetLiteral.CreateInlineStringCell("L" + rowText, ReferenceSetPolicy.Unobserved),
                    SpreadsheetLiteral.CreateInlineStringCell("M" + rowText, ComparisonInput),
                    SpreadsheetLiteral.CreateInlineStringCell("N" + rowText, source.TemplateVersion ?? ReferenceSetPolicy.Unobserved),
                    SpreadsheetLiteral.CreateInlineStringCell("O" + rowText, source.ComparisonVersion ?? ReferenceSetPolicy.Unobserved),
                    SpreadsheetLiteral.CreateInlineStringCell("P" + rowText, source.NormalizationVersion ?? ReferenceSetPolicy.Unobserved));
                data.Append(row);
                rowNumber++;
            }
        }

        WorkbookSheetWriter.AddWorksheet(workbookPart, sheetName, new Worksheet(data));
        return new ReferenceAnswersSheetWriteResult(sheetName, materialized.Length);
    }

    private const string ComparisonInput = "質問文字列のみ（学生回答・Prompt・補助列・採点基準・他参照回答・外部検索・履歴を含めない）";

    private static void Validate(
        ImmutableArray<QuestionDefinition> expected,
        ImmutableArray<ReferenceAnswerSheetRow> rows)
    {
        HashSet<(string QuestionId, int Slot)> slots = [];
        foreach (ReferenceAnswerSheetRow? row in rows)
        {
            if (row is null
                || string.IsNullOrWhiteSpace(row.QuestionId)
                || !slots.Add((row.QuestionId, row.SlotIndex))
                || row.SlotCount < 0
                || row.SlotCount > ReferenceSetPolicy.SlotCount
                || (row.SlotCount == 0 ? row.SlotIndex != 0 : row.SlotIndex < 1 || row.SlotIndex > row.SlotCount))
            {
                throw new ArgumentException("Reference rows must have unique question IDs and slots.", nameof(rows));
            }

            try
            {
                EphemeralEvaluationRunner.ValidateModelId(row.ModelId);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException("The reference model ID is invalid.", nameof(rows), exception);
            }

            if (row.ReasoningEffort is not null
                && !ReasoningEffortPolicy.IsSafeReasoningEffort(row.ReasoningEffort))
            {
                throw new ArgumentException("The reference reasoning effort is invalid.", nameof(rows));
            }

            if (string.IsNullOrWhiteSpace(row.StatusCode))
            {
                throw new ArgumentException("The reference status code is required.", nameof(rows));
            }

            if (row.GeneratedAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("The reference timestamp must use UTC.", nameof(rows));
            }

            if (string.Equals(row.StatusCode, "SUCCESS", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(row.Answer))
            {
                throw new ArgumentException("A successful reference row requires an answer.", nameof(rows));
            }

            if (row.Answer is { Length: > ConfigSheetWriter.MaximumCellCharacters })
            {
                throw new ArgumentException("A reference answer exceeds the Excel cell limit.", nameof(rows));
            }
        }

        string[] expectedIds = expected.Select(question => question.Id).ToArray();
        if (rows.Any(row => !expectedIds.Contains(row.QuestionId, StringComparer.Ordinal)))
        {
            throw new ArgumentException("Reference rows must match enabled questions exactly.", nameof(rows));
        }

        foreach (string id in expectedIds)
        {
            ReferenceAnswerSheetRow[] questionRows = rows
                .Where(row => string.Equals(row.QuestionId, id, StringComparison.Ordinal))
                .ToArray();
            // 設問ごとの行数は予定 slot 数（旧形式は 1）と一致し、集合は同じ予定数を共有する。
            if (questionRows.Length == 0
                || questionRows.Any(row => row.SlotCount != questionRows[0].SlotCount)
                || questionRows.Length != ReferenceSetPolicy.PlannedSlots(questionRows[0].SlotCount))
            {
                throw new ArgumentException("Reference rows must match enabled questions exactly.", nameof(rows));
            }
        }
    }

    private static Row Header() => new(
        SpreadsheetLiteral.CreateInlineStringCell("A1", "QuestionId"),
        SpreadsheetLiteral.CreateInlineStringCell("B1", "DisplayName"),
        SpreadsheetLiteral.CreateInlineStringCell("C1", "QuestionText"),
        SpreadsheetLiteral.CreateInlineStringCell("D1", "ModelId"),
        SpreadsheetLiteral.CreateInlineStringCell("E1", "ReferenceAnswer"),
        SpreadsheetLiteral.CreateInlineStringCell("F1", "Status"),
        SpreadsheetLiteral.CreateInlineStringCell("G1", "GeneratedAtUtc"),
        SpreadsheetLiteral.CreateInlineStringCell("H1", "ReasoningEffort"),
        SpreadsheetLiteral.CreateInlineStringCell("I1", "SlotIndex"),
        SpreadsheetLiteral.CreateInlineStringCell("J1", "SlotCount"),
        SpreadsheetLiteral.CreateInlineStringCell("K1", "ContextTier"),
        SpreadsheetLiteral.CreateInlineStringCell("L1", "ObservedConditions"),
        SpreadsheetLiteral.CreateInlineStringCell("M1", "ComparisonInput"),
        SpreadsheetLiteral.CreateInlineStringCell("N1", "SystemTemplateVersion"),
        SpreadsheetLiteral.CreateInlineStringCell("O1", "ComparisonVersion"),
        SpreadsheetLiteral.CreateInlineStringCell("P1", "NormalizationVersion"))
    {
        RowIndex = 1,
    };
}
