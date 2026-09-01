# Requirement traceability — v4.0 implementation baseline

| 項目 | 値 |
|---|---|
| Requirement | `docs/requirements-definition.md` v4.0 |
| Decision | ADR-0012 |
| Detailed design | `docs-dev/detailed-design.md` |
| Plan | `work/20260901-v4-implementation-plan.md` |
| Baseline validation | Release 485/485 PASS at HEAD `b7b0c0ac91148c42c4aa61e04f1dab4efbb0fc98` |
| Current status | IMPLEMENTATION_IN_PROGRESS |

この表の`PLANNED`は未実装をPASSと称しない。task完了後にproduction symbol、direct test、gate identityへ更新する。旧v3 traceabilityはGit履歴とADR-0011に保持する。

## Status vocabulary

| Status | Meaning |
|---|---|
| `PLANNED` | owner/file/testを割当済み。実装またはpassing evidenceは未確認 |
| `PASS_REQUIRED` | production behaviorとdirect deterministic testが成功 |
| `PASS_EXTERNAL` | credentialed／platform external testを実行して成功 |
| `NOT_RUN_EXTERNAL_PREREQUISITE` | source/pipelineは存在するがrequired credential／runnerがなく実行していない |
| `FAILED` | required verificationが失敗 |
| `BLOCKED_REVIEW` | 再現したblocker/high review findingが未解決 |
| `NOT_REQUIRED` | v4 scope外。不要性を一般化しない |

## Acceptance criteria mapping

| AC | Requirement surface | Production task | Required test owner | Status |
|---|---|---|---|---|
| AC-001 | picker/path、immutable input、separate output | X-01/U-01/X-04 | input/path/atomic tests | PLANNED |
| AC-002 | question row 1/2、sheet/rows/normal/special mapping | X-01/U-01/U-02 | metadata/mapping/UI tests | PLANNED |
| AC-003 | repository sample F/I + G/J/K candidate、input identity | X-01 | sample structural test | PLANNED |
| AC-004 | base 60、special 0、similarity weight 0.1 | C-01/U-02 | domain/design tests | PLANNED |
| AC-005 | equal initial question points、explicit equalize、manual preservation | C-02/U-02 | allocation/UI tests | PLANNED |
| AC-006 | exact allocation total 100 | C-02/C-04/X-02 | validator/formula tests | PLANNED |
| AC-007 | dynamic Knowledge/Custom criterion raw only | C-05/A-03 | Core/Copilot tests | PLANNED |
| AC-008 | special item and equal averages | C-01/C-03/A-03/X-03 | domain/result/formula tests | PLANNED |
| AC-009 | one `auto` reference/question/run and References sheet | A-02/X-02/W-02 | reference/writer/resume tests | PLANNED |
| AC-010 | 0〜1 similarity and per-question penalty | A-04/C-03/X-03 | similarity/scoring/formula tests | PLANNED |
| AC-011 | Excel-owned earned/special/penalty/raw/clamped final | C-03/C-04/X-03 | hand oracle/writer/reopen tests | PLANNED |
| AC-012 | empty-zero、technical-blank | C-03/A-02..04/X-03 | operation/formula/E2E tests | PLANNED |
| AC-013 | result naming、four final sheets、atomic commit | X-02/X-03/X-04 | writer/path/fault tests | PLANNED |
| AC-014 | partial create and durable reference/row checkpoints | W-01/W-02 | checkpoint/workflow tests | PLANNED |
| AC-015 | closed resume validation and completed-row skip | W-01/W-02 | mismatch/skip tests | PLANNED |
| AC-016 | stage/progress/completion/path/count display | U-03 | execution/results UI tests | PLANNED |
| AC-017 | exact warning text and nonblocking | U-04 | warning/accessibility tests | PLANNED |
| AC-018 | `--input`, repeated `--prompt`, explicit apply, no auto-run | L-01/U-02 | parser/startup/UI tests | PLANNED |
| AC-019 | selected same-row payload and no-content logs | C-05/A-02..04/W-01 | capability/logger/canary tests | PLANNED |
| AC-020 | Windows self-contained package/install | P-01 | packaging/install smoke | PLANNED |
| AC-021 | macOS arm64/x64 bundle and signed/notary pipeline | P-02/P-03 | contract + external runner | PLANNED |
| AC-022 | user/dev docs and current screenshots | D-01..05 | documentation/screenshot tests | PLANNED |

## Test requirement mapping

| TR | Requirement | Required evidence owner | Status |
|---|---|---|---|
| TR-01 | Forms synthetic row 1/2 | X-01 tests | PLANNED |
| TR-02 | sample structure/identity | X-01 E2E | PLANNED |
| TR-03 | picker and format | U-01/input tests | PLANNED |
| TR-04 | allocation | C-02/U-02 | PLANNED |
| TR-05 | definition validation | C-02 | PLANNED |
| TR-06 | normal/special Prompt/schema | C-05/A-03 | PLANNED |
| TR-07 | reference once/auto/resume | A-02/W-02 | PLANNED |
| TR-08 | score boundaries/empty/failure | C-03/A-03/A-04 | PLANNED |
| TR-09 | independent score oracle | C-03 | PLANNED |
| TR-10 | formula/ref/DAG/cached | C-04/X-03 | PLANNED |
| TR-11 | final sheet set/preservation | X-02/X-03 | PLANNED |
| TR-12 | path naming/atomic | X-04 | PLANNED |
| TR-13 | checkpoint save/fault | W-01 | PLANNED |
| TR-14 | resume mismatch/skip | W-02 | PLANNED |
| TR-15 | AI failure/retry/cancel | A-02..04/W-02 | PLANNED |
| TR-16 | selected-row/literal/no-content | C-05/A-02..04/X-03 | PLANNED |
| TR-17 | 4-step UI/warning/progress/accessibility | U-01..04 | PLANNED |
| TR-18 | launch options/Prompt/no auto-run | L-01/U-02 | PLANNED |
| TR-19 | Windows delivery | P-01 | PLANNED |
| TR-20 | macOS deterministic delivery | P-02 | PLANNED |
| TR-21 | macOS credentialed release | P-03 external | PLANNED |
| TR-22 | docs/screenshots | D-01..04 | PLANNED |
| TR-23 | fixed-seed new/resume E2E | E-01/E-02 | PLANNED |
| TR-24 | optional live/recalculation | E-03 advisory | PLANNED |

## Mandatory safety surfaces

| Surface | Owner | Required negative evidence | Status |
|---|---|---|---|
| Input never modified | X-01/X-04/W-01 | success/failure/cancel/resume hash unchanged | PLANNED |
| Empty is 0, failure is blank | C-03/X-03 | no runner for empty; no failure-to-zero | PLANNED |
| Allocation exact 100 | C-02/X-02/X-03 | mismatch run blocked and formula blank | PLANNED |
| Reference once/question/run | A-02/W-02 | no duplicate across resume | PLANNED |
| `auto` no fallback | A-01/A-02/A-04 | auto absent causes preflight error | PLANNED |
| Closed AI tools | A-02..04 | unknown/missing/duplicate/range/evidence rejected | PLANNED |
| Selected same row only | C-05/A-02..04 | other row/column/path absent | PLANNED |
| No content logs | all runtime | canary text absent | PLANNED |
| Formula allowlist/ref/DAG | C-04/X-03 | raw/external/cycle/limits rejected | PLANNED |
| Literal untrusted strings | X-02/X-03/W-01 | formula markers remain strings | PLANNED |
| Atomic partial update | W-01 | old partial survives every pre-replace fault | PLANNED |
| Closed resume identity | W-01/W-02 | each mismatch rejects without write | PLANNED |
| Atomic final output | X-04 | valid final or no final | PLANNED |
| Warning no dependency | U-04 | zero acknowledgement/gate state | PLANNED |
| No CLI auto-run | L-01 | runner/session call count 0 after startup | PLANNED |
| Bundled CLI pinning | A-01/P-01/P-02 | PATH fallback and hash mismatch rejected | PLANNED |
| Platform claims | P-01..03/D-01..05 | no unverified macOS/signing PASS | PLANNED |

## Review protocol

各task review recordは少なくとも次を記録する。

- reviewed task IDとfile list
- target test/build result
- finding severity、reproduction、disposition
- rejected findingの明確な根拠
- applied fixとfollow-up result
- content data included = false

レビュー本文へ学生回答、Prompt、reference、AI reason/evidenceを含めない。

## Final gate prerequisites

1. AC-001〜022が`PASS_REQUIRED`または明示的なexternal statusへ更新済み。
2. TR-01〜24がpassing required evidenceへ接続済み。
3. 全required test、Release build、package testがPASS。
4. sample input identity不変。
5. unresolved reproducible blocker/high finding 0。
6. Windows package/install smoke PASS。
7. macOS deterministic package/launch test PASS on macOS runner。
8. signing/notaryはcredentialed runならPASS、未実行なら`NOT_RUN_EXTERNAL_PREREQUISITE`。未実行をPASSとしない。
9. optional live Copilot／spreadsheet recalculationをrequired evidenceへ算入しない。
10. docsとscreenshotsがcurrent UIへ同期。
