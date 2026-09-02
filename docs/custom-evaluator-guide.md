# Custom evaluator

このガイドでは、利用者が編集できる通常Custom evaluatorと固有評価のPromptを説明します。

## Knowledge、Custom、固有評価

| 種別 | 用途 | AI出力 |
|---|---|---|
| Knowledge (`KNOWLEDGE_COVERAGE`) | 知識の説明、関係、具体的適用 | criterion別raw、reason、evidence、source |
| Custom (`CUSTOM_PROMPT`) | 論理性、具体性、調査、文章等の独自観点 | criterion別raw、reason、evidence、source |
| 固有評価 | 学生Prompt等、通常回答とは別sourceの評価 | 0〜1、reason、evidence、source |

Knowledgeのsemantic instructionとstructured result schemaはapp-ownedです。利用者はKnowledgeのcriterion、description、range、weightを編集できますが、app-owned instruction自体は編集しません。

Customと固有評価では利用者がPrompt templateを編集します。AIへ配点、Question earned、Final score、合否を要求しないでください。

## source列

通常Custom evaluatorはQuestionの主回答列と補助列を使います。固有評価は項目ごとに別の主source列と補助列を持てます。

- workbook由来で送るのはcurrent rowの選択済みsourceだけです。
- 他row、非選択列、workbook pathは送信しません。
- 同じ項目内で主列と補助列を重複させることはできません。
- 空の主値はAIへ送らず0相当です。
- 非空の値に対する技術的AI失敗はblankです。

## placeholder

許可されるplaceholderは6個だけです。

| Placeholder | 展開値 |
|---|---|
| `{設問}` | Question text |
| `{回答}` | current rowの主値 |
| `{補助情報}` | current rowの選択済み補助値とsource ID |
| `{評価項目}` | criterion ID、表示名、description、effective range |
| `{最小点}` | evaluator default minimum |
| `{最大点}` | evaluator default maximum |

### 通常Custom evaluator

`{回答}`と`{評価項目}`が必須です。他の4個は任意です。

```text
次の回答を、指定された評価項目ごとに分析してください。
回答にない内容を推測しないでください。

### 設問
{設問}

### 回答
{回答}

### 補助情報
{補助情報}

### 評価項目
{評価項目}
```

### 固有評価

`{回答}`が必須です。固有評価は単一の0〜1値を返すため、`{評価項目}`は任意です。

```text
学生が作成した次のPromptを、第三者が再現できる指示かという観点で評価してください。
目的、入力、制約、期待出力、検証方法を確認し、書かれていない内容を補わないでください。

### 関連する設問
{設問}

### 学生Prompt
{回答}

### 同じ行の補助情報
{補助情報}
```

## braceとsingle-pass展開

literalの`{`と`}`は`{{`と`}}`でescapeします。

```text
literal object: {{"source":"selected-row-only"}}
```

展開後は`{"source":"selected-row-only"}`になります。挿入した回答や補助情報の中にplaceholderに似た文字列があっても再展開しません。

## range、criterion、weight

- evaluator minimumはmaximumより小さくします。
- criterionは個別rangeを省略でき、その場合は親evaluator rangeを使います。
- criterion/evaluator weightはenabledなら0より大きくします。
- weightを100へ揃える必要はありません。親内の比率として正規化します。
- Question間はweightではなくQuestion pointsの絶対配点です。
- `Base + Special + enabled Question points`を正確に100へ合わせます。

## Prompt fileを適用する

`--prompt`で読み込んだ`.txt`はDesign画面のImported Promptsへ表示されます。

1. Imported Promptを選択します。
2. 適用先として通常Custom evaluatorまたは固有評価を選びます。
3. **Promptを適用**を選びます。
4. template欄へcopyされた内容と技術検証を確認します。

選択しただけでは適用されません。同じPromptを複数の対象へ再利用できます。詳しくは[Promptファイルから起動](prompt-launch.md)を参照してください。

## 主なvalidation

| 状況 | 修正 |
|---|---|
| templateが空 | 必須placeholderを含むPromptを入力 |
| `{回答}`がない | 主値を入れる位置へ追加 |
| 通常Customに`{評価項目}`がない | criterion一覧を入れる位置へ追加 |
| 未知placeholder | 許可された6個だけを使う |
| braceが閉じていない／余分 | 対応するbraceを追加するか`{{` / `}}`でescape |
| nested brace | nested構造を除去 |
| source列が不正 | 読込済みsheetの列から選び直す |
| 配点合計が100でない | Base、Special、Question pointsを修正 |
| Special pointsが正で固有評価がない | enabled固有評価を追加するかSpecial pointsを0へ戻す |

同じPrompt validationはDesign、Execution開始条件、snapshot作成で再実行されます。invalid Promptでは新しいAI処理を開始しません。

## AI出力の扱い

expected ID、closed schema、range、evidence sourceを満たす結果だけを採用します。duplicate、unknown、partial、range外の結果を部分採用・clamp・0点化しません。

> [!WARNING]
> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません
