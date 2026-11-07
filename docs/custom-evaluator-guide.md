# 独自の評価指示を書く（Custom evaluator）

このガイドでは、先生の採点基準をAIへの指示文（Prompt）にする方法を説明します。まず評価項目を決め、それから指示文を書きます。AIに最終成績を決めさせるためのものではありません。

> **確認日：2026-11-07。** 以下の編集場所は現行ソース`0.8.8`です。公開`v0.8.6`では通常評価を設定画面で編集します。旧`v0.8.1`とは配置が異なります。6個の置換用の記号（placeholder）の規則は同じです。公開版の検証状況は[はじめに](getting-started.md#v086の検証範囲)を参照してください。

## 最初に書くなら

1. **2 採点設計**で設問を選び、評価方法をCustom（独自）にします。公開版は**通常評価 → 設定 → 通常評価**を開きます。
2. 評価項目に「主張と根拠の対応」など観点を書き、説明欄に「回答に書かれた根拠だけを評価する」のように具体的な基準を入れます。点数範囲と重みも確認します。
3. 下の[通常Customの例](#通常custom-evaluator)をPrompt欄へ貼り、必要な説明を足します。`{回答}`と`{評価項目}`は消さないでください。アプリが実行時の回答と評価項目に置き換えます。
4. プレビューで置換の形を確認します。これは仮の値であり、試し採点ではありません。
5. 架空の少数回答で実行し、理由・引用が採点基準に合うか確認します。次回も使う場合は**設定を保存**します。

「よい回答を高く評価する」だけでは判断が揺れます。回答中の何を確認するか、書かれていない内容を補わないことを明示してください。

## Knowledge、Custom、固有評価

| 種別 | 用途 | AI出力 |
|---|---|---|
| Knowledge (`KNOWLEDGE_COVERAGE`) | 知識の説明、関係、具体的適用 | criterion別raw、reason、evidence、source |
| Prompt (`PROMPT_ANALYSIS`、現行ソースのみ) | 学生のPromptをベストプラクティスの観点で評価（組込みPrompt、編集不可） | criterion別raw、reason、evidence、source |
| Custom (`CUSTOM_PROMPT`) | 論理性、具体性、調査、文章等の独自観点 | criterion別raw、reason、evidence、source |
| 固有評価 | 学生Prompt等、通常回答とは別sourceの評価 | 0〜1、reason、evidence、source |

Knowledge・Promptのsemantic instructionとstructured result schemaはapp-ownedです。利用者はcriterion、description、range、weightを編集できますが、app-owned instruction自体は編集しません。評価項目のdescriptionの既定値は種類ごとに異なります。

Customでは利用者がPrompt templateを編集します。固有評価の例は既存の採点定義を保守する人向けで、現行画面からの編集手順ではありません。AIへ配点、Question earned、Final score、合否を要求しないでください。

## 編集する場所

### 通常評価の設定

現行ソースでは、採点設計画面内の通常評価の編集領域で、**設問 → 評価方法 → 評価項目**を選びます。公開`v0.8.6`では採点設計の**通常評価**から**設定 → 通常評価**を開きます。

- **基本**: Knowledge／Prompt／Customの追加・複製・並替え・削除、有効状態、名前、評価方法の点数範囲・重み、評価項目の名前・説明・個別範囲・重みを編集します。全件ではなく、選択した対象の詳細を表示します。Promptは現行ソースのみです。
- **Prompt**: Knowledgeはapp-owned instructionとstructured output instructionの全文を読取専用で確認します。Customはtemplate全文を編集し、読取専用previewで展開を確認します。
- Customのpreviewは安全な仮値を使います。実際の学生回答やAIによる試し採点ではありません。Knowledgeへの種類変更ではCustom本文を消去するため、保存したい本文がある場合は変更前に確認してください。

主画面には対象設問の評価方法・評価項目の概要と絶対配点を残しています。Question points、Base、Special、類似度減点係数は**採点設計**、定義名・revision・丸め桁数は**設定 → 共通**で変更します。通常モデル・並列度・指定出力先も共通で編集し、実行画面では実効値を読取専用で確認します。

### 固有評価項目

固有評価項目は、既存の採点定義に含まれる値が読込・保存で保持され、採点に使われます。画面からは編集できません。評価範囲は**0〜1固定**です。Special pointsは採点設計の主画面で変更し、0なら固有評価AIを実行しません。類似度減点係数0はSimilarity自体を無効にしないため、この非実行条件とは別です。詳しくは[設定の保存と適用](settings.md)を参照してください。

## source列

通常Custom evaluatorはQuestionの主回答列と補助列を使います。固有評価は項目ごとに別の主source列と補助列を持てます。

通常の主回答列と設問文は**入力**、補助列は読み込んだ採点定義の値を保持します。固有評価のsourceも定義の値で、通常の主回答列とは別です。

- workbook由来で送るのはcurrent rowの選択済みsourceだけです。
- 他row、非選択列、workbook pathは送信しません。
- 同じ項目内で主列と補助列を重複させることはできません。
- 主値が空と確定した場合はAIへ送らず、完了済み行の当該評価を0相当として扱います。取消や未確認の主値を空回答と推測して0にしません。
- 非空の値に対する技術的AI失敗はblankです。

## placeholder

許可されるplaceholderは6個だけです。

| Placeholder | 展開値 |
|---|---|
| `{設問}` | Question text |
| `{回答}` | current rowの主値。通常はQuestionの主回答列、固有評価は固有項目の主対象列 |
| `{補助情報}` | current rowの当該評価に選択済みの補助値とsource ID。補助列なしは`(none)` |
| `{評価項目}` | 通常はenabled criterionのID、表示名、description、effective range。固有評価は空文字 |
| `{最小点}` | 通常はevaluator default minimum（criterion個別rangeの最小値ではない）。固有評価は`0` |
| `{最大点}` | 通常はevaluator default maximum（criterion個別rangeの最大値ではない）。固有評価は`1` |

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

- 現行ソースは採点設計内の通常評価の**基本**、公開版は**設定 → 通常評価 → 基本**で、点数範囲と重みを確認します。
- evaluator minimumはmaximumより小さくします。
- criterionは個別rangeを省略でき、その場合は親evaluator rangeを使います。
- criterion/evaluator weightはenabledなら0より大きくします。
- weightを100へ揃える必要はありません。親内の比率として正規化します。
- Question間はweightではなくQuestion pointsの絶対配点です。
- 採点設計で`Base + Special + enabled Question points`を正確に100へ合わせます。表示ページ外も含めて合計し、丸めて100に見せる判定はしません。

## Promptファイルの起動引数

現行ソースの`--prompt`は受理されますが、読み込んだ内容は画面に表示せず、どの評価にも適用しません。通常Custom evaluatorのPromptは**採点設計 → 通常評価の編集領域 → Prompt**で直接編集します。公開版との違いは[起動引数のガイド](prompt-launch.md)を参照してください。

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

同じPrompt validationは設定の編集時、採点設計の**再検証**、Execution開始条件、snapshot作成で再実行されます。採点設計のエラーを選んで**全文**を確認し、**問題箇所へ**で該当する入力欄や設定を開いて修正してください。invalid Promptでは新しいAI処理を開始しません。

## 編集した定義を保存・再利用する

候補版の**設定を保存**は、共通設定と任意の採点定義1件を`setting.txt`へ明示保存します。定義にはcriterion・range・weight・source・Promptを含みます。編集だけではdiskへ保存しません。

起動時の読込や**設定を再読込**では、保存定義を現在の入力へ自動適用しません。Excelを読み込み、**設定 → 共通 → 保存定義 → 現在の入力に適用**を選び、検証成功後に使います。実行中の一括適用はできません。詳細編集は次回用で、進行中runのsnapshotや前回の結果には混ぜません。

複数profileやfinal workbookからの定義importには対応しません。`--prompt`の内容は設定の保存対象外です。設問文や貼り付けたPrompt等は明示保存時に平文で含まれ得るため、[設定の保存と適用](settings.md)の保存範囲を確認してください。

## AI出力の扱い

expected ID、closed schema、range、evidence sourceを満たす結果だけを採用します。duplicate、unknown、partial、range外の結果を部分採用・clamp・0点化しません。

> [!WARNING]
> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません
