# Sample workbook structural profile

| 項目 | 値 |
|---|---|
| Task | B-07 |
| 状態 | **CURRENT STRUCTURAL PROFILE** |
| Requirement | `docs/requirements-definition.md` v4.3 |
| Plan | `work/20260902-readme-end-user-release-plan.md` |
| Scope decision | ADR-0012 / ADR-0013 |
| Sample | `sample/SampleReport.xlsx` |
| Verification date | 2026-09-04 |
| Inspection boundary | read-only metadata/header-role inspection; response bodies not recorded |

## Identity

| Property | Verified value |
|---|---|
| Bytes | 470,806 |
| SHA-256 | `73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA` |
| Container | readable ZIP / Office Open XML `.xlsx` |
| ZIP entries | 11 |
| Package relationships | 8 |
| Required structural entries | `[Content_Types].xml`; `xl/workbook.xml`; `xl/_rels/workbook.xml.rels` |

SHA-256は本sampleのexact-byte identityであり、署名、作成者identity、内容の正しさ、教育的妥当性を証明しない。別の利用者workbookはこのhashと一致する必要がない。

## Workbook structure

| Worksheet | State | Used dimension | Initial treatment |
|---|---|---|---|
| 1件（name SHA-256 `88D759EA02CEF4B82885C6C620473162757C75522805707C20E2BE76A40A2825`） | Visible | `A1:L531` | initial candidate source sheet |

このworksheetでは1行目をheader候補、2〜531行をdata row候補とする。これは初期suggestionであり、利用者はsource sheet、header row、first/last data rowを変更できる。

## Initial column-role suggestions

| Column | Observed header-derived candidate | Initial suggestion |
|---|---|---|
| A〜E | header semanticsから評価対象候補にならない | unselected |
| F | primary semantics | primary candidate |
| G | primary + student Prompt semantics | primary / student Prompt candidate |
| H | primary + supporting semantics | primary / supporting candidate |
| I | primary semantics | primary candidate |
| J | primary + student Prompt semantics | primary / student Prompt candidate |
| K | supporting semantics | supporting candidate for J |
| L | header semanticsから評価対象候補にならない | unselected |

このprofileはheaderの意味から得たsuggestionだけを固定する。列位置やheader文字列でquestion typeを確定せず、候補外の列も利用者が任意のprimary／supporting columnとして選択できる。同じ列を複数questionへ使えるが、同一question内のprimary/supporting重複は拒否する。

`Student Prompt candidate`は、学生Prompt自体を定量化するときにその列をquestionのprimaryへ選べるという意味である。列へevaluator typeを固定する意味ではなく、Knowledge／Custom evaluatorはquestion definition上で別に構成する。

## Content handling boundary

- 回答本文、学生Prompt、考慮事項、feedback、identifier値を本profileへ転記しない。
- sample本文をunit test fixture、snapshot、log、gate artifact、live AI smokeへコピーしない。
- real-sample testはidentity、sheet/dimension、header-role suggestion、input不変だけを検証する。
- sample契約はexact path `sample/SampleReport.xlsx`だけを使い、同directoryの他fileを列挙、fallback、代用しない。
- local deterministic technical E2Eも同じ`sample/SampleReport.xlsx`をread-onlyで使う。
- AI、reason、evidence、scoreを本sampleから生成してpreflight evidenceにしない。
- actual user processingでは、利用者が選択した同一行primary/supporting cellだけをPromptへ含める。

## Synthetic test replacement

実装testはfixed-seed generatorで、次の構造を持つ非実データworkbookを作る。

- minimal valid workbook
- 531行のlarge synthetic workbook
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

**PASS.** 現sampleは要求v4.3／B-07の構造profileとして利用できる。これは定量化結果、教育評価精度、Copilot live処理のPASSを意味しない。
