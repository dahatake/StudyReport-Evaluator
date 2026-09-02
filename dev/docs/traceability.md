# Requirement traceability — v4.1 implementation baseline

| 項目 | 値 |
|---|---|
| Requirement | `docs/requirements-definition.md` v4.1 |
| Decision | ADR-0012（機能）/ ADR-0013（platform） |
| Detailed design | `dev/docs/detailed-design.md` |
| Plan | `work/20260901-v4-implementation-plan.md` |
| Current validation | Release build warning/error 0。full 670/670 PASS（Core 190 / App 480）。canonical sample technical E2E 1/1、documentation contract 14/14 PASS |
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
| `BLOCKED_EXTERNAL` | required input/artifactがrepository外にあり、安全なlocal代替がない |
| `MIXED_ADVISORY` | optional evidenceを実行し、PASSとFAILED_ADVISORYが混在。required gateへ算入しない |

## Acceptance criteria mapping

| AC | Requirement surface | Production task | Required test owner | Status |
|---|---|---|---|---|
| AC-001 | picker/path、immutable input、separate output | X-01/U-01/X-04 | input/path/atomic tests | PASS_REQUIRED |
| AC-002 | question row 1/2、sheet/rows/normal/special mapping | X-01/U-01/U-02 | metadata/mapping/UI tests | PASS_REQUIRED |
| AC-003 | canonical repository sample F〜K candidate、input identity | X-01 | sample structural test | PASS_REQUIRED |
| AC-004 | base 60、special 0、similarity weight 0.1 | C-01/U-02 | domain/design tests | PASS_REQUIRED |
| AC-005 | equal initial question points、explicit equalize、manual preservation | C-02/U-02 | allocation/UI tests | PASS_REQUIRED |
| AC-006 | exact allocation total 100 | C-02/C-04/X-02 | validator/formula tests | PASS_REQUIRED |
| AC-007 | dynamic Knowledge/Custom criterion raw only | C-05/A-03 | Core/Copilot tests | PASS_REQUIRED |
| AC-008 | special item and equal averages | C-01/C-03/A-03/X-03 | domain/result/formula tests | PASS_REQUIRED |
| AC-009 | one `auto` reference/question/run and References sheet | A-02/X-02/W-02 | reference/writer/resume tests | PASS_REQUIRED |
| AC-010 | 0〜1 similarity and per-question penalty | A-04/C-03/X-03 | similarity/scoring/formula tests | PASS_REQUIRED |
| AC-011 | Excel-owned earned/special/penalty/raw/clamped final | C-03/C-04/X-03 | hand oracle/writer/reopen tests | PASS_REQUIRED |
| AC-012 | empty-zero、technical-blank | C-03/A-02..04/X-03 | operation/formula/E2E tests | PASS_REQUIRED |
| AC-013 | result naming、four final sheets、atomic commit | X-02/X-03/X-04 | writer/path/fault tests | PASS_REQUIRED |
| AC-014 | partial create and durable reference/row checkpoints | W-01/W-02 | checkpoint/workflow tests | PASS_REQUIRED |
| AC-015 | closed resume validation and completed-row skip | W-01/W-02 | mismatch/skip tests | PASS_REQUIRED |
| AC-016 | stage/progress/completion/path/count display | U-03 | execution/results UI tests | PASS_REQUIRED |
| AC-017 | exact warning text and nonblocking | U-04 | warning/accessibility tests | PASS_REQUIRED |
| AC-018 | `--input`, repeated `--prompt`, explicit apply, no auto-run | L-01/U-02 | parser/startup/UI tests | PASS_REQUIRED |
| AC-019 | selected same-row payload and no-content logs | C-05/A-02..04/W-01 | capability/logger/canary tests | PASS_REQUIRED |
| AC-020 | Windows self-contained unsigned ZIP/hash/bundled CLI/clean launch | P-01 | packaging/launch smoke | PASS_REQUIRED |
| AC-021 | unsupported platform/installer/signing claim exclusion | P-01/D-01..05 | documentation/package contract | PASS_REQUIRED |
| AC-022 | user/dev docs and current screenshots | D-01..05 | documentation/screenshot tests | PASS_REQUIRED |

## Test requirement mapping

| TR | Requirement | Required evidence owner | Status |
|---|---|---|---|
| TR-01 | Forms synthetic row 1/2 | X-01 tests | PASS_REQUIRED |
| TR-02 | sample structure/identity | X-01 E2E | PASS_REQUIRED |
| TR-03 | picker and format | U-01/input tests | PASS_REQUIRED |
| TR-04 | allocation | C-02/U-02 | PASS_REQUIRED |
| TR-05 | definition validation | C-02 | PASS_REQUIRED |
| TR-06 | normal/special Prompt/schema | C-05/A-03 | PASS_REQUIRED |
| TR-07 | reference once/auto/resume | A-02/W-02 | PASS_REQUIRED |
| TR-08 | score boundaries/empty/failure | C-03/A-03/A-04 | PASS_REQUIRED |
| TR-09 | independent score oracle | C-03 | PASS_REQUIRED |
| TR-10 | formula/ref/DAG/cached | C-04/X-03 | PASS_REQUIRED |
| TR-11 | final sheet set/preservation | X-02/X-03 | PASS_REQUIRED |
| TR-12 | path naming/atomic | X-04 | PASS_REQUIRED |
| TR-13 | checkpoint save/fault | W-01 | PASS_REQUIRED |
| TR-14 | resume mismatch/skip | W-02 | PASS_REQUIRED |
| TR-15 | AI failure/retry/cancel | A-02..04/W-02 | PASS_REQUIRED |
| TR-16 | selected-row/literal/no-content | C-05/A-02..04/X-03 | PASS_REQUIRED |
| TR-17 | 4-step UI/warning/progress/accessibility | U-01..04 | PASS_REQUIRED |
| TR-18 | launch options/Prompt/no auto-run | L-01/U-02 | PASS_REQUIRED |
| TR-19 | Windows publish/package/bundled CLI/clean launch | P-01 | PASS_REQUIRED |
| TR-20 | unsigned ZIP/hash/safe layout/reproducibility | P-01 | PASS_REQUIRED |
| TR-21 | unsupported platform/installer/signing claim exclusion | P-01/D-01..05 | PASS_REQUIRED |
| TR-22 | docs/screenshots | D-01..04 | PASS_REQUIRED |
| TR-23 | fixed-seed new/resume E2E | E-01/E-02 | PASS_REQUIRED |
| TR-24 | optional live/recalculation | E-03 advisory | MIXED_ADVISORY |

## Mandatory safety surfaces

| Surface | Owner | Required negative evidence | Status |
|---|---|---|---|
| Input never modified | X-01/X-04/W-01 | success/failure/cancel/resume hash unchanged | PASS_REQUIRED |
| Empty is 0, failure is blank | C-03/X-03 | no runner for empty; no failure-to-zero | PASS_REQUIRED |
| Allocation exact 100 | C-02/X-02/X-03 | mismatch run blocked and formula blank | PASS_REQUIRED |
| Reference once/question/run | A-02/W-02 | no duplicate across resume | PASS_REQUIRED |
| `auto` no fallback | A-01/A-02/A-04 | auto absent causes preflight error | PASS_REQUIRED |
| Closed AI tools | A-02..04 | unknown/missing/duplicate/range/evidence rejected | PASS_REQUIRED |
| Selected same row only | C-05/A-02..04 | other row/column/path absent | PASS_REQUIRED |
| No content logs | all runtime | canary text absent | PASS_REQUIRED |
| Formula allowlist/ref/DAG | C-04/X-03 | raw/external/cycle/limits rejected | PASS_REQUIRED |
| Literal untrusted strings | X-02/X-03/W-01 | formula markers remain strings | PASS_REQUIRED |
| Atomic partial update | W-01 | old partial survives every pre-replace fault | PASS_REQUIRED |
| Closed resume identity | W-01/W-02 | each mismatch rejects without write | PASS_REQUIRED |
| Atomic final output | X-04 | valid final or no final | PASS_REQUIRED |
| Warning no dependency | U-04 | zero acknowledgement/gate state | PASS_REQUIRED |
| No CLI auto-run | L-01 | runner/session call count 0 after startup | PASS_REQUIRED |
| Bundled CLI pinning | A-01/P-01 | PATH fallback and hash mismatch rejected | PASS_REQUIRED |
| Platform claims | P-01/D-01..05 | Windows 11 x64だけを対応表示し、macOS/Linux/Windows Arm64/installer/signingを表示しない | PASS_REQUIRED |

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
6. Windows package/展開先launch smoke PASS。
7. Windows packageの展開先launchとbundled CLI resolverがPASS。
8. macOS、Linux、Windows Arm64、installer、code signing、notarizationを対応済みと表示していない。
9. optional live Copilot／spreadsheet recalculationをrequired evidenceへ算入しない。
10. docsとscreenshotsがcurrent UIへ同期。
