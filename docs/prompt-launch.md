# Promptファイルから起動する

StudyReport Evaluatorは、入力workbookと複数の評価Promptを起動時に事前入力できます。この機能はGUIの準備だけを行い、AI処理を自動開始しません。

## 2種類のPrompt

1. **起動依頼Prompt** — GitHub Copilot等のcommand実行可能agentへ、ローカルアプリの起動を依頼する文章
2. **評価Prompt file** — StudyReport Evaluatorが通常Custom evaluatorまたは固有評価へcopyするUTF-8 `.txt`

両者を混同しないでください。

## 起動option

```text
StudyReportEvaluator.App.exe --input <xlsx-path> --prompt <txt-path> [--prompt <txt-path> ...]
```

| Option | 回数 | 動作 |
|---|---:|---|
| `--input` | 0または1 | Input画面へpathを設定し、safe read-only読込を開始 |
| `--prompt` | 0回以上 | 指定順でImported Promptsへ読込 |

- option名は大文字・小文字を区別しません。
- relative pathは起動時のworking directoryを基準にabsolute化します。
- 同じPrompt pathの重複は最初の1件だけを使います。
- unknown option、重複`--input`、値なし、`.txt`以外は起動errorです。
- `--run`や`--resume`はありません。

## 評価Prompt file

- extension: `.txt`
- encoding: strict UTF-8、BOMあり／なしを受理
- length: 1〜32,767 UTF-16 code units
- empty、invalid UTF-8、読込失敗は拒否

Prompt本文はDesign画面で確認し、対象を選んで**Promptを適用**した時だけtemplate欄へcopyします。filenameによる自動割当は行いません。

## PowerShellから起動する例

```powershell
& ".\StudyReportEvaluator.App.exe" --input "C:\Data\responses.xlsx" --prompt "C:\Data\prompts\content.txt" --prompt "C:\Data\prompts\student-prompt.txt"
```

日本語や空白を含むpathは1つのargumentとして引用します。起動しただけではCopilot sessionやAI送信を開始しません。

## GitHub Copilotへ貼る起動依頼例

`<...>`を実在pathへ置き換えます。

```text
ローカルのStudyReport Evaluatorを、次の入力と評価Promptを事前入力して起動してください。

アプリ:
<展開済み StudyReportEvaluator.App.exe のabsolute path>

入力workbook:
<標準 .xlsx のabsolute path>

評価Prompt file（順序を維持）:
1. <UTF-8 .txt のabsolute path>
2. <UTF-8 .txt のabsolute path>

条件:
- fileの存在とextensionだけを確認し、workbook本文を開いたりchatへ表示したりしないでください。
- 入力workbookと既存Prompt fileを変更しないでください。
- argumentは--inputを1回、--promptを上記順に渡してください。
- --inputと--prompt以外のoptionを追加しないでください。
- GUIを起動したら停止し、画面clickやAI評価を自動実行しないでください。
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
