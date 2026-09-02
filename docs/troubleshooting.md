# トラブルシューティング

回答やPrompt本文をerror report、issue、chatへ貼らず、表示されたcode、field、件数、file形式だけで切り分けてください。

## ZIPを起動できない

| 状況 | 対処 |
|---|---|
| SHA-256がsidecarと違う | 起動せず、入手元からZIPとsidecarを再取得 |
| Windowsが発行元warningを表示 | packageはunsigned。入手元とhashを確認できなければ実行しない |
| `.exe`が見つからない | ZIP全体を新しいdirectoryへ展開し、package rootの`StudyReportEvaluator.App.exe`を確認 |
| .NETのinstallを求められる | 正しいself-contained `win-x64` packageか確認。source build outputと混同しない |
| Windows 11 x64以外 | 初版対応対象外。互換性を推測して実行しない |

## 入力を読み込めない

| 表示／分類 | 意味・対処 |
|---|---|
| `UnsupportedExtension` | 標準`.xlsx`を指定 |
| `LegacyBinaryWorkbook` | `.xls` / `.xlsb`。元systemから標準`.xlsx`を再export |
| `CommaSeparatedValues` / `PortableDocumentFormat` | CSV / PDF。標準`.xlsx`を用意 |
| `MacroEnabledWorkbook` | `.xlsm`等。macroなしの標準`.xlsx`を用意 |
| `EncryptedOrRightsProtected` | appは復号しない。組織規則に従い復号済みcopyの利用可否を判断 |
| `InvalidZipSignature` / `CorruptPackage` | Open XML packageとして読めない。元systemから再export |
| `UnsafePackage` | resource上限超過。内容を切り詰めず、file構造とsizeを確認 |
| `InvalidRelationship` | external/unsafe relationship等を除いた別copyを用意 |

native pickerが開かない場合はfull pathを直接入力できます。picker取消時は現在の入力状態を変更しません。

## mappingを完了できない

| Code | 対処 |
|---|---|
| `SOURCE_SHEET_NOT_FOUND` | 読込済みsheetから選択し直す |
| `HEADER_METADATA_MISMATCH` | 質問文の行を1または2から選び、見出しを再読込 |
| `SELECTED_ROW_LIMIT_EXCEEDED` | 回答rowを20,000以下へ分ける |
| `PRIMARY_COLUMN_REUSED` | 同じ項目の主列を補助列から外す |
| `DUPLICATE_SUPPORTING_COLUMN` | 重複補助列を1件にする |

候補mappingは確定値ではありません。実際の見出しと授業設計に合わせて変更してください。

## 設計がinvalid

| 状況／Code | 対処 |
|---|---|
| Base/Special/Question pointsが範囲外 | 0〜100の有限値へ戻す |
| `ALLOCATION_TOTAL_INVALID` | `Base + Special + enabled Question points = 100`へ合わせる |
| `SPECIAL_ITEMS_REQUIRED` | Special pointsを0へ戻すかenabled固有評価を追加 |
| `SIMILARITY_WEIGHT_OUT_OF_RANGE` | 0〜1へ戻す |
| `WEIGHT_MUST_BE_POSITIVE` | enabled criterion/evaluator weightを0より大きくする |
| `SCORE_RANGE_INVALID` | minimumをmaximumより小さくする |
| `ROUNDING_OUT_OF_RANGE` | 0〜6へ戻す |
| Prompt error | [Custom evaluator](custom-evaluator-guide.md#主なvalidation)を確認 |

step移動ができても、技術検証がinvalidな状態ではrunを開始できません。

## Copilotを利用できない

CLIはZIPへ同梱され、manifestでpath、RID、version、SHA-256を固定しています。PATH上の別`copilot.exe`は使いません。

| 表示 | 確認 |
|---|---|
| CLI unavailable | ZIPを一部だけ移動していないか確認し、package全体を再展開 |
| runtime確認失敗 | `copilot-runtime.json`または`runtimes\win-x64\native\copilot.exe`の欠落・変更が考えられる。hash確認済みZIPから再展開 |
| loginが必要 | 同梱CLIの対話loginを完了し、**Copilot 状態を確認**を再実行 |
| modelがない | accountで利用できるmodelを確認。model IDを推測入力しない |
| `auto`がない | Reference/Similarityに必要。別modelへfallbackせず、CLI/account状態を確認 |
| model容量を取得できない | 別の列挙済みmodelを選ぶかruntime状態を再確認。上限を推測して回避しない |

アプリへtoken、password、client secretを入力しないでください。

## capacity error

| Code | 対処 |
|---|---|
| `COLUMN_LIMIT_EXCEEDED` / `HEADER_CELL_LIMIT_EXCEEDED` | enabled Question/evaluator/criterion数またはID長を減らす |
| `FORMULA_LENGTH_EXCEEDED` / `FUNCTION_ARGUMENT_LIMIT_EXCEEDED` | enabled child数を減らす |
| `REQUEST_SCALAR_LIMIT_EXCEEDED` | Prompt、補助列、criterionを縮小 |
| `REQUEST_CONTEXT_BUDGET_EXCEEDED` | contentを黙って切らず、定義または選択列を見直す |
| `ATTEMPT_BUDGET_TOO_LARGE` | 対象rowまたはenabled evaluatorを分割 |

これらは新しいAI送信前に検査されます。

## run status

| Status | 意味 | scoreへの影響 |
|---|---|---|
| `SUCCESS` | valid result | 数値を使用 |
| `EMPTY` | 主値が空 | AI callなし、0相当 |
| `NOT_RUN_ZERO_BUDGET` | Special pointsが0 | 固有評価callなし、Special earned 0 |
| `AI_OUTPUT_INVALID` | schema、ID、range、source等が不正 | 対象値blank |
| `AI_TIMEOUT` | retry後もtimeout | 対象値blank |
| `NETWORK_FAILED` | retry後もnetwork failure | 対象値blank |
| `AUTH_REQUIRED` | login利用不可 | 対象値blank |
| `CANCELLED` | 未完了operation | 対象値blank、保存済みpartialは保持 |
| `CLEANUP_FAILED` | sessionまたはfile cleanup失敗 | 表示されたcauseとpathを確認 |
| `AI_RUNTIME_FAILED` | その他runtime failure | 対象値blank |

空回答の0と技術的失敗のblankを混同しないでください。

## checkpointから再開できない

再開時は次を完全一致で検証します。

- checkpoint schema
- input path、SHA-256、size、last-write time
- definition SHA-256
- normal modelと`auto`
- app schema compatibility
- CLI version/SHA-256とSDK identity

不一致時はpartialを変更せず、同じ入力・設計・model・packageで再試行するか、新規runを開始します。保存済みfinalはcheckpointとして指定できません。

## finalが作られない

| 表示／Code | 対処 |
|---|---|
| `TARGET_EXISTS` | 既存fileを削除せず、新規runなら次のsuffix、override出力なら別名を使う |
| `INPUT_CHANGED` | 入力編集を完了してから最初から読み直す |
| `OUTPUT_INVALID` | partialを保持し、package/formula検証のsafe codeを確認 |
| `CANCELLED` | partial pathを確認し、条件が一致する場合は再開 |
| `PARTIAL_CLEANUP_FAILED` | finalがvalidならfinalは保持される。表示されたpartialを内容を開かず管理 |

新規runのfinalは全処理成功時に自動作成されます。Resultsのoverride反映版だけが任意の別名出力です。

## outputを扱うとき

final/partialは入力全体、Prompt、Reference、AI resultを含み得ます。issueへ添付せず、必要な場合は機密本文を含まないcode・件数・basenameだけを共有してください。詳しくは[データとprivacy](privacy-and-data-handling.md)を参照してください。
