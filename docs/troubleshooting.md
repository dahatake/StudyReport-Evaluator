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
> Designのsnapshot preflightがinvalidでもstep navigation自体は次へ進めます。現在はExecution開始時にPromptを再検査しないため、必ずDesignへ戻り「設計は有効です」を確認してください。既知差分: [`implementation-status.md` IMPL-GAP-001](../docs-dev/implementation-status.md#impl-gap-001--custom-promptをrun開始前に再検査しない)。

## Copilotを利用できない

![Copilot状態、Auto model、concurrency、evaluation planを確認する実行画面。認証状態はfake](../images/05-execution-auto.png)

| 表示 | 確認事項 |
|---|---|
| CLI unavailable | `copilot.exe`が導入済みでWindowsの`PATH`から解決できるか |
| loginが必要 | Copilot CLI側で対話loginを行い、アプリで再確認 |
| runtime確認失敗 | CLI fileが0 byteでないか、version情報を読めるか、15秒以内にstart/pingできるか |
| modelがない | login/accountの利用条件をCLI側で確認。model IDを推測入力しない |

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

## AI実行後にexportで上限エラーになる

現行実装ではResults列数、formula長、function argument数の完全なpreflightはexport時です。run前に全capacityを拒否する要求との差分があり、多数unitのAI処理後にexport不能となる可能性があります。定義を小さく分割してください。

既知差分: [`implementation-status.md` IMPL-GAP-002](../docs-dev/implementation-status.md#impl-gap-002--excel--request-capacityの完全なpreflightがrun前ではない)。

## package版とsource版

self-contained package版は.NET runtimeを同梱するため、利用端末へ.NETを別途導入する必要はありません。source buildには`global.json`で選択される.NET SDK 10.0.400 feature bandが必要です。外部仕様: Microsoft [.NET application publishing overview](https://learn.microsoft.com/dotnet/core/deploying/#publish-as-self-contained)（2026-09-01確認）。repository根拠: [`global.json`](../global.json)、[`scripts/publish-windows.ps1`](../scripts/publish-windows.ps1)。
