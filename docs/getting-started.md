# はじめに

このガイドでは、Windows 11 x64でStudyReport Evaluatorを起動し、標準`.xlsx`から最初の結果workbookを作る手順を説明します。

現在の公開版は`0.8.1`です。以下の手順は、[GitHub Releases](https://github.com/dahatake/StudyReport-Evaluator/releases)から実在するZIPとsidecarを取得した場合、または開発者から同じ二つのfileを受領した場合に使用します。development MSIXは検証専用で、一般利用者向けの入手・起動手順には含めません。

> [!WARNING]
> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません

> **対象版:** 入手・hash確認・起動は公開`0.8.1`向けです。以下の画像と、設問text自動同期・固定表示の採点計算式・幅に応じて折り返す設問カードの説明は、**UNRELEASED（未リリース）の`0.8.3`候補**向けです。`0.8.1`ではこの追加UI／動作を前提にせず、設問textと対象設問を利用中の画面で確認・設定してください。

## 準備

必要なもの:

- 同じrelease／配布元の`StudyReportEvaluator-win-x64.zip`と`StudyReportEvaluator-win-x64.zip.sha256`
- Windows 11 x64
- 評価対象の標準`.xlsx`
- AI処理を行う場合は、利用可能なGitHub Copilot accountと対話login

.NET Runtime、.NET SDK、Microsoft Excel、Office、LibreOffice、別途導入したCopilot CLIは必要ありません。CLIはZIP内に同梱されています。

配布ZIPはunsignedです。発行者を示すcode signingやSmartScreen reputationがあると誤認しないでください。入手元とSHA-256を確認してから使用します。

## ZIPを確認して起動する

1. ZIPと`.sha256`を同じdirectoryへ置きます。
2. ZIPのSHA-256を取得します（下記の方法から選択）。
3. `.sha256`の先頭64文字と一致することを確認します。
4. ZIPを新しいdirectoryへ展開します。
5. `StudyReportEvaluator-win-x64\StudyReportEvaluator.App.exe`を起動します。

PowerShell 7が既にある場合の任意のSHA-256確認例（PowerShell 7の導入は必須ではありません）:

```powershell
(Get-FileHash -Algorithm SHA256 .\StudyReportEvaluator-win-x64.zip).Hash
```

PowerShell 7を導入しない場合は、Windows 11標準の`certutil`の`-hashfile`機能でダウンロードしたZIPを指定し、アルゴリズムを`SHA256`として確認できます。表示された64桁のhashを`.sha256`の先頭64文字と照合してください。

正式なdownload URLは、実在するrelease assetが公開されている場合だけ案内できます。URLを推測したり、repository内の`artifacts`を一般配布先として扱ったりしないでください。

## 4stepの流れ

```mermaid
flowchart LR
    A[1. 入力] --> B[2. 定量化設計]
    B --> C[3. 実行]
    C --> D[4. 結果・出力]
```

## 1. 入力

![標準xlsxの選択、sheet、行範囲を設定する入力画面。synthetic dataを使用](../images/01-input-workbook.png)

1. **ファイルを選択**でnative pickerを開くか、**ファイル path（標準 .xlsx）**へfull pathを入力します。
2. **read-only で読込**を選びます。
3. **回答 sheet**を選びます。
4. **質問文の行**を1または2から選びます。
5. **回答開始行**と**回答終了行**を確認します。
6. 質問ごとの表示名、設問text、主回答列、補助列を確認します。
7. 技術検証の問題をすべて解消します。

**0.8.3候補の追加動作:** 主回答列を選択すると、選択中の回答sheetで**質問文の行**と同じ列が交差するセルの値を、同じ質問の**設問text**へ即座に表示します。設問textを手入力した後に主回答列を選び直すと、新しい列のセル値で置き換わります。交差するセルが空の場合は代替文を作らず設問textも空になるため、内容を確認して手入力してください。

候補mappingは出発点です。列位置だけで役割を確定せず、workbookの見出しと授業設計に合わせて変更してください。同じ質問内で主回答列と補助列を重複させることはできません。**質問文の行**を変更した場合は、以前の行の値を使わないよう、**見出し行を再読込**してから主回答列を確認してください。

## 2. 定量化設計

![採点計算式と複数の設問カードを表示する定量化設計画面。synthetic dataを使用](../images/03-design-knowledge.png)

設計画面で次を設定します。

- **Base points:** 既定60
- **Special points:** 既定0
- **Similarity penalty weight:** 既定0.1、範囲0〜1
- **Question points:** 設問ごとの絶対配点
- **Knowledge / Custom evaluator:** 通常回答の評価方法
- **criterion:** evaluator内の評価項目、range、weight
- **固有評価:** 学生Prompt等、別source列を0〜1で定量化する任意項目
- **丸め桁数:** 0〜6

**0.8.3候補の設計UI:** 画面上部の**採点計算式**の要約とBase・Special・類似度減点係数の現在値は、設問カードや評価詳細を縦にスクロールしても残ります。各設問の配点は設問カードで確認します。本文の**計算式の詳細**を展開すると、色と記号の対応、`Rq`（通常評価率）、`Lq`（類似度）、固有評価平均の説明を確認できます。これらの率は実行時に計算されます。丸め桁数は表示だけでなく計算の各段階と最終点に影響します。

**設問一覧（すべて表示）**には、入力画面で選んだ全設問がカードで表示されます。幅に余裕があれば横に並び、狭い場合は次の行へ折り返します。カードでは設問名、設問text、絶対配点、主回答列、補助列を比較できます。カードを選ぶと太枠になり、その設問のKnowledge／Custom評価、評価項目、固有評価、Promptを下の詳細欄で編集できます。

入力欄、選択欄、追加・複製・移動・削除などの操作へマウスを合わせると、設定する内容、値の範囲、計算への影響を説明するヘルプが表示されます。

起動時に読み込んだPromptを適用する場合（以下のカード操作・表示名は`0.8.3`候補向け。`0.8.1`は[Promptファイルから起動](prompt-launch.md)を参照）:

1. **IMPORTED PROMPTS**からPromptを選びます。
2. 設問カードを選び、**Prompt適用先の設問**が目的の設問になっていることを確認します。
3. 適用先の種類をCustom評価方法または固有評価から選びます。
4. 「設問名 → Custom: 評価方法名」または「設問名 → 固有評価: 固有評価名」と表示されたことを確認し、**Promptを適用**を選びます。

適用はPromptのコピーだけで、AI処理は開始しません。AI評価は「3. 実行」の**定量化を開始**を選ぶまで行われません。

![選択した設問のCustom Promptと評価項目を編集する画面。synthetic dataを使用](../images/04-design-custom-prompt.png)

配点は次を正確に満たす必要があります。

$$
BasePoints + SpecialPoints + \sum_{q=1}^{N} QuestionPoints_q = 100
$$

各$QuestionPoints_q$は1つの有効な設問へ割り当てる絶対配点で、$N$は有効な設問数です。

**設問配点を均等化**は利用者が選んだときだけ実行されます。設問追加やBase/Special変更だけで、手動入力したQuestion pointsを黙って変更しません。

Custom Promptの作り方は[Custom evaluator](custom-evaluator-guide.md)を参照してください。Promptファイルを起動時に読み込む場合は[Promptファイルから起動](prompt-launch.md)を参照してください。

## 3. 実行

![Copilot状態、model、並列度、新規runまたは再開を設定する実行画面。fake authenticationを使用](../images/05-execution-auto.png)

### 新規run

1. **Copilot 状態を確認**を選びます。
2. loginが利用可能と表示されたら、列挙された**Model**を選びます。
3. **Concurrency**を1〜3から選びます。既定は1です。
4. **既存checkpointから再開**をoffにします。
5. **出力directory**を確認します。初期値は入力fileに隣接する`result`です。
6. evaluation planと技術検証を確認します。
7. **定量化を開始**を選びます。

run開始時にfinalとpartialの未使用名を予約します。

- final: `eval-yyyyMMdd-HHmm[-NN].xlsx`
- partial: `eval-yyyyMMdd-HHmm[-NN].partial.xlsx`

処理順は参照回答生成、学生行評価、checkpoint保存、finalizationです。全処理と検証が成功するとfinalを自動作成し、Resultsへ移動します。

### checkpointから再開

1. **既存checkpointから再開**をonにします。
2. **再開するpartial checkpoint**へ`.partial.xlsx`のfull pathを入力します。
3. Copilot状態とmodelを確認します。
4. 技術検証を通過したら**定量化を開始**を選びます。

再開にはinput、definition、model、app/SDK/CLI runtime identityの一致が必要です。保存済み参照回答とcomplete student rowを再利用し、最初の未完了rowから続けます。partialを書換えて一致を回避しないでください。

### cancel

**cancel**後は新しいAI送信を開始せず、最後にatomic保存されたcomplete rowまでのpartialを保持します。処理中だったrowはcomplete扱いにしません。

## 4. 結果・出力

![finalまたはpartialと行別scoreを確認する結果画面。fake scoreを使用](../images/06-results-review.png)

結果画面では次を確認します。

- run statusとfinal/partial path
- 対象row、成功、空回答、技術失敗の件数
- Question earned、Special earned、Similarity penalty
- Final rawと0〜100へ収めたFinal score
- criterionのAI raw、effective raw、normalized value
- cleanup warning

空回答と技術的失敗は区別します。

- **空回答:** AIを呼ばず、その設問の通常評価率と獲得点を0として扱います。
- **技術的失敗:** AI処理で値を確定できなかった場合、対象値をblankとして記録します。

0とblankは異なります。blankが必要な計算へ伝播するとFinal rawとFinal scoreもblankになります。

criterion overrideが必要な場合はrange内の値を入力し、任意の新しい`.xlsx`へ反映版を出力できます。この操作は既存finalを変更せず、別名workbookを作ります。

## final workbook

finalは入力workbookの全sheetとdataを保持し、次の4sheetを追加します。

- `Quantification_Config`
- `Quantification_References`
- `Quantification_Results`
- `Quantification_Run`

入力に同名sheetがある場合は、既存sheetを変更せず` (2)`等の一意名を使います。partialは入力全体と`Quantification_Checkpoint`を含みます。

final/partialには入力全体、Prompt、参照回答、AI結果が含まれ得ます。入力と同等以上に機密なfileとして扱ってください。詳細は[データとprivacy](privacy-and-data-handling.md)を参照してください。

## 対応しない操作

- `.xlsx`以外や暗号化／macro-enabled workbookの読込
- 保存済みfinalのアプリへの再import
- definition profileだけの独立save/load
- macOS、Linux、Windows Arm64
- installer、code signing、notarization
- AIによる最終評点・合否・不正行為の確定

問題がある場合は[トラブルシューティング](troubleshooting.md)を参照してください。
