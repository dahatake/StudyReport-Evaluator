# Sample workbook structural profile

| 項目 | 値 |
|---|---|
| Task | B-02 |
| 状態 | **CURRENT STRUCTURAL PROFILE** |
| Requirement | `docs/requirements-definition.md` v3.0 |
| Plan | `work/20260831-implementation-plan.md` v4.0 |
| Scope decision | ADR-0011 |
| Sample | `sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx` |
| Verification date | 2026-09-01 |
| Inspection boundary | read-only metadata/header-role inspection; response bodies not recorded |

## Identity

| Property | Verified value |
|---|---|
| Bytes | 661,189 |
| SHA-256 | `446386E20BB4096561CB4AFD6D74B8EAA9D50EAE53C97F984BA7F70EBEAD0DE5` |
| Container | readable ZIP / Office Open XML `.xlsx` |
| ZIP entries | 18 |
| Required structural entries | `[Content_Types].xml`; `xl/workbook.xml`; `xl/_rels/workbook.xml.rels` |

SHA-256は本sampleのexact-byte identityであり、署名、作成者identity、内容の正しさ、教育的妥当性を証明しない。別の利用者workbookはこのhashと一致する必要がない。

## Workbook structure

| Sheet | Used dimension | Initial treatment |
|---|---|---|
| `Old` | `A1:AF531` | preserve; not initially selected |
| `Original` | `A1:L531` | initial candidate source sheet |
| `Final` | `A1:AD531` | preserve; not initially selected |

`Original`では1行目をheader候補、2〜531行をdata row候補とする。これは初期suggestionであり、利用者はsource sheet、header row、first/last data rowを変更できる。

## Initial column-role suggestions

| Column | Observed header-derived candidate | Initial suggestion |
|---|---|---|
| A〜E | management metadata | unselected |
| F | question 1 report answer candidate | primary candidate |
| G | question 1 student Prompt candidate | Custom primary candidate |
| H | another answer headed as question | primary or supporting candidate |
| I | question 2 report answer candidate | primary candidate |
| J | question 2 student Prompt candidate | Custom primary candidate |
| K | Prompt considerations/viewpoints | supporting candidate for J |
| L | PBL feedback | unselected |

このsampleではheaderから2つのquestion grouping候補を観測したが、appのquestion数を2へ固定する意味ではない。このprofileはheaderの意味から得たsuggestionだけを固定する。列位置やheader文字列でquestion typeを確定せず、F〜K以外も利用者が任意のprimary／supporting columnとして選択できる。同じ列を複数questionへ使えるが、同一question内のprimary/supporting重複は拒否する。

`Custom primary candidate`は、学生Prompt自体を定量化するときにその列をquestionのprimaryへ選べるという意味である。列へevaluator typeを固定する意味ではなく、Knowledge／Custom evaluatorはquestion definition上で別に構成する。

## Content handling boundary

- 回答本文、学生Prompt、考慮事項、feedback、identifier値を本profileへ転記しない。
- sample本文をunit test fixture、snapshot、log、gate artifact、live AI smokeへコピーしない。
- real-sample testはidentity、sheet/dimension、header-role suggestion、input不変だけを検証する。
- AI、reason、evidence、scoreを本sampleから生成してpreflight evidenceにしない。
- actual user processingでは、利用者が選択した同一行primary/supporting cellだけをPromptへ含める。

## Synthetic test replacement

実装testはfixed-seed generatorで、次の構造を持つ非実データworkbookを作る。

- minimal valid workbook
- `Original!A1:L531`相当のsample-like workbook
- 1／2／10 questions
- 1／2／5 evaluators per question
- 1／4／20 criteria per evaluator
- empty primary、blank supporting、formula-marker text、Unicode、cell length boundary
- corrupt ZIP、macro/protected classification、sheet collision、output fault seams

Synthetic valueはsample本文から導出せず、期待値と同時にreview可能な固定seedで生成する。

## Drift rule

このrepository内sampleについてbytesまたはSHA-256が変わった場合、silent updateとして受け入れない。read-onlyで再profileし、変更理由、sheet/dimension、mapping suggestion、privacy boundaryをreviewして本artifactを改版する。

App自体は任意の標準`.xlsx`を扱うため、runtime inputへ本sample hashを要求しない。runtimeでは選択file自身の開始時snapshotを取得し、final rename直前にSHA-256、size、last-write timeの厳密一致を再確認する。

## Verification result

**PASS.** 現sampleは要求v3.0／計画v4.0の構造profileとして利用できる。これは定量化結果、教育評価精度、Copilot live処理、production implementationのPASSを意味しない。
