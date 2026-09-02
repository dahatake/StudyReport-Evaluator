# StudyReport Evaluator 要求定義書

| 項目 | 内容 |
|---|---|
| 文書版 | 4.1 |
| 基準日 | 2026-09-02 |
| 状態 | 正式公開scope同期済み baseline |
| 入力 | Microsoft Forms または Google Forms から export した標準 `.xlsx` 1ファイル |
| 出力 | 入力を変更せず作成する別の標準 `.xlsx` 1ファイル |
| 対応環境 | Windows 11 x64 |
| UI / Runtime | Avalonia / .NET 10 self-contained |
| AI | GitHub Copilot SDK for .NET。通常評価は利用者選択model、参照回答と類似度は `auto` |
| 旧版 | v4.0のplatform scopeを本版でsupersede。v3.0はv4.0により全面的にsupersede済み |

> 本版は、2026-09-01の要求所有者指示と、その後のdefault plan採用指示に加え、2026-09-02の正式公開README実装指示を反映する。platform scopeは[ADR-0013](../dev/docs/adr/0013-windows-only-public-release.md)に基づき、実packageと実行証跡があるWindows 11 x64へ限定する。
>
> 本書の「AI評価」は成績を確定する自動判定ではない。AIは定量化候補を作り、最終的な評点と利用判断の責任は利用者が負う。

## 1. 目的

本アプリケーションは、Formsから出力された学生レポート回答workbookを元本として読み込み、次を行うローカルデスクトップアプリケーションである。

1. 利用者が元本 `.xlsx` を容易に選択する。
2. 1行目または2行目にある質問文を候補として取得する。
3. 学生ごとの回答をGitHub Copilot SDKへPromptとして送り、通常設問、設問固有項目、LLM生成回答との類似度を定量化する。
4. AIは定量値、理由、根拠だけを返す。
5. ベース点、設問配点、固有設定配点、類似度減点はExcel数式で計算する。
6. 元本全体を保持した別workbookを作成し、元本は一切変更しない。
7. 長時間処理の進捗を表示し、プロセス終了後もcheckpointから再開できるようにする。
8. Windows 11 x64で、言語runtimeやSDKを別途導入せず利用できる配布物を提供する。

## 2. 対象利用者と基本原則

### 2.1 対象利用者

- 学生レポートを評価する教師、講師、採点担当者
- 評価Promptや採点項目を調整する教育担当者
- Custom Prompt、配布、検証を保守するソフトウェアエンジニア

### 2.2 基本原則

1. 入力workbookはread-onlyで扱い、成功、失敗、取消、再開のいずれでも変更しない。
2. 出力は必ず別fileとして作り、既存fileを黙って上書きしない。
3. AIが行うのは定量化だけであり、配点の加減算と最終評点はExcel数式が行う。
4. 設問数、通常評価方法数、通常評価項目数、固有評価項目数を業務上の固定値にしない。
5. 設定は実行開始時にimmutable snapshotへ固定し、実行中の編集を現在runへ混入させない。
6. 空回答と技術的AI失敗を区別する。空回答は0点相当、技術的失敗は空欄とする。
7. 回答本文、Prompt本文、AI理由、AI根拠、credentialをapplication logへ記録しない。
8. 教育上の警告は常時表示するが、checkbox、同意、承認、score gate、実行blockにしない。
9. 実測していないAI品質、対応platform、性能、署名状態を保証として表示しない。
10. 要求にない汎用plugin、cloud backend、database、policy engine、抽象layerは追加しない。

## 3. 利用前提

### 3.1 必須入力

- Microsoft FormsまたはGoogle Formsからexportされた、標準Office Open XML `.xlsx` 1ファイル
- 回答を含むworksheet
- 質問文を含む1行目または2行目
- 1行を1学生または1提出として扱える回答行

アプリはexport元サービスを推測して処理を分岐しない。入力形式の契約は標準 `.xlsx` とworksheet構造で定義する。

### 3.2 GitHub Copilot

AI処理にはGitHub Copilotを利用できるaccountと対話loginが必要である。

- 配布物は、固定したGitHub Copilot SDKと互換なCopilot CLI runtimeを同梱する。
- PATH上の任意CLIへ黙ってfallbackしない。
- loginは利用者本人がGitHubとの対話で行う。アプリはPAT、password、client secretを入力・保存しない。
- CLIまたはloginが利用できない場合、アプリ起動、Excel読込、mapping、設計編集、checkpoint確認は利用できるが、新しいAI処理は開始できない。
- 利用できない理由と、loginを再試行する操作をExecution画面へ表示する。

### 3.3 Spreadsheet runtime

Microsoft Excel、Office、LibreOffice、COM automationはrequired runtimeではない。formula対応spreadsheetで開く場合にExcel数式が再計算されるよう設定するが、アプリ内previewとrequired testはOfficeなしで成立させる。

## 4. 入力workbook契約

### 4.1 file選択

1. 起動直後の入力画面にnative open-file pickerを設ける。
2. pickerは単一の `.xlsx` だけを選択対象として提示する。
3. full pathの直接入力も維持し、Prompt起動時の事前入力と高度な利用を可能にする。
4. picker取消時は現在の入力状態を変更しない。
5. 選択後、fileをread-onlyで検査してから採用する。

### 4.2 対応形式

受け入れるのは標準 `.xlsx` だけとする。次は明示的に対象外とする。

- `.xls`、`.xlsb`、CSV、PDF
- `.xlsm`等のmacro-enabled形式
- password／rights-protected／暗号化file
- 破損ZIP、外部relationship等の安全境界に違反するpackage

### 4.3 worksheet・質問行・回答行

1. worksheetを利用者が選択できる。
2. 質問文行は1行目または2行目から選択でき、既定候補を構造から提示する。
3. 質問文行より後の行を回答開始行として選択する。
4. 回答終了行を選択できる。
5. 1回答行を1学生または1提出として扱う。
6. 各設問は、質問文、主回答列、0件以上の補助列を持つ。
7. mapping候補は自動提示するが、すべて画面で変更できる。
8. 同一質問内で主回答列と補助列を重複させない。別質問間で同じ列を使うことは許可する。

### 4.4 サンプルworkbook

repository内の現行サンプル正本は次とする。

`sample/SampleReport.xlsx`

2026-09-02に回答本文を出力せず、production readerで構造とheader由来mapping候補だけを再確認したprofileを使用する。

| 項目 | 実測値 |
|---|---|
| Bytes | 469,995 |
| SHA-256 | `F7C5364449B1026F2725828F47418B8E105D7E50CF4DF0B224FE4EAF134A2E3D` |
| `Sheet2` | `A1:J531` |
| 初期primary候補 | D、E、F、G、H |
| 学生Prompt primary候補 | E、H |
| supporting候補 | F、I |
| Hの初期supporting候補 | I |
| 初期対象外 | A〜C、J |

D〜Iの役割はheader semanticsから得た候補であり、列位置だけで固定しない。利用者は実際のheaderと授業設計を確認し、通常Questionまたは固有評価のprimary/supportingを画面で変更する。

### 4.5 元本不変

- 処理開始時にSHA-256、size、last-write timeを取得する。
- checkpoint更新、再開、final commit前に元本identityを再確認する。
- identityが一致しない場合は新規AI送信とfinal commitを停止し、`INPUT_CHANGED`を表示する。
- 出力は元本のbyte-copyから作り、元の全sheetとdataを保持する。

## 5. 評価定義

### 5.1 Root設定

1つの評価定義は次を持つ。

- definition ID、name、revision
- source sheet、question text row、first/last data row
- ベース点 `BasePoints`。既定60
- 固有設定配点 `SpecialPoints`。既定0
- 類似度減点係数 `SimilarityPenaltyWeight`。既定0.1
- 丸め桁数 `RoundingDigits`。既定1、範囲0〜6
- 1件以上の設問

数値制約は次とする。

$$
0\le BasePoints\le100
$$

$$
0\le SpecialPoints\le100
$$

$$
0\le SimilarityPenaltyWeight\le1
$$

### 5.2 通常設問

各設問は次を持つ。

- question ID、display name、question text
- primary answer column
- 0件以上のsupporting columns
- 設問配点 `Points`
- 1件以上の通常evaluator
- 0件以上の固有評価項目
- enabled flag

通常evaluatorは現行の次の2種類を維持する。

- `KNOWLEDGE_COVERAGE`: app-owned semantic instructionで知識の説明、関係、適用を評価
- `CUSTOM_PROMPT`: 利用者が入力したPromptで評価

各通常evaluatorは1件以上のcriterion、raw range、criterion内weightを持つ。AIのcriterion rawを0〜100へ正規化し、evaluatorおよび設問内の通常評価値をExcel数式で加重平均する。

### 5.3 設問固有評価項目

各設問は、Prompt能力等を評価する0件以上の固有評価項目を持てる。各項目は次を持つ。

- special item ID、display name
- 評価対象source column 1件
- 0件以上のsupporting columns
- Custom Prompt template
- enabled flag

固有評価のAI定量値は0〜1とする。初版では固有項目ごとのweightを設けず、同一設問内のenabled項目を等分平均する。これは要求されていない追加設定を増やさないための意図的な制約である。

主値が空の場合、その固有項目はAIへ送らず0とする。技術的AI失敗では空欄とする。

### 5.4 Prompt placeholder

通常Custom evaluatorと固有評価で許可するplaceholderは次の6件だけとする。

- `{設問}`
- `{回答}`
- `{補助情報}`
- `{評価項目}`
- `{最小点}`
- `{最大点}`

通常Custom evaluatorは`{回答}`と`{評価項目}`を必須とする。固有評価は単一の0〜1値を返すため、`{回答}`を必須とし、`{評価項目}`は任意とする。

literal braceは`{{`と`}}`でescapeする。未知placeholder、未閉鎖brace、不正escapeはAI送信前に拒否する。挿入値を再走査しない。

この6 placeholder制約は、利用者が編集できる通常Custom evaluatorと固有評価のtemplateにだけ適用する。Knowledgeはapp-owned template、参照回答と類似度はapp-owned Promptであり、利用者は編集できない。参照回答は固定template内の`{設問}`だけをappが置換し、類似度は検証済みの設問、学生回答、参照回答を固定sectionへ直接組み立てる。`{参照回答}`を第7の利用者placeholderとして公開せず、app-owned Promptを利用者template validatorへ通さない。

## 6. 配点

### 6.1 配点不変条件

有効な通常設問を$q=1..N$、各設問配点を$P_q$、ベース点を$B$、固有設定配点を$S$とする。

$$
B+S+\sum_{q=1}^{N}P_q=100
$$

- `Points`は0以上の有限数とする。
- 合計が100と一致しない定義ではrunを開始しない。
- 比較はdecimalのexact valueで行い、表示上の丸め値で判定しない。

### 6.2 初期配点

有効設問数が$N>0$のとき、初期設問配点は次とする。

$$
P_q=\frac{100-B-S}{N}
$$

例:

- ベース60、固有0、2問: 各20点
- ベース60、固有10、2問: 各15点

割り切れない場合は、丸め誤差を残さないよう最後の有効設問へ差分を割り当て、合計を正確に`100-B-S`へ一致させる。

### 6.3 手動配点と均等配分

- 利用者は各設問のPointsを個別に変更できる。
- 例: ベース60、固有0、設問1を30、設問2を10。
- base、special、設問の追加／削除／有効化変更時に、既存の手動Pointsを黙って変更しない。
- 初期mapping作成時と、利用者が明示的に「均等配分」を実行した場合だけ自動配分する。
- 合計不一致は明確な技術検証errorとして表示する。

## 7. AI処理

### 7.1 共通境界

- GitHub Copilot SDK for .NETの固定versionを使う。
- 通常評価のmodelは、SDKが実行時に列挙したmodelから利用者が選択する。
- 参照回答生成と類似度評価にはmodel ID `auto`を使う。
- `auto`が列挙されない場合はrunを開始せず、別modelへ黙ってfallbackしない。
- 1 attemptごとにrestricted sessionを使用し、app-owned structured result toolだけを公開する。
- shell、filesystem、Web、GitHub write、MCP、ambient memoryを公開しない。
- finite timeout、有限retry、cancel、session cleanupを必須とする。

### 7.2 参照回答生成

各有効設問について、質問文をそのまま`auto`へ入力し、LLM生成回答を1件作る。

- 生成回数は1設問につき1runで1回だけとする。
- 同じrunの全学生は同一の参照回答を使用する。
- 再開時はcheckpointに保存済みの参照回答を再利用し、再生成しない。
- 参照回答はfinal outputの`Quantification_References` sheetへ保存する。
- question ID、質問文、model ID、生成status、生成時刻を併記する。
- 生成失敗はblankと技術statusを保持し、その設問の類似度とFinalScoreをblankにする。

参照回答と類似度はAI生成品質に依存する。高い類似度は不正行為を証明せず、低い類似度は回答品質を保証しない。既定係数0.1は初期値であり、利用者は授業目的に応じて0を含む範囲で変更し、結果を自ら確認する。

### 7.3 通常回答評価

評価単位は1回答行 × 1通常設問 × 1enabled evaluatorとする。

AIはenabled criterionごとの次だけを返す。

- criterion ID
- raw score
- short reason
- evidence
- evidence source kind
- same-row stable source column ID

AIに配点、設問獲得点、固有設定獲得点、類似度減点、最終評点、合否を返させない。

主回答が空の場合はAIへ送らず、その設問の通常評価率と設問獲得点を0とする。技術的AI失敗では通常評価率と獲得点をblankにする。

### 7.4 固有評価

評価単位は1回答行 × 1設問 × 1enabled special itemとする。

AIは次だけを返す。

- special item ID
- score 0〜1
- short reason
- evidence
- evidence source kind
- same-row stable source column ID

主値が空の場合はAIへ送らず0とする。技術的失敗はblankとする。

### 7.5 類似度評価

各回答行の各有効設問について、学生回答と、その設問の参照回答の類似度を`auto`へPromptとして送り、0〜1で返す。

- 0は類似しない、1は同一または実質同一を表す。
- 文字列距離をアプリ側でAI値の代替として計算しない。
- 学生回答が空の場合はAIへ送らず0とする。
- 参照回答がblank、または類似度AIが技術的に失敗した場合はblankとする。
- 範囲外、NaN、Infinity、複数提出、unknown fieldを拒否する。

### 7.6 retryとfailure

- schema不正は新sessionで最大1回再試行する。
- transient network errorとtimeoutは新sessionで最大2回再試行する。
- attempt timeoutの既定は120秒とする。
- cleanup失敗後は追加retryを行わない。
- cancel後に新規sessionを開始しない。
- 技術的失敗を0へ変換しない。

## 8. Excel計算

### 8.1 通常設問獲得点

設問$q$の通常評価率を$R_q\in[0,1]$とする。既存criterion正規化値が0〜100である場合は100で除算する。

$$
QuestionEarned_q=P_qR_q
$$

空回答では$R_q=0$、技術的AI失敗では$R_q=\mathrm{blank}$とする。

### 8.2 固有設定獲得点

設問$q$にenabled special itemが$K_q>0$件ある場合、各scoreを$s_{q,k}\in[0,1]$として次を計算する。

$$
SpecialQuestion_q=\frac{\sum_{k=1}^{K_q}s_{q,k}}{K_q}
$$

固有項目を持つ有効設問数を$M$とする。

$$
SpecialEarned=
\begin{cases}
S\displaystyle\frac{\sum_{q=1}^{M}SpecialQuestion_q}{M} & S>0\\
0 & S=0
\end{cases}
$$

- `SpecialPoints > 0`の場合、1件以上のenabled special itemを必須とする。
- `SpecialPoints > 0`かつenabled special itemが0件の場合、Design検証とrun前preflightの両方で`SPECIAL_ITEMS_REQUIRED`として拒否する。
- `SpecialPoints = 0`の場合、固有評価AIを実行せず、`SpecialEarned=0`とする。
- 固有項目または対象設問の技術的失敗が1件でもあれば`SpecialEarned`はblankとする。

### 8.3 類似度減点

設問$q$の類似度を$L_q\in[0,1]$、減点係数を$W\in[0,1]$とする。

$$
SimilarityPenalty_q=P_qL_qW
$$

各設問の類似度と類似度減点を、通常設問評価とは別の列へ出力する。

例: $P_q=20$、$L_q=0.99$、$W=0.1$のとき、減点は1.98点。

### 8.4 FinalRawとFinalScore

$$
FinalRaw=B+\sum_q QuestionEarned_q+SpecialEarned-\sum_q SimilarityPenalty_q
$$

通常評価、必要な固有評価、参照回答、類似度のいずれかが技術的失敗でblankの場合、`FinalRaw`と`FinalScore`もblankとする。

監査用に`FinalRaw`を保持し、表示用評点は0〜100へclampする。

$$
FinalScore=
\begin{cases}
0 & FinalRaw<0\\
100 & FinalRaw>100\\
FinalRaw & \text{otherwise}
\end{cases}
$$

### 8.5 formula ownership

- AI raw、reason、evidence、source、statusはliteral cellとする。
- effective raw、normalization、平均、QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScoreはExcel formula cellとする。
- base、special、question points、similarity weight、range、rounding digitsはConfig cellを参照する。
- 配点や係数をformula textへliteral埋込みしない。
- raw user formulaを生成しない。
- formula cellへapp previewのcached valueを保存する。
- formula allowlist、参照、length、function arguments、DAGをAI送信前とfinal write時に検証する。

### 8.6 手動変更

出力後、利用者がConfigのbase、special、question points、similarity weightを変更するとformulaが再計算される構造とする。合計100制約を破った場合はConfig上の検証列で明示し、`FinalScore`をblankにする。

既存の通常criterion overrideは維持する。固有評価と類似度は初版UIでoverride欄を追加せず、必要な変更は出力workbookのliteral値またはformulaを利用者責任で編集する。

## 9. 出力workbook

### 9.1 pathと命名

入力fileのdirectoryを基準に`result` subdirectoryを初期出力先とする。利用者は開始前に別の既存または作成可能directoryへ変更できる。

既定完成名:

`result/eval-{yyyyMMdd-HHmm}.xlsx`

同一分に衝突した場合は次の未使用名を使う。

- `eval-{yyyyMMdd-HHmm}-02.xlsx`
- `eval-{yyyyMMdd-HHmm}-03.xlsx`
- 以下同様

既存fileを上書きしない。完成pathはrun開始時に予約し、画面とcheckpointへ保存する。

### 9.2 app-owned sheets

| Sheet | 内容 |
|---|---|
| `Quantification_Config` | immutable definition snapshot、base、special、question points、similarity weight、Prompt、mapping、range、rounding |
| `Quantification_References` | 設問ごとの質問文、`auto`生成回答、model、status、生成時刻 |
| `Quantification_Results` | 行ごとの通常評価、固有評価、類似度、減点、FinalRaw、FinalScore、理由、根拠、status、Excel formula |
| `Quantification_Run` | input/definition/checkpoint identity、app/SDK/CLI/model、開始/終了、件数、error、token usage、実sheet名 |

同名sheetが入力に存在する場合は既存sheetを変更せず、` (2)`、` (3)`の最小suffixを付ける。

### 9.3 atomic finalization

- target directory内の一意tempへ元本をbyte-copyする。
- app-owned sheetsとformulaを書き、flush、close、read-only reopen、validateする。
- 元本identityを再確認する。
- 同一volumeのno-overwrite renameだけで完成名を作る。
- valid finalまたはfinalなしのどちらかにする。

## 10. checkpointと再開

### 10.1 checkpoint file

run開始時、完成名に対応する次のfileを作る。

`eval-{yyyyMMdd-HHmm}.partial.xlsx`

衝突suffixは完成名と同じ番号を使う。checkpointは元本全体のbyte-copyに`Quantification_Checkpoint` sheetを追加した標準 `.xlsx` とする。

### 10.2 保存単位

- 参照回答を生成するたびに保存する。
- 学生1行に必要な通常評価、固有評価、類似度がすべて終わるたびに保存する。
- 保存はtarget-local tempへのwrite、reopen validation、atomic replaceで行う。
- process強制終了時も、最後に成功したatomic checkpointまで復旧できる。

### 10.3 保存内容

- schema version
- input identity
- definition canonical snapshotとSHA-256
- final/partial path
- 通常評価model ID、参照／類似度model ID `auto`
- app、SDK、CLI runtime identity
- 参照回答とstatus
- 完了済みunitの定量値、reason、evidence、status、token usage
- 完了済み学生行番号
- 開始時刻、最終checkpoint時刻

### 10.4 再開条件

再開時は次を完全一致で検証する。

- checkpoint schema version
- input SHA-256、size、last-write time
- definition SHA-256
- 通常評価model ID
- reference/similarity model ID `auto`
- app major schema compatibility
- Copilot CLI runtime identity

不一致時は当該checkpointを変更せず、具体的なsafe errorを表示して再開しない。回答、Prompt、reason、evidence本文をerrorへ含めない。

### 10.5 再開動作

- 保存済み参照回答は再利用する。
- 完了済み学生行はskipする。
- 最初の未完了行以降だけを処理する。
- 同じ学生行の途中状態は完了扱いにせず、その行を再実行する。
- 完成成功後にpartial fileを削除する。削除失敗は完成fileを無効にせず、明確なcleanup warningを表示する。
- cleanup warningは結果画面へnon-modalに表示し、完成file pathと残存partial pathを示す。確認操作をworkflow条件にしない。
- 新規runの候補pathに既存partial fileがある場合は上書きせず、再開可能性を検査するか、次の未使用suffixを割り当てる。

## 11. UI workflow

アプリは4 stepを維持する。

1. **入力**
   - native pickerまたはpath入力
   - sheet、質問文行1/2、回答行
   - 設問、主回答、補助列、固有項目列のmapping
2. **採点設計**
   - base、special、question points、similarity weight
   - 均等配分
   - Knowledge / Custom evaluatorとcriteria
   - 固有項目とPrompt
   - formula／capacity preflight
3. **実行**
   - Copilot login、通常model、`auto` availability
   - output／partial path
   - 参照生成、通常評価、固有評価、類似度、finalizationの段階表示
   - completed rows / total rows、in-flight、error、cancel
   - 利用可能checkpointの再開
4. **結果**
   - 完了／一部失敗／取消を明示
   - finalまたはpartial path
   - 行ごとのQuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScore
   - output folderを開く操作

### 11.1 完了表示

全処理とfinal validationが成功した場合、次を表示する。

- 「処理が終了しました」
- 完成file path
- 対象学生行数、成功、空回答、技術失敗件数
- total similarity penaltyとFinalScore preview
- 元本が不変であること

技術失敗を含む場合は成功と誤表示せず、「処理は完了しましたが、空欄の評価があります」と表示する。

### 11.2 教育倫理warning

次の文面をshell rootのpersistent non-modal bannerとして全stepで常時表示する。

> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません

- 文言を省略、要約、言い換えない。
- warningは設定値、snapshot、score、statusへ保存しない。
- checkbox、同意、dismiss、role、期限を要求しない。
- warning操作をrun、cancel、resume、formula、finalizationの条件にしない。

## 12. Promptファイルからの起動

### 12.1 command line

次をサポートする。

`StudyReportEvaluator.App --input <xlsx-path> --prompt <txt-path> [--prompt <txt-path> ...]`

- `--input`は任意。指定時は入力pathを事前入力し、安全なread-only読込を開始する。
- `--prompt`は複数指定可能。
- Prompt fileはUTF-8 plain text `.txt`とする。
- 1fileは32,767文字以下とする。
- unknown option、重複`--input`、missing value、非txt Promptを明確な起動errorとする。

### 12.2 GUIへの反映

- 読み込んだPromptはbasenameと本文をDesign画面のimported Prompt一覧へ表示する。
- Prompt一覧はcommand lineへ指定された順序を維持し、同じPromptを複数の対象へ再利用できる。
- 利用者が一覧からPromptを選び、通常Custom evaluatorまたは固有項目を選んだうえで「Promptを適用」buttonを明示実行する。
- filename規則による設問への暗黙割当を行わない。
- Prompt適用はtemplateのcopyだけを行い、step遷移やAI処理を開始しない。Execution画面の「実行」buttonを利用者が押すまでAI送信を0件とする。
- warningと設計検証を表示し、利用者が実行buttonを押した場合だけrunを開始する。

独自VS Code拡張、custom URI scheme、background daemonは初版へ追加しない。GitHub CopilotのPromptから実行fileと引数を指定する運用例を文書化する。

## 13. 配布とinstall

### 13.1 共通

- end-user packageは.NET 10 self-containedとし、利用端末への.NET Runtime／SDK導入を不要にする。
- GitHub Copilot SDKと互換なCLI runtimeをpackageへ同梱する。
- source build用SDKを一般利用者端末へ導入しない。
- GitHub loginは利用者本人の対話が必要であり、scriptがcredentialを収集しない。

### 13.2 Windows

- Windows 11 x64 packageを作る。
- .NET 10 self-containedのunsigned ZIPとSHA-256 sidecarを作る。
- ZIPを展開したdirectoryから起動し、administrator権限、.NET Runtime、.NET SDKを要求しない。
- publish/package scriptを実行する場合はPowerShell 7以上だけを使用し、Windows PowerShell 5.1へfallbackしない。
- installer、Start Menu shortcut、code signing済みpackageが存在するとは表示しない。

### 13.3 非対応platformと将来変更

- macOS、Linux、Windows Arm64は初版正式公開の対応対象外とし、package、起動、署名、notarizationを提供済みと表示しない。
- 将来対応する場合は要求を改版し、対象OS/architectureごとのself-contained package、bundled CLI、実runner launch、署名・notarization等の必要証跡を別途定義する。
- WindowsのbuildまたはAvalonia/.NETの一般的なcross-platform対応を、他platformでの本製品動作証跡として代用しない。

## 14. privacy・security・安全境界

- AIへ送るworkbook由来の値は、現在行の選択済みprimary/supporting/special sourceだけとする。
- question text、Prompt、criterion metadata、参照回答、closed schema metadataは必要範囲で送る。
- 他行、非選択列、workbook pathを送らない。
- reference answerはその質問の全学生比較へ使用するため、類似度Promptに含める。
- untrusted textはExcel string cellとして保存し、formulaとして実行しない。
- outputとpartial workbookは元本全体、Prompt、AI結果を保持するため、元本と同等以上に機密として扱う。
- app-owned cloud backend、database、telemetry本文送信を追加しない。
- checkpointは暗号化containerではない。保存先のaccess controlは利用者のOS権限に従う。

## 15. performance・capacity

- 回答行上限は20,000とする。
- concurrencyは既定1、最大3とする。
- Excel row、column、cell、formula、function argument上限をwrite前に検査する。
- Promptとschemaのapp-owned request上限を測定し、model contextの安全marginをAI送信前に検査する。
- retry込みattempt上限を実行前に検査し、黙って切り詰めない。
- checkpoint保存時間をprogressへ含める。
- AI待機を除く531行workbookのread/write/final validation性能は、対応OSごとに実測値と環境を記録する。未実測platformの数値を保証しない。

## 16. failure behavior

| Condition | Result |
|---|---|
| 空の通常回答 | AI callなし、通常評価率0、QuestionEarned 0、similarity 0 |
| 空の固有項目 | AI callなし、当該special score 0 |
| invalid definition / 配点不一致 | AI call前に具体的field error |
| `auto` unavailable | AI call前に停止。別modelへfallbackしない |
| reference generation failure | reference / similarity / FinalRaw / FinalScore blank、技術status |
| invalid AI response after retry | 対象値blank、FinalRaw / FinalScore blank、`AI_OUTPUT_INVALID` |
| authentication unavailable | 新規AI callなし、`AUTH_REQUIRED` |
| timeout after retry | 対象値blank、`AI_TIMEOUT` |
| network failure after retry | 対象値blank、`NETWORK_FAILED` |
| cancel | 新規送信停止。最後の完了行checkpointを保持 |
| process終了 | 最後にatomic保存したcheckpointから再開可能 |
| input changed | checkpoint更新とfinal renameを停止、`INPUT_CHANGED` |
| checkpoint mismatch | checkpointを変更せず再開拒否 |
| output validation failure | 完成名を作らずpartialを保持、`OUTPUT_INVALID` |
| partial cleanup failure after success | finalは有効、cleanup warningを表示 |

## 17. Scope

### 17.1 v4.1 required scope

- Windows 11 x64
- .NET 10 self-contained app
- bundled compatible Copilot CLI runtime
- standard `.xlsx` from Microsoft Forms or Google Forms
- native input picker
- base / question / special point allocation
- reference answer generation and per-question similarity penalty
- Excel-owned formulas
- per-student-row `.partial.xlsx` checkpoint and process-restart resume
- GUI prefill from input/Prompt text command-line options
- unsigned Windows ZIPとSHA-256 sidecar
- teacher, operator, engineer documentation and actual synthetic screenshots

### 17.2 out of scope

- macOS、Linux、Windows Arm64 support claim
- `.xls`、CSV、PDF、macro、encrypted workbook
- Microsoft Forms API、Google Forms API、LMS API
- cloud database、server backend、multi-user service
- custom VS Code extension、custom URI protocol、background daemon
- repeated stochastic voting／majority／median
- plagiarism or misconduct determination
- AI text detector claim
- institutional policy、legal、fairness approval enforcement
- automated credential collection
- automatic AI run from command-line arguments
- installer、code signing、notarization

## 18. Acceptance criteria

| ID | 条件 |
|---|---|
| AC-001 | native pickerまたはpathから標準 `.xlsx`を選び、元本を変更せず別 `.xlsx`を作る。 |
| AC-002 | question text rowを1または2から選び、sheet、回答行、質問／通常回答／固有項目列を変更可能な候補として表示する。 |
| AC-003 | 指定sampleでF/Iを通常回答、G/J/KをPrompt関連の固有項目候補として提示し、元本identityを維持する。 |
| AC-004 | base既定60、special既定0、similarity weight既定0.1を表示・変更できる。 |
| AC-005 | 設問Pointsの初期値が`(100-base-special)/有効設問数`となり、明示的な均等配分以外で手動値を変更しない。 |
| AC-006 | `base + special + Σ question points = 100`をrun前とformulaで検証する。 |
| AC-007 | 通常Knowledge/Custom評価を動的に構成し、AIはcriterion rawだけを返す。 |
| AC-008 | 各設問へ0件以上の固有評価項目を設定し、同一設問内と対象設問間を等分平均してSpecialEarnedを計算する。 |
| AC-009 | `auto`で各設問1件の参照回答を生成し、同一runの全学生で共有してReferences sheetへ保存する。 |
| AC-010 | 各学生回答と参照回答の類似度を0〜1で定量化し、`Points × Similarity × Weight`を設問別に出力する。 |
| AC-011 | QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、0〜100 clamp済みFinalScoreをConfig参照Excel数式で計算する。 |
| AC-012 | 空入力はAIを呼ばず0、技術的AI失敗はblankとし、FinalScoreへblankを伝播する。 |
| AC-013 | `result/eval-yyyyMMdd-HHmm[-NN].xlsx`をno-overwrite atomic commitし、元全sheetと4 app-owned sheetsを保持する。 |
| AC-014 | run開始時に`.partial.xlsx`を作り、参照生成および各学生行完了後にatomic checkpointする。 |
| AC-015 | input、definition、model、runtime identity一致時だけ再開し、参照回答と完了行を再実行しない。 |
| AC-016 | 進捗、処理段階、完了／一部失敗、final／partial pathと件数を画面表示する。 |
| AC-017 | 指定警告文を全stepで常時nonblocking表示する。 |
| AC-018 | `--input`と複数`--prompt`でGUIを事前入力し、利用者操作なしにAI実行しない。 |
| AC-019 | selected same-row dataだけをAIへ送り、本文／Prompt／reason／evidence／credentialをlogへ残さない。 |
| AC-020 | Windows 11 x64 self-contained unsigned ZIPがcleanな展開先で起動し、SHA-256 sidecarとbundled CLI identityを検証できる。 |
| AC-021 | macOS、Linux、Windows Arm64、installer、code signing、notarizationを初版対応として表示せず、packageにも存在すると主張しない。 |
| AC-022 | README、教師tutorial、Prompt例、install、privacy、troubleshooting、開発設計、実画面screenshotsを現行UIと同期する。 |

## 19. Test requirements

1. Microsoft Forms型、Google Forms型、question row 1/2の匿名化synthetic workbook test。
2. sample identity、sheet、dimension、F〜K role suggestion、input不変test。
3. native picker cancel/select、path direct input、unsupported format test。
4. base/special/question pointsの初期配分、最後の設問への端数、手動値保持、均等配分test。
5. 配点合計、range、similarity weight、enabled special minimumのvalidation test。
6. Knowledge/Custom evaluatorとspecial Prompt placeholder／closed schema test。
7. reference answer exactly once/question/run、`auto`必須、failure、resume reuse test。
8. normal/special/similarityの0/1境界、range外、empty-zero、failure-blank test。
9. QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、clampの独立手計算oracle。
10. Config参照formula、blank伝播、cached preview、formula allowlist/ref/DAG/length test。
11. References/Results/Run/Config sheetと元sheet保持、name collision test。
12. result path分単位命名、`-02` suffix、directory作成、no-overwrite、atomic fault test。
13. partial作成、reference checkpoint、学生行checkpoint、atomic replace、強制終了相当fault test。
14. checkpoint schema/input/definition/model/runtime mismatchと完了行skip test。
15. auth、timeout、network、schema、cleanup、cancel、no-send-after-cancel test。
16. selected-column／same-row isolation、literal string、no-content log test。
17. 4-step UI、warning exact text/nonblock、progress、resume、completion、keyboard、200% scale test。
18. CLI option parser、UTF-8 Prompt file、複数Prompt、明示適用、no-auto-run test。
19. Windows x64 publish/package layout、bundled CLI、clean launch test。
20. unsigned ZIP、SHA-256 sidecar、safe layout、再現可能な再package test。
21. README、利用者文書、package契約がmacOS、Linux、Windows Arm64、installer、code signing、notarizationを対応済みと主張しないtest。
22. documentation link、screenshot provenance、unsupported claim、Prompt examples contract test。
23. fixed-seed 531-row synthetic end-to-end、resume end-to-end、input hash不変test。
24. optional authenticated synthetic Copilot smokeとexternal spreadsheet recalculation smoke。required deterministic testの代替にしない。

## 20. 外部仕様出典

本要求の実装時は、固定するversionの一次資料を再確認する。

- GitHub Copilot SDK: [Getting started](https://github.com/github/copilot-sdk/blob/main/docs/getting-started.md)
- GitHub Copilot SDK: [Bundled CLI](https://github.com/github/copilot-sdk/blob/main/docs/setup/bundled-cli.md)
- GitHub Copilot SDK: [Session persistence](https://github.com/github/copilot-sdk/blob/main/docs/features/session-persistence.md)
- Avalonia: [File dialogs](https://github.com/AvaloniaUI/avalonia-docs/blob/main/docs/services/file-dialogs.md)
- Microsoft: [.NET application publishing overview](https://learn.microsoft.com/dotnet/core/deploying/)
- Microsoft: [Working with formulas](https://learn.microsoft.com/office/open-xml/spreadsheet/working-with-formulas)
- Microsoft: [Excel specifications and limits](https://support.microsoft.com/office/excel-specifications-and-limits-1672b34d-7043-467e-8e27-269d656771c3)

## 21. Traceability summary

| Surface | Primary owner |
|---|---|
| Input/sample/mapping/picker | App workbook adapter + UI |
| Base/question/special allocation | Core domain/scoring + Design UI |
| Normal/special Prompt | Core prompting + Copilot adapter |
| Reference answer/similarity | Copilot adapter + workflow |
| Excel formulas/sheets | Core formula + App workbook writer |
| Checkpoint/resume | App workflow + workbook adapter |
| Prompt-file launch | App composition + Design UI |
| Warning/progress/results | App UI |
| Windows delivery | scripts + packaging tests |
| User/developer documentation | docs + dev/docs + screenshot tests |

## 22. Approval record

| 項目 | 内容 |
|---|---|
| Approver | 本repositoryの要求所有者 |
| Initial requirement source | 2026-09-01の本セッションで提示されたアプリケーション要件 |
| Default-decision approval source | 同日の後続指示「不明点はデフォルトのプランを採用してください。全てのタスクを実行してください」 |
| Release-scope update source | 2026-09-02の正式公開README実装指示とADR-0013 |
| Approved scope | 本書§1〜§21。base／special／similarity、checkpoint再開、Prompt起動、Windows 11 x64配布を含む |
| Meaning | repository要求baselineの承認記録。組織の法務・教育・security承認または電子署名を意味しない |
