# Promptファイルから起動する

StudyReport Evaluatorは、入力workbookと複数の評価Promptを起動時に事前入力できます。この機能はGUIの準備だけを行い、loginやAI処理を自動開始しません。

これは任意の高度な起動方法です。**通常のGUI起動に、利用者のterminal操作やcommand入力は不要です。**

## 対象版と確認範囲

- **未公開候補`0.8.4`（UNRELEASED・単一EXE）**: 配布名は`StudyReportEvaluator-win-x64.exe`です。以下の候補版の例は取得済みのEXEを使うもので、公開済みとは扱わず、未公開のdownload URLも案内しません。
- **現在の公開版`v0.8.1`（ZIP）**: `StudyReportEvaluator-win-x64.zip`を展開した`StudyReportEvaluator.App.exe`を使います。この起動名と引数は従来どおり維持します。

> **既存P06の確認範囲:** 開発host上の実GUIで日本語pathの入力workbookと複数Promptの指定順維持を確認済みで、P06全7件PASS・review未解決指摘0です。追加ソフト未導入のfresh OS（clean-host）での試験と利用者本人のloginは`NOT_RUN`であり、その成功や候補EXEの公開完了を示しません。

## 2種類のPrompt

1. **起動依頼Prompt** — GitHub Copilot等のcommand実行可能agentへ、ローカルアプリの起動を依頼する文章
2. **評価Prompt file** — StudyReport Evaluatorが通常Custom evaluatorまたは固有評価へcopyするUTF-8 `.txt`

両者を混同しないでください。

## 起動option

未公開`0.8.4`候補の単一EXE:

```text
StudyReportEvaluator-win-x64.exe --input "<xlsx-path>" --prompt "<txt-path>" [--prompt "<txt-path>" ...]
```

公開`v0.8.1`のZIPを展開したEXE（従来の起動方法）:

```text
StudyReportEvaluator.App.exe --input "<xlsx-path>" --prompt "<txt-path>" [--prompt "<txt-path>" ...]
```

| Option | 回数 | 動作 |
|---|---:|---|
| `--input` | 0または1 | Input画面へpathを設定し、safe read-only読込を開始 |
| `--prompt` | 0回以上 | 指定順でImported Promptsへ読込 |

- option名は大文字・小文字を区別しません。
- relative pathは起動時のworking directory（cwd）を基準にabsolute化します。基準は起動元のdirectoryのままで、EXE配置先や単一EXEの抽出cacheへ変更しません。
- absolute化後の同じPrompt pathの重複は最初の1件だけを使い、残りの指定順を維持します。Windowsではpathの大文字・小文字を区別しません。
- unknown option、重複`--input`、値なし、不正path、`--prompt`に指定した`.txt`以外のfileは起動errorです。
- `--run`や`--resume`はありません。

## 評価Prompt file

- extension: `.txt`
- encoding: strict UTF-8、BOMあり／なしを受理
- length: 1〜32,767 UTF-16 code units
- empty、invalid UTF-8、読込失敗は拒否

Prompt本文はDesign画面で確認し、対象を選んで**Promptを適用**した時だけtemplate欄へcopyします。filenameによる自動割当は行いません。

起動引数やPrompt適用からloginやAI処理を自動開始しません。AI評価の開始にはExecution画面の**定量化を開始**を利用者自身が選ぶ必要があります。候補版の**GitHubにログイン**も別の明示操作です。

## PowerShellから起動する例

既にPowerShell 7がある場合の任意の例です。通常のGUI起動のためにPowerShellを導入したり、terminalへcommandを入力したりする必要はありません。

未公開`0.8.4`候補の単一EXE:

```powershell
& "C:\配布 アプリ\StudyReportEvaluator-win-x64.exe" --input "C:\授業 データ\回答.xlsx" --prompt "C:\授業 データ\評価 Prompt\内容 評価.txt" --prompt "C:\授業 データ\評価 Prompt\学生 Prompt.txt"
```

公開`v0.8.1`のZIPを展開したEXE:

```powershell
& "C:\配布 アプリ\StudyReportEvaluator-win-x64\StudyReportEvaluator.App.exe" --input "C:\授業 データ\回答.xlsx" --prompt "C:\授業 データ\評価 Prompt\内容 評価.txt" --prompt "C:\授業 データ\評価 Prompt\学生 Prompt.txt"
```

日本語や空白を含むpathは1つのargumentとして引用します。relative pathを使う場合も起動元のcwdを維持し、EXEのdirectoryや抽出cacheへ移動しません。起動しただけではCopilot session、login、AI送信を開始しません。

## GitHub Copilotへ貼る起動依頼例

`<...>`を実在pathへ置き換え、引用符を残します。アプリには取得済みの未公開`0.8.4`候補の単一EXE、または公開`v0.8.1`のZIPを展開したEXEのどちらか1つを指定します。

```text
ローカルのStudyReport Evaluatorを、次の入力と評価Promptを事前入力して起動してください。

アプリ:
"<StudyReportEvaluator-win-x64.exe または ZIP内 StudyReportEvaluator.App.exe のabsolute path>"

入力workbook:
"<標準 .xlsx のabsolute path>"

評価Prompt file（順序を維持）:
1. "<UTF-8 .txt のabsolute path>"
2. "<UTF-8 .txt のabsolute path>"

条件:
- fileの存在とextensionだけを確認し、workbook本文を開いたりchatへ表示したりしないでください。
- 入力workbookと既存Prompt fileを変更しないでください。
- argumentは--inputを1回、--promptを上記順に渡し、各pathを引用して1つのargumentとして扱ってください。
- 起動元のworking directory（cwd）を維持し、EXEのdirectoryや抽出cacheへ移動しないでください。
- --inputと--prompt以外のoptionを追加しないでください。
- GUIを起動したら停止し、login、画面clickやAI評価を自動実行しないでください。
- fileが見つからない場合は別pathを推測せず、不足しているpathだけを知らせてください。
```

## 通常Custom evaluator用Prompt例

`content-quality.txt`としてUTF-8保存できます。

```text
次の回答を、指定された評価項目ごとに分析してください。
同じ行の回答と補助情報だけを根拠にし、未記載の事実を補わないでください。

### 設問
{設問}

### 回答
{回答}

### 補助情報
{補助情報}

### 評価項目
{評価項目}
```

## 固有評価用Prompt例

`student-prompt-quality.txt`としてUTF-8保存できます。

```text
学生が作成した次のPromptについて、目的、入力、制約、期待出力、検証方法が第三者にも明確かを評価してください。
表現の華やかさだけで判断せず、同じ行の情報だけを使ってください。

### 関連する設問
{設問}

### 学生Prompt
{回答}

### 考慮事項
{補助情報}
```

通常Customでは`{回答}`と`{評価項目}`が必須です。固有評価では`{回答}`が必須で、`{評価項目}`は任意です。詳細は[Custom evaluator](custom-evaluator-guide.md)を参照してください。

## 起動後の確認

### Input

- 目的の`.xlsx`がread-onlyで読み込まれた
- sheet、質問文の行1/2、回答行が正しい
- 主回答／補助／固有評価sourceが意図どおり

### Design

- Base、Special、Question pointsの合計が100
- Imported Promptを正しい対象へ明示適用した
- Custom／固有評価の必須placeholderがある
- Similarity penalty weightを確認した
- 技術検証が有効

### Execution

- **Copilot 状態を確認**でlogin/modelを確認した
- concurrency 1〜3を選んだ
- 新規runなら出力directory、再開ならpartial pathを確認した
- 利用者自身が**定量化を開始**を選んだ

### Results

- finalまたはpartialの実在pathを確認した
- emptyの0と技術的失敗のblankを区別した
- Similarityだけで不正行為と判定しない

## checkpoint再開

command lineにresume optionはありません。通常どおり`--input`と必要な`--prompt`で起動し、Execution画面で**既存checkpointから再開**をonにして`.partial.xlsx`を指定します。

checkpointのinput、definition、model、app/SDK/CLI runtime identityが一致しない場合は再開を拒否します。partialを編集、rename、copyして一致を回避しないでください。

## privacy

起動依頼Promptへ学生の回答、氏名、email、credentialを貼らないでください。agentへworkbook本文を開かないよう明示してください。final/partialは入力全体と評価情報を含むため、入力と同等以上に機密です。

詳しくは[データとprivacy](privacy-and-data-handling.md)を参照してください。
