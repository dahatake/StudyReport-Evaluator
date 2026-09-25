# 機能と点数

このガイドでは、StudyReport Evaluatorの評価単位、配点、status、出力workbookを説明します。

> **対象版:** このガイドの新しい画面配置、主回答列から設問textへの即時同期、設定の保存・明示適用は、**UNRELEASED（未リリース）の`0.8.6`候補**を対象とします。現在の公開版`v0.8.1`（ZIP）の機能・画面配置とは区別してください。候補版の公開完了を示すものではありません。

## 画面で確認・変更する場所

主画面は **1 入力 → 2 採点設計 → 3 実行 → 4 結果** の4ステップです。詳細編集は同じウィンドウの**設定**で行います。設定は第5ステップではありません。

| 主画面 | 主に確認・操作するもの | 詳細の変更先 |
|---|---|---|
| 入力（Input） | `.xlsx`のread-only読込、回答sheet、質問文の行1/2、回答範囲、対象設問、有効状態、主回答列、選択した設問文 | **設定 → 入力詳細**で補助列、列候補、設問名、追加・複製・並替え・削除 |
| 採点設計（Design） | Base／Special／類似度減点係数、選択設問の絶対配点・有効状態、全有効設問を含む配点合計・過不足、評価方法・評価項目の概要 | **設定 → 通常評価**でKnowledge／Customとcriterionの詳細、**固有評価**でsource・Prompt、**読込Prompt**で取込本文と適用先 |
| 実行（Execution） | 認証の明示確認・ログイン、新規／checkpoint再開、中断からの再開準備、開始・停止、進捗、今回ジョブのコスト詳細・ジョブログ。モデル・並列度・実効出力先は読取専用の有効値表示 | **設定 → 共通**で通常モデル、並列度、指定出力先を編集。診断も共通で確認 |
| 結果（Results） | 行一覧、選択行の詳細、1 criterionずつの任意override、エラー移動、別名workbook出力、対応ジョブのコスト詳細・ジョブログ | 結果とoverride、別名出力pathの編集は**結果画面のまま**。設定へは移しません |

入力の**入力詳細…**、採点設計の**設問の詳細／通常評価／固有評価／読込Prompt (件数)**、実行の**変更**から、該当する設定カテゴリを1操作で開けます。設問を対象にする入口では、表示名やページ上の位置ではなく同じ設問ID（`questionId`）を保ちます。**設定から戻る**で編集内容を保持して元のステップへ戻ります。

多数の設問・結果はページ切替や対象選択で確認し、詳細は選択した対象だけを表示します。全項目の編集カードを主画面へ縦に並べる構成ではありません。長い設問文・Promptは全文欄で確認できます。詳しい操作は[はじめに](getting-started.md)と[設定の保存と適用](settings.md)を参照してください。

## 評価定義

1つの定義は、Base points、Special points、Similarity penalty weight、丸め桁数と、1件以上のQuestionを持ちます。

各Questionは次を持てます。

- 設問text
- 1つの主回答列
- 0件以上の補助列
- 1件以上のKnowledgeまたはCustom evaluator
- evaluator内の1件以上のcriterion
- 0件以上の固有評価
- Question points

設問、evaluator、criterion、固有評価の件数は固定ではありません。enabled項目だけを現在runへ含め、run開始時にimmutable snapshotへ固定します。

入力画面で主回答列を選択すると、選択中の回答sheetにある質問文行と同じ列の交差セル値を、設問textへそのまま即座に反映します。主回答列を選び直した場合は新しい交差セル値で置き換え、空または存在しないheader cellには代替文を生成しません。反映後の設問textは手動で変更できます。

質問文行を変更した場合は、以前の行の値を使わないよう、見出し行を再読込してから主回答列を確認します。同一Questionの主回答列と補助列に同じ列を重複指定することはできません。

## 設定の保存・再読込・明示適用

候補版では、共通設定と**任意の採点定義1件**を、利用者別の`setting.txt`（UTF-8 JSON）へ保存できます。複数profileの管理や、完成済みfinal workbookから定義をimportする機能ではありません。

1. **設定を保存**で共通設定と現在の採点定義を明示保存します。編集・画面移動・Prompt適用だけでは自動保存しません。初回はこの操作までfileを作りません。不正な設定や保存失敗では、既存fileと現在の編集を保持し、保存済みとは表示しません。
2. 起動時の読込、または**設定を再読込**で保存済み共通設定を復元します。採点定義は保存定義として保持するだけで、現在のInput／Designへ自動適用しません。
3. Excel読込後、**設定 → 共通 → 保存定義**でsheet・設問数・行範囲を確認し、**現在の入力に適用**を選びます。sheet・行・列等の検証成功時だけ、ID・順序・設問文・配点・Promptを保って反映します。失敗・取消時は現在の編集を変更しません。実行中の一括適用はできません。

入力未読込で共通設定を保存する場合も、読み込んだ保存定義を消しません。保存定義の適用は、起動時のImported Prompts一覧・順序・原文を変更せず、未適用Promptを自動適用しません。

保存対象には見出しから取り込んだ設問文と適用済みPrompt、利用者が貼り付けた内容が平文で含まれ得ます。入力xlsxのpath・bytes、回答本文の自動収集、AI結果・参照回答、final／partialの状態、認証情報、未適用Promptのfile一覧は保存対象外です。保存場所・制約は[設定の保存と適用](settings.md)を確認してください。

## 4つのAI処理

| 処理 | 入力 | Model | 出力 |
|---|---|---|---|
| Reference | 設問text | `auto` | 設問ごとの参照回答 |
| Normal | 同じrowの主回答・選択済み補助列 | 利用者選択 | criterion別raw、reason、evidence、source |
| Special | 同じrowの固有評価主値・選択済み補助列 | 利用者選択 | 0〜1、reason、evidence、source |
| Similarity | 同じrowの主回答と同じ設問の参照回答 | `auto` | 0〜1の類似度とreason |

Referenceは1Questionにつき1runで1回生成し、同じrunの全rowで共有します。checkpointから再開すると保存済みReferenceを再利用します。

NormalとSpecialの「利用者選択」には`auto`も選べます。`auto`はrouterのためSDKがtoken上限を公開せず、その場合はmodel相対の容量検査を行いません。実際にroutingされたmodelは通常の実行記録には保存されません。コスト内訳でSDKが報告したモデルは匿名化した識別子だけを表示・記録します。

## ジョブごとのコスト観測

**実行**のコスト表示をオンにすると、入力・出力token、推論・cache内訳、SDK報告の`nano-AI units`、premium request消費量、観測状態を確認できます。**結果**では、その結果を作成した同じジョブの最終スナップショットを確認できます。次回の設定変更や別の実行で、前回結果のコストを再計算・置換しません。

「ジョブログ」では最新200行を確認でき、**ログを開く**／**保存先を開く**は明示操作です。ログは各開始操作ごとに独立し、再開時も以前のジョブと合算しません。値は観測値であり、請求確定額・通貨・アカウント全体の残量ではありません。SDKが返さない項目は未取得として表示し、0とは扱いません。保存内容・保持期間・機密性は[データとprivacy](privacy-and-data-handling.md#ジョブコストログ)を確認してください。

AIへBase points、Question earned、Special earned、Similarity penalty、Final score、合否を返させません。これらはapp previewとExcel formulaが計算します。

## 実行設定と出力先

通常モデル、並列度（1〜8）、指定出力先の利用者編集は**設定 → 共通**で行います。実行画面には次回の実効値と、今回／前回runの開始時に固定した値を分けて表示します。Reference／Similarityのmodelは固定の`auto`です。

- 共通設定は、今回の明示編集、保存済み設定、既定値の順で使います。**Copilot 状態を確認**で利用可能modelを確認し、保存希望IDが使えない場合は別modelへ自動変更せず、共通設定で選び直します。
- 明示した出力先は別の入力Excelへ変更しても保持します。保存済みなら再起動後も復元します。入力が未選択になっても明示指定は失われません。
- **指定出力先**を空欄にすると未指定（`null`）へ戻ります。この場合だけ、読込済み入力に隣接する`result`を算出します。入力未選択なら**入力後に決定**となり、自動算出したpathを明示指定として保存しません。
- 指定先が利用できない場合、無断で別の出力先へfallbackしません。設定の復元だけで出力directoryを作ることもありません。
- 実行中の編集は次回用です。進行中runのsnapshot・モデル・並列度・出力条件や、前回の結果は変更しません。画面移動、設定の読込・保存・適用、Promptの適用からloginやAI評価を自動開始しません。

## 配点

有効Questionを$q=1..N$とします。

$$
BasePoints + SpecialPoints + \sum_{q=1}^{N}QuestionPoints_q = 100
$$

| 設定 | 既定 | 範囲・条件 | 変更先 |
|---|---:|---|---|
| Base points | 60 | 0〜100 | 採点設計の基礎配点 |
| Special points | 0 | 0〜100。正ならenabled固有評価が必要 | 採点設計の固有配点 |
| Similarity penalty weight | 0.1 | 0〜1 | 採点設計の類似度減点係数 |
| 丸め桁数 | 1 | 0〜6 | 設定 → 共通の採点定義情報 |

採点設計の配点合計・不足／超過は、表示ページにない設問も含めた**全enabled Question**から計算します。合計は丸めず正確に100でなければなりません。設問を選んで配点や有効状態を変えても、他の設問へ勝手に再配分しません。評価方法・評価項目と固有評価の概要は現在の設定を示すもので、学生の仮の評点ではありません。

初期Question pointsは残点をenabled Questionへ均等配分し、割り切れない差分を最後のQuestionへ付けて合計を正確に合わせます。その後の自動再配分は**設問配点を均等化**を選んだ場合だけです。

## 通常評価

Knowledge／Custom、criterionのdescription・range・weight・有効状態は**設定 → 通常評価**で対象を選択して編集します。Knowledgeのsemantic instructionは読取専用、CustomのPromptは編集可能です。詳細は[Custom evaluator](custom-evaluator-guide.md)を参照してください。

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

固有評価は学生Prompt等、通常回答とは別のsource列を0〜1で定量化します。**設定 → 固有評価**で設問と固有項目を選び、主対象列・補助列・Prompt・有効状態を編集します。評価範囲は0〜1固定、総配点は採点設計で変更します。enabled固有評価をQuestion内で等分平均し、固有評価を持つenabled Question間でも等分平均します。

$$
SpecialEarned=SpecialPoints\times average(SpecialQuestionRate)
$$

`SpecialPoints=0`の場合は固有評価AIを実行せず（`NOT_RUN_ZERO_BUDGET`）、Special earnedを0とします。

## 類似度減点

$$
SimilarityPenalty_q=QuestionPoints_q\times Similarity_q\times SimilarityPenaltyWeight
$$

**Similarity penalty weightを0にしても、Reference生成やSimilarity処理自体は無効になりません。** 有効なSimilarity値に対する減点が0になる設定です。必要なReference／Similarityが技術的失敗でblankなら、係数0でもblankを0に置き換えません。固有配点0によるSpecialの非実行とは異なります。

Similarityは0〜1です。高い値は不正行為の証明ではなく、低い値も回答品質を保証しません。授業目的に応じてpenalty weightを0を含む範囲で設定し、結果を確認してください。

## Final rawとFinal score

$$
FinalRaw=BasePoints+\sum_q QuestionEarned_q+SpecialEarned-\sum_q SimilarityPenalty_q
$$

$$
FinalScore=\min(100,\max(0,FinalRaw))
$$

Final rawは監査用に保持し、Final scoreを0〜100へ収めます。Configの配点合計が100でない場合や、必要なAI値が技術的失敗でblankの場合は、最終値もblankになります。

上の式は計算関係を示します。実際の正規化・加重平均・獲得点等には定義の丸め桁数を各段階で適用しますが、配点合計のexact 100判定は丸めません。

## 空回答と技術的失敗

| 状態 | AI call | 値 |
|---|---:|---|
| 通常主回答が空と確定した完了済み行 | Normal／Similarityなし | Question rate / earned 0、Similarity 0 |
| 固有評価主値が空と確定した完了済み行 | 当該Specialなし | 当該Special score 0 |
| Reference失敗（通常主回答が非空で参照回答が必要な場合） | 失敗statusを保存 | Reference、対象のSimilarity、最終値blank |
| Normal/Special/Similarityの技術的失敗 | retry後に失敗status | 対象値と必要な上位計算blank |
| 取消、主値未確認、学生行の処理未完了 | 未送信または処理途中 | 未確定値はblank。空回答として0にしない |

該当設問の通常主回答が空と確定した完了済み行では、Reference失敗の有無にかかわらずSimilarityは0です。ほかの必要値と配点が有効なら最終値を計算できます。

0は空回答等の確定した状態に基づく計算値です。空回答のQuestion earnedが0でも、未実行criterionのAI rawやFinal scoreまで0という意味ではありません。blankは技術的失敗・取消・未確定等で値がない状態で、画面では**—**と表示します。取消runの完了・保存済み行と、未処理・未確定行も区別してください。

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

## 結果の一覧・詳細とoverride

1. **結果**の一覧で、元のExcel行番号、最終点、固有点、類似減点、状態、設問別得点を確認します。**前／次**でページを切り替え、**元の行番号 → 移動**で対象行へ移動できます。完了件数は成功件数とは別です。
2. 行を選んで**詳細・override**を開き、その行の設問・評価方法・criterionを選びます。全criterionの編集欄を並べるのではなく、選んだ**1 criterion**のAI raw、range、status、override、適用値と計算previewを表示します。
3. 編集可能なcriterionにだけ任意overrideを入力します。run開始時のsnapshotのeffective range内で検証し、不正な非空値では出力を止めます。**エラー n 件・次へ**で、別ページも含めた該当行・設問・評価方法・criterionへ移動して修正できます。
4. **一覧に戻る**やページ切替でも入力済みoverrideは保持します。**未保存の override あり**は修正版が未保存という表示で、元のfinalを変更したという意味ではありません。保存する場合は別名の新規`.xlsx`を指定して**検証して出力**を選びます。入力と既存の完成版は上書きしません。

結果画面は**前回の実行結果**を保持します。入力や設定を次回用に編集しても、この結果を新しいdraftで自動再評価しません。overrideの計算preview・別名出力もAIの再実行ではありません。

## checkpointと再開

新規runではfinalと同じstemの`.partial.xlsx`を作ります。

- 各Reference完了後に保存
- 1 student rowのNormal/Special/Similarityがすべて終わった後に保存
- target-local tempを検証してatomic replace
- process終了後も最後のcomplete rowから再開
- final成功後にpartialを削除

中断後は部分結果と再開元を画面に表示します。同じ起動中なら**中断した処理を再開準備**を選び、再起動後は**参照…**から`.partial.xlsx`を選ぶかfull pathを入力して、項目別の再開条件を確認します。いずれもAI処理は開始せず、**定量化を開始**を明示的に選んだときだけ再開します。

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
- concurrency: 1〜8
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

