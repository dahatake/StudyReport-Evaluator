# Requirement traceability — v4.4 input synchronization baseline

| 項目 | 値 |
|---|---|
| Requirement | `docs/requirements-definition.md` v4.4 |
| Decision | ADR-0012（機能）/ ADR-0015（target delivery）/ ADR-0013（current public evidence） |
| Detailed design | `dev/docs/detailed-design.md` |
| Plan | `work/20260904-publication-remediation-plan.md` |
| Previous validation | delivery変更前 Release build warning/error 0、full 670/670 PASS（Core 190 / App 480）。現在の0.8.0 delivery evidenceへ流用しない |
| Recorded focused validation | 2026-09-05: InputViewTests 21、WorkbookMetadataReaderTests 11、ColumnMappingSuggesterTests 14、計46/46 PASS。DocumentationContractTests 14/14 PASS |
| Audited full required validation | 2026-09-05、source `20c8121`のbaseline: Core 190 + App deterministic 524 + sample構造 1 + Windows ZIP 3 = 718/718 PASS（App計528、failed 0、skipped 0）。opt-inの未実行経路を含む |
| Current status | 監査済み`20c8121`のv4.4 baselineはPASS。現行`0.8.3` candidateはUNRELEASED、公開済みreleaseは`v0.8.1`。今回修正版の最終判定は以下のreview summaryと対象sourceの照合で行う |

この表の`PLANNED`は未実装をPASSと称しない。task完了後にproduction symbol、direct test、gate identityへ更新する。旧v3 traceabilityはGit履歴とADR-0011に保持する。

## Evidence integrity and storage

監査済みcandidateのsource identityは`20c8121c2474a13f409c0d7f0fde9d4c41f74698`、product versionは`0.8.3`である。集約記録は`artifacts/test/final-recovery-20c8121/phases.json`。以下のPASS系statusはこのsource、または明記した履歴の範囲に限定する。

- 集約記録のrestore、build、version、version-self-test、deterministic、sample、windows-zip、windows-msix、release-matrixの9工程はすべてexit code 0。件数は同directoryの各phase logに基づき、718/718は上表の4群の合計である。
- test runnerのPASS集計には、opt-in未指定で`NOT_RUN`としてreturnする経路やpolicy経路の成功を含む。skipped 0でも、実際のLive AI、外部Excel再計算、RealData処理はこのbaselineで`NOT_RUN`。sample構造確認やfake/synthetic実行をそれらの実処理完了へ読み替えない。
- `artifacts/`はGit対象外の生成物で、再実行により変化し、公開checkoutでの存在を保証しない。pathはcode spanとして記録し、公開Markdownリンクや永続release evidenceの代わりにしない。source commit、製品版、実行条件、phase log、および対象artifactのbytes／SHA-256を照合して読む。
- source commitの記載とexit codeだけで、未commit差分の不存在、全実行経路、production trustを証明しない。公開`v0.8.1`のrelease identityと、`0.8.3`のlocal matrix／package検証は別の証拠である。

今回reviewの文書修正およびその後のcode/UI変更はこのbaselineの検証対象外であり、変更後のsourceで再検証が必要。既存gateやPNGを、新しい変更の検証済み証拠として扱わない。

今回の[修正記録](../../work/20260905-adversarial-review.md)に対応する最終実行の正本は`artifacts/test/adversarial-review/final/summary.json`。`sourceCommit`一致、前後clean、全体`PASSED`、各TRXの件数／outcome／assembly／SHA-256が揃う場合だけ、そのsourceの完了と判定する。記録なし、`RUNNING`、`FAILED`は合格ではない。再実行で前回記録を上書きせず、別run directoryへ保存する。

## Status vocabulary

| Status | Meaning |
|---|---|
| `PLANNED` | owner/file/testを割当済み。実装またはpassing evidenceは未確認 |
| `NOT_RUN` | 対象の実処理を実行していない。test methodのPASSや過去evidenceで置き換えない |
| `NOT_RUN_CURRENT_CANDIDATE` | sourceと過去evidenceはあるが、現在のworking treeに対するrequired regressionを未実行 |
| `PASS_REQUIRED` | production behaviorとdirect deterministic testが成功 |
| `PASS_EXTERNAL` | credentialed／platform external testを実行して成功 |
| `NOT_RUN_EXTERNAL_PREREQUISITE` | source/pipelineは存在するがrequired credential／runnerがなく実行していない |
| `FAIL` | required verificationが失敗 |
| `BLOCKED_REVIEW` | 再現したblocker/high review findingが未解決 |
| `NOT_REQUIRED` | v4 scope外。不要性を一般化しない |
| `BLOCKED_EXTERNAL` | required input/artifactがrepository外にあり、安全なlocal代替がない |
| `PASS_MECHANISM` | test certificateまたはunsigned artifactでpackage mechanismだけが成功。非公開であり、production readinessを意味しない |
| `PASS_PRODUCTION` | production trust pathとclean target OSで成功 |
| `MIXED_ADVISORY` | optional evidenceを実行し、PASSとFAILED_ADVISORYが混在。required gateへ算入しない |

AC-024／AC-025とTR-26／TR-27の`PASS_REQUIRED`は、現版でrequiredなmacOS static source contract 2件の成功だけを表す。production artifact、署名、公証、clean-host実行は現版scope外であり、`PASS_PRODUCTION`を表さない。

AC-028／TR-29では、公開済み`v0.8.1`のdraft asset／matrix hash照合・公開後の認証なしre-downloadという履歴と、`20c8121`（`0.8.3`）のlocal matrix生成・semantic検証／workflow contract testのbaselineを区別する。後者は`0.8.3`の公開workflowや公開後re-downloadの完了を示さない。

## Acceptance criteria mapping

| AC | Requirement surface | Production task | Required test owner | Status |
|---|---|---|---|---|
| AC-001 | picker/path、immutable input、separate output | X-01/U-01/X-04 | input/path/atomic tests | PASS_REQUIRED |
| AC-002 | question row 1/2、sheet/rows/normal/special mapping、primary選択時のquestion text同期 | X-01/U-01/U-02 | metadata/mapping/Input UI tests | PASS_REQUIRED |
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
| AC-020 | Windows legacy self-contained ZIP regression | P-01 | packaging/launch smoke | PASS_REQUIRED |
| AC-021 | unverified platform/installer/signing claim exclusion | P-01..04/D-01..05 | documentation/package contract | PASS_REQUIRED |
| AC-022 | user/dev docs and current delivery evidence | D-01..05 | documentation/screenshot tests | PASS_REQUIRED |
| AC-023 | non-public unsigned Windows development MSIX mechanism | P-02 | manifest/package/unpack/hash/policy/cleanup | PASS_MECHANISM |
| AC-024 | macOS source foundationを非公開で保持（source-only） | P-03 | macOS static source contract | PASS_REQUIRED |
| AC-025 | macOS future production order contract（source-only） | P-03 | sign/notary source contract | PASS_REQUIRED |
| AC-026 | Windows ZIP取得/hash/展開/起動docs | P-01/D-01..05 | ZIP journey + docs contract | PASS_REQUIRED |
| AC-027 | public/development packageにuser workbookを含めない | P-01/P-02 | package layout + input identity | PASS_REQUIRED |
| AC-028 | publish flag/status matrixとrelease boundary | P-04 | workflow contract + release evidence | PASS_REQUIRED |

## Test requirement mapping

| TR | Requirement | Required evidence owner | Status |
|---|---|---|---|
| TR-01 | Forms synthetic row 1/2とprimary columnからquestion textへの同期 | X-01/U-01 tests | PASS_REQUIRED |
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
| TR-17 | 4-step UI/warning/progress/accessibilityとInput binding同期 | U-01..04 | PASS_REQUIRED |
| TR-18 | launch options/Prompt/no auto-run | L-01/U-02 | PASS_REQUIRED |
| TR-19 | Windows publish/package/bundled CLI/clean launch | P-01 | PASS_REQUIRED |
| TR-20 | unsigned ZIP/hash/safe layout/reproducibility | P-01 | PASS_REQUIRED |
| TR-21 | unsupported platform/installer/signing claim exclusion | P-01/D-01..05 | PASS_REQUIRED |
| TR-22 | docs/screenshots | D-01..04 | PASS_REQUIRED |
| TR-23 | fixed-seed new/resume E2E | E-01/E-02 | PASS_REQUIRED |
| TR-24 | optional live/recalculation | E-03 advisory（2026-09-02〜03の履歴はMIXED_ADVISORY） | NOT_RUN |
| TR-25 | non-public unsigned Windows development MSIX mechanism | P-02 | PASS_MECHANISM |
| TR-26 | macOS source foundation static contract（source-only） | P-03 | PASS_REQUIRED |
| TR-27 | macOS future trust order static contract（source-only） | P-03 | PASS_REQUIRED |
| TR-28 | Windows ZIP launch/input不変 + MSIX package exclusion | P-01/P-02 | PASS_REQUIRED |
| TR-29 | publish matrix/secret/public re-download | P-04 | PASS_REQUIRED |

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
| Platform claims | P-01..04/D-01..05 | Windows ZIPだけを対応表示し、development MSIX/macOSを公開しない | PASS_REQUIRED |

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

1. AC-001〜028が`PASS_REQUIRED`、`PASS_MECHANISM`、`PASS_PRODUCTION`または正確なblock statusへ更新済み。
2. TR-01〜29がrequired evidenceまたは正確なexternal statusへ接続済み。
3. 全required test、Release build、package testがPASS。
4. sample input identity不変。
5. unresolved reproducible blocker/high finding 0。
6. Windows ZIP regression、sidecar、bundled CLI、clean extract/launchが`PASS_REQUIRED`。
7. development MSIXのpackage/unpack/hash/policy/cleanupが`PASS_MECHANISM`で、install/launchはrequiredでなく未実行、release assetと一般利用者手順から除外されている。
8. macOS、Linux、Windows Arm64、production-signed MSIX、未実測artifactを対応済みと表示していない。
9. optional live Copilot／spreadsheet recalculationをrequired evidenceへ算入しない。
10. docsとscreenshotsがcurrent UI、Windows ZIP公開境界、実在assetへ同期。
