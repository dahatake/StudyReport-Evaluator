# StudyReport Evaluator

標準`.xlsx`のレポート回答をGitHub Copilotで定量化し、入力を変更せず別の`.xlsx`へ結果を作成するWindowsデスクトップアプリです。

> [!WARNING]
> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません

AIが作る値は確認対象です。最終的な評点と利用判断は、授業目的や所属組織の方針に基づいて利用者が行ってください。Similarityは不正行為の証明ではなく、低いSimilarityも回答品質を保証しません。

## できること

- native pickerまたはfull pathから標準`.xlsx`をread-onlyで読み込む
- 質問、主回答列、補助列、Knowledge／Custom evaluator、固有評価を設定する
- Base、Question、Specialの絶対配点とSimilarity penalty weightを設定する
- QuestionごとのReference、通常評価、固有評価、Similarityを実行する
- Reference完了ごと・student row完了ごとに`.partial.xlsx`へcheckpointする
- finalを自動作成し、必要ならcriterion override反映版を別名で出力する

## 対応環境

| 項目 | 対応内容 |
|---|---|
| OS / architecture | Windows 11 x64 |
| 配布 | .NET 10 self-contained、unsigned ZIP、SHA-256 sidecar |
| 入力 | 標準Office Open XML `.xlsx` 1file |
| AI runtime | ZIPへ同梱したGitHub Copilot CLI。PATH上の別CLIへfallbackしません |
| AI login | 利用者本人のGitHub Copilot対話login |
| Spreadsheet runtime | Microsoft Excel、Office、LibreOfficeはアプリ実行に不要 |

`.xls`、`.xlsb`、CSV、PDF、`.xlsm`等のmacro-enabled file、password／rights-protected／暗号化workbook、安全境界に違反するpackageは対象外です。

## 公開状況・SHA-256確認・起動

現在の公開版は`0.8.1`です。[GitHub Releases](https://github.com/dahatake/StudyReport-Evaluator/releases)の同一releaseから`StudyReportEvaluator-win-x64.zip`と`StudyReportEvaluator-win-x64.zip.sha256`の2 fileを取得してください。

1. ZIPとSHA-256 sidecarを同じdirectoryへdownloadします。
2. ZIPのSHA-256を確認します。
3. sidecarの先頭64文字と完全一致することを確認します。
4. ZIPを新しいdirectoryへ展開します。
5. `StudyReportEvaluator-win-x64\StudyReportEvaluator.App.exe`を起動します。

PowerShell 7が既にある場合のSHA-256確認例（任意）です。PowerShell 7の導入は必須ではなく、Windows 11標準の`certutil`を使う方法は[はじめに](docs/getting-started.md)を参照してください。

```powershell
(Get-FileHash -Algorithm SHA256 .\StudyReportEvaluator-win-x64.zip).Hash
```

配布ZIPはunsignedです。code signing済み、installer形式、SmartScreen reputation確立済みとは表示していません。入手元とhashを確認できない場合は実行しないでください。

## 5分クイックスタート

> **画面・追加操作の対象版:** 以下の画像と、利用者ガイドの設問text自動同期・固定表示の採点計算式・幅に応じて折り返す設問カードは、**UNRELEASED（未リリース）の`0.8.3`候補**を対象にしています。公開`0.8.1`でこれらの追加UI／動作を前提にしないでください。手順の対象版は[はじめに](docs/getting-started.md)を参照してください。

### 1. 入力

![標準xlsxをnative pickerまたはpathからread-onlyで読み込む入力画面の上部。synthetic dataを使用](images/01-input-workbook.png)

1. **ファイルを選択**でnative pickerを開くか、**ファイル path（標準 .xlsx）**へfull pathを入力します。
2. **read-only で読込**を選びます。
3. 回答sheet、質問文の行1/2、回答開始・終了行を確認します。
4. 質問ごとの主回答列と補助列を確認・変更します。

mapping候補は確定値ではありません。workbookの見出しと授業設計に合わせてください。

### 2. 定量化設計

- Base pointsは既定60、Special pointsは既定0です。
- Question pointsは設問ごとの絶対配点です。
- Similarity penalty weightは0〜1で、既定0.1です。
- 通常評価はKnowledgeまたはCustom evaluatorを使います。
- 学生Prompt等を別sourceで評価する場合は固有評価を追加します。
- `Base + Special + enabled Question points = 100`になるよう設定します。

Custom Promptには許可されたplaceholderだけを使います。通常Custom evaluatorでは`{回答}`と`{評価項目}`、固有評価では`{回答}`が必須です。

### 3. 実行

1. **Copilot 状態を確認**を選びます。
2. 表示されたmodelから通常評価用modelを選びます。ReferenceとSimilarityは利用者が選択せず、固定で`auto`を使います。
3. Concurrencyを1〜3から選びます。
4. 新規runでは出力directory、再開では`.partial.xlsx`のpathを確認します。
5. 技術検証を通過したら**定量化を開始**を選びます。

起動引数やPrompt適用だけでAI処理を自動開始しません。

### 4. 結果・出力

![自動final path、row別配点・減点・Final scoreを確認する結果画面。fake scoreを使用](images/06-results-review.png)

finalまたはpartial path、status、Question earned、Special earned、Similarity penalty、Final raw、Final scoreを確認します。criterionのAI raw、effective raw、normalized valueも確認できます。

criterion overrideを使う場合、range内の値を入力して未使用の別名`.xlsx`へ任意出力します。既存finalを上書きしません。

## 点数の読み方

有効Questionを$q=1..N$とします。

$$
BasePoints + SpecialPoints + \sum_{q=1}^{N}QuestionPoints_q = 100
$$

$$
QuestionEarned_q=QuestionPoints_q\times QuestionRate_q
$$

$$
SimilarityPenalty_q=QuestionPoints_q\times Similarity_q\times SimilarityPenaltyWeight
$$

$$
FinalRaw=BasePoints+\sum_q QuestionEarned_q+SpecialEarned-\sum_q SimilarityPenalty_q
$$

Final scoreはFinal rawを0〜100へ収めた表示用の値です。

- 空の主回答: AI callなし、Question earnedとSimilarityは0相当
- 非空回答の技術的AI失敗: 対象値はblank
- 必要な値がblank: Final raw / Final scoreもblank

0とblankは意味が異なります。詳しくは[機能と点数](docs/features.md)を参照してください。

## 出力と再開

新規runの初期出力先は入力fileに隣接する`result` directoryです。

- final: `eval-yyyyMMdd-HHmm[-NN].xlsx`
- partial: `eval-yyyyMMdd-HHmm[-NN].partial.xlsx`

既存final/partialがある場合は、共通の次suffixを使って上書きを避けます。cancelまたはprocess終了後は、Execution画面で**既存checkpointから再開**を選び、保存済みcomplete rowの次から再開できます。

finalは入力workbookの全sheetを保持し、次を追加します。

- `Quantification_Config`
- `Quantification_References`
- `Quantification_Results`
- `Quantification_Run`

同名sheetが入力にある場合は既存sheetを変更せず、` (2)`等の一意名を使います。partialは入力全体と`Quantification_Checkpoint`を含みます。

## データとprivacy

workbook由来でAIへ送る値は、処理に必要なcurrent rowの選択済みprimary／supporting／special sourceだけです。処理に応じてQuestion text、Prompt、criterion metadata、Reference、closed schema metadataも送ります。他row、非選択列、workbook pathは通常payloadへ含めません。

application logは回答、Prompt、Reference、reason、evidence、credentialを受け取るfree-text surfaceを持ちません。

final/partialには入力全体、Prompt、Reference、AI結果が含まれ得ます。入力と同等以上に機密なfileとして扱ってください。詳しくは[データとprivacy](docs/privacy-and-data-handling.md)を参照してください。

## Promptファイルから起動する

```text
StudyReportEvaluator.App.exe --input <xlsx-path> --prompt <txt-path> [--prompt <txt-path> ...]
```

- `--input`は0または1回
- `--prompt`は0回以上、指定順を維持
- Prompt fileはstrict UTF-8の`.txt`、1〜32,767文字
- Imported Promptを選び、適用先を選んで**Promptを適用**した時だけtemplateへcopy
- AI処理はExecution画面の明示操作まで開始しない

詳しい例は[Promptファイルから起動](docs/prompt-launch.md)を参照してください。

## 制限と非保証

- macOS、Linux、Windows Arm64は初版対応対象外です。
- installer、code signing、notarizationを提供しません。
- 保存済みfinal workbookのアプリへの再importと、definition profileだけの独立save/loadは提供しません。
- AI品質、教育的妥当性、公平性、法的適合性、組織policy適合性、不正行為を保証・判定しません。
- 未実測の処理時間、token数、費用を保証しません。

## ガイド

- [はじめに](docs/getting-started.md)
- [機能と点数](docs/features.md)
- [Custom evaluator](docs/custom-evaluator-guide.md)
- [Promptファイルから起動](docs/prompt-launch.md)
- [データとprivacy](docs/privacy-and-data-handling.md)
- [トラブルシューティング](docs/troubleshooting.md)

## ライセンス

[MIT License](LICENSE)
