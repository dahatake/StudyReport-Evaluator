# GitHub CopilotからPromptで起動する

対象読者は、コマンド操作を覚えずにStudyReport Evaluatorを起動したい教員・採点者と、授業用の評価Promptを準備する担当者です。本書の例は、そのままテキストファイルへ保存してGitHub Copilotへ貼り付けられます。

> [!IMPORTANT]
> 本書には2種類の「Prompt」が登場します。
>
> 1. **Copilot起動依頼Prompt** — GitHub Copilotへ貼り、ローカルアプリを`--input`／`--prompt`付きで起動してもらう指示です。
> 2. **評価Promptファイル** — StudyReport Evaluatorが各回答を評価するときに使うUTF-8の`.txt`です。Design画面でCustom evaluatorまたは固有評価へ明示的に適用します。
>
> 両者を混同しないでください。

## できることと安全上の境界

GitHub Copilotのコマンド実行可能なagentへ起動依頼Promptを貼ると、次をまとめて依頼できます。

1. アプリ、入力workbook、評価Promptファイルの存在を確認する。
2. 必要なら、指定した評価Prompt本文をUTF-8 `.txt`として保存する。
3. `StudyReportEvaluator.App`を`--input`と複数の`--prompt`付きで起動する。
4. アプリのInput画面へworkbook pathを事前入力し、read-only読込を開始する。
5. Design画面のImported Promptsへ、指定順のbasenameと本文を表示する。

現在の安全契約では、command lineから**AI評価を自動開始しません**。起動後、教員が次を画面で確認します。

- 対象sheet、質問文の行、回答行、primary／supporting／special source列
- Base points、Special points、各Question points、Similarity penalty weight
- Imported Promptの適用先
- Copilot login、model、concurrency、final／partialの出力先

教員がExecution画面の**定量化を開始**を押した後は、参照回答、各学生行、checkpoint、final workbookの順に処理され、成功時は既定で次のファイルが自動作成されます。

`<入力workbookのdirectory>/result/eval-{yyyyMMdd-HHmm}[-NN].xlsx`

この確認を残す理由は、GitHub Copilotへ貼った一文だけで、別のsheetや列を誤って全学生分処理することを防ぐためです。GitHub Copilotや本アプリが採点責任を引き受けるものではありません。

## 前提

- コマンド実行を許可できるGitHub Copilot agentを使用します。コマンドを実行できないchatでは、起動手順だけが回答される場合があります。
- StudyReport Evaluatorの実行ファイルがローカルにあります。
- AI評価を行う場合は、同梱された互換Copilot CLIを利用でき、GitHubへの対話loginが完了している必要があります。
- 入力は標準`.xlsx`です。`.xls`、`.xlsm`、CSV、PDF、暗号化workbookは対象外です。
- 評価PromptファイルはUTF-8 plain textの`.txt`、1〜32,767文字です。

## まず使う起動依頼Prompt

次の全体を`launch-study-report-evaluator.txt`などへ保存し、`<...>`だけ自分の環境に合わせて変更してからGitHub Copilotへ貼り付けます。

```text
ローカルのStudyReport Evaluatorを、次の入力と評価Promptを事前入力して起動してください。

アプリ実行ファイル:
<StudyReportEvaluator.App.exe の絶対path>

入力workbook:
<評価する標準 .xlsx の絶対path>

評価Promptファイル（この順序を維持）:
1. <1つ目の UTF-8 .txt の絶対path>
2. <2つ目の UTF-8 .txt の絶対path>

実行条件:
- 各pathをabsolute pathとして検証してください。
- 入力workbookの回答本文、氏名、メールアドレス、他のcell内容を読み取ったり、terminalやchatへ表示したりしないでください。
- 入力workbookとPromptファイルを変更しないでください。
- 実行ファイルが見つからない場合は、推測した別アプリを起動せず、安全に停止して不足pathだけを知らせてください。
- 引数は --input を1回、--prompt を上記順に反復して渡してください。
- 日本語や空白を含むpathを1つのargumentとして扱ってください。
- GUI processを開始したら、process IDと「Input画面でread-only読込が開始される」ことだけを知らせてください。
- デスクトップUIを自動操作せず、AI評価を自動開始せず、結果fileが存在するまで作成済みと報告しないでください。

起動後に私が画面で行う確認も、短いchecklistで示してください。
```

### リポジトリ内sampleを使う場合

リポジトリを開いているGitHub Copilot agentには、絶対pathの代わりに次の探索規則を含むPromptを使えます。

```text
現在のworkspaceからStudyReport Evaluatorのsample runを準備して起動してください。

1. `StudyReportEvaluator.slnx`があるworkspace rootを基準にしてください。
2. 入力は `sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx`です。
3. アプリは、次の順で最初に見つかった`StudyReportEvaluator.App.exe`を使ってください。
   - 私がこのchatで明示したabsolute path
   - `artifacts/package/publish/win-x64/StudyReportEvaluator.App.exe`
   - 展開済み`StudyReportEvaluator-win-x64/StudyReportEvaluator.App.exe`
4. 見つからない場合はbuildやdownloadを勝手に始めず、不足していることだけを報告してください。
5. workbook本文は開かず、fileの存在と`.xlsx`拡張子だけを確認してください。
6. このPromptの後で指定するUTF-8 `.txt`を、記載順の`--prompt`として渡してください。
7. `--input`と`--prompt`以外のoptionを追加しないでください。
8. GUIを起動した時点で停止し、AI評価や画面clickを自動実行しないでください。
```

## sample workbookの確認済み構造

本書では、回答本文を転記せず、read-onlyで確認済みのheader由来の構造だけを利用します。
repository sampleのexact-byte identityと検査境界は[sample workbook structural profile](../docs-dev/preflight/sample-workbook-profile.md)に記録しています。このhashはrepository sample専用であり、利用者自身のworkbookへ一致を要求するものではありません。

| 項目 | sampleでの初期候補 |
|---|---|
| Workbook | `sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx` |
| Source sheet | `Original` |
| 質問文の行 | `1` |
| 回答行 | `2`〜`531` |
| A〜E | 管理metadata候補。初期評価対象外 |
| F | 設問1のレポート回答primary候補 |
| G | 設問1に関する学生Prompt候補 |
| H | 別の回答候補。primaryまたはsupportingとして要確認 |
| I | 設問2のレポート回答primary候補 |
| J | 設問2に関する学生Prompt候補 |
| K | Prompt作成時の考慮事項／観点。Jのsupporting候補 |
| L | PBL feedback。初期評価対象外 |

列の意味は自動確定ではなく**候補**です。画面に表示された実際のheaderと授業設計を教員が確認してください。本書の設問文は具体例として新規作成したもので、sample内の学生回答を引用したものではありません。

## ユースケース1 — 機械学習概念の理解を確認する

### 想定シナリオ

機械学習サブフィールドPBLのレポートで、学生が用語を列挙しただけでなく、概念を正しく説明し、概念間の関係を示し、PBL課題へ適用できているかを確認します。

### 推奨mapping例

- Question 1 primary: `F`
- Question 2 primary: `I`
- `H`は実際のheaderを確認し、必要な設問だけsupportingへ追加
- G／J／Kはこのユースケースでは評価対象外でも構いません

### 配点例

| 項目 | 点 |
|---|---:|
| Base points | 60 |
| Question 1 | 20 |
| Question 2 | 20 |
| Special points | 0 |
| Similarity penalty weight | 0.1 |
| 合計 | 100 |

通常のKnowledge evaluatorだけでも実行できます。独自の授業観点を追加する場合は、次を`ml-concept-understanding.txt`としてUTF-8保存し、Custom evaluatorへ適用します。

```text
機械学習サブフィールドPBLのレポート回答を、指定された評価項目ごとに分析してください。
単なる用語の出現ではなく、次を重視してください。
- 概念を学生自身の言葉で正確に説明しているか
- 複数の概念や処理段階の関係を説明しているか
- PBLの課題、データ、評価方法へ具体的に適用しているか
- 回答にない事実を推測して補わず、同じ行の回答と補助情報だけを使っているか

### 設問
{設問}

### 学生の回答
{回答}

### 同じ行の補助情報
{補助情報}

### 評価項目
{評価項目}
```

### criterion例

- `概念の正確性` — 用語の定義と説明に重大な誤りがない
- `概念間の関係` — データ、学習、推論、評価等の関係を説明している
- `PBLへの適用` — 対象課題での使い方を具体化している
- `限界への言及` — データ量、偏り、過学習、評価指標等の制約を認識している

## ユースケース2 — PBL提案の具体性と実行可能性を確認する

### 想定シナリオ

学生が機械学習を使うと言うだけでなく、解決したい課題、必要なデータ、評価方法、失敗条件を具体的に計画できているかを確認します。

次を`ml-pbl-feasibility.txt`として保存し、FまたはIをprimaryとするCustom evaluatorへ適用します。

```text
次のPBLレポート回答について、機械学習を使う提案の具体性と実行可能性を評価してください。
回答に書かれた範囲だけを根拠にし、未記載のデータ、実験、成果を推測しないでください。

特に次の観点を、指定された評価項目へ対応付けて確認してください。
- 解決する課題と利用者が明確か
- 機械学習を使う必要性が説明されているか
- 収集するデータ、特徴、正解または評価方法が具体的か
- 精度以外の制約、失敗時の対応、倫理・privacy上の注意があるか
- 小さく検証できる最初の実験が示されているか

### 設問
{設問}

### 回答
{回答}

### 補助情報
{補助情報}

### 評価項目
{評価項目}
```

### criterion例

- `課題設定`
- `データ計画`
- `評価計画`
- `実行可能性`
- `リスクと限界`

## ユースケース3 — 学生が作成したPromptの品質を固有評価する

### 想定シナリオ

通常のレポート内容とは別に、学生が生成AIへ与えたPromptの明確さ、制約、期待出力、検証可能性を0〜1の固有評価として扱います。

### 推奨mapping例

- 設問1の固有評価primary: `G`
- 設問2の固有評価primary: `J`
- Jのsupporting: `K`
- Gに対応するsupportingはheaderと授業設計を見て選択

### 配点例

| 項目 | 点 |
|---|---:|
| Base points | 60 |
| Question 1 | 15 |
| Question 2 | 15 |
| Special points | 10 |
| Similarity penalty weight | 0.1 |
| 合計 | 100 |

次を`student-prompt-quality.txt`として保存し、各Questionの固有評価へ再利用します。固有評価Promptでは`{回答}`が必須で、`{評価項目}`は使用しません。

```text
学生が作成した次のPromptを、授業で再現可能な指示になっているかという観点で評価してください。
表現の華やかさではなく、第三者が同じ目的で使える具体性を重視してください。

確認する観点:
- 目的と対象読者が明確か
- 入力として何を使うかが明確か
- 守るべき制約や、してはいけないことが明確か
- 欲しい出力形式や粒度が明確か
- 結果を確認・検証する方法が含まれるか
- 個人情報や未確認情報を不必要に要求していないか

### 関連する設問
{設問}

### 学生が作成したPrompt
{回答}

### 同じ行に記載された考慮事項・観点
{補助情報}
```

### 画面での適用

1. Design画面でQuestion 1を選び、**固有評価を追加**します。
2. primaryを`G`にします。
3. Imported Promptsから`student-prompt-quality.txt`を選びます。
4. targetを**選択中の固有評価**にし、**Promptを適用**します。
5. Question 2でも固有評価を追加し、primaryを`J`、supportingを`K`にします。
6. 同じPromptをもう一度選び、Question 2の固有評価へ適用します。
7. Special pointsを正の値にし、Base + Special + enabled Question pointsが正確に100であることを確認します。

## ユースケース4 — Prompt作成時の考慮事項を含めて評価する

### 想定シナリオ

学生Promptそのものだけでなく、「なぜその制約や観点を入れたか」という考慮事項を評価へ含めます。sampleではJをprimary、Kをsupporting候補として使えます。

次を`prompt-rationale.txt`として保存し、J／Kをmappingした固有評価へ適用します。

```text
学生が作成したPromptと、同じ行に記載した考慮事項を合わせて評価してください。
Prompt本文だけが整っていても、考慮事項と矛盾する場合は高く評価しないでください。
一方、文章表現だけで判断せず、目的、制約、期待出力、検証方法の整合性を確認してください。

### 設問
{設問}

### 学生Prompt
{回答}

### 作成時の考慮事項
{補助情報}
```

このPromptは「不正利用を判定する」ものではありません。Prompt品質の学習支援用の数値候補を作るだけです。

## ユースケース5 — まず10行だけpilot実行する

530行すべてを最初から処理せず、設計と出力を確認するために2〜11行だけでpilot runを行います。

GitHub Copilotへ貼る起動依頼例:

```text
StudyReport Evaluatorのpilot runを準備して起動してください。

入力workbookはworkspace rootの
`sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx`
です。
評価Promptは、私が指定するUTF-8 `.txt`を記載順の`--prompt`で渡してください。

禁止事項:
- workbook本文をterminalやchatへ表示しない
- 入力workbookを変更しない
- AI評価を自動開始しない
- 結果fileが実在する前に成功と報告しない

起動後、私がInput画面で次を確認するためのchecklistを表示してください。
- sheet: Original
- 質問文の行: 1
- pilot回答行: 2〜11
- F/I: 通常回答候補
- G/J: 学生Prompt候補
- K: Jのsupporting候補
- A〜E/L: 初期対象外

その後のmodel選択、Prompt適用、配点、実行開始は私が画面で行います。
```

pilotのfinal workbookで、sheet構成、formula、blank、reason／evidence、配点を確認してから、同じ設計を全行へ適用します。pilotと全行runは別のsnapshot／checkpoint／final fileです。

## ユースケース6 — 中断したrunを再開する

`--input`／`--prompt`には`--resume` optionはありません。再開はExecution画面で明示します。

GitHub Copilotへ貼る起動依頼例:

```text
StudyReport Evaluatorを、前回と同じ入力workbookおよび同じ評価Promptファイルで起動してください。
--inputは1回、--promptは前回と同じ順序で渡してください。
`.partial.xlsx`を直接編集、rename、copy、削除しないでください。
GUIを起動したら停止してください。

起動後、私がExecution画面で「既存checkpointから再開」を選び、前回の
`result/eval-....partial.xlsx`
を指定します。checkpointのinput identity、definition、model、CLI/SDK runtimeが一致しない場合は、回避や書換えをせずエラーを報告してください。
```

保存済み参照回答と完了済み学生行は再利用され、最初の未完了行から続行します。途中までしか終わっていない学生行は完了扱いにせず、その行を再実行します。

## 評価PromptファイルをCopilotに作成してもらう

次の起動依頼Promptは、2つの評価Promptファイルを作成してからsample workbookと一緒に起動します。保存先は自分で変更できます。

```text
StudyReport Evaluator用の評価Promptを2件作成し、sample workbookと一緒にアプリを起動してください。

workspace root:
`StudyReportEvaluator.slnx`があるdirectory

入力workbook:
`sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx`

保存先directory:
`<自分のDocuments directory>/StudyReportEvaluator/prompts`

作成するfile 1: `ml-concept-understanding.txt`
本文開始:
機械学習サブフィールドPBLのレポート回答を、指定された評価項目ごとに分析してください。
単なる用語の出現ではなく、概念の正確な説明、概念間の関係、PBLへの具体的な適用を重視してください。
回答にない事実を推測しないでください。

### 設問
{設問}

### 回答
{回答}

### 補助情報
{補助情報}

### 評価項目
{評価項目}
本文終了

作成するfile 2: `student-prompt-quality.txt`
本文開始:
学生が作成した次のPromptを、目的、入力、制約、期待出力、検証可能性が第三者にも明確かという観点で評価してください。
表現の華やかさだけで評価せず、同じ行の情報だけを使ってください。

### 関連する設問
{設問}

### 学生Prompt
{回答}

### 考慮事項
{補助情報}
本文終了

実行条件:
- fileはUTF-8 plain text、BOMありまたはなしで保存してください。
- 既存fileを黙って上書きしないでください。内容が同じなら再利用し、異なるなら`-02` suffixを提案してください。
- workbook本文は読まないでください。
- appへは --input を1回、--promptをfile 1、file 2の順で渡してください。
- unknown option、--run、--resume、独自optionを追加しないでください。
- GUIを起動したら停止し、AI評価や画面操作を自動実行しないでください。
- 作成したPrompt fileのpathと、起動したprocessだけを報告してください。Prompt本文、回答本文、credentialをlogへ出さないでください。
```

## 起動後の画面checklist

### Input

- native pickerまたは`--input`から目的の`.xlsx`がread-onlyで読み込まれた
- sampleなら`Original`、質問文の行1、回答行2〜531が候補になった
- 通常回答と学生Promptを別のQuestion／special sourceとして意図どおりmappingした
- A〜E等の管理列を不用意にsupportingへ追加していない

### Design

- Base、Special、各Questionの配点合計が100
- Special pointsが正ならenabled固有評価が1件以上ある
- Custom evaluator用Promptには`{回答}`と`{評価項目}`がある
- 固有評価用Promptには`{回答}`がある
- Imported Promptを選んだだけでは適用されず、対象を選んで**Promptを適用**した
- Similarity penalty weightを授業目的に合わせて確認した
- snapshot preflightが有効

### Execution

- Copilot loginとruntime identityを確認した
- 利用可能なmodelを選択した
- concurrencyは1〜3
- 新規runまたはcheckpoint再開を明示した
- final／partial directoryを確認した
- **定量化を開始**を教員自身が押した

### Results

- finalまたはpartial pathが表示された
- QuestionEarned、SpecialEarned、SimilarityPenalty、FinalRaw、FinalScoreを確認した
- 技術的失敗のblankを0点と読み違えない
- 高い類似度だけで不正行為と判定しない
- cleanup warningがあっても、final fileが有効かを別に確認した

## GitHub Copilotへ結果fileの存在だけ確認してもらう

アプリで処理が完了した後に使います。学生回答やscore本文をCopilot chatへ出さず、file identityだけを確認します。

```text
StudyReport Evaluatorの実行後確認をしてください。

入力workbookのdirectory直下にある`result` directoryを対象に、
`eval-*.xlsx`のうち`.partial.xlsx`ではないfinal候補を列挙してください。

実施してよいこと:
- fileの存在、absolute path、byte size、last-write time、SHA-256の確認

禁止事項:
- workbookを開いてcell、sheet本文、氏名、回答、score、reason、evidenceを読むこと
- fileを変更、rename、copy、削除すること
- `.partial.xlsx`をfinalと報告すること
- 存在しないfileを作成済みと報告すること

候補が1件ならidentityだけを報告してください。0件または複数件なら、推測せずその件数を報告してください。
```

## よくある失敗

| 状況 | 確認 |
|---|---|
| Copilotがコマンドを実行しない | コマンド実行可能なagentを選択し、terminal実行の確認を許可する |
| app executableが見つからない | 展開／installした実行ファイルのabsolute pathを起動依頼Promptへ記載する |
| `LAUNCH_UNKNOWN_OPTION` | `--input`と`--prompt`以外を削除する |
| `LAUNCH_DUPLICATE_INPUT` | `--input`を1回だけにする |
| `PROMPT_EXTENSION_INVALID` | `.txt`として保存する |
| `PROMPT_UTF8_INVALID` | UTF-8 plain textとして保存し直す |
| `PROMPT_LENGTH_INVALID` | 1〜32,767文字にする |
| Imported Promptを適用できない | Custom evaluatorまたは固有評価を選択し、正しいtargetを選ぶ |
| Designがinvalid | placeholder、配点合計100、source列、enabled childを確認する |
| finalが作られない | Resultsに表示されたpartial pathとsafe status codeを確認する |
| Copilotが「完了」と言ったがfileがない | Copilotの文章ではなく、実在する`eval-*.xlsx`を確認する |

詳細なerror切り分けは[トラブルシューティング](troubleshooting.md)を参照してください。

## privacyと採点責任

- GitHub Copilotへ貼る起動依頼Promptには、学生の回答本文、氏名、メールアドレスを含めないでください。
- 起動補助をするCopilot agentには、workbook本文を開かないよう明示してください。
- StudyReport EvaluatorがAIへ送るworkbook由来の値は、処理中の同じ学生行にある選択済みsourceだけです。
- output／partial workbookは入力全体と評価結果を保持するため、入力と同等以上に機密です。
- 類似度は不正行為の証明ではありません。
- AIの数値は評価候補です。最終的な評点と利用判断は教員が行います。

> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません

## 関連文書

- [はじめに](getting-started.md)
- [Custom evaluatorガイド](custom-evaluator-guide.md)
- [機能リファレンス](features.md)
- [データとprivacy](privacy-and-data-handling.md)
- [トラブルシューティング](troubleshooting.md)
- [sample workbook structural profile](../docs-dev/preflight/sample-workbook-profile.md)
