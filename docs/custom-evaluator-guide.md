# Custom evaluator ガイド

対象読者は、Knowledge以外の観点を独自Promptで定量化する教員・評価設計者です。開発者向けの実装根拠は本書末尾にまとめます。

## KnowledgeとCustomの使い分け

| 種別 | 適した用途 | Prompt ownership |
|---|---|---|
| Knowledge (`KNOWLEDGE_COVERAGE`) | 知識ポイントが説明され、他概念と関係付けられ、具体的に適用されている程度 | semantic instructionはapp-owned。利用者は知識ポイント、description、range、weightを編集 |
| Custom (`CUSTOM_PROMPT`) | 論理性、具体性、調査の深さ、文章品質、Prompt設計等 | 利用者が分析templateを編集。structured-output contractはapp-owned |

どちらもAIへaggregate、weight、合否を要求しません。criterion別raw score、reason、evidence、sourceだけを受け取り、aggregateはExcel formulaが計算します。実装根拠: [`BuiltInPromptTemplates.cs`](../src/StudyReportEvaluator.Core/Prompting/BuiltInPromptTemplates.cs)、[`SafeEvaluationPayloadBuilder.cs`](../src/StudyReportEvaluator.Core/Prompting/SafeEvaluationPayloadBuilder.cs)。

## primary / supporting columns

1つのQuestionは、利用者が選んだ任意の1列をprimary column、0件以上の列をsupporting columnsとして持ちます。学生Prompt列もprimaryにできます。

- primaryが空白なら`EMPTY`となり、AIへdispatchせず、overrideとscore chainをblankにします。
- supportingが空白でもprimaryがあれば評価を続けます。
- 同じQuestion内ではprimary/supportingおよびsupporting同士の重複を拒否します。
- workbook由来でPromptへ入る値は、対象と同じ行の選択済みprimary/supportingだけです。他行、非選択列、workbook pathは含めません。

この他に、question text、criterion metadata、Custom template、app-owned output contractもPromptへ含まれます。詳細は[データとprivacy](privacy-and-data-handling.md)を参照してください。

## placeholder契約

![Custom Prompt template、range、weightを編集する画面。合成データのみ](../images/04-design-custom-prompt.png)

許可するplaceholderは次の6個だけです。

| Placeholder | 展開値 |
|---|---|
| `{設問}` | Question text |
| `{回答}` | 同じ行のprimary value |
| `{補助情報}` | 同じ行のsupporting valuesとstable source column IDs |
| `{評価項目}` | enabled criteriaのID、表示名、description、effective range |
| `{最小点}` | evaluator default rangeのminimum |
| `{最大点}` | evaluator default rangeのmaximum |

Custom templateは空白にできず、`{回答}` と `{評価項目}` をそれぞれ1回以上含める必要があります。他の4個は任意です。

literalの`{`と`}`は`{{` と `}}` でescapeします。例えば`{{"形式":"例"}}`は`{"形式":"例"}`というliteral textになります。placeholder展開はsingle-passです。回答・補助情報・評価項目への挿入値は **opaque** として扱い、再走査しません。

実装根拠: [`PromptTemplateRenderer.cs`](../src/StudyReportEvaluator.Core/Prompting/PromptTemplateRenderer.cs#L7-L93)。

## rangeとweight

- evaluatorはdefault rangeを持ちます。
- criterionは個別rangeを省略でき、その場合は親evaluatorのrangeを使います。
- `{最小点}` / `{最大点}`は常にevaluator default rangeです。
- `{評価項目}`にはcriterion effective rangeが入ります。
- minimumはmaximumより小さく、weightは0より大きい値にします。
- weightを100へ揃える必要はありません。各階層で`個別weight / weight合計`へ正規化し、UIには実効percentageを表示します。

## 安全なtemplate例

### 最小template

```text
次の回答を、指定された評価項目ごとに分析してください。

### 回答
{回答}

### 評価項目
{評価項目}
```

### supporting informationとliteral braceを使うtemplate

```text
設問と同じ行の回答・補助情報だけを使い、根拠のない推測を避けてください。
説明用のliteral object例: {{"source":"selected-row-only"}}

### 設問
{設問}

### 回答
{回答}

### 補助情報
{補助情報}

### 評価項目
{評価項目}

親evaluatorの既定範囲: {最小点} から {最大点}
```

JSON schemaやtool call手順をtemplateへ追加する必要はありません。appがrender後にclosed structured-output contract、expected evaluator / criterion IDs、許可source IDsを付加します。

## validation errors

| Code | 原因 | 修正 |
|---|---|---|
| `TEMPLATE_REQUIRED` | templateが空 | `{回答}`と`{評価項目}`を含む本文を入力 |
| `ANSWER_PLACEHOLDER_REQUIRED` | `{回答}`がない | 評価対象を挿入する位置へ追加 |
| `CRITERIA_PLACEHOLDER_REQUIRED` | `{評価項目}`がない | criterion一覧を挿入する位置へ追加 |
| `UNKNOWN_PLACEHOLDER` | 許可リスト外または空のplaceholder | 6個のいずれかへ修正 |
| `UNCLOSED_PLACEHOLDER` | opening braceが閉じていない | `}`を補うかliteralとしてescape |
| `MALFORMED_PLACEHOLDER` | nested opening brace | nested braceを除去 |
| `UNMATCHED_CLOSING_BRACE` | 単独の`}` | literalなら`}}`へ修正 |

Design画面の**snapshot preflightが有効になるまでrunを開始しないでください**。現行実装ではDesign validationをExecution開始条件で再検査しない既知差分があります。[`implementation-status.md` IMPL-GAP-001](../docs-dev/implementation-status.md#impl-gap-001--custom-promptをrun開始前に再検査しない)

## AI出力と人手確認

AIへaggregateを要求しないでください。required resultは、expected criterionごとのraw score、短いreason、同じ行の連続substring evidence、evidence source、stable source column IDです。partial、duplicate、unknown criterion、range外、出所不一致はpayload全体を不正として扱い、部分採用・clamp・0点化をしません。

> AIによる定量値には誤りや偏りが含まれる可能性があります。利用目的に応じて結果を確認してください。

warningはnonblockingです。結果確認とrange内overrideは利用できますが、mandatory human review、承認checkbox、倫理gate、合否gateはありません。

## 実装・テスト出典

- Prompt template: [`BuiltInPromptTemplates.cs`](../src/StudyReportEvaluator.Core/Prompting/BuiltInPromptTemplates.cs)
- placeholder renderer: [`PromptTemplateRenderer.cs`](../src/StudyReportEvaluator.Core/Prompting/PromptTemplateRenderer.cs)
- selected-row payload: [`SafeEvaluationPayloadBuilder.cs`](../src/StudyReportEvaluator.Core/Prompting/SafeEvaluationPayloadBuilder.cs)
- result validation: [`QuantificationResultValidator.cs`](../src/StudyReportEvaluator.Core/Validation/QuantificationResultValidator.cs)
- deterministic tests: [`PromptTemplateRendererTests.cs`](../tests/StudyReportEvaluator.Core.Tests/Prompting/PromptTemplateRendererTests.cs)、[`SafeEvaluationPayloadBuilderTests.cs`](../tests/StudyReportEvaluator.Core.Tests/Prompting/SafeEvaluationPayloadBuilderTests.cs)
