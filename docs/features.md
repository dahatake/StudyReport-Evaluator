# 機能と点数

このガイドでは、StudyReport Evaluatorの評価単位、配点、status、出力workbookを説明します。

## 評価定義

1つの定義は、Base points、Special points、Similarity penalty weight、丸め桁数と、1件以上のQuestionを持ちます。

各Questionは次を持てます。

- 1つの主回答列
- 0件以上の補助列
- 1件以上のKnowledgeまたはCustom evaluator
- evaluator内の1件以上のcriterion
- 0件以上の固有評価
- Question points

設問、evaluator、criterion、固有評価の件数は固定ではありません。enabled項目だけを現在runへ含め、run開始時にimmutable snapshotへ固定します。

## 4つのAI処理

| 処理 | 入力 | Model | 出力 |
|---|---|---|---|
| Reference | 設問text | `auto` | 設問ごとの参照回答 |
| Normal | 同じrowの主回答・選択済み補助列 | 利用者選択 | criterion別raw、reason、evidence、source |
| Special | 同じrowの固有評価主値・選択済み補助列 | 利用者選択 | 0〜1、reason、evidence、source |
| Similarity | 同じrowの主回答と同じ設問の参照回答 | `auto` | 0〜1の類似度とreason |

Referenceは1Questionにつき1runで1回生成し、同じrunの全rowで共有します。checkpointから再開すると保存済みReferenceを再利用します。

AIへBase points、Question earned、Special earned、Similarity penalty、Final score、合否を返させません。これらはapp previewとExcel formulaが計算します。

## 配点

有効Questionを$q=1..N$とします。

$$
BasePoints + SpecialPoints + \sum_{q=1}^{N}QuestionPoints_q = 100
$$

| 設定 | 既定 | 範囲・条件 |
|---|---:|---|
| Base points | 60 | 0〜100 |
| Special points | 0 | 0〜100。正ならenabled固有評価が必要 |
| Similarity penalty weight | 0.1 | 0〜1 |
| 丸め桁数 | 1 | 0〜6 |

初期Question pointsは残点をenabled Questionへ均等配分し、割り切れない差分を最後のQuestionへ付けて合計を正確に合わせます。その後の自動再配分は**設問配点を均等化**を選んだ場合だけです。

## 通常評価

criterion rawをrange内で0〜100へ正規化します。criterion個別rangeがない場合は親evaluator rangeを使います。

$$
Normalized_c=100\times\frac{EffectiveRaw_c-Min_c}{Max_c-Min_c}
$$

通常評価内のcriterion/evaluator weightは親内の比率として使います。Question間はweightではなく絶対Question pointsです。

$$
QuestionEarned_q=QuestionPoints_q\times QuestionRate_q
$$

criterion overrideが空ならvalidなAI rawを使い、range内overrideがある場合はoverrideを使います。不正な非空overrideをAI rawへ黙ってfallbackしません。

## 固有評価

固有評価は学生Prompt等、通常回答とは別のsource列を0〜1で定量化します。enabled固有評価をQuestion内で等分平均し、固有評価を持つenabled Question間でも等分平均します。

$$
SpecialEarned=SpecialPoints\times average(SpecialQuestionRate)
$$

`SpecialPoints=0`の場合は固有評価AIを実行せず、Special earnedを0とします。

## 類似度減点

$$
SimilarityPenalty_q=QuestionPoints_q\times Similarity_q\times SimilarityPenaltyWeight
$$

Similarityは0〜1です。高い値は不正行為の証明ではなく、低い値も回答品質を保証しません。授業目的に応じてpenalty weightを0を含む範囲で設定し、結果を確認してください。

## Final rawとFinal score

$$
FinalRaw=BasePoints+\sum_q QuestionEarned_q+SpecialEarned-\sum_q SimilarityPenalty_q
$$

$$
FinalScore=\min(100,\max(0,FinalRaw))
$$

Final rawは監査用に保持し、Final scoreを0〜100へ収めます。Configの配点合計が100でない場合や、必要なAI値が技術的失敗でblankの場合は、最終値もblankになります。

## 空回答と技術的失敗

| 状態 | AI call | 値 |
|---|---:|---|
| 空の通常主回答 | なし | Question rate / earned 0、Similarity 0 |
| 空の固有評価主値 | なし | 当該Special score 0 |
| Reference失敗 | 失敗statusを保存 | Reference、Similarity、最終値blank |
| Normal/Special/Similarityの技術的失敗 | retry後に失敗status | 対象値と必要な上位計算blank |

0は学生入力に基づく値、blankは技術的に値を確定できなかった状態です。混同しないでください。

## 主なstatus

- `SUCCESS`
- `EMPTY`
- `NOT_RUN_ZERO_BUDGET`
- `AI_OUTPUT_INVALID`
- `AI_TIMEOUT`
- `NETWORK_FAILED`
- `AUTH_REQUIRED`
- `CANCELLED`
- `CLEANUP_FAILED`
- `AI_RUNTIME_FAILED`

statusは原因を示し、失敗を0点へ変換しません。

## checkpointと再開

新規runではfinalと同じstemの`.partial.xlsx`を作ります。

- 各Reference完了後に保存
- 1 student rowのNormal/Special/Similarityがすべて終わった後に保存
- target-local tempを検証してatomic replace
- process終了後も最後のcomplete rowから再開
- final成功後にpartialを削除

partial削除だけが失敗した場合、validなfinalは無効になりません。結果画面にcleanup warningと残存pathを表示します。

## final workbook

| Sheet | 内容 |
|---|---|
| `Quantification_Config` | definition snapshot、配点、Prompt、mapping、range |
| `Quantification_References` | 設問、参照回答、model、status、時刻 |
| `Quantification_Results` | row別raw、reason、evidence、status、formula、Final score |
| `Quantification_Run` | input/definition/runtime identity、時刻、件数、観測usage |

入力に同名sheetがある場合、既存sheetを保持して新しいsheetに` (2)`等を付けます。formula cellにはapp previewのcached valueを保存し、formula対応spreadsheetで開いた際にfull calculationを要求する設定を入れます。

## 上限

- selected rows: 20,000以下
- concurrency: 1〜3
- retry込みattempt budget: 20,000以下
- app-owned request: 65,536 Unicode scalars以下かつmodel容量の安全margin内
- Excel column、cell、formula、function argumentの媒体上限をwrite前に検証

上限超過時は内容を黙って切り詰めず、AI送信前に技術エラーを表示します。

## 非保証

- AI scoreの正確性、教育的妥当性、公平性
- 法的適合性、組織policy適合性
- 類似度による不正行為判定
- 未実測の処理時間、token数、費用

最終的な評点と利用判断は利用者が行います。
