# StudyReport Evaluator 要求定義書

| 項目 | 内容 |
|---|---|
| 文書版 | 3.0 |
| 基準日 | 2026-09-01 |
| 状態 | 動的Prompt定量化・Excel加重計算版（実装前） |
| 入力 | レポート回答を含む標準 `.xlsx` 1ファイル |
| 出力 | 入力を変更せず作成する別の `.xlsx` 1ファイル |
| 初版対応環境 | Windows 11 x64 |
| UI / Runtime | Avalonia / .NET 10 |
| AI | GitHub Copilot SDK for .NET。既存のCopilot CLIログインを使用 |
| 旧版 | v2.0は本版により全面的にsupersede |

> 本版は、2026-09-01の要求所有者指示「入力Excelの内容をPromptで定量化し、アプリで設定した重みをExcel数式で計算し、別ファイルへ出力する」「知識評価と自由Prompt評価を動的に増減する」「教育倫理は警告表示だけにし、処理を制限しない」を反映する。
>
> `/samle/`は実在しないため、サンプル正本は`sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx`とする。

## 1. 目的

本アプリケーションは、Forms等から出力されたレポート回答Excelを読み込み、利用者が選択した回答列へ1件以上の評価方法を適用して数値化し、その数値を利用者がアプリ内で設定した重みとExcel数式で集計した別のExcelファイルを作る。

最小workflowは次のとおりである。

1. 入力 `.xlsx` を選択する。
2. 回答sheet、見出し行、回答行、定量化する列を選択する。
3. 質問ごとに評価方法を追加する。
4. 知識評価では、含まれるべき知識ポイントを入力する。
5. それ以外の評価では、分析用Promptを入力する。
6. 評価項目と重みをアプリで設定する。
7. Copilotが評価項目ごとの数値を返す。
8. 別ファイルのExcel数式が評価方法別点、質問別点、全体点を計算する。

## 2. 基本原則

1. 入力workbookは変更しない。
2. 出力は必ず別fileとして作る。
3. 質問数、評価方法数、評価項目数を固定しない。
4. AIは評価項目ごとのraw数値だけを返し、重み付けと集計はExcel数式で行う。
5. 重みは利用者がアプリ内で設定し、出力workbookのConfig sheetへ保存する。
6. 倫理上の注意は画面へ表示するだけで、設定、checkbox、承認、score gate、実行blockにしない。
7. 妥当なAI数値は既定で直ちにExcel式へ使い、有効range内の任意の手動上書きがある場合だけ上書き値を優先する。
8. 技術的に不正なfile、設定、AI応答は安全に停止または空欄化する。これは倫理制限ではなく、破損・計算不能・security事故を防ぐ技術境界である。

## 3. 利用者が用意するもの

### 3.1 必須

- Forms等の回答を含む標準Office Open XML `.xlsx` 1ファイル

質問、知識ポイント、分析Prompt、評価項目、配点range、重みはアプリ内で入力・保存できる。実装前の別artifactとして提供する必要はない。

### 3.2 Runtime前提

AI定量化を実行する端末では、GitHub Copilotを利用できるaccountでCopilot CLIへlogin済みである必要がある。

- アプリ固有OAuth App、client ID、client secret、PATを要求しない。
- credentialは利用者がGitHubとの対話画面で直接扱い、アプリの設定・log・Excelへ保存しない。
- 未login時はAI実行だけを利用不可とし、Excel読込、定義編集、既存結果表示は利用できる。
- アプリの読込、preview、出力にMicrosoft Excel、Office、LibreOffice、COM automationのinstallを要求しない。

## 4. サンプルworkbook profile

### 4.1 実測identity

2026-09-01に、回答本文を出力せずread-onlyで構造だけを確認した。

| 項目 | 実測値 |
|---|---|
| Path | `sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx` |
| Bytes | 661,189 |
| SHA-256 | `446386E20BB4096561CB4AFD6D74B8EAA9D50EAE53C97F984BA7F70EBEAD0DE5` |
| Container | 読取可能なZIP / Office Open XML |
| ZIP entries | 18 |
| Structural entries | `[Content_Types].xml`; `xl/workbook.xml`; `xl/_rels/workbook.xml.rels` |
| `Old` | `A1:AF531` |
| `Original` | `A1:L531` |
| `Final` | `A1:AD531` |

### 4.2 初期mapping候補

`Original`を初期候補sheet、1行目を見出し、2〜531行を回答候補とする。次は見出しから得た候補であり、固定mappingではない。

| Column | 初期候補 |
|---|---|
| A〜E | 管理metadata。初期状態では評価対象にしない |
| F | 質問1のレポート回答 |
| G | 質問1に関連する学生Prompt |
| H | `質問`という別回答。利用者が評価対象または補助列として選択可能 |
| I | 質問2のレポート回答 |
| J | 質問2に関連する学生Prompt |
| K | Prompt作成時の工夫・観点・論点。Jの補助列候補 |
| L | PBL feedback。初期状態では評価対象にしない |

`Old`と`Final`は入力内に保持するが、初期評価対象にしない。アプリはsheet名や列位置だけで確定せず、利用者が変更できるmapping候補として表示する。

## 5. Excel入力契約

### 5.1 行・列

1. 既定では1行目を見出し、2行目以降を回答とする。
2. 1行を1回答者または1提出として扱う。
3. sheet、見出し行、開始行、終了行は変更できる。
4. 1つの質問定義は1つの主回答列と0件以上の補助列を持つ。
5. 主回答には任意の選択列を使える。レポート本文列だけでなく、学生Prompt列をCustom evaluatorの主回答にすることもできる。
6. 同じ列を複数の質問または補助情報へ使用できる。同一質問内では、補助列の重複と主回答列との重複を拒否する。
7. 空の主回答は`EMPTY`としてAIへ送らず、`ScorableCell=0`、AI raw cell、effective raw、normalized score、上位集計を空欄にする。score cellへ0やsentinel文字列を入れず、overrideも受け付けない。
8. 補助列が空でも主回答があれば評価を続ける。
9. 質問の追加、複製、並べ替え、無効化、削除ができる。
10. mapping候補は自動提示できるが、すべて画面で変更できる。

### 5.2 対応file

- 標準 `.xlsx`だけを受け入れる。
- `.xls`、CSV、PDF、macro-enabled形式、破損ZIP、password／rights-protected fileは対象外として明示する。
- 入力をread-onlyで開く。
- 処理前後のSHA-256、size、last-write timeを比較する。
- 同じpathまたは同じfileへ出力しない。

## 6. 動的定量化definition

### 6.1 Root

1つの`QuantificationDefinition`は次を持つ。

- definition ID、name、revision
- source sheet、header row、first/last data row
- 1件以上の`QuestionDefinition`
- rounding digits（既定1、小数0〜6）

実行開始時にdefinitionをcanonical JSONへserializeし、SHA-256 snapshotを作る。実行中の編集は現在runへ反映せず、次runで新snapshotを使う。

run、Prompt rendering、結果validation、formula生成は同じimmutable snapshotだけを参照する。実行中もUIのdraftは次run用として編集できるが、現在runのcollection、range、weight、Prompt、mappingは変化しない。full snapshotは`Quantification_Config`へ、hashは`Quantification_Run`へ保存する。

教育倫理warningは`QuantificationDefinition`へ保存せず、UIの固定説明resourceとして表示する。

### 6.2 QuestionDefinition

各質問は次を持つ。

- question ID、display name、question text
- primary answer column
- 0件以上のsupporting columns
- question weight（有限数、0より大きい）
- 1件以上の`EvaluatorDefinition`
- enabled flag

複数質問を同じ列へ設定できる。これは同じ回答を異なるPrompt・評価方法で数値化する用途に使う。Custom evaluatorでは、学生Prompt列を含む任意の選択列を主回答にできる。

### 6.3 EvaluatorDefinition

各評価方法は次を持つ。

- evaluator ID、display name
- type: `KNOWLEDGE_COVERAGE`または`CUSTOM_PROMPT`
- evaluator weight（有限数、0より大きい）
- score minimum / maximum（有限数、minimum < maximum）
- 1件以上の`CriterionDefinition`
- Knowledgeの場合はapp-owned built-in template version、Customの場合は利用者入力Prompt template
- enabled flag

1質問へ任意数の評価方法を追加、複製、並べ替え、無効化、削除できる。複数評価方法は、異なる尺度を同じ回答へ各1回適用する意味であり、同じPromptの反復実行回数ではない。

### 6.4 CriterionDefinition

各評価項目は次を持つ。

- criterion ID、display name、description
- criterion weight（有限数、0より大きい）
- score minimum / maximum。省略時は親evaluatorのrangeを使う
- enabled flag

重みは合計100へ手入力で揃える必要はない。アプリとExcel式は各階層で`個別weight / weight合計`へ正規化し、UIには実効percentageを表示する。

実行可能snapshotは、1件以上のenabled question、各enabled question内に1件以上のenabled evaluator、各enabled evaluator内に1件以上のenabled criterionを必須とする。disabled nodeとその子孫はAI呼出し、blank判定、formula、Results列から除外するが、監査用definition snapshotには保持する。

## 7. 評価方法

### 7.1 Knowledge coverage

`KNOWLEDGE_COVERAGE`では、利用者が入力した各知識ポイントを1つのcriterionとして扱う。AIは文字列の単純一致ではなく、回答がその知識を説明・適用している程度をcriterion range内で数値化する。

このsemantic coverage規則とstructured-output instructionはapp-owned instructionとし、利用者が削除またはkeyword件数評価へ置換できない。利用者が編集するのは知識ポイント、criterion description、range、weightである。別の分析方法が必要な場合は`CUSTOM_PROMPT`を使う。

初期Prompt template:

```text
次の設問への回答について、指定された各知識ポイントが回答内でどの程度説明されているかを評価してください。
単語が存在するだけで満点にせず、内容上の説明、関係、適用が確認できる程度を評価してください。
各評価項目について指定range内の数値、短い理由、回答内の根拠を返してください。

### 設問
{設問}

### 評価対象回答
{回答}

### 補助情報
{補助情報}

### 知識ポイントと評価項目
{評価項目}
```

### 7.2 Custom Prompt

`CUSTOM_PROMPT`では、利用者が分析用Promptと評価項目を自由入力する。Prompt能力、論理性、具体性、調査の深さ、文章品質など、知識含有以外の評価に使用できる。

Custom evaluatorは、レポート本文に限らず、学生が入力したPrompt列など任意の選択列を`{回答}`として評価できる。主回答列の種類をheader名や列位置で制限しない。

Prompt能力用の初期preset:

```text
次の設問に対して作成されたPromptを分析してください。
評価項目ごとに、そのPromptが必要な視点を引き出せる具体性、論理性、実行可能性を指定range内で数値化してください。
補助情報にPrompt作成時の工夫・観点・論点がある場合は、それを重要な評価情報として使い、実際のPromptへ反映されているかを評価してください。
各評価項目について短い理由と、評価対象回答または補助情報内の根拠を返してください。

### 設問
{設問}

### 評価対象Prompt
{回答}

### 補助情報
{補助情報}

### 評価項目
{評価項目}
```

### 7.3 Placeholder

許可するplaceholderは次だけとする。

- `{設問}`
- `{回答}`
- `{補助情報}`
- `{評価項目}`
- `{最小点}`
- `{最大点}`

Custom Prompt templateは空でなく、`{回答}`と`{評価項目}`をそれぞれ1回以上含むことを必須とする。`{最小点}`と`{最大点}`はcriterion固有rangeの有無にかかわらず、常に親evaluatorの既定rangeへ展開する。criterionごとのeffective rangeは`{評価項目}`へ含める。

literal braceは`{{`と`}}`でescapeする。未知placeholder、未閉鎖brace、不正なescapeを実行前に拒否する。placeholderへ挿入した回答、補助情報、評価項目の文字列は再走査せず、内部のbraceやplaceholder風文字列を展開しない。Knowledgeのsemantic instructionと全type共通のstructured-output instructionはappが利用者templateの外側へ付加し、利用者Promptから削除できない。

## 8. AI出力contract

AIは1 evaluatorについて次だけを返す。

- evaluator ID
- criterion results
  - criterion ID
  - raw score
  - short reason
  - evidence
  - evidence source: `PRIMARY_ANSWER` / `SUPPORTING_COLUMN` / `NONE`
  - evidence source column ID: appがPrompt内で割り当てたstable source ID。`NONE`では空

AIにevaluator総合点、question総合点、全体点、weight、合否を返させない。これらはExcel数式だけで計算する。

Validation:

- expected evaluator/criterion IDと完全一致
- raw scoreは有限数かつcriterion range内
- `PRIMARY_ANSWER`ではsource column IDが主回答IDと一致し、evidenceは同じ行の主回答に存在する連続substring
- `SUPPORTING_COLUMN`ではsource column IDが実際に送った補助列の1つと一致し、evidenceはその列の同じ行に存在する連続substring
- evidenceがない場合は空文字、`NONE`、空のsource column ID
- unknown field、missing criterion、duplicate criterion、NaN、Infinity、range外、複数提出を拒否
- criterionが1件でも欠落したpartial responseは全体を不正応答として拒否し、一部scoreだけを採用しない
- 不正応答を0点へ変換しない

## 9. 重み・Excel計算式

### 9.1 Effective raw score

各criterionについて、手動上書きが有効なら上書き値、そうでなければ妥当なAI raw scoreを使う。

$$
R_{effective}=\begin{cases}
R_{override} & \text{主回答が非空で、overrideが有効range内の数値}\\
R_{AI} & \text{主回答が非空、overrideが空で、AI rawが妥当}\\
\mathrm{blank} & \text{それ以外}
\end{cases}
$$

手動上書きは任意であり、未入力でも妥当なAI raw scoreを直接計算へ使用する。主回答が非空なら、有効なoverrideはAI失敗または空欄時にもscoreを補完できる。overrideは空欄またはcriterion effective range内の有限数だけを受け入れ、範囲外、NaN、Infinity、非数値はfield errorとして出力前に訂正またはclearを要求する。空の主回答にはoverrideを許可しない。これは人手確認gateではなく数値整合性validationである。

出力後にExcelでoverride cellが範囲外数値へ変更された場合も、formulaはclampやAI値へのfallbackをせずeffective rawを空欄にする。人手確認statusや承認checkboxを数式条件にしない。

### 9.2 Criterion normalization

$$
N_c=100\times\frac{R_{effective}-min_c}{max_c-min_c}
$$

Excel式はeffective raw選択とnormalizationを別cellに分ける。`ScorableCell`はappが主回答の非空／空から作るliteral `1`／`0`である。AI未実行、AI失敗、cancelによる空AI rawをExcelの算術0として扱わない。

```text
EffectiveRaw:
=IF(ScorableCell<>1,"",IF(OverrideCell="",IF(ISNUMBER(AiRawCell),IF(AiRawCell<MinCell,"",IF(AiRawCell>MaxCell,"",AiRawCell)),""),IF(ISNUMBER(OverrideCell),IF(OverrideCell<MinCell,"",IF(OverrideCell>MaxCell,"",OverrideCell)),"")))

Normalized:
=IF(ISNUMBER(EffectiveRawCell),IFERROR(ROUND((EffectiveRawCell-MinCell)/(MaxCell-MinCell)*100,RoundingDigitsCell),""),"")
```

### 9.3 Evaluator score

$$
E=\frac{\sum_c N_c w_c}{\sum_c w_c}
$$

1件でもenabled criterion scoreが空欄ならevaluator scoreを空欄にする。disabled criterionはscore参照、blank count、weight分母から除外する。不要なcriterionはweight 0にせずdefinitionから無効化または削除する。

### 9.4 Question score

$$
Q=\frac{\sum_e E_e w_e}{\sum_e w_e}
$$

1件でもenabled evaluator scoreが空欄ならquestion scoreを空欄にする。

### 9.5 Overall score

$$
O=\frac{\sum_q Q_q w_q}{\sum_q w_q}
$$

1件でもenabled question scoreが空欄ならoverall scoreを空欄にする。

### 9.6 Formula rules

- weight、minimum、maximum、rounding digitsは`Quantification_Config`のcellを参照する。
- 集計式の分子と分母はenabled childのscore cellとConfig上のraw weight cellを参照し、weightやrangeをformulaへliteral埋込みしない。
- scoreは0〜100へ正規化する。
- 各親式は算術前に`COUNT`で全enabled child scoreが数値であることを確認し、欠損時は空文字を返す。
- 結果は設定したrounding digitsで`ROUND`する。Excelと同じmidpoint away-from-zeroを使い、Core previewは`decimal`と`MidpointRounding.AwayFromZero`で最終丸め値を一致させる。
- normalized、evaluator、question、overallの各階層で丸め、親階層は既に丸められたchild score cellを入力にする。
- formulaは`IF`、`IFERROR`、`ISNUMBER`、`COUNT`、`SUM`、`SUMPRODUCT`、`ROUND`、比較、四則演算、cell/range参照だけを使う。
- 循環参照、外部参照、raw user formula、macroを生成しない。
- 生成前に各formulaの文字数、function引数数、参照、DAGを検証する。8,192文字以上になるformulaはwriteも切詰めもせず、該当question／evaluator／criterionのIDと表示名、fieldまたはformula階層、実測dimensionを示してdefinitionを拒否する。回答本文やPrompt本文はerrorへ含めない。
- app内previewとformula cellのcached valueは、独立した手計算oracleの最終丸め値と一致させる。
- formula cellへcached preview valueを書き、workbookをautomatic／full-calculation-on-loadに設定する。Excel、Office、LibreOffice、COM automationをapp実行またはrequired testの前提にしない。外部spreadsheetでの再計算smokeはoptionalとする。

## 10. 出力workbook

### 10.1 File

- 既定名: `{入力名}_quantified_{yyyyMMdd-HHmmss}.xlsx`
- 入力を別pathへbyte-copyし、元sheetを保持したcopyへ結果を追加する。
- 入力file自体へ書き込まない。
- target directory内の一意な一時fileへwrite、flush、close、reopen、validateした後だけ、同一volumeで完成名へrenameする。既存fileを黙って上書きしない。
- final commit開始前のcancelは一時fileをbest effortで削除し、完成名を作らない。rename critical section開始後はcancelを次の安全点まで遅延し、valid finalかfinalなしのどちらかにする。

### 10.2 App-owned sheets

| Sheet | 内容 |
|---|---|
| `Quantification_Config` | definition snapshot、質問、評価方法、評価項目、range、weight、rounding、source mapping |
| `Quantification_Results` | 行ごとのscorable flag、AI raw、override、effective raw／normalized formula、evaluator/question/overall formula、reason、evidence、source column ID、status |
| `Quantification_Run` | input hash、definition hash、app/SDK/CLI/model version、開始/終了、件数、error count |

同名sheetがある場合は既存sheetを変更せず、`Quantification_Config (2)`のように一意名を作る。

### 10.3 Formula ownership

- scorable flag、AI raw、override、reason、evidence、source、statusはliteral value／string cell。
- effective raw、normalized、evaluator、question、overall scoreはExcel formula cell。
- formulaは`Quantification_Config`のweight/range cellsを参照する。
- アプリでweightを変更して再出力するとConfig値と式参照先が新snapshotへ一致する。
- override cellにはeffective rangeのdata validationを設定する。ただし式自身のrange checkを最終防御とする。

## 11. UI

初版は4 stepで構成する。

1. **入力**: file、sheet、header/data rows、質問と主回答／補助列mapping。
2. **定量化設計**: 質問、Knowledgeの知識ポイント／built-in Prompt preview、Custom分析Prompt、評価項目、range、3階層weight。
3. **実行**: Copilot login状態、model、評価単位数、progress、cancel、technical error。
4. **結果・出力**: AI raw、任意override、Excel計算preview、別file出力、input不変確認。

### 11.1 教育倫理warning

次の趣旨を入力画面と結果画面のpersistent non-modal bannerとして常時表示する。

> AIによる定量値には誤りや偏りが含まれる可能性があります。利用目的に応じて結果を確認してください。

このwarningは表示専用とする。

- 設定値として保存しない。
- checkbox、同意、role、承認、期限を要求しない。
- 実行、AI呼出し、formula計算、出力をblockしない。
- scoreやweightを変更しない。
- warningを閉じる操作を必須にしない。
- warningへfocusまたは操作をしなくてもmapping、実行、cancel、override、出力を行える。

## 12. Copilot実行

- current stable GitHub Copilot SDK for .NETをexact versionで固定する。
- SDKの既定logged-in user credentialsを使う。
- 評価単位は1行×1質問×1evaluator。
- evaluatorごとに新しいephemeral sessionを使う。
- structured result toolを1件だけ公開し、shell、filesystem、Web、GitHub write、MCPを公開しない。
- 構造不正は新sessionで最大1回、transient network errorは最大2回再試行する。
- 各attemptはapp-ownedの有限timeoutを持ち、既定120秒とする。timeout後のretry上限到達時は`AI_TIMEOUT`とし、0点へ変換しない。
- cancel後に新規sessionを開始しない。
- sessionを明示削除する。
- 既定並列度1、最大3。
- 回答本文、Prompt、reason、evidence、tokenをlogへ記録しない。

## 13. Technical safety and failure behavior

### 13.1 維持する技術境界

- input immutability
- output atomicity
- selected primary/supporting columnsだけをAIへ送る
- 他行、file path、非選択列を送らない
- untrusted textをExcel string cellとして保存する
- closed structured output validation
- no-content/no-token logging
- session cleanup
- formula allowlistとDAG validation

これらは教育倫理gateではなく、file破損、情報漏えい、formula injection、計算不正を防ぐ技術要件である。

### 13.2 Failure

| Condition | Result |
|---|---|
| Empty primary answer | AI callなし、raw/formula score空欄、`EMPTY` |
| Invalid definition | run開始前に具体的field errorを表示 |
| Invalid AI response after retry | raw/formula score空欄、`AI_OUTPUT_INVALID` |
| Authentication unavailable | AI callなし、`AUTH_REQUIRED` |
| AI attempt timeout after retry | raw/formula score空欄、`AI_TIMEOUT` |
| Network failure after retry | raw/formula score空欄、`NETWORK_FAILED` |
| Cancel | 新規送信停止。完了済みraw値だけを保持し、部分結果の出力を許可 |
| Input changed during run | final renameを行わず、出力を完成扱いにせず`INPUT_CHANGED` |
| Output validation failure | 完成名を作らず`OUTPUT_INVALID` |

技術的失敗以外の教育倫理warningをerror codeやblock条件にしない。

## 14. Performance and limits

- 使用行上限: 20,000。超過選択は実行前に拒否し、黙って切り詰めない
- 質問: 1件以上。技術上限は出力列数とrequest budgetから実行前に計算する
- evaluator/criterion: 1件以上。Excel列上限を超えるdefinitionは実行前に拒否する
- 1 cell: 32,767 characters以下
- 1 formula: 8,192 characters未満。出力列数、function引数数、formula長から実行可能上限をpreflightで計算する
- concurrency: 1〜3
- 開発端末の合成531行workbookで、AI待機を除くread/write/validationの中央値30秒以下を目標とする
- 実測していない値を保証として表示しない

## 15. Scope

### 15.1 Initial release

- Windows 11 x64
- .NET 10 self-contained folder
- unsigned local ZIP + SHA-256
- standard `.xlsx`
- dynamic Knowledge/Custom evaluators
- GitHub Copilot CLI logged-in user
- separate output workbook with Excel formulas

### 15.2 Out of scope

- macOS、Linux、Windows Arm64 support claim
- production installer signing／notarization
- protected workbook decryption
- Forms／LMS API
- cloud database／server backend
- repeated stochastic evaluation／majority vote／median aggregation
- AI text detection or misconduct determination
- institutional policy／legal／education approval enforcement
- educational fairness threshold or mandatory human review gate

## 16. Acceptance criteria

| ID | 条件 |
|---|---|
| AC-001 | 標準 `.xlsx`を1file選び、inputを変更せず別 `.xlsx`を作る。 |
| AC-002 | 指定sampleをread-onlyで開き、`Original!A1:L531`とF/G/H/I/J/K/Lのmapping候補を表示できる。 |
| AC-003 | 質問数を動的に追加、複製、並べ替え、無効化、削除できる。 |
| AC-004 | 1質問へKnowledge/Custom evaluatorを任意数追加し、Knowledgeの知識ポイントとCustomの分析Prompt、および各criterion、range、weightを設定できる。 |
| AC-005 | Knowledge evaluatorはapp-owned semantic instructionにより、利用者入力の各知識ポイントについて単語有無ではなく説明・関係・適用の含有度をraw scoreとして返す。 |
| AC-006 | Custom evaluatorは学生Prompt列を含む任意の選択列を主回答にでき、利用者入力Promptでcriterion別raw scoreへ定量化する。 |
| AC-007 | AIはcriterion raw scoreだけを返し、evaluator/question/overall scoreを返さない。 |
| AC-008 | アプリでcriterion/evaluator/question weightを設定し、Configのrange/weight/rounding cellを参照するExcel式が各階層を0〜100の加重平均へ計算する。 |
| AC-009 | 主回答が非空なら、有効なoverrideはAI rawの有無にかかわらず優先し、overrideが空なら妥当なAI rawを確認待ちなく使い、どちらもなければscoreを空欄にする。 |
| AC-010 | Config、Results、Run sheetと式を持つ別fileをatomicに出力し、元sheetを保持する。 |
| AC-011 | 空回答はoverrideの有無にかかわらず、失敗・取消は有効なoverrideがない限り、scoreを0へ変換せず空欄とstatusを出力する。 |
| AC-012 | 教育倫理warningはpersistent non-modal banner表示だけで、操作を要求せず、設定、確認、承認、score、実行、出力へ影響しない。 |
| AC-013 | selected primary/supporting columns以外、他行、file pathをCopilotへ送らず、本文/tokenをlogへ残さない。 |
| AC-014 | 既存Copilot CLI loginで動作し、アプリ固有client ID、secret、org policyを要求しない。 |
| AC-015 | Windows 11 x64でbuild、unit/integration/E2E、self-contained publish、local ZIP生成が成功する。 |

## 17. Test requirements

1. sample identity、sheet、dimension、header-role suggestion test。
2. 1／2／10 questions、1／2／5 evaluators、1／4／20 criteria、各階層のenabled最小数のdynamic definition test。
3. Knowledge evaluatorの知識point、semantic coverage instruction、criterion schema test。
4. Custom evaluatorの任意主回答列、学生Prompt列、自由Prompt、brace escape、unknown placeholder、supporting columns test。
5. score range、weight正規化、override absent/present/AI失敗/range外、blank raw、midpoint roundingのhand-calculated test。
6. criterion→evaluator→question→overallのExcel formula、Config参照、cached preview、blank propagation golden test。
7. empty、invalid AI、auth、network、cancel、input changed、output invalidのsingle-cause test。
8. formula injection、external reference、cycle、formula length、cell length test。
9. input hash/size/time不変、copy preservation、atomic output fault test。
10. selected columns only、exact evidence source column、other-row isolation、no-content log、tool capability test。
11. ethics warning visible and nonblocking test。
12. existing login auth、finite timeout contract、Office非依存required test、optional synthetic live／external spreadsheet recalculation smoke test。
13. 4-step keyboard/focus/200% UI test。
14. sample-like fixed-seed 531-row synthetic E2E。
15. Windows x64 publish/package/documentation contract test。

## 18. Implementation start gate

Revised GATE-0 requires:

1. Requirement v3.0 and plan v4.0 committed.
2. Sample profile、dynamic evaluator model、weight/formula contract、warning-only semantics documented.
3. Current baseline、task map、traceability updated.
4. Independent review has zero unresolved blocker/high defects.
5. Production `src/` file count is 0 before gate evaluation.

No external policy、education approval、legal decision、signing identity、cross-platform runner is required for this gate。

## 19. Traceability summary

| Surface | Primary lane |
|---|---|
| Input/sample/mapping | App Excel adapter |
| Dynamic questions/evaluators | Core domain |
| Knowledge/Custom Prompt | Core prompting + Copilot adapter |
| Weights/formulas | Core scoring + App Excel writer |
| Separate output | App Excel writer |
| Nonblocking warning | App UI |
| Technical safety | Core validation + App adapters |
| Windows delivery | App publish/package tests |
