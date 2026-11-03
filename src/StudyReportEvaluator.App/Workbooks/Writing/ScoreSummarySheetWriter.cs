using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace StudyReportEvaluator.App.Workbooks.Writing;

internal sealed record ScoreSummaryQuestion(
    int Number,
    string EvaluationColumn,
    string? PromptColumn,
    int PromptEvaluatorIndex);

internal sealed record ScoreSummaryRow(
    int SourceRow,
    decimal?[] Evaluation,
    decimal?[] Prompt,
    decimal? FinalScore);

internal sealed record ScoreSummarySource(
    string ResultsSheetName,
    string SourceRowColumn,
    string FinalScoreColumn,
    IReadOnlyList<ScoreSummaryQuestion> Questions,
    IReadOnlyList<ScoreSummaryRow> Rows);

/// <summary>
/// FR-113: 完成した結果workbookにだけ入る `採点結果` シート。
/// 値は Quantification_Results（実際に書かれたシート名）を参照する数式で、空欄は空欄のまま。
/// </summary>
public sealed class ScoreSummarySheetWriter
{
    public const string BaseName = "採点結果";
    public const string IdHeader = "ID";
    public const string TotalHeader = "合計";

    private readonly UntrustedStringCellWriter stringCellWriter = new();

    /// <summary>`採点結果` の実シート名を、既存のシート名衝突回避規則で決める。</summary>
    public static string ResolveName(AppOwnedSheetNameResolver resolver, IEnumerable<string> existingSheetNames, AppOwnedSheetNames sheetNames)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(existingSheetNames);
        ArgumentNullException.ThrowIfNull(sheetNames);
        return resolver.ResolveUniqueName(BaseName, existingSheetNames.Concat(sheetNames.AllSheetNames));
    }

    public void Write(
        SpreadsheetDocument document,
        AppOwnedSheetNames sheetNames,
        ResultsSheetWriteResult results)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sheetNames);
        ArgumentNullException.ThrowIfNull(results);
        string summaryName = sheetNames.ScoreSummarySheetName
            ?? throw new InvalidOperationException("The score summary sheet name is not bound.");
        ScoreSummarySource source = results.ScoreSummary
            ?? throw new InvalidOperationException("The results sheet did not provide score summary data.");
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The workbook part is missing.");

        string sheetRef = "'" + source.ResultsSheetName.Replace("'", "''", StringComparison.Ordinal) + "'!";
        SheetData data = new();

        Row header = new() { RowIndex = 1 };
        int column = 1;
        stringCellWriter.Write(header, Ref(column++, 1), IdHeader);
        foreach (ScoreSummaryQuestion question in source.Questions)
        {
            string n = question.Number.ToString(CultureInfo.InvariantCulture);
            stringCellWriter.Write(header, Ref(column++, 1), $"質問{n}の評価");
            if (question.PromptColumn is not null)
            {
                stringCellWriter.Write(header, Ref(column++, 1), $"質問{n}のPromptの評価");
            }
        }

        stringCellWriter.Write(header, Ref(column, 1), TotalHeader);
        data.Append(header);

        int rowNumber = 1;
        foreach (ScoreSummaryRow source1 in source.Rows)
        {
            rowNumber++;
            int src = source1.SourceRow;
            Row row = new() { RowIndex = checked((uint)rowNumber) };
            column = 1;
            row.Append(FormulaCell(Ref(column++, rowNumber), sheetRef + source.SourceRowColumn + src.ToString(CultureInfo.InvariantCulture), src));
            for (int q = 0; q < source.Questions.Count; q++)
            {
                ScoreSummaryQuestion question = source.Questions[q];
                row.Append(BlankSafeCell(Ref(column++, rowNumber), sheetRef, question.EvaluationColumn, src, source1.Evaluation[q]));
                if (question.PromptColumn is not null)
                {
                    row.Append(BlankSafeCell(Ref(column++, rowNumber), sheetRef, question.PromptColumn, src, source1.Prompt[q]));
                }
            }

            row.Append(BlankSafeCell(Ref(column, rowNumber), sheetRef, source.FinalScoreColumn, src, source1.FinalScore));
            data.Append(row);
        }

        WorkbookSheetWriter.AddWorksheet(workbookPart, summaryName, new Worksheet(data));
    }

    public override string ToString() =>
        $"{nameof(ScoreSummarySheetWriter)} {{ Content = <redacted> }}";

    // 参照元が数値のときだけ値を出し、空欄・失敗（数値でない）は "" のまま（0にしない）。
    private static Cell BlankSafeCell(string reference, string sheetRef, string column, int sourceRow, decimal? cached)
    {
        string target = sheetRef + column + sourceRow.ToString(CultureInfo.InvariantCulture);
        return FormulaCell(reference, $"IF(ISNUMBER({target}),{target},\"\")", cached);
    }

    private static Cell FormulaCell(string reference, string formula, decimal? cached)
    {
        Cell cell = new() { CellReference = reference, CellFormula = new CellFormula(formula) };
        if (cached is decimal value)
        {
            cell.DataType = CellValues.Number;
            cell.CellValue = new CellValue(value.ToString("G29", CultureInfo.InvariantCulture));
        }
        else
        {
            cell.DataType = CellValues.String;
            cell.CellValue = new CellValue(string.Empty);
        }

        return cell;
    }

    private static string Ref(int column, int row)
    {
        Span<char> buffer = stackalloc char[3];
        int position = buffer.Length;
        int remaining = column;
        while (remaining > 0)
        {
            remaining--;
            buffer[--position] = (char)('A' + (remaining % 26));
            remaining /= 26;
        }

        return new string(buffer[position..]) + row.ToString(CultureInfo.InvariantCulture);
    }
}
