# StudyReport Evaluator 要求定義書

| 項目 | 内容 |
|---|---|
| 文書版 | 4.6 |
| 基準日 | 2026-09-07 |
| 状態 | 要求承認済み。UI・設定保存差分と記録済み文書contractはVERIFIED_SCOPED。T01〜T38はREVIEWED、T39は追加native FAIL・本人確認等の外部前提によりBLOCKED。製品0.8.5は未公開候補、F01はREVIEWED。F02最終再検証は本同期時点では親担当で未完了、以後は実行記録の最新F02欄を参照。CH-01〜06はNOT_RUN_EXTERNAL_PREREQUISITE、公開gateは未達 |
| 入力 | Microsoft Forms または Google Forms から export した標準 `.xlsx` 1ファイル |
| 出力 | 入力を変更せず作成する別の標準 `.xlsx` 1ファイル |
| 対応環境 | Windows 11 x64。macOS、Linux、Windows Arm64は現版の正式公開対象外 |
| UI / Runtime | Avalonia `12.1.1` / .NET `10.0.11` self-contained。build SDK `10.0.400`を固定 |
| AI | GitHub Copilot SDK for .NET `1.0.11` / bundled CLI `1.0.79`を固定。通常評価は利用者選択model、参照回答と類似度は `auto` |
| 旧版 | v4.5の業務・入力・採点・数式・checkpoint・privacy・delivery境界をcarry forwardし、承認されたUI簡素化・設定保存・復元の差分だけを追加。v3.0はv4.0により全面的にsupersede済み |

> 本版は、要求所有者が2026-09-07に[UI・設定保存プランv2](../work/20260907-ui-settings-redesign-plan-v2.md)のD01〜D20デフォルト・全実装を明示承認した差分を反映する。元プランの承認待ち表記は履歴であり、後続指示を優先する。最小実装契約と測定範囲は[UI layout contract](../dev/docs/ui-layout-contract.md)へ分離する。D17の当初上書き「全タスク完了後だけUnreleased追記、続いて製品PATCH `0.8.4` → `0.8.5`」と「T01では製品版・CHANGELOGを変更しない」は履歴として保持する。さらに後続の利用者不在時の自律続行指示により、T39をBLOCKEDのままF01／F02を進める。F01はREVIEWED、親担当による製品PATCHは反映済みだが、G4・全タスク完了・公開PASSを意味しない。以後のF02最終再検証は[実行記録](../work/20260907-ui-settings-execution-record.md)の最新欄を正本とする。
>
> v4.5で継承したdelivery契約は、2026-09-06に承認された[1操作起動プラン](../work/20260906-0617-one-action-startup-plan.md)、[実行上書き](../work/20260906-one-action-startup-execution.md)、[ADR-0016](../dev/docs/adr/0016-windows-one-action-startup.md)に基づくWindows 11 x64 self-contained unsigned単一EXEの主配布追加である。[ADR-0013](../dev/docs/adr/0013-windows-only-public-release.md)・[ADR-0015](../dev/docs/adr/0015-windows-macos-installer-delivery.md)のZIPとSHA-256 sidecarは代替経路として維持する。既存のnon-public development MSIX sourceと機構回帰は保持するだけで拡張せず、一般利用者へ配布しない。macOS source/static contractと公開対象外の境界は変更しない。
>
> [S01/G1](../dev/docs/preflight/windows-singlefile-feasibility.md)は固定version・開発hostでの.NET標準App限定single-file全内容展開の方式適合だけを確認した。clean-host、MOTW／Windows保護、本人loginは`NOT_RUN`であり、本書は新EXEの実装完了・公開済み・OS-only受入完了を示さない。
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
8. Windows 11 x64で、取得済みのself-contained単一EXEを開く1操作から入力画面へ到達する配布経路とSHA-256 sidecarを提供し、ZIPとsidecarも代替として残す。追加runtime導入・手動展開を主導線に要求せず、OS保護・本人認証は§13の別条件として明示する。

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

AI処理にはGitHub Copilotを利用できるaccount、本人の対話login、必要なnetwork接続と組織policy上の利用許可が必要である。これらはGUI起動の前提とは分離する。

- 配布物は、固定したGitHub Copilot SDKと互換なCopilot CLI runtimeを同梱する。
- PATH上の任意CLIへ黙ってfallbackしない。
- loginは利用者本人がCLI／ブラウザーを通じてGitHubとの対話で行う。アプリはPAT、password、client secret、token、device codeを入力・収集・解析・保存しない。
- CLIまたはloginが利用できない場合、アプリ起動、Excel読込、mapping、設計編集、checkpoint確認は利用できるが、新しいAI処理は開始できない。
- 利用できない理由、明示的な「GitHubにログイン」開始・取消、既存の「Copilot 状態を確認」による再確認をExecution画面へ表示する。D-06は採用済みとし、動作とprocess所有範囲は§11.3に従う。

### 3.3 Spreadsheet runtime

Microsoft Excel、Office、LibreOffice、COM automationはrequired runtimeではない。formula対応spreadsheetで開く場合にExcel数式が再計算されるよう設定するが、アプリ内previewとrequired testはOfficeなしで成立させる。

### 3.4 GUI起動とAI利用の分離

- 取得済みの単一EXEだけで、Windows 11 x64の標準userがofflineでGUI、Excel読込、mapping、採点設計を利用できることを要求する。管理者昇格、setup script、terminalへのcommand入力、手動展開を要求しない。
- GUI起動に.NET Runtime／SDK、PowerShell、Node.js／npm、Git、GitHub CLI（`gh`）、別Copilot CLI、Office、IDEの導入を要求しない。sidecar、repository、隣接DLL／manifest、既存のCLI cacheや認証情報も起動の前提にしない。
- 起動時にloginやAI評価を自動開始しない。CLIのStart/Ping、認証状態確認、本人login、実AI評価は別々に検証し、GUI表示やCLI helpの成功をAI-readyへ読み替えない。
- OS保護による警告・拒否と、network／account／認証の必要性は追加runtime不要の契約とは別であり、すべての端末での無警告・無条件起動を保証しない。

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
9. 利用者が主回答列を選択した場合、選択中worksheetの質問文行と当該列が交差する1セルの値を、同じ設問の質問文へ即時反映する。回答行や列全体の値は連結しない。
10. 主回答列を選び直すたびに質問文を新しい交差セルの値で置き換える。反映後も利用者は質問文を手動変更できるが、その後に主回答列を再選択した場合は再び交差セルの値を反映する。
11. 交差セルが空または存在しない場合は質問文を空として扱い、列名や代替文を生成しない。質問文の必須検証を表示し、利用者の手入力で解消できるようにする。
12. 質問文行を変更してmetadataを再読込する前は、以前の質問文行の値を反映しない。現在の質問文を保持し、質問文行とmetadataの不一致を解消する再読込を要求する。

### 4.4 サンプルworkbook

repository内の現行サンプル正本は次とする。

`sample/SampleReport.xlsx`

2026-09-03に回答本文を出力せず、production readerで構造とheader由来mapping候補だけを再確認したprofileを使用する。このexact pathだけをrepository sample契約として使い、同directoryの他fileを列挙、fallback、代用しない。local deterministic technical E2Eも同じfileを使う。

| 項目 | 実測値 |
|---|---|
| Bytes | 470,806 |
| SHA-256 | `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA` |
| package entries / relationships | 11 / 8 |
| worksheet / dimension | 1件 / `A1:L531` |
| worksheet name SHA-256 | `88D759EA02CEF4B82885C6C620473162757C75522805707C20E2BE76A40A2825` |
| 初期primary候補 | F、G、H、I、J |
| 学生Prompt primary候補 | G、J |
| supporting候補 | H、K |
| Jの初期supporting候補 | K |
| 初期target / 対象外 | F〜K / A〜E、L |

F〜Kの役割はheader semanticsから得た候補であり、列位置だけで固定しない。利用者は実際のheaderと授業設計を確認し、通常Questionまたは固有評価のprimary/supportingを画面で変更する。

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

出力先は明示指定（output override）と実効pathを分離する。今回の明示編集、保存済みの明示指定、未指定（`null`）の順に決定し、明示指定はabsolute pathとして再起動・入力Excel変更後も保持する。空欄への明示編集は`null`への変更として扱い、過去の保存済み指定へ戻さない。

明示指定が`null`の場合だけ、入力fileのdirectoryを基準に`result` subdirectoryを都度算出する。自動算出したresult pathを明示指定として保存しない。入力未選択なら「入力後に決定」と表示する。利用者は開始前に別の既存または作成可能directoryへ変更でき、次回の実効出力先を主画面で確認できる。

指定先が利用不可でも別pathへfallbackせず、修正または既存の作成可能先検証を促す。設定復元だけではdirectoryを作成しない。既存checkpointの予約済みfinal／partial pathと再開条件は変更しない。

明示指定がない場合の既定完成名（指定時も同じbasename規則）:

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
   - 対象設問、有効化、主回答列、設問text、必須エラー。補助列・候補詳細等は同じ対象の設定へ移動
2. **採点設計**
   - base、special、question points、similarity weight
   - 均等配分
   - Knowledge / Custom evaluatorとcriteria、固有項目の有効設定概要と詳細設定への入口
   - 読込Promptの件数と設定への入口
   - formula／capacity preflight
3. **実行**
   - Copilot loginの明示開始・取消・既存buttonでの状態再確認、通常modelの実効選択、`auto` availability
   - 実効output／partial pathと設定変更への入口
   - 参照生成、通常評価、固有評価、類似度、finalizationの段階表示
   - completed rows / total rows、in-flight、error、cancel
   - 利用可能checkpointの再開
4. **結果**
   - 完了／一部失敗／取消を明示
   - finalまたはpartial path
   - 行ごとのQuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScore
   - criterion overrideの確認と、override反映版を既存fileへ上書きせず任意の別workbookとして出力する操作

### 11.1 完了表示

全処理とfinal validationが成功した場合、次を表示する。

- 「処理が終了しました」
- 完成file path
- 対象学生行数、成功、空回答、技術失敗件数
- total similarity penaltyとFinalScore preview
- 元本が不変であること

技術失敗を含む場合は成功と誤表示せず、「処理は完了しましたが、空欄の評価があります」と表示する。

### 11.2 教育倫理warning

次の文面をshell rootのpersistent non-modal bannerとして全stepと設定画面で常時全文表示する。

> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません

- 文言を省略、要約、言い換えない。
- warningは設定値、snapshot、score、statusへ保存しない。
- checkbox、同意、dismiss、role、期限を要求しない。
- warning操作をrun、cancel、resume、formula、finalizationの条件にしない。

### 11.3 同梱CLIによるlogin開始（D-06採用済み）

1. 利用者が「GitHubにログイン」を押した場合だけloginを開始する。GUI起動、Prompt適用、認証状態確認から暗黙に開始しない。
2. 既存のbundled resolverでmanifest、RID、SDK／CLI版、SHA-256を検証した絶対CLI pathだけを使い、固定CLI `1.0.79`の`login` subcommandを直接子processとして起動する。shell command文字列、PowerShell、`cmd /c`、任意command実行を介さない。optionは固定版で実在と動作を確認したものだけに限定する。
3. 認証用console／ブラウザーとcredential保管はCLIに委譲する。アプリはtokenやdevice code等を解析・収集・保存せず、引数・標準入力・application logへ渡さない。独自OAuth、token入力UI、WebView、callback serverを追加しない。
4. loginの二重開始と評価実行中のlogin開始を防ぐ。取消・失敗時もGUI、Excel読込、mapping、設計編集を利用可能に保ち、safeな理由と再試行操作を表示する。
5. login子processを取消・強制終了するのは利用者のlogin取消またはアプリ終了時だけとし、アプリが開始・所有した当該login processだけを終了・解放する。process tree全体や名前一致で一括killせず、ブラウザー、他のCLI、credential storeに触れない。credentialの削除、logout、失効を行わない。
6. 完了・取消・失敗後は利用者が既存の「Copilot 状態を確認」で認証を再確認する。process起動・終了codeだけを認証成功とせず、自動model選択変更やAI評価開始を追加しない。
7. login前後で固定CLIのversion／hashを維持し、自己更新によるmanifest不一致を許容しない。必要な更新抑止optionも固定版の確認に基づく。CLI欠落・不一致時は配布物の再取得／展開状態の確認を案内し、PATH上の別CLI導入やhash検証緩和で回避しない。

### 11.4 主画面と設定の分担

- 固定の「1 入力 / 2 採点設計 / 3 実行 / 4 結果」を維持する。設定は同一ウィンドウ内の独立した内容画面であり、第5ステップ、modal、drawerではない。
- 主画面は対象、配点、実行の開始判断、結果を扱い、有効値と変更入口を残す。「変更」から同じ対象IDの設定カテゴリへ1操作で移動し、「設定から戻る」で元ステップへ復帰する。同じ入力欄を二重配置しない。
- 設定は「共通」「入力詳細」「通常評価」「固有評価」「読込Prompt」の5カテゴリとする。共通内の定義名・revision・丸めはDesignの採点定義に属し、独立した6番目のカテゴリや実行設定へ移さない。
- 入力詳細は候補再適用・補助列・設問名・複製・並替え等、通常評価は既存evaluator／criterionのCRUD・range・weight・Knowledge読取・Custom編集、固有評価はsource／補助列／Prompt／enabledを扱う。結果とoverrideは設定へ移さない。
- 対象がないカテゴリも位置を保ち、利用できない理由を表示する。カテゴリ変更でworkflowを進めず、前後操作は行先を明示する。訪問済みと準備完了・処理成功を区別し、結果画面に無効な最終ステップ主ボタンを残さない。
- 既存FluentThemeと必要な静的Fluent System Iconsだけを使い、常時見える日本語ラベルを併記する。素材・revision・LICENSE／NOTICEは採用時に記録し、新UI framework、renderer、NuGet依存、テーマ切替を追加しない。

### 11.5 通常表示・ページ切替・例外到達

- 最小1024×720 DIP、初期1180×800 DIPを維持し、寸法を引き上げて達成扱いにしない。Window指定値と実ClientSizeを分けて実測する。
- 最小サイズ以上の通常画面は外側スクロール不要とし、初期offsetで警告全文、現在地、主要操作、有効値、前後移動が実viewport内に完全包含されることを要求する。外側ScrollViewerを残す場合は`Extent <= Viewport`を確認し、scrollbarの非表示やclippingだけを合格にしない。
- 多数の設問・結果はコンパクト一覧とページ切替で扱い、前／次、表示範囲、全件数、結果の元行番号への移動を提供する。表示件数は残領域・実際の行高から決め、業務上限にしない。有限高さとvirtualizationを維持し、全件Control生成を避ける。
- 追加・削除・並替え・リサイズ時にページ範囲を補正し、対象が残る限りIDで選択を保持する。削除時だけ隣接対象へ移動する。空一覧を明示し、多数のevaluator／criterionは選択詳細で編集する。複数エラーも件数・対象・次の問題への移動を示し、隠して検証成功にしない。
- 長文の設問text／Prompt、全文path、dropdown候補には局所スクロールを許容し、keyboardで全文へ到達可能にする。760×600 standaloneと200%表示はreflow・表示行数削減を先に行い、不足時だけ本文の縦スクロールを許容する。固定領域で操作やfocusを覆わず、通常サイズの非スクロール成功へ算入しない。
- 本文・入力14 DIP、主操作target最小44 DIPを維持する。MinHeightは下限であり実高さの固定値ではない。余白は4／8／12／16を基準に重複を減らし、未測定の固定表示件数を約束しない。
- Tab／Shift+Tab／Enter／Space、カテゴリ切替後と設定終了時のfocus復帰、日本語Accessible Name、文字／icon／focusのcontrast、色以外の状態表示を検証する。Automation IDは既存の安定IDを可能な限り保ち、対象ID＋操作名等で一意にする。ページ連番・表示名を識別子にしない。
- native Windowsの実DPI／Narrator確認とheadless測定を区別する。論理作業領域が最小window未満の環境で無条件に収まることや、WCAG適合認証を保証しない。

### 11.6 設定fileの読込と明示保存

- 保存先はOSのLocalApplicationData配下`StudyReportEvaluator/setting.txt`とする。Windowsでは通常`%LOCALAPPDATA%`配下であり、EXE、入力、出力、抽出cache、CLI credential storeと分離する。新しい製品CLI引数・必須環境変数を追加しない。
- UTF-8 JSON、設定schema整数1で、共通設定（通常model希望ID、並列度1〜3・既定1、absoluteな明示出力先または`null`）と任意の採点定義1件だけを保存する。設定schemaはcanonical schema・要求版・製品版とは独立する。
- 採点定義はID・name・revision、sheet／header／行範囲、base／special／similarity係数、設問text・mapping・Points・enabled、evaluator／criterion／range／weight、固有評価、適用済みPrompt、丸めを含む。ID・順序・decimal・Unicode・改行・Promptとcanonical hashの往復一致を要求する。
- 保存しないものは入力xlsxのpath・bytes、回答行を自動収集した本文、AI結果・reason・evidence・参照回答、run／checkpoint状態、credential・login状態・CLI hash、warning承認状態、未適用Prompt一覧・本文、Control・Command・選択ID・表示ページ・操作履歴とする。平文に含まれ得る内容は§14に従う。
- 有効な編集は次回用draftへ反映するが、diskへの書込は利用者の明示保存だけとする。起動・読込・主列変更・画面遷移・終了で自動保存しない。fileなしは既定値で継続し、最初の明示保存までfileを作らない。設定読込完了前に未読の保存定義を空で上書きできないようにする。
- BOMあり／なしを受理し、型・必須値・enum・範囲、schema欠損／不正型／未知版を検査する。同schemaの未知項目も拒否し、黙って捨てて再保存しない。破損・未知版・読込拒否でも元fileを保持し通知してoffline編集を継続する。自動修復・移行・削除を行わない。
- 明示保存前に共通値と、保存対象にある定義・Prompt・配点を既存validatorで検証する。不正draftと旧fileを保持し、入力未読込で共通設定だけを保存する場合は読込済みの保存定義を消さない。
- 保存時点の値を固定し、同じdirectoryの一意tempへwrite／flush／close後にatomic置換する。先に旧fileを削除したり、直接切り詰め書込したりしない。失敗時は旧file・現在draftを保持し、自分のtempだけを後始末する。
- 「未保存」「保存中」「保存済み」「保存失敗」を実際の結果で区別する。保存中に再編集した現在draftは保存成功後も未保存とする。同一画面の二重保存を防ぎ、別processでは最後に成功した保存が優先する。merge・監視・履歴を追加せず、電源断・任意network filesystemまで無条件の耐久性を保証しない。
- production構成でのみ保存場所を解決する。自動testは一時directoryのabsolute pathをstoreへ渡し、単体VMの既定生成から実利用者設定へアクセスしない。汎用filesystem抽象を追加しない。

### 11.7 保存定義の明示適用

- 起動時は保存定義を保持するだけで自動適用しない。Excel読込後、保存済みsheet・行範囲・mapping・設問数を示し、利用者が「現在の入力に適用」を明示した場合だけ適用する。run中の一括適用は無効にして理由を示す。
- 保存定義のheader行に対応したmetadataを既存read-only loaderから取得し、sheet・行・列・定義全体を検証する。以前のheader metadataを流用しない。
- 成功時だけInput metadata・選択値・draftとDesignを一緒に更新する。失敗・取消では現在の状態と保存fileを変更しない。
- 保存したID・順序・設問text・Prompt・配点を保持し、候補再生成や別header値への暗黙置換を行わない。その後に利用者が主回答列を変更した場合だけ、従来の§4.3の設問text同期を行う。
- Imported Promptの一覧・本文・順序を変更・消去せず、未適用Promptを定義へcopyしない。別Excelの意味的な適合は利用者が確認し、列・sheetの存在だけで授業内容一致やcheckpoint再開可能とは判定しない。

### 11.8 往復・次回設定・現在runの分離

- 同一起動中の値・対象ID・ページ・設定カテゴリ・入力途中の編集状態を往復で保持する。Input／Designが既存draftを所有し、設定はその編集面を束ねる。最新の編集元を1回同期し、古いcopyや再生成前のDesign参照で上書きしない。Undo／Redo engineや第三の汎用draft storeを作らない。
- 共通設定は今回の明示編集 → 保存済み共通設定 → 従来既定値の順に使い、出力先は§9.1に従う。保存modelの希望IDと実効選択を分離し、利用者の明示状態確認で候補に存在するときだけ実効選択へ反映する。
- 希望modelが利用不可なら実効選択を未選択にして明示変更を要求し、別modelへfallbackしない。確認失敗だけで保存希望IDを消さない。希望ID未指定の初回は従来の明示確認後の初期選択を維持し、通常modelと固定`auto`のavailabilityを別々に検証する。
- 画面遷移、Prompt／保存定義の適用、設定読込・保存、login完了から認証確認・login・AI runを自動開始しない。開始requestには実際に利用可能と確認した値だけを入れる。
- run開始時にrequest／immutable snapshotを固定し、実行中も戻る・次回用編集を許可するが現在runを再構成しない。進捗入口・停止・予約済みpathを全画面で利用可能にし、現在runと次回条件を別表示する。設定表示中にrunが完了しても設定を閉じず、結果到着だけを通知する。
- 同じ入力・定義の単なる往復では新規／再開・partial指定・進捗・直前runを初期化しない。入力／定義を変更した場合だけ次回準備を更新し、再開指定を再確認または解除して理由を表示する。新規／再開・partial指定は設定fileへ永続化しない。
- 結果は前回runとして保持し、次回draftを過去結果へ混入させない。結果の一覧／詳細は追加ステップではなく、ページ移動後も元Results collectionのoverrideを保持する。0とblank、完成版と未保存修正版、予定名／予約済み／保存中／完成を実データから区別し、未実行scoreや未完成fileを生成済みとして表示しない。

## 12. Promptファイルからの起動

### 12.1 command line

次をサポートする。

`StudyReportEvaluator.App --input <xlsx-path> --prompt <txt-path> [--prompt <txt-path> ...]`

単一EXEの配布名`StudyReportEvaluator-win-x64.exe`でも同じ引数契約を提供する。ZIP内の`StudyReportEvaluator.App.exe`による既存起動を維持し、新しい起動引数は追加しない。

- `--input`は任意。指定時は入力pathを事前入力し、安全なread-only読込を開始する。
- `--prompt`は複数指定可能。
- Prompt fileはUTF-8 plain text `.txt`とする。
- 1fileは32,767文字以下とする。
- unknown option、重複`--input`、missing value、非txt Promptを明確な起動errorとする。
- 相対pathは起動時のcwdを基準に解決し、EXE配置先やruntime抽出先へ変更しない。任意cwd、日本語・空白を含むpath、複数Promptの順序と同一pathの既存取扱いを維持する。

### 12.2 GUIへの反映

- 読み込んだPromptはbasenameと本文を設定の「読込Prompt」一覧へ表示し、Design主画面に件数と同じ適用対象の設定への入口を残す。
- Prompt一覧はcommand lineへ指定された順序を維持し、同じPromptを複数の対象へ再利用できる。
- 利用者が一覧からPromptを選び、通常Custom evaluatorまたは固有項目を選んだうえで「Promptを適用」buttonを明示実行する。
- filename規則による設問への暗黙割当を行わない。
- Prompt適用はtemplateのcopyだけを行い、step遷移、認証確認、loginやAI処理を開始しない。Execution画面の「実行」buttonを利用者が押すまでAI送信を0件とする。未適用Prompt一覧はsetting.txtへ保存せず、保存定義の適用でも変更・消去しない。
- warningと設計検証を表示し、利用者が実行buttonを押した場合だけrunを開始する。

独自VS Code拡張、custom URI scheme、background daemonは初版へ追加しない。GitHub CopilotのPromptから実行fileと引数を指定する運用例を文書化する。

## 13. 配布とinstall

### 13.1 共通

- end-user packageは.NET 10 self-containedとし、利用端末への.NET Runtime／SDK導入を不要にする。
- GitHub Copilot SDKと互換なCLI runtimeをpackageへ同梱する。
- source build用SDKを一般利用者端末へ導入しない。
- GitHub loginは利用者本人の対話が必要であり、scriptがcredentialを収集しない。
- end-user primary pathは正式配布元から取得済みのWindows単一EXEを開く操作からGUI表示までとする。ダブルクリックを1起動gestureと数え、download、任意のhash比較、OS警告への操作、本人loginは含めない。ZIPは手動展開を伴う代替経路として残す。
- SHA-256 sidecarはEXE／ZIPの両方で公開し、CI・公開gateで最終bytesとのexact一致を必須とする。利用者の手動hash比較は任意の推奨であり、起動の必須操作ではない。sidecarをEXEのruntime依存にしない。同一配布元のhash一致は発行者の真正性やSmartScreen reputationを保証しない。
- package作成、展開、起動は入力／出力／checkpoint workbookを変更または削除しない。
- test certificate、unsigned package作成、cross-publishだけの成功は`PASS_MECHANISM`であり、正式公開証跡にしない。単一EXEの公開には§13.6〜13.7のexact artifact検証とclean-host実測を別途要求する。

### 13.2 Windows

- Windows 11 x64のpublic primary artifactは`StudyReportEvaluator-win-x64.exe`と`StudyReportEvaluator-win-x64.exe.sha256`とする。GitHub Releasesからdirect配布し、取得済みEXE1個から手動展開・追加installなしに入力画面を開く。
- 代替artifactは`StudyReportEvaluator-win-x64.zip`と`StudyReportEvaluator-win-x64.zip.sha256`とする。ZIPを新しいdirectoryへ展開し、`StudyReportEvaluator.App.exe`を起動する既存経路を維持する。両経路の手動hash比較は§13.1の任意推奨とする。
- EXE／ZIPはunsignedであり、SmartScreen、Smart App Control（SAC）、企業policyによる警告・実行拒否があり得る。「誰でも1操作」「すべての端末で警告なし」とは表示しない。拒否を回避する保護無効化、MOTW除去、証明書の自動trust、execution policy変更、UAC回避を実装・案内しない。
- Windows 11で証明書なしのcontainer互換性を先行試験する場合は、Publisherの最終fieldに固定marker`OID.2.25.311729368913984317654407730594956997722=1`を置き、明示的なunsigned test artifact名を使用する。実行codeを含むため、使い捨てVMまたは復元可能なsnapshot上で管理者PowerShellの`Add-AppxPackage -AllowUnsigned`による全ユーザーinstallとして実行し、結果を`PASS_MECHANISM`に限定する。この経路を一般利用者setup、signature trust、production配布の証拠にしない。
- development MSIX required gateはpackage作成、unpack、manifest/version/RID、block map、public payload、bundled CLI hash、sidecar、policy negative、cleanupまでとする。install／launch／upgrade／repair／uninstallは実行した場合だけ追加の`PASS_MECHANISM` evidenceとし、初回公開をblockしない。
- development MSIXの既存sourceと非公開regressionを維持するだけとし、新しいinstaller機能やrequired install試験へ拡張しない。
- public ZIPはcleanな展開先でself-contained apphostとbundled CLI identityを検証する。installer、trusted package、SmartScreen reputation確立済みとは表示しない。
- engineering用publish/package scriptはPowerShell 7以上だけを使用し、Windows PowerShell 5.1へfallbackしない。PowerShellをend-user setup要件にしない。

### 13.3 macOS source foundation（現版公開対象外）

- `osx-arm64`と`osx-x64`のpublish、bundle、sign、notary script foundationをrepositoryに保持し、secretなしのstatic contractを検証できる。
- bundle identifierは`com.github.dahatake.study-report-evaluator`とし、`Info.plist`のexecutable、version、icon、minimum OSをartifactへ一致させる。
- DMGの一般利用者手順とdownload URLは、将来のproduction evidenceが揃うまで公開しない。
- apphostとbundled CLIをnative architectureで検証し、x64成功をArm64へ、Rosetta成功をnative x64／Arm64へ代用しない。
- Developer ID Applicationでnested executableを内側から署名し、shipped CLI hashをruntime manifestへ反映してからouter appを署名する。outer署名後にbundleを変更しない。
- hardened runtime、必要最小限のentitlement、secure timestamp、appとDMGのnotarization／stapling／validation、quarantine付きFinder launchを必須とする。
- macOS 14、15、26は将来の試験候補であり、現版では対応表示しない。将来scopeへ追加する場合だけ、exact OS build × architectureの`PASS_PRODUCTION`行を要求する。

### 13.4 非対応platform・外部前提・release境界

- Linux、Windows Arm64、macOS 13以前、universal macOS artifact、Microsoft Store、Mac App Storeは本版scope外とする。
- Windows production signing identityは現版で要求しない。Apple Team／Developer ID／notarization credential、approved visual assets、macOS clean hostsをrepository内の仮値で代用しない。
- macOS外部入力がない状態は現版のrequired release blockerにせず、macOS artifactと対応claimを公開しない。
- Windowsのbuild、macOS cross-publish、Avalonia/.NETの一般的なcross-platform対応を、対象platformでの本製品install／launch／CLI／workbook証跡として代用しない。

### 13.5 Windows単一EXEの標準publish・展開

- production projectは既存のCoreとAppの2つだけを維持する。Windows single-file profileをAppだけへ適用し、solution全体やCoreへ適用しない。通常folder／ZIP publishの引数なし動作・出力とmacOS source/static contractは変更しない。
- restoreとpublishに同じ`win-x64`、self-contained、`PublishSingleFile=true`、`IncludeNativeLibrariesForSelfExtract=true`、`IncludeAllContentForSelfExtract=true`の条件を渡し、folder出力から分離する。`PublishTrimmed=false`、`PublishReadyToRun=false`、圧縮無効、symbols非配布を維持する。固定versionはheaderのとおり変更せず、実在するsingle-file build-only依存だけをlockし、検証やanalyzerを無効化して通さない。
- `IncludeAllContentForSelfExtract`はMicrosoftが**非推奨**とする.NET Core 3.1互換モードであり、将来削除される可能性がある。S01で固定.NET SDK `10.0.400`／runtime `10.0.11`と固定Avalonia／SDK／CLIへの適合を開発hostで確認したことを条件に採用する。これはclean-host成功や将来versionの互換性を保証せず、正式公開前には§13.7の実測を必須とする。
- .NET/native依存、固定CLI、runtime manifest、既存ZIPで明示allowlistにあるREADME／利用者docs／画像／LICENSEをbundle前に含める。repository全体のglobを使わず、sample、利用者input／final／partial、setting.txt、work、tests、secretを含めない。最終配布名へ配置したEXEのbytesからsidecarを作り、検証後にEXEを書き換えない。
- 全内容展開後の`AppContext.BaseDirectory`を基準とする既存manifest／CLI相対配置を保持する。RID、SDK／CLI版、CLI SHA-256の検証とPATH fallback禁止を維持し、独自launcherによる引数組み直しやresolverの検証緩和を行わない。
- Windowsでは.NET標準hostが起動前に通常`%TEMP%/.net/<app>/<bundle-id>/`配下へ内容を展開する。1ファイル配布は「ディスク上でも1ファイル」「痕跡なし」を意味しない。`DOTNET_BUNDLE_EXTRACT_BASE_DIR`は試験の隔離に使えるが、利用者の設定や独自環境変数・API keyを要求しない。
- 標準hostのcache再利用・欠落復元・並行起動処理を利用し、独自の展開／cache／locking engine、cache管理UI、自動掃除を追加しない。標準抽出を全cached fileの暗号学的検証とみなさず、CLI integrity検証を残し、別権限userから書換え可能な抽出先を対応済みとしない。
- cacheはアプリ配置用であり、input／final／partial／setting.txtの保存先にしない。§9.1の明示出力先を復元し、未指定（null）の場合だけ入力隣接の`result`を使う。配布・展開・起動のためにworkbookを移動・削除しない。checkpoint形式と同じ製品版・runtime identityのEXE／ZIP間の既存再開条件を維持する。§9〜10のatomic finalizationと完成成功後のpartial cleanupは変更しない。

### 13.6 公開matrix v2とcandidate拘束

新経路の公開はmachine-readable matrix v2を使い、次の3行だけのclosed setとする。v1は過去の契約として残し、unknown／duplicate／missing rowを受け入れない。

| Required row | artifact／sidecar | publish | 必要な証跡 |
|---|---|---|---|
| Windows x64単一EXE | `StudyReportEvaluator-win-x64.exe` / `StudyReportEvaluator-win-x64.exe.sha256` | `true` | package required testsとexact EXEのCH-01〜06 |
| Windows x64 ZIP | `StudyReportEvaluator-win-x64.zip` / `StudyReportEvaluator-win-x64.zip.sha256` | `true` | 既存ZIPのpackage／clean extract／起動／CLI／data保護のrequired tests |
| Windows x64 development MSIX | candidateで実物検証したartifact／sidecarのdescriptor | `false` | 既存範囲の`PASS_MECHANISM`検証記録 |

1. 順序は**候補生成 → exact EXEのclean-host試験 → protected publish時のv2最終matrix確定**とする。candidate生成時に未実施のclean-host結果や公開可能matrixを生成しない。candidateのpackage／mechanism required testsを満たした後に限り、EXE／ZIPと各sidecarの計4 assetをdraftへ添付する。publicのassetも同じ4個だけとする。
2. EXE／ZIPは同じsource commit、製品版、SDK／CLI版から作る。candidate run ID／commit、artifact basename／bytes／SHA-256、sidecar、package evidence、clean-host evidenceを同じ成果物へ結び付ける。doc同梱や版変更等で最終EXEのbytesが変われば、以前のclean-host結果を流用せず再package・再検証する。
3. development MSIXは同じcandidate runで実物検証した結果とartifact／sidecar descriptorを既存の内部control artifactに保存する。本体をGitHub ReleaseにもActions artifactにもuploadしない。公開時はcandidateに拘束された記録を照合し、MSIX本体を再取得・再作成・再検証したと扱わない。
4. clean-host担当者は当該candidateのEXEで試験し、candidate run ID／commit、EXE basename／bytes／SHA-256／製品版を含むmetadata限定のclosed JSONを作る。OS edition／build／architecture、標準user、追加依存・保護状態、実build SDK／bundled runtime／Copilot SDK・CLIの版とCLI hash、試験ID別結果と操作数、実施記録の参照／hashを記録し、username、token、device code、学生本文、環境変数値一覧、生ログを含めない。公開前に製品repositoryへcommitしない。
5. 既存protected publish workflowの`clean_host_evidence_json`入力でJSONを受け、環境変数経由で一時file化し、型・長さ・許可field・必須試験IDを検証する。workflow式をshell本文へ直接埋め込まない。新しいstorage／workflow／証跡基盤を作らず、人の試験記録であることを明記し、hash一致だけで実施事実が自動証明されたとしない。
6. protected publishはcandidateが指定repositoryの`release.yml`による成功runであることとtag commitの一致を確認する。EXE／ZIPの4 assetを再downloadしてversion／bytes／hash／sidecarを照合し、MSIXは同runの検証記録とdescriptorを照合する。v2最終matrixと受領JSONは内部control artifactへ保存し、public assetへ含めない。
7. 既存Core／App／packageのrequired testsと§13.7の必須試験がすべてPASSの場合だけ公開可能とする。必須試験の欠落・FAIL・NOT_RUN、別candidate／source／version／hashの証跡、sidecar不一致を拒否する。unsignedの`PASS_REQUIRED`は署名・installer等の`PASS_PRODUCTION`を意味しない。tag／push／draft／public Releaseの操作承認は実装承認から分離する。public化にはprotected environmentの公開承認を要求する。

### 13.7 clean-host公開必須試験

fresh Windows 11 x64実機またはVMの標準userでexact EXEを試験する。開発hostのPATH／.NET環境変数隔離やhosted CI成功をOS-only証跡にしない。guestに検証用SDKやPowerShellを導入してからOS-onlyと呼ばず、OS付属.NET Framework等と追加.NET Runtimeを区別する。EXE size、展開容量、初回／再起動の所要時間は実測を記録し、数値SLAは設けない。

| ID | 内容 | 公開条件 |
|---|---|---|
| CH-01 | OS edition／build／x64、fresh標準user、.NET SDK／Runtime、PowerShell 6+、Node／npm、Git／gh、別Copilot CLI、Office、IDEの未導入を確認 | 必須PASS |
| CH-02 | EXE1個だけからofflineで入力画面、Excel読込、設計を利用。sidecar／repository／隣接file／既存CLI cache・認証に依存しない | 必須PASS。保護機能による実行拒否を起動成功としない |
| CH-03 | 同梱CLIのStart／Ping／auth状態確認を外部PowerShell／Node／Git／gh／CLIなしで実行 | 必須PASS。正常な未認証応答とruntime failureを区別 |
| CH-04 | 移動、再起動、同時起動、任意cwd／起動引数、日本語・空白path、read-onlyなEXE配置先、cache欠落からの復元、data保護 | 必須PASS |
| CH-05 | 標準ブラウザーで取得したMOTW付きEXEのSmartScreen／SAC／企業policy状態、警告・拒否、実際の操作数を記録し承認範囲と比較 | 必須PASS。無警告を一律要求せず、警告や追加操作を隠して1操作成功へ丸めない |
| CH-06 | fresh userの本人login、既存buttonでの完了後・再起動後の再確認、取消／アプリ終了時の当該login processだけの終了と他process／data／credential保護 | D-06採用済みのため必須PASS。JSONで任意化・N/A化しない |
| ADV-01 | 本人が明示承認したsynthetic入力による実AI評価 | 任意。NOT_RUN可。必須GUI／CLI／login試験の代替にしない |
| ADV-02 | Office等の外部spreadsheetによる再計算 | 従来どおり任意。NOT_RUN可 |

fake／help／process終了だけの成功はCH-06の本人認証に代用しない。必須試験未実施は公開を拒否するが、ADV-01／02のNOT_RUNを必須試験の失敗へ変換しない。

## 14. privacy・security・安全境界

- AIへ送るworkbook由来の値は、現在行の選択済みprimary/supporting/special sourceだけとする。
- question text、Prompt、criterion metadata、参照回答、closed schema metadataは必要範囲で送る。
- 他行、非選択列、workbook pathを送らない。
- reference answerはその質問の全学生比較へ使用するため、類似度Promptに含める。
- untrusted textはExcel string cellとして保存し、formulaとして実行しない。
- outputとpartial workbookは元本全体、Prompt、AI結果を保持するため、元本と同等以上に機密として扱う。
- app-owned cloud backend、database、telemetry本文送信を追加しない。
- checkpointは暗号化containerではない。保存先のaccess controlは利用者のOS権限に従う。
- runtime抽出cacheと利用者workbook／CLI credential storeを分離する。配布・展開・起動のために利用者workbookを移動・削除しない。アプリはcacheの再帰削除、旧版の自動掃除、credential削除を行わない。
- loginでは検証済みCLIとブラウザーに本人認証を委譲し、アプリはtoken／device code等を収集・解析・保存・log出力しない。取消／アプリ終了による終了対象は§11.3の所有login processだけとする。
- Windowsの保護設定を変更せず、実行拒否を回避しない。公開用evidenceも§13.6のmetadataに限定し、利用者本文やcredentialをcontrol artifact／公開物へ混入させない。
- 定義を明示保存すると、主回答列選択によって見出しセルから取り込まれた設問textもsetting.txtに平文で含まれる。Prompt・評価基準等へ利用者が貼り付けた内容、sheet名、明示出力pathも含まれ得る。読込・主列変更だけで自動保存する意味ではない。
- 回答行本文の自動収集は追加しないが、機密な本文が設定へ絶対に含まれないとは保証しない。setting.txtは暗号化containerではなく、OSの利用者別保存先のaccess controlに従う。§11.6の非保存対象を守り、設定の実内容を公開物・画像・log・共有証跡へ混入させず、自動削除機能を追加しない。

## 15. performance・capacity

- 回答行上限は20,000とする。
- concurrencyは既定1、最大3とする。
- Excel row、column、cell、formula、function argument上限をwrite前に検査する。
- Promptとschemaのapp-owned request上限を測定し、model contextの安全marginをAI送信前に検査する。
- retry込みattempt上限を実行前に検査し、黙って切り詰めない。
- checkpoint保存時間をprogressへ含める。
- AI待機を除く531行workbookのread/write/final validation性能は、対応OSごとに実測値と環境を記録する。未実測platformの数値を保証しない。
- 表示ページの件数を業務上限にせず、全件到達と有限高さのvirtualizationを維持する。UI試験の100行／530行syntheticと20,000件のページ境界計算は分離し、境界計算成功を20,000件実画面の性能実測へ読み替えない。

## 16. failure behavior

| Condition | Result |
|---|---|
| 空の通常回答 | AI callなし、通常評価率0、QuestionEarned 0、similarity 0 |
| 空の固有項目 | AI callなし、当該special score 0 |
| 質問文行とmetadataの不一致 | 現在のQuestionTextを保持し、以前の質問文行の値を反映せず、`HEADER_METADATA_MISMATCH`で再読込を要求 |
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

### 17.1 v4.6 required scope

- Windows 11 x64
- RID別.NET 10 self-contained app
- bundled compatible Copilot CLI runtime
- standard `.xlsx` from Microsoft Forms or Google Forms
- native input picker
- base / question / special point allocation
- reference answer generation and per-question similarity penalty
- Excel-owned formulas
- per-student-row `.partial.xlsx` checkpoint and process-restart resume
- GUI prefill from input/Prompt text command-line options
- App限定の.NET標準single-file全内容展開によるWindows self-contained unsigned EXEとSHA-256 sidecarを主配布へ追加
- Windows self-contained unsigned ZIPとSHA-256 sidecarを代替として維持
- D-06採用による同梱CLIの明示login開始・取消・既存buttonでの認証再確認
- non-public development MSIXのpackage/unpack/integrity `PASS_MECHANISM` evidence
- clean extract、apphost起動、bundled CLI identityのWindows evidence
- exact EXEのclean-host CH-01〜06と、EXE／ZIP／development MSIXのclosed 3-row matrix v2による公開判定
- teacher, operator, engineer documentation and actual synthetic screenshots
- 4ステップを維持した同一window内の設定5カテゴリ、通常最小サイズの外側スクロール不要、ページ切替と長文／狭小／拡大の到達性例外
- 利用者別setting.txtへの共通設定＋採点定義1件の明示保存、検証後の保存定義明示適用、明示出力先復元とnull時だけの入力隣接result
- 値・対象・ページ・カテゴリの往復保持、保存model希望IDと実効選択の分離、現在run／次回draft／前回結果の区別

### 17.2 out of scope

- macOS、Linux、Windows Arm64、universal macOS artifact support claim
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
- production-signed/public MSIX、Developer ID/notarized DMG、Microsoft Store、Mac App Store
- end-user primary pathとしてのPowerShell／shell setup script
- 新しいproduction project／application、独自launcher／自己展開engine、汎用process runner／認証provider、将来用の拡張点
- online bootstrap、auto-update／差分更新、独自cache／locking engine、cache管理UI、runtime cacheの自動cleanup
- 新規installer、既存development MSIXの機能拡張、registry／PATH恒久変更、file association、保護機能の回避
- token入力UI、独自OAuth／WebView／callback server、依頼に伴うSDK／CLI／Avalonia更新、Native AOT化、trimming全面適用
- 設定の自動保存・保存定義の自動適用、複数profile、cloud同期、設定暗号化container、migration／backup／監視／merge／永続操作履歴engine
- 第5workflowステップ、汎用設定provider／filesystem抽象／router／form builder／pagination framework、Undo／Redo、AI設定提案、実AI preview、新規UI framework／NuGet／製品CLI引数／必須環境変数

## 18. Acceptance criteria

| ID | 条件 |
|---|---|
| AC-001 | native pickerまたはpathから標準 `.xlsx`を選び、元本を変更せず別 `.xlsx`を作る。 |
| AC-002 | question text rowを1または2から選び、sheet、回答行、質問／通常回答／固有項目列を変更可能な候補として表示する。主回答列を選択すると、同じsheet・question text rowの交差セル値を当該設問textへ即時反映する。 |
| AC-003 | 指定sampleでF〜Jをprimary候補、G/Jを学生Prompt primary候補、H/Kをsupporting候補、KをJの初期supporting候補として提示し、元本identityを維持する。 |
| AC-004 | base既定60、special既定0、similarity weight既定0.1を表示・変更できる。 |
| AC-005 | 設問Pointsの初期値が`(100-base-special)/有効設問数`となり、明示的な均等配分以外で手動値を変更しない。 |
| AC-006 | `base + special + Σ question points = 100`をrun前とformulaで検証する。 |
| AC-007 | 通常Knowledge/Custom評価を動的に構成し、AIはcriterion rawだけを返す。 |
| AC-008 | 各設問へ0件以上の固有評価項目を設定し、同一設問内と対象設問間を等分平均してSpecialEarnedを計算する。 |
| AC-009 | `auto`で各設問1件の参照回答を生成し、同一runの全学生で共有してReferences sheetへ保存する。 |
| AC-010 | 各学生回答と参照回答の類似度を0〜1で定量化し、`Points × Similarity × Weight`を設問別に出力する。 |
| AC-011 | QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、0〜100 clamp済みFinalScoreをConfig参照Excel数式で計算する。 |
| AC-012 | 空入力はAIを呼ばず0、技術的AI失敗はblankとし、FinalScoreへblankを伝播する。 |
| AC-013 | §9.1の実効出力先へ`eval-yyyyMMdd-HHmm[-NN].xlsx`をno-overwrite atomic commitし、元全sheetと4 app-owned sheetsを保持する。明示出力先がnullの場合だけ入力隣接`result`を使う。 |
| AC-014 | run開始時に`.partial.xlsx`を作り、参照生成および各学生行完了後にatomic checkpointする。 |
| AC-015 | input、definition、model、runtime identity一致時だけ再開し、参照回答と完了行を再実行しない。 |
| AC-016 | 進捗、処理段階、完了／一部失敗、final／partial pathと件数を実データから表示する。4ステップと設定は最小1024×720 DIP以上の通常画面で外側スクロール不要かつhorizontal overflowなしとし、警告全文・主要操作・状態を実viewport内へ完全包含する。多数項目はページ切替・対象行移動、長文は局所scroll、760×600 standalone／200%表示はreflow後に必要な本文縦scrollで全操作へ到達し、keyboard、44 DIP target、安定した一意Automation ID、virtualizationを維持する。clippingを合格にしない。 |
| AC-017 | 指定警告文を全stepと設定画面で常時全文nonblocking表示する。 |
| AC-018 | `--input`と複数`--prompt`でGUIを事前入力し、利用者操作なしにAI実行しない。 |
| AC-019 | selected same-row dataだけをAIへ送り、本文／Prompt／reason／evidence／credentialをlogへ残さない。 |
| AC-020 | 移行中もWindows 11 x64 self-contained unsigned ZIPがcleanな展開先で起動し、SHA-256 sidecarとbundled CLI identityを検証できる。 |
| AC-021 | `PASS_PRODUCTION`がないplatform、architecture、installer、signing、notarizationを対応済みとして表示しない。ただし本版のunsigned Windows EXE／ZIPは§13.6〜13.7の`PASS_REQUIRED`を公開判定とし、署名・installer・notarization等のproduction trustを満たしたとは表示しない。 |
| AC-022 | README、教師tutorial、Prompt例、install、privacy、troubleshooting、開発設計、実画面screenshotsを現行UIとcurrent delivery evidenceへ同期する。 |
| AC-023 | Windows x64 development MSIXを作成・unpackし、manifest/version/RID、block map、payload、bundled CLI、SHA-256 sidecar、test-only identityを検証し、一般配布しない。 |
| AC-024 | macOS publish/sign/notary source foundationのstatic contractを検証し、native production evidenceなしにartifactまたはsupport claimを公開しない。 |
| AC-025 | macOS scriptがnested sign、shipped CLI hash、outer sign、notary log、app／DMG staple、strict verificationの順序を要求する。実行結果は現版のrequired acceptanceにしない。 |
| AC-026 | Windows public EXEを主導線、ZIPの取得・展開・起動を代替として文書化する。sidecar公開とCIのexact hash検証は必須、利用者の手動比較は任意推奨、sidecarはEXE起動の前提にしない。.NET Runtime／SDK、PowerShell／Node／Git／gh、別Copilot CLI、Officeの導入やsetup scriptをGUI起動に要求せず、development MSIXを一般利用者手順へ含めない。 |
| AC-027 | public EXE／ZIP/packageとdevelopment MSIXにsample、利用者入力、final、partialを含めず、package作成・展開・起動で利用者workbookを変更・削除しない。 |
| AC-028 | release workflowはcandidateのpackage／mechanism required testsを満たしたEXE／ZIPと各sidecarの4 assetだけをdraftへ添付する。exact EXEの必須clean-host結果を照合したprotected publish時にEXE／ZIP／non-public development MSIXのclosed 3行でmatrix v2を確定し、`publish=true`かつrequired statusを満たすartifactだけを公開する。development MSIX本体・secret・未実測claimを漏らさない。 |
| AC-029 | OSのみのfresh Windows 11 x64・標準userで、取得済みEXE1個を開く1起動gestureからofflineで入力画面を表示し、手動展開・追加導入・昇格を要求しない。OS警告・拒否・実操作数は別途記録し、無条件・無警告を保証しない。 |
| AC-030 | 単一EXEに.NET/native依存・固定CLI・manifest・既存公開docs／画像／LICENSEを含め、外部PATH／SDKやsidecarに依存せず既存CLI integrity検証を維持する。App限定profileとCore／Appの2 production projectを維持する。 |
| AC-031 | EXE／ZIPで任意cwd、日本語・空白path、相対`--input`、複数`--prompt`、既存invalid入力・明示適用の契約を維持する。起動によるAI送信は0件で、EXE配置先や抽出先へcwd基準を変えない。 |
| AC-032 | 初回・再起動・同時起動・cache欠落・抽出中断・容量／権限不足でも利用者input／final／partialを変更・削除しない。cacheとdata／setting.txtを分離し、明示出力先の復元と未指定時だけの入力隣接result、同版EXE／ZIP間の既存checkpoint再開条件を維持する。 |
| AC-033 | 本人のbutton操作だけで検証済み同梱CLIのloginを直接開始し、shellを介さずcredential／device codeを収集しない。二重開始・評価中開始を防ぎ、取消・失敗後もGUIを継続する。取消／アプリ終了時だけ所有login processを終了し、ブラウザー・他CLI・credentialに触れず、既存buttonで再確認する。自動AI実行しない。 |
| AC-034 | candidate run／source／version／bytes／hashに拘束されたexact EXEとCH-01〜06のPASSが一致した場合だけ新経路をprotected publishする。metadata限定JSON、candidate-bound MSIX検証記録、4 public assetsの再download照合を要求し、必須試験の欠落・FAIL・NOT_RUN・差替えを拒否する。未実測のOS-only／署名／installer claimを出さない。 |
| AC-035 | 利用者別setting.txt（UTF-8 JSON、schema整数1）へ共通設定＋任意の採点定義1件を明示保存し、ID／decimal／Prompt／canonical hashを復元する。header由来の設問text・貼付内容は明示保存時に平文で含まれ、§11.6の非保存対象は保存しない。破損・未知schema・IO失敗は元fileとdraftを保持しoffline継続する。保存中再編集は未保存のまま、同時保存は最後の成功が優先する。明示出力先は再起動・入力変更後も復元し、初回の未指定または空欄への明示編集によるnullの場合だけ入力隣接resultを算出し、fallback・復元時directory作成をしない。 |
| AC-036 | 保存定義はExcel読込後の明示操作で、保存headerのmetadata・sheet・行・列・定義全体を検証してからInput／Designへ一括適用する。失敗・取消時は現在状態とfileを変更せず、成功時はID・順序・設問text・Prompt・配点を保持する。Imported Prompt一覧は不変、run中の一括適用は禁止し、既存checkpoint admissionを緩めない。 |
| AC-037 | 設定を第5ステップにせず、同じ対象へ1操作で移動し、値・対象ID・ページ・カテゴリ・入力途中の編集を保持して戻れる。希望modelは明示確認後だけ実効選択にし、不在なら未選択・no fallback、確認失敗だけで保存希望を消さない。遷移・設定読込／保存／適用・login完了で認証確認／login／runを自動開始せず、実行中の次回draft編集は現在snapshotへ混入しない。全画面で進捗・停止を保持し、設定中完了は結果通知だけとする。前回結果・override・未保存修正版を次回設定から分離する。 |

## 19. Test requirements

以下の番号は追跡ID `TR-01`〜`TR-36`に対応する。既存1〜33を再番号付けせず、17等の直接関連する到達契約を追補し、34〜36を末尾へ追加する。T01時点の未実装・試験NOT_RUNは履歴であり、現在の実装・局所検証は[traceability](../dev/docs/traceability.md)のVERIFIED_SCOPEDに限定する。T25〜27の74/74（T26の27ケース・T27の7ケースを含む）と最新修正を含むT28の216/216は別の対象集合で、合算しない。

**0.8.4での記録済み結果:** T35の対象文書試験は4/4成功・敵対的レビュー済み（`artifacts/test/ui-settings/t35/t35-reviewed.trx`、指摘0）。T36の文書・画像contract全体は別scopeで、独自の`artifacts/test/ui-settings/t36/t36-current.trx`が21/21成功・REVIEWED。T37は`artifacts/test/ui-settings/t37/t37.trx`の9/9（実ZIP＋MSIX静的契約）、T38は`artifacts/test/ui-settings/t38/t38.trx`の114/114とP06実EXE 7/7・P07 `PASS_DEVELOPMENT`。T39の自動回帰は`artifacts/test/ui-settings/t39/reviewed/`の2026-09-07の2 TRXでCore 190＋App 1702＝1892/1892、skip 0。初回1失敗→fixture修正→126/126・レビュー指摘0→全体再実行成功の履歴を保持する。MSIX実物は`artifacts/package/mechanism/StudyReportEvaluator-win-x64.unsigned.test.evidence.json`の`PASS_MECHANISM`（256 entries、0.8.4.0）で、install／公開の成功ではない。

これらをF02後の`0.8.5`や全受入のPASSへ流用しない。追加nativeは3試行で停止し、最新`artifacts/test/ui-settings/t39/native-final-attempt.json`は`CONTROL_ID_PREDICATE_NOT_UNIQUE`でFAIL。120 DPI・1475×1000 pixel＝1180×800 DIP・合成入力読込・入力／EXE不変・実利用者設定非作成は部分観測であり、4画面・5カテゴリ・1024×720・実keyboardの成功ではない。Narrator／本人walkthrough4項目／隔離利用者でのnative保存とCH-01〜06は`NOT_RUN_EXTERNAL_PREREQUISITE`。以下は受入に必要な試験契約であり、要求承認・局所検証・過去evidenceを未実施範囲の成功へ拡張しない。

1. Microsoft Forms型、Google Forms型、question row 1/2の匿名化synthetic workbook test。各rowで主回答列変更後の設問textが同列の交差セル値へ一致すること、および質問文行変更後・metadata再読込前は旧行の値を反映せず`HEADER_METADATA_MISMATCH`で再読込を要求することを含む。
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
17. 4-step＋設定、warning exact text/nonblock、progress、resume、completion、keyboard、200% scale test。1024×720／1180×800の通常shellは実ClientSize、Extent／Viewport、主要Control完全包含を実測して外側scroll不要とhorizontal overflowなしを確認する。多数項目のページ切替・元行移動・空一覧・最終ページ・選択保持・リサイズとvirtualization、長文／path／dropdownの局所scroll、760×600 standalone／200%表示のreflow後の本文縦scroll例外を分離し、全操作への到達を検証する。44 DIP、focus復帰、一意Automation ID、native DPI／Narratorをheadlessと分けて記録する。主回答列ComboBox操作後の可視設問text同期も維持する。
18. CLI option parser、UTF-8 Prompt file、複数Prompt、明示適用、no-auto-run test。
19. Windows x64 legacy publish/package layout、bundled CLI、clean launch test。
20. unsigned ZIP、SHA-256 sidecar、safe layout、再現可能な再package regression test。
21. README、利用者文書、package契約が未実測platform、architecture、installer、signing、notarizationを対応済みと主張しないtest。
22. documentation link、screenshot provenance、unsupported claim、Prompt examples contract test。
23. fixed-seed 531-row synthetic end-to-end、resume end-to-end、input hash不変test。
24. optional authenticated synthetic Copilot smokeとexternal spreadsheet recalculation smoke。required deterministic testの代替にしない。
25. Windows development MSIXのmanifest/version/layout、unpack、block map、bundled CLI、sidecar、unsigned OID、policy negative、cleanup test。
26. macOS RID別publish／bundle／sign／notary script、Info.plist、entitlement、secret boundaryのstatic contract test。
27. macOS scriptがhardened runtime、nested/outer signature、secure timestamp、notary log、staple、DMG integrity、quarantine launchをproduction evidenceとして要求することのcontract test。
28. Windows ZIPのclean extract/apphost/bundled CLI/input不変と、development MSIX packageに利用者workbookを含めないことのE2E／package test。
29. Windows ZIPをrequired publish row、development MSIXをnon-public mechanism rowとして扱うmatrix、secret isolation、public candidate re-download、未実測artifact非公開のrelease workflow test。
30. App限定single-file profile／locked restore／publishと最終EXEのpackage test。固定version、restore／publish条件一致、通常ZIP／Core／macOSへの非適用、AMD64／版、実行必須file1個、symbols除外、native／CLI／manifest integrity、docs／画像／LICENSE allowlist、禁止物非混入、sidecarのfinal bytes一致とsidecarなし起動を検証する（AC-029／030）。
31. 実EXEの初回／再起動／同時起動／cache欠落復元／抽出中断／容量・権限不足、移動・read-only配置、任意cwd・日本語／空白・相対path・複数Prompt・invalid引数・明示適用・no-auto-run test。synthetic input／既存final／partialのhash・size・時刻不変、default result位置、同版EXE／ZIPのcheckpoint互換を検証する。実施できないfaultをPASSにしない（AC-031／032）。
32. login専用service／Execution UIのdeterministic test。検証済み絶対CLI／固定login引数だけの直接起動、shell非使用、token／device code非収集・非log、本人操作まで未開始、二重開始／評価中開始防止、失敗・取消後のGUI継続と再試行、取消／app close時の所有process限定終了、ブラウザー／他CLI／credential非干渉、既存buttonでの再確認、no-auto-run、login前後のCLI identity維持、keyboard／Automation ID／200% scaleを検証する。本人loginの実測はCH-06として分離する（AC-033）。
33. exact candidate EXEのfresh OS試験CH-01〜06と公開境界test。matrix v2のclosed 3行／public 4 assets、candidate workflow／run／source／version／hash／sidecar拘束、metadata限定JSONの型・長さ・許可field、MSIXのcandidate検証記録のみ受渡し・本体非upload、再download照合後の最終matrix確定を検証する。unknown／duplicate／missing row、必須試験欠落・FAIL・NOT_RUN、CH-06の任意化、別EXE証跡、機微field、MSIX公開を拒否し、ADV-01／02のNOT_RUNは許容する。開発host／fake成功をclean-host／本人認証の代用にしない（AC-028／029／034）。
34. 一時absolute pathに限定した設定storeの明示保存・再読込test。fileなし、BOM、型／enum／範囲、schema欠損／不正型／未知版／未知項目、定義なし／不正draft、入力未読込時の保存定義保持、ID／decimal／Unicode／Prompt／canonical hash往復、保存中再編集、atomic置換と旧bytes保持・temp後始末を確認する。Windowsの排他file・file-valued親path等で実際のIO拒否を確認し、ReadOnlyディレクトリだけを拒否根拠にしない。合成header／貼付内容の平文保存、回答自動収集・credential／AI結果非保存・no-content logを検証する。未指定で入力A→Bのresult、明示先の再起動→入力Bでの復元、空欄→null、利用不可no fallback、復元時directory非作成を確認し、実利用者設定を使わない（AC-035）。
35. 保存定義の明示適用test。Excel未読込／run中の禁止、同入力／別header／sheet・列・行不一致、取消・失敗時のInput metadata／draft／Design／保存file無変更、成功時のID・順序・設問text・Prompt・配点・canonical hash保持を確認する。後続主列変更の既存同期、Imported Prompt一覧・本文・順序不変、AI送信0、既存checkpoint admission維持を検証する（AC-036）。
36. Input→Design→Settings→Input→Executionの往復と交互編集→保存、入力途中、選択ID／ページ／カテゴリ保持、同じ入力・定義での再開指定非初期化と変更時の再確認、保存希望modelの有／無／欠落・確認失敗・auto欠落、no fallback／no-auto-login/runをfake境界で検証する。実行中の次回編集で現在request／snapshot不変、全画面の進捗・停止、設定中完了の非強制遷移、前回結果とoverrideの保持を確認する。save→新VM／store→read-only Excel明示適用→fake run→別名出力・resumeのdeterministic E2Eで元本、exact100、zero／blank、formula、checkpoint契約を維持する。本人walkthroughは別途実施し未確認を合格にしない（AC-035〜037、AC-016〜019）。

## 20. 外部仕様出典

本要求の実装時は、固定するversionの一次資料を再確認する。

2026-09-06のdelivery追補ではsingle-file互換モードの非推奨注意、Windows保護、CLI本人認証の境界を確認した。一般仕様は本製品のclean-host／login成功証拠ではない。GitHubの最新main資料の構成・API・optionを固定SDK `1.0.11`／CLI `1.0.79`へそのまま適用せず、固定版のhelpと実測で確認する。

- GitHub Copilot SDK: [Getting started](https://github.com/github/copilot-sdk/blob/main/docs/getting-started.md)
- GitHub Copilot SDK: [Bundled CLI](https://github.com/github/copilot-sdk/blob/main/docs/setup/bundled-cli.md)
- GitHub Copilot CLI: [Authenticating GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/authenticate-copilot-cli)
- GitHub Copilot SDK: [Session persistence](https://github.com/github/copilot-sdk/blob/main/docs/features/session-persistence.md)
- Avalonia: [File dialogs](https://github.com/AvaloniaUI/avalonia-docs/blob/main/docs/services/file-dialogs.md)
- Microsoft: [.NET application publishing overview](https://learn.microsoft.com/dotnet/core/deploying/)
- Microsoft: [Single-file deployment overview](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview)
- Microsoft: [dotnet publish](https://learn.microsoft.com/dotnet/core/tools/dotnet-publish)
- Microsoft: [SmartScreen reputation](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation)
- Microsoft: [.NET RID catalog](https://learn.microsoft.com/dotnet/core/rid-catalog)
- Microsoft: [Choose a Windows app distribution path](https://learn.microsoft.com/windows/apps/package-and-deploy/choose-distribution-path)
- Microsoft: [Sign an MSIX package](https://learn.microsoft.com/windows/msix/package/signing-package-overview)
- Microsoft: [Package a desktop app manually](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-manual-conversion)
- Avalonia: [macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos)
- Avalonia: [Supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)
- Apple: [Notarizing macOS software](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)
- Apple: [Customizing the notarization workflow](https://developer.apple.com/documentation/security/customizing-the-notarization-workflow)
- Apple: [Resolving common notarization issues](https://developer.apple.com/documentation/security/resolving-common-notarization-issues)
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
| 4-step／設定・通常／例外layout・往復 | MainWindow／SettingsViewと既存Input・Design draft／Execution snapshot（AC-016／017／037、TR-17／36）。MainWindowSettings／WorkflowState／ResponsiveLayout／CompactWorkflowLayout testsで局所検証済み（VERIFIED_SCOPED）。最小契約・測定条件は`dev/docs/ui-layout-contract.md`。T39追加nativeは部分観測でFAIL、Narrator・本人walkthroughはNOT_RUN_EXTERNAL_PREREQUISITE |
| 設定明示保存・出力先復元／保存定義明示適用 | ApplicationSettings／SettingsFileStore、SettingsViewModel、Inputのread-only明示適用、Executionの希望値と実効値（AC-035／036、TR-34／35）。一時pathのstore／VM試験とSettingsWorkflowSystemTestsの実file E2Eで局所検証済み（VERIFIED_SCOPED）。新instance復元を別process再起動とせず、既存checkpointは別責務 |
| Windows delivery | App限定single-file profile／EXE package（AC-030、TR-30）+ 既存public ZIP publish/package tests + development MSIX mechanism tests |
| 標準抽出・起動引数・data保護 | .NET標準host + 既存App起動契約 + 実EXE package tests（AC-031／032、TR-31） |
| Login開始・取消・終了 | bundled login専用service + Execution UI（AC-033、TR-32）。本人認証はCH-06 |
| macOS source foundation | macOS publish/package/sign/notary static contract tests |
| OS-only実測 | fresh Windows 11 x64標準userでのexact EXE CH-01〜06（AC-029／034、TR-33） |
| Platform release matrix | 既存protected CI/release workflow + candidate-bound v2 matrix／metadata evidence（AC-028／034、TR-29／33） |
| User/developer documentation | docs + dev/docs + screenshot tests |

## 22. Approval record

| 項目 | 内容 |
|---|---|
| Approver | 本repositoryの要求所有者 |
| Initial requirement source | 2026-09-01の本セッションで提示されたアプリケーション要件 |
| Default-decision approval source | 同日の後続指示「不明点はデフォルトのプランを採用してください。全てのタスクを実行してください」 |
| Release-scope update source | 2026-09-02の正式公開README実装指示とADR-0013 |
| Sample consolidation source | 2026-09-03の`sample/SampleReport.xlsx`を唯一のsampleとして扱う要求所有者指示 |
| Setup simplification source | 2026-09-03の「WindowsとMac OSでのセットアップをシンプルに」「不明点はデフォルトのプラン」「全てのタスクを実行」指示 |
| Question text synchronization source | 2026-09-05の「主回答列を選択したら、その列のExcelのシートの値を設問 textに表示」要求所有者指示 |
| One-action startup approval source | 2026-09-06の[1操作起動プラン](../work/20260906-0617-one-action-startup-plan.md)のデフォルト案・全タスク実行承認と[実行上書き](../work/20260906-one-action-startup-execution.md)。D-06のlogin buttonは採用済み。元プランの未承認表記ではなく、この後続承認・最新指示を優先する |
| UI/settings approval source | 2026-09-07の要求所有者による[UI・設定保存プランv2](../work/20260907-ui-settings-redesign-plan-v2.md)のD01〜D20デフォルト採用・全実装の明示承認。D18に従い要求版をv4.6／本日へ更新し、元プランのG0未通過・承認待ち表記より後続承認を優先する。D20は明示出力先を再起動・入力変更後も復元し、nullの場合だけ入力隣接result |
| Delivery decision | [ADR-0015](../dev/docs/adr/0015-windows-macos-installer-delivery.md)の履歴を保持し、[ADR-0016](../dev/docs/adr/0016-windows-one-action-startup.md)で単一EXE主配布・ZIP代替・標準全内容展開・login導線・matrix v2を追加 |
| Approved scope | 本書§1〜§21の要求。UI・設定保存／明示適用／出力先復元の差分と、既存業務・入力・採点・数式・checkpoint・privacy、Windows unsigned EXE／ZIPと各sidecar、既存non-public development MSIX回帰、exact EXE clean-host公開gateを含む。D19は非公開MSIXの同梱文書と検証リスト同期だけでinstall／署名／公開要件を拡張しない。macOS source/staticは変更せずproduction deliveryはscope外 |
| Delivery scope revision | 2026-09-04の要求所有者指示「開発用のMSIXでOKです」「外部ブロッカーの情報はないです」 |
| Previous version / changelog override（履歴） | D-14／R03の予定MINOR更新を取り消す最新指示を優先し、実装・文書task完了後のR03でKeep a Changelog形式のUnreleasedへ概要・実装済み変更を追加して、製品版をPATCH `0.8.3` → `0.8.4`とした。R03後の最終artifact再検証を要求し、版変更だけを公開成功へ読み替えない。要求文書v4.5を製品版や公開版と混同しない |
| Latest version / changelog override | UIプランD17の当初上書きは全タスク後のUnreleased追記 → PATCH `0.8.4` → `0.8.5`だった。さらに要求所有者の利用者不在時の自律続行指示により、T39をBLOCKEDのまま承認済みF01／F02を進める。F01はREVIEWED、親担当が製品版正本を0.8.5へ一度だけ更新済み。T01では製品版・CHANGELOGを変更しないという履歴は維持する。要求v4.6・製品版・公開版は独立し、最終bytes変更後の再検証・clean-host公開条件を省略しない |
| Implementation / validation status | 要求承認済み。T01は要求・契約・追跡・要求版metadataの同期で、当時のUI／設定は未実装・試験NOT_RUNだった。T01〜T35はREVIEWEDだった履歴を維持し、現在は2026-09-07の[実行記録](../work/20260907-ui-settings-execution-record.md)と後続引継ぎでT01〜T38がREVIEWED、T39はBLOCKED。実装と局所試験の対応はVERIFIED_SCOPED。T35の対象文書試験は4/4成功・敵対的レビュー済みで、別scopeのT36は自身のt36-current.trxで21/21成功。製品`0.8.5`は未公開候補、公開済みは`v0.8.1` ZIPのまま。0.8.4のT37実ZIP・T38実EXE・T39自動回帰／MSIX成功と追加native FAILを分離する。F02最終再検証は本同期時点では親担当で未完了、以後は実行記録の最新F02欄へ接続する。本人確認／隔離利用者保存／CH-01〜06はNOT_RUN_EXTERNAL_PREREQUISITEで、G4・全タスク完了・新EXE公開は未達 |
| Meaning | repository要求baselineの承認記録。実装完了・試験成功・release存在・tag／push／draft／公開操作の承認、組織の法務・教育・security承認または電子署名を意味しない |
