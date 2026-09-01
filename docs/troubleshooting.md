# トラブルシューティング

対象読者は利用者と一次サポート担当です。error本文へ回答やPrompt本文を転記せず、code、field、件数、file形式だけで切り分けてください。

## 入力fileを読み込めない

| 状態／code | 意味 | 対処 |
|---|---|---|
| `UnsupportedExtension` | `.xlsx`以外 | 標準 `.xlsx`を指定 |
| `LegacyBinaryWorkbook` | `.xls` / `.xlsb` | `.xlsx`へ安全に変換したcopyを用意 |
| `CommaSeparatedValues` / `PortableDocumentFormat` | CSV / PDF | 標準 `.xlsx`を用意 |
| `MacroEnabledWorkbook` | `.xlsm`等 | macroを含まない標準 `.xlsx`を用意 |
| `EncryptedOrRightsProtected` | password／rights protectionまたは暗号化container | 復号済みcopyの利用可否を組織規則に従って判断。アプリは復号しない |
| `InvalidZipSignature` / `CorruptPackage` | Open XML packageとして読めない | 元systemから再exportする |
| `UnsafePackage` | resource安全上限を超過 | file構造とsizeを確認し、内容を黙って切り詰めない |
| `InvalidRelationship` | external／不正relationship等 | 外部link等を除いた安全なcopyを別途作成する |

実装根拠: [`FileFormatClassifier.cs`](../src/StudyReportEvaluator.App/Workbooks/Intake/FileFormatClassifier.cs#L8-L24)、UI message: [`InputViewModel.cs`](../src/StudyReportEvaluator.App/ViewModels/InputViewModel.cs#L1324-L1367)。

### package安全上限

| Dimension | 現行上限 |
|---|---:|
| 入力file | 100 MiB |
| 展開総量 | 1 GiB |
| 1 ZIP entry | 256 MiB |
| ZIP entry数 | 10,000 |
| 圧縮率 | 100:1 |
| 1 partの文字数 | 64 Mi characters |

根拠: [`FileFormatClassifier.cs`](../src/StudyReportEvaluator.App/Workbooks/Intake/FileFormatClassifier.cs#L58-L65)。Excelのworksheet、cell、sheet name等の媒体上限はMicrosoftの[Excel specifications and limits](https://support.microsoft.com/office/excel-specifications-and-limits-1672b34d-7043-467e-8e27-269d656771c3)も参照してください（2026-09-01確認）。

## mapping／設計を完了できない

| Code | 対処 |
|---|---|
| `SOURCE_SHEET_NOT_FOUND` | 読込済みsheetから選択し直す |
| `HEADER_METADATA_MISMATCH` | 見出し行を再読込する |
| `SELECTED_ROW_LIMIT_EXCEEDED` | 回答行を20,000以下へ分割する |
| `PRIMARY_COLUMN_REUSED` | 同じ質問のprimaryをsupportingから外す |
| `DUPLICATE_SUPPORTING_COLUMN` | 重複supportingを1件にする |
| `WEIGHT_MUST_BE_POSITIVE` | enabled nodeのweightを0より大きくする |
| `SCORE_RANGE_INVALID` | minimumをmaximumより小さくする |
| `ROUNDING_OUT_OF_RANGE` | 0〜6へ戻す |

根拠: [`QuantificationDefinitionValidator.cs`](../src/StudyReportEvaluator.Core/Validation/QuantificationDefinitionValidator.cs)、[`ColumnMappingValidator.cs`](../src/StudyReportEvaluator.App/Workbooks/Mapping/ColumnMappingValidator.cs)。

## Custom Promptがinvalid

placeholder別の対処は[Custom evaluatorガイド](custom-evaluator-guide.md#validation-errors)を参照してください。

> [!IMPORTANT]
> Designのsnapshot preflightがinvalidでもstep navigation自体は次へ進めますが、Executionとsnapshot作成で同じPrompt検証を再実行するためrunは開始できません。Designへ戻り、該当fieldを修正してください。

## Copilotを利用できない

![Copilot状態、Auto model、concurrency、evaluation planを確認する実行画面。認証状態はfake](../images/05-execution-auto.png)

| 表示 | 確認事項 |
|---|---|
| CLI unavailable | `copilot.exe`が導入済みでWindowsの`PATH`から解決できるか |
| loginが必要 | Copilot CLI側で対話loginを行い、アプリで再確認 |
| runtime確認失敗 | CLI fileが0 byteでないか、version情報を読めるか、15秒以内にstart/pingできるか |
| modelがない | login/accountの利用条件をCLI側で確認。model IDを推測入力しない |
| model prompt上限を取得できない | SDKのmodel一覧がprompt/context上限を返していません。別の利用可能modelを選ぶか、CLI / SDK状態を再確認。上限を推測入力して回避しない |

アプリへtokenやpasswordを入力しないでください。実装根拠: [`CopilotClientFactory.cs`](../src/StudyReportEvaluator.App/Copilot/CopilotClientFactory.cs)、[`CopilotAuthenticationService.cs`](../src/StudyReportEvaluator.App/Copilot/CopilotAuthenticationService.cs)。

## run status

| Status | 意味 | score |
|---|---|---|
| `EMPTY` | primaryが空白 | blank。override不可 |
| `AI_OUTPUT_INVALID` | schema、ID、range、evidence等が不正 | valid overrideがなければblank |
| `AI_TIMEOUT` | retry上限後もtimeout | 同上 |
| `NETWORK_FAILED` | retry上限後もnetwork failure | 同上 |
| `AUTH_REQUIRED` | 認証不可 | 同上 |
| `CANCELLED` | 未完了unit | 同上。部分出力可 |
| `CLEANUP_FAILED` | ephemeral session cleanup failure | 同上 |
| `AI_RUNTIME_FAILED` | その他のruntime failure | 同上 |

根拠: [`ResultsStatusCodes`](../src/StudyReportEvaluator.App/Workbooks/Writing/ResultsSheetWriter.cs#L11-L40)、[`EvaluationScheduler.cs`](../src/StudyReportEvaluator.App/Workflow/EvaluationScheduler.cs#L516-L586)。

## outputできない

| 表示／code | 対処 |
|---|---|
| output path invalid | 入力と異なる `.xlsx`、既存directory、未使用の完成名を指定 |
| `TARGET_EXISTS` | 既存fileを削除せず、別名を指定 |
| `OVERRIDE_INVALID` | 不正overrideをrange内数値へ直すかclear |
| `INPUT_CHANGED` | 入力の編集を完了後、最初からread-onlyで再読込してrunする |
| `OUTPUT_INVALID` | package/formula検証失敗。完成名は作られない |
| `CANCELLED` | final commit前に取消済み |
| `CLEANUP_FAILED` | target directoryに`.study-report-evaluator-*.working.xlsx`が残っていないか、内容を開かず管理者へ連絡 |

実装根拠: [`ResultsOutputViewModel.cs`](../src/StudyReportEvaluator.App/ViewModels/ResultsOutputViewModel.cs#L134-L177)、[`AtomicOutputCommitter.cs`](../src/StudyReportEvaluator.App/Workbooks/Writing/AtomicOutputCommitter.cs#L5-L14)。

## run開始前にcapacity errorになる

| Code | 意味／対処 |
|---|---|
| `COLUMN_LIMIT_EXCEEDED` / `HEADER_CELL_LIMIT_EXCEEDED` | Results列数またはheaderがExcel上限を超過。question / evaluator / criterion数またはID長を減らす |
| `FORMULA_LENGTH_EXCEEDED` / `FUNCTION_ARGUMENT_LIMIT_EXCEEDED` | 実際に生成するformulaが8,191文字または255引数を超過。enabled child数を減らす |
| `REQUEST_SCALAR_LIMIT_EXCEEDED` | Prompt + schema + tool contractが65,536 Unicode scalarsを超過。Prompt、補助列、criterionを縮小する |
| `REQUEST_CONTEXT_BUDGET_EXCEEDED` | app-owned requestの保守的UTF-8容量がSDK model上限の80%を超過。本文を切り詰めず、定義または選択列を見直す |
| `ATTEMPT_BUDGET_TOO_LARGE` | evaluation units × 最大3 attemptsが20,000を超過。対象行またはenabled evaluatorを分割する |

これらはAI dispatch前に全selected rowを測定し、本文をerrorへ表示しません。export時にも同じformula validatorを最終防御として再実行します。

## package版とsource版

self-contained package版は.NET runtimeを同梱するため、利用端末へ.NETを別途導入する必要はありません。source buildには`global.json`で選択される.NET SDK 10.0.400 feature bandが必要です。外部仕様: Microsoft [.NET application publishing overview](https://learn.microsoft.com/dotnet/core/deploying/#publish-as-self-contained)（2026-09-01確認）。repository根拠: [`global.json`](../global.json)、[`scripts/publish-windows.ps1`](../scripts/publish-windows.ps1)。
