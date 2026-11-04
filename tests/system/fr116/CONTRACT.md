# FR-116 ローカル対照評価 — System Test の契約

要求定義書 FR-116（AC-086〜AC-090）の受入基準から、System Test が前提にする外部契約を定める。
実装はこの契約に合わせる（入出力の形を変えるときは test-designer にケースの更新を依頼する）。

## 起動

```
<cmd> --dataset <dataset.json> --split <split.json> --rules <rules.json> --output <dir>
```

- `<cmd>` の既定: `dotnet run --project src/StudyReportEvaluator.ControlEvaluation --`。
  環境変数 `SRE_CONTROL_EVAL_CMD`（空白区切り）で置き換えられる。
- 実 AI・credential・ネットワーク・外部書込・課金 API を使わない（テストは credential の環境変数を消して実行する）。
- 出力: `<dir>/evaluation-record.json`（UTF-8）。評価済みなら exit 0、評価不成立なら exit ≠ 0 で記録は書く。
- 入力ファイルと、ほかのファイル（学生の点数・保存済み結果）を変更しない。

## 入力

- dataset: `dataset_version`, `questions[{id, group, text, references[]}]`,
  `items[{id, question_id, label, creation_method, source_item_id, relation, answer}]`。
  `label` は `追加指示なし|追加指示あり|抜粋|編集|自力`（作成者が定めた対照条件。AI 使用の実証ではない）。
- split: `tuning_groups[]`, `holdout_groups[]`, `seed`, `order[]`（グループの固定順序）。
- rules: `rules[{id, metric, threshold}]`, `tuned_on_groups[]`。
  `metric` は `legacy_composite|student_coverage|reference_coverage|normalized_exact`。値 ≥ threshold で `要確認`、未満で `非該当`。

## 評価不成立（status=`評価不成立`、`invalid_reasons` に理由）

空の holdout、同じグループが両群にある、派生回答（`source_item_id`）が別の群の回答を元にする、
`tuned_on_groups` と holdout が重なる（holdout でルールを調整した）。

## 評価記録 `evaluation-record.json`

- `status`（`評価済み|評価不成立`）, `invalid_reasons[]`, `generated_at`（再実行の比較から除く）
- `dataset_version`, `dataset_sha256`（入力ファイルのバイト列の SHA-256、小文字 hex）, `split_sha256`, `rules_sha256`
- `split`（入力の tuning_groups・holdout_groups・seed・order）, `conditions`（object）, `rules`（入力のルール）
- `comparison_versions`: `{legacy, improved}`（従来の単独参照合成値と改善した診断の版。異なる文字列）
- `items[]`: `id, question_id, label, creation_method, relation, source_item_id, group, partition(tuning|holdout),
  expected(対照陽性|対照陰性), metrics{legacy_composite, raw_exact, normalized_exact, student_coverage, reference_coverage},
  classifications{<rule id>: 要確認|非該当|保留}`。回答の欠損・空は metrics を null、分類を `保留` にする。
  閾値・metric の欠損や不正なルールは、そのルールの分類をすべて `保留` にする。
- `counts{<rule id>: {overall, by_label{<label>}, by_partition{tuning, holdout}}}`。各集計は
  `tp, fp, tn, fn, hold, n, tpr, fpr`。tpr=tp/(tp+fn)、fpr=fp/(fp+tn)、分母 0 は null。保留は正解に数えない。
- `limitations`（文字列）: 合成対照データでの評価・実学生について未検証、履歴不在、参照生成の非決定性、短文の共通正答、
  追加指示・編集・抜粋による見逃し、設問／model 条件への依存を説明する。AI 使用確率・不正判定の項目を持たない。

## 指標（§7.5）

正規化: NFKC → 小文字化 → 空白・句読点・記号を除く。被覆率 = 最長共通部分文字列の長さ ÷ 正規化後の長さ（4 桁丸め）。
`raw_exact` は正規化前の完全一致、`normalized_exact` は正規化後の完全一致（真偽値。ルールでは 1/0 として比べる）。
