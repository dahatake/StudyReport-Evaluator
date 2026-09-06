# トラブルシューティング

回答やPrompt本文をerror report、issue、chatへ貼らず、表示されたcode、field、件数、file形式だけで切り分けてください。

> **対象版・確認範囲（2026-09-06）:** 公開`v0.8.1`はZIP配布で、単一EXEも「GitHubにログイン」buttonもありません。本頁の単一EXEとlogin buttonの説明は、**UNRELEASED（未公開）の`0.8.4`候補**向けです。
>
> P06の実EXE試験は開発hostで**7件すべてPASS・独立レビュー指摘0**です。一方、**clean-host（MOTW／Windows保護の実測を含む）と本人loginは`NOT_RUN`**です。今後のEXE主配布は、最終成果物のCH-01〜06のPASSと公開判定待ちであり、まだ公開済みではありません。同梱文書や製品版の変更でEXEのbytesが変われば、再package・当該EXEの再検証が必要です。

以下は症状を切り分けるための確認事項です。列挙した症状すべてを、再現済みの製品不具合として扱うものではありません。

## 単一EXEを起動できない（未公開候補）

候補`StudyReportEvaluator-win-x64.exe`は.NET標準hostが、通常は標準userの一時領域`%TEMP%/.net/<app>/<bundle-id>/`へ内容を展開してからGUIを起動します。**EXEの配置先と抽出先は別**で、EXEを置いたdirectoryがread-onlyであることと、抽出先へ書き込めないことを区別します。

| 状況 | 対処 |
|---|---|
| 入力画面が開く前に抽出先への書込が拒否される | 現在の標準userの一時領域への書込権限を確認。拒否が続く場合は停止して管理者へ相談し、管理者起動や保護設定変更で回避しない |
| 抽出中に空き容量不足になる | EXE保存先だけでなく、抽出先のvolumeの空き容量を確認。EXE本体に加えて展開用の容量が必要。起動のために入力／final／partialを移動・削除しない |
| 以前のcacheが欠落した、抽出が中断された | アプリとその所有CLIの終了を確認後、同じ版の完全なEXEから再起動して.NET標準hostの欠落復元を確認。再失敗時は権限・容量・Windows保護を切り分け、cacheへDLLやCLIを手動で継ぎ足さない |
| EXEのSHA-256がsidecarと違う | 実行せず、同じ版のEXEとsidecarを正式配布元から再取得。不一致のまま実行しない |
| .NETのinstallを求められる | 正しいself-contained `win-x64`配布物か確認。source build outputや別RIDと混同せず、追加runtime導入で回避しない |

アプリとその所有CLIの終了後も、cacheは残って再利用され得ます。これは**アプリ配置用file**であり、学生の入力／final／partialやCLI credential storeとは別です。単一EXEは「ディスク上も1ファイル」「痕跡なし」を意味しません。アプリによる独自cache管理・自動掃除はなく、本頁でも任意directoryや一時領域全体の再帰削除を復旧手順にしません。既定の結果保存先は従来どおり入力隣接の`result`で、抽出cacheではありません。

## ZIPを起動できない

公開`v0.8.1`では`StudyReportEvaluator-win-x64.zip`を使います。単一EXEの公開後もZIPは代替経路として維持します。

| 状況 | 対処 |
|---|---|
| SHA-256がsidecarと違う | 起動せず、同じreleaseのZIPとsidecarを正式配布元から再取得。異なる版の組合せで比較しない |
| Windowsが発行元warningを表示 | packageはunsigned。[Windowsの警告・実行拒否](#windowsの警告実行拒否)を確認し、出所不明・hash不一致なら実行しない |
| `.exe`が見つからない | ZIP全体を新しいdirectoryへ展開し、package rootの`StudyReportEvaluator.App.exe`を確認 |
| .NETのinstallを求められる | 正しいself-contained `win-x64` packageか確認。source build outputと混同しない |
| Windows 11 x64以外 | 初版対応対象外。互換性を推測して実行しない |

## Windowsの警告・実行拒否

EXE／ZIPはunsignedです。SmartScreen、Smart App Control（SAC）、企業policyによる警告・実行拒否は、抽出先の権限・容量不足とは分けて確認してください。code signing済み、SmartScreen reputation確立済み、すべての端末で無警告・無条件に1操作で起動できるとは保証しません。

- 利用者の手動SHA-256比較は任意・推奨で、sidecarは起動依存ではありません。比較する場合は同じrelease／候補のsidecarと完全一致を確認し、不一致や入手元不明の場合は実行しないでください。同じ配布元のhash一致だけでは発行者の真正性やreputationを保証しません。
- 保護機能が実行を拒否した場合は停止し、保護機能名と表示されたcodeを確認して所属組織の管理者へ相談してください。拒否を起動成功として扱いません。
- 保護機能の無効化、MOTW除去、証明書の自動trust、execution policy変更、UAC回避は行いません。ZIP代替も保護拒否の回避策ではありません。

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

### 起動引数の入力・Promptが見つからない

候補の`StudyReportEvaluator-win-x64.exe`とZIP内の`StudyReportEvaluator.App.exe`は、同じ`--input`／`--prompt`契約です。

- 相対pathの基準は**起動時のcwd**です。EXE配置先や.NET抽出cacheではありません。起動時の作業directoryと指定pathの組合せを確認するか、実在するfull pathを指定してください。入力やPromptをcacheへ移動する必要はありません。
- 日本語・空白を含むpathは1つのargumentとして引用します。`--input`は0または1回、`--prompt`は複数指定でき、指定順を維持します。同じPrompt pathの重複は最初の1件だけを使います。
- unknown option、重複`--input`、値なしは拒否されます。Promptはstrict UTF-8の`.txt`、1〜32,767 UTF-16 code unitsです。詳細は[Promptファイルから起動](prompt-launch.md)を確認してください。
- 起動引数はGUIの準備用です。Promptは適用先を選んで**Promptを適用**した時だけcopyされ、起動・適用からloginやAI評価を自動開始しません。

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

CLIはEXE／ZIPの配布物へ同梱され、manifestでpath、RID、SDK／CLI version、SHA-256を検証します。PATH上の別`copilot.exe`は使いません。GUI起動とAI利用は別条件で、AIにはnetwork、本人login、利用可能なCopilot account／model、組織policy上の許可が必要です。

| 表示 | 確認 |
|---|---|
| CLI unavailable | 配布物の欠落を確認。ZIPを一部だけ移動していないか確認し、下記の再取得・再展開で復旧 |
| runtime確認失敗 | Start／Ping、認証・model取得、timeout、終了処理でも発生する。network・現在の処理状態・組織policyを確認し、所有CLIの終了を確認してから**Copilot 状態を確認**を明示的に再実行。不整合が確認できた場合だけ下記の再取得へ進む |
| CLIのhash／版不一致が確認された | `copilot-runtime.json`または`runtimes\win-x64\native\copilot.exe`の欠落・変更、RID／version／SHA-256不一致を切り分ける。検証を緩めず、下記の再取得・再展開で確認 |
| loginが必要 | 対象版に応じて同梱CLIの本人対話loginを完了し、利用者が**Copilot 状態を確認**を再実行。対象版別の操作は下記を参照 |
| modelがない | accountで利用できるmodelを確認。model IDを推測入力しない |
| `auto`がない | Reference/Similarityに必要。別modelへfallbackせず、CLI/account状態を確認 |
| model容量を取得できない | 別の列挙済みmodelを選ぶかruntime状態を再確認。上限を推測して回避しない |

CLI欠落・不一致時は処理を止め、利用中と**同じ製品版の正式配布物を再取得**してください。ZIP版はpackage全体を新しいdirectoryへ展開します。.NET標準抽出は書換え済みcacheの完全性を保証せず、再取得したEXEでも同じcacheを再利用し得ます。不一致が続く場合は、提供元が同じ製品版・runtime identityのZIPを提供していれば新しいdirectoryへ全体を展開し、なければ提供元へ確認してください。未公開候補の代わりに公開`v0.8.1`を使ってcheckpoint互換を推測しないでください。

PATH上の別CLIのinstall／追加、他版CLIのcopy、manifest変更やhash検証緩和で回避しないでください。

### 本人loginと完了後の再確認

**「GitHubにログイン」「ログインを取り消す」は未公開`0.8.4`候補のみ**です。公開`v0.8.1`にはこのbuttonがなく、従来どおり同梱CLIで本人loginを行い、既存の状態確認buttonを使います。buttonがないことだけを不具合と判断しないでください。

1. Execution画面で**Copilot 状態を確認**を選びます。この操作はlogin開始ではありません。
2. 未認証の場合、候補版では利用者自身が**GitHubにログイン**を選びます。検証済みの同梱native CLIだけを直接起動し、CLIのconsole／ブラウザーの案内に従って本人のGitHub accountで対話認証を完了します。
3. 完了・取消・失敗後やアプリ再起動後は、利用者が**Copilot 状態を確認**をもう一度選びます。CLI processの起動・終了やexit codeだけを認証成功とは扱いません。
4. 状態と列挙されたmodelを確認し、利用者が通常評価用modelを選択します。AI評価は**定量化を開始**の明示操作まで始まりません。

GUI起動・起動引数・Prompt適用・状態確認からloginを暗黙に開始せず、login後もmodelを自動変更したりAI評価を自動開始したりしません。アプリへPAT、token、password、client secret、device codeを入力しないでください。認証とcredential保管はCLI／ブラウザーに委譲し、アプリはこれらを収集・解析・保存・log出力しません。認証console／ブラウザーの秘密情報をerror reportへ貼らないでください。

### loginの失敗・取消・終了処理

次は未公開候補のlogin専用処理です。評価runの`CLEANUP_FAILED`や、final完成後の`PARTIAL_CLEANUP_FAILED`とは区別します。

| 状況 | 対処 |
|---|---|
| login buttonを押せない | loginの二重開始や評価中の開始を防ぎます。現在の処理状態と終了を確認し、buttonを連打して別CLIを開始しない |
| console／ブラウザーが開かない、本人認証に失敗する | 同梱CLI検証、network接続、本人accountのCopilot利用権限と組織policyを確認。CLIの対話が残っていれば**ログインを取り消す**で取り消し、終了を確認してから明示的に再試行。Windowsの拒否なら保護設定を回避しない |
| 「ログイン処理が終了しました。認証状態は未確認です」と表示 | 自動で認証済みにはしません。利用者が**Copilot 状態を確認**を選ぶ。未認証なら上記の本人loginを確認 |
| 取消後もブラウザーが開いている、既存loginが残る | 取消はlogoutではありません。下記の所有範囲に従う動作で、ブラウザーやcredentialを削除して解決しようとしない |
| 「ログイン処理の終了を確認できませんでした」等の子process終了・cleanup失敗 | login再試行・AI開始を控え、入力・既存final／partialを保持する。アプリの再起動や認証確認成功だけでは旧login CLIの終了を保証しない。旧処理の終了を確認できてから再確認し、確認できなければsafeな表示情報だけで問い合わせる。名前一致の一括強制終了は行わない |

**ログインを取り消す**またはアプリ終了時に終了・解放する対象は、**このアプリが開始・所有した当該login CLI processだけ**です。process名一致やprocess tree全体の一括killは行わず、ブラウザー・他のCLIを終了しません。credential storeの削除、logout、失効も行いません。終了失敗時も名前指定での強制終了を復旧手順にしないでください。

取消・失敗後もExcel読込、mapping、設計編集、checkpoint確認は利用できます。実行中のアプリはlogin終了未確認時に認証確認・評価を停止しますが、この状態は再起動を越えて保存されません。旧処理の終了未確認のまま、新しいloginやAI処理を開始しないでください。

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
