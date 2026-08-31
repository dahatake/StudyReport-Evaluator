# StudyReport Evaluator 実装プラン — 単一Excel入力版

| 項目 | 内容 |
|---|---|
| 計画版 | 3.0 |
| 作成日 | 2026-08-31 |
| 要求正本 | `docs/requirements-definition.md` v2.0 |
| 状態 | 要求簡素化反映済み・実装開始前 |
| 唯一の利用者提供物 | Forms回答 `.xlsx` 1ファイル |
| 初版target | Windows 11 x64 / .NET 10 / Avalonia |
| 旧版 | v2.1を全面的にsupersede |

## 1. 変更理由

要求所有者は、実装と利用開始に必要な提供情報をForms回答Excelだけに限定した。旧v2.1でGATE-0をblockしていたEXT-03〜EXT-09、機関署名policy、教育評価baseline、法務判断、production signing、cross-platform runnerは、初版実装の依存から除外する。

これらを「提供済み」とみなすのではなく、初版scopeから外す。実装前に外部承認を捏造せず、アプリは利用者へ送信previewと責任範囲を表示する。macOS/Linux/Arm64、署名installer、formal educational validation等を将来追加する場合は、新しい要求改版と実測を必要とする。

## 2. 未回答事項に採用した既定

利用者が不在だった確認事項には次を採用する。すべて要求v2.0へ反映済みである。

| ID | 決定 |
|---|---|
| D-01 | 1回答者または1提出を1行とする。Forms列を画面でmanual mappingする。 |
| D-02 | 設問本文・論点は提示された2設問をeditable presetとして初期表示し、Excel見出しも候補にできる。 |
| D-03 | レポートは総合0〜30、Prompt能力は総合1〜10を既定とし、論点別に`MET/PARTIAL/NOT_FOUND`を返す。 |
| D-04 | AI点は候補点。人が採用または上書きするまで確認済み点を空欄にする。 |
| D-05 | アプリ固有OAuth Appを作らず、GitHub Copilot SDKの既存logged-in user credentialsを使用する。 |
| D-06 | identifierを除外し、毎runの送信previewと利用者確認後だけ送信する。 |
| D-07 | 初版正式targetはWindows 11 x64のみ。macOS/Linux/Arm64を未検証のまま対応表示しない。 |
| D-08 | 提示Promptの誤字を修正し、一般的な整理に合わせ「プログラミングは演繹的、機械学習は帰納的な側面」を初期論点にする。 |
| D-09 | 学生PromptとPrompt考慮事項は独立した任意列。考慮事項だけなら`CONSIDERATIONS_ONLY`とし、存在時は考慮事項を最優先基準として実Promptへの反映度を評価する。 |
| D-10 | 入力を変更せず、新しい `.xlsx` へConfig、Results、Run sheetを追加する。 |

## 3. Architecture

### 3.1 Production projects

| Project | Responsibility | Allowed references |
|---|---|---|
| `src/StudyReportEvaluator.Core/` | domain、mapping、definition、prompt、result validation、review、orchestration ports | BCL only |
| `src/StudyReportEvaluator.Workbooks/` | read-only intake、Open XML copy-on-write、results、validation、atomic output | Core |
| `src/StudyReportEvaluator.Platform/` | Copilot SDK auth/session/tool/retry、safe log | Core |
| `src/StudyReportEvaluator.Desktop/` | Avalonia UI、ViewModel、composition root | Core、Workbooks、Platform |

### 3.2 Test projects

- `tests/StudyReportEvaluator.Core.Tests/`
- `tests/StudyReportEvaluator.Workbooks.Tests/`
- `tests/StudyReportEvaluator.Platform.Tests/`
- `tests/StudyReportEvaluator.Desktop.Tests/`
- `tests/StudyReportEvaluator.E2E.Tests/`

### 3.3 Dependency rule

- CoreはOpen XML、Copilot SDK、Avaloniaを参照しない。
- PlatformはWorkbooksを参照しない。
- Copilot tool handlerからworkbook writerを呼ばない。
- Desktopのcomposition rootだけがconcrete adapterを結合する。
- student contentをdomain result以外のlog、exception message、telemetryへ渡さない。

## 4. Runtime flow

```mermaid
flowchart LR
    A[回答Excelを選択] --> B[read-only検査]
    B --> C[設問ごとに回答/Prompt/考慮事項列をmapping]
    C --> D[プリセットを選択して設問/論点/Promptを編集]
    D --> E[送信previewと件数を確認]
    E --> F[Copilotで候補点と論点判定]
    F --> G[人が採用/上書き/保留/却下]
    G --> H[新しいXLSXをatomic保存]
```

AI未認証またはofflineの場合はA〜Eと既存結果のreview/outputを利用できる。新規AI評価だけを停止する。

## 5. File ownership

| Path | Owner task |
|---|---|
| `docs/requirements-definition.md` | G0-01 |
| `work/20260831-implementation-plan.md` | G0-01 |
| `docs-dev/adr/0010-single-input-local-scope.md` | G0-02 |
| `docs-dev/preflight/requirements-baseline.md` | G0-03 |
| `docs-dev/preflight/implementation-task-file-map.md` | G0-03 |
| `docs-dev/traceability.md` | G0-04 and each `*-TR` |
| `docs-dev/preflight/gate-result.md` | GATE-0 |
| `StudyReportEvaluator.sln` | F-03 |
| `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `NuGet.Config` | F-01 |
| `.gitignore` | F-02 |
| `Desktop/Composition/ServiceRegistration.cs` | U-07 |
| `README.md` | D-01 |

既に別作業の未commit変更があるshared fileは、その内容を上書きせずowner taskで統合する。

## 6. Phase 0 — New baseline and gate

| Task | Dependencies | Artifacts | Completion |
|---|---|---|---|
| G0-01 | none | requirements v2.0; plan v3.0 | sole-input scope and defaults recorded |
| G0-02 | G0-01 | ADR-0010 | old external-gate scope explicitly superseded |
| G0-03 | G0-01/G0-02 | baseline; exact file map | hashes、commit anchor、no hidden external input |
| G0-04 | G0-03 | traceability | every v2.0 acceptance criterion and requirement surface mapped |
| GATE-0 | G0-01〜G0-04 | gate result | doc review PASS、`src/` files 0、external blocker 0 |

GATE-0前にproduction `src/`を作らない。GATE-0 PASS後は以下を直列依存とparallel laneに従って実装する。

## 7. Phase 1 — Foundation

| Task | Dependencies | Files | Completion |
|---|---|---|---|
| F-01 | GATE-0 | `global.json`; `Directory.Build.props`; `Directory.Packages.props`; `NuGet.Config` | .NET 10 exact SDK、nullable、warnings、deterministic、locked restore |
| F-02 | F-01 | `.gitignore` | bin/obj/artifacts/local auth/session/evidenceを除外。既存変更を保全してmerge |
| F-03 | F-01 | solution; 4 production `.csproj`; 5 test `.csproj` | clean restore/build/test hosts |
| F-04 | F-03 | `Core/Ports/Ports.cs`; architecture tests | one-way references and narrow ports |
| F-05 | F-03 | all `packages.lock.json`; package tests | exact packages、floating version 0 |
| F-TR | F-04/F-05 | traceability | foundation evidence serialized |
| GATE-1 | F-TR | `artifacts/test/gate-foundation.json` | restore/build/architecture/package lock PASS |

Package candidates:

- `DocumentFormat.OpenXml` exact stable version verified at implementation time.
- `GitHub.Copilot.SDK` exact stable version verified at implementation time.
- Avalonia exact stable version compatible with .NET 10.
- MSTest or xUnit exact version selected once and locked.

## 8. Phase 2 — Domain and preset contracts

| Task | Dependencies | Files | Completion |
|---|---|---|---|
| T-01 | GATE-1 | `Core/Domain/StatusCodes.cs`; tests | AI、validation、review、run、Prompt modeのclosed codes |
| T-02 | GATE-1 | `Core/Domain/QuestionColumnMapping.cs`; tests | required answer + optional Prompt/considerations + identifier/ignore |
| T-03 | GATE-1 | `Core/Domain/EvaluationDefinition.cs`; `RunSnapshot.cs`; tests | editable questions/prompts/criteria/ranges、immutable run hash |
| T-04 | T-03 | `Core/Presets/BuiltInEvaluationPresets.cs`; tests | requirements v2.0の2設問と2 prompt templatesをexact初期表示 |
| T-05 | T-02/T-03 | `Core/Prompting/PromptTemplateRenderer.cs`; tests | seven closed placeholders、nonrecursive、unknown reject |
| T-06 | T-02/T-05 | `Core/Privacy/PiiCandidateDetector.cs`; `Core/Prompting/SafeEvaluationPayloadBuilder.cs`; tests | email/phone候補のwarningと伏字preview、検知限界表示、identifier/other row/other question exclusion |
| T-07 | T-03 | `Core/Domain/EvaluationResult.cs`; tests | overall score、criterion status/reason/evidence/evidence source、Prompt mode |
| T-08 | T-06/T-07 | `Core/Validation/EvaluationResultValidator.cs`; tests | finite/range/exact criteria/limits。根拠を実際に送信した同一行・同一設問の指定source fieldと変換なしでsubstring照合し、不一致を拒否 |
| T-09 | T-07/T-08 | `Core/Review/ReviewDecisionService.cs`; tests | candidate separate from accepted/override/pending/rejected |
| T-10 | T-02〜T-09 | synthetic JSON fixtures and manifest | fixed seed、PII 0、1/2/10 questions、optional columns |
| T-TR | T-01〜T-10 | traceability | contract evidence serialized |
| GATE-2 | T-TR | `artifacts/test/gate-contracts.json` | positive and single-cause negative tests PASS |

### Domain limits

- Workbook used rows: maximum 20,000.
- Questions: minimum 1; technical maximum selected after serialized output-column test, not a lesson-specific default.
- Score ranges must be finite and `minimum < maximum`.
- Default report range: 0〜30.
- Default Prompt range: 1〜10.
- Each string cell: maximum 32,767 characters.
- Prompt/output request limits use the smaller of product limits and adopted SDK/model constraints.
- Invalid configuration is rejected before session creation; values are not silently truncated.

## 9. Phase 3A — Workbook lane

| Task | Dependencies | Files | Completion |
|---|---|---|---|
| W-01 | GATE-2 | `Workbooks/Intake/FileFormatClassifier.cs`; tests | OOXML xlsx accept、CFB/encrypted/macro/corrupt reject |
| W-02 | W-01 | `Workbooks/Intake/InputSnapshotService.cs`; tests | read-only same-handle snapshot、hash/size/time、TOCTOU guard |
| W-03 | W-02 | `Workbooks/Reading/WorkbookMetadataReader.cs`; tests | sheet/header/used-row metadata、body log 0 |
| W-04 | W-03/T-02 | `Workbooks/Mapping/ColumnMappingValidator.cs`; tests | header identity、role conflicts、1/2/10 question mapping |
| W-05 | W-02 | `Workbooks/Writing/WorkingPackage.cs`; tests | byte-copy input、no-op preservation |
| W-06 | W-05/T-03 | `Workbooks/Writing/ControlSheetWriter.cs`; tests | Config/Run snapshot、hash/version、no token |
| W-07 | W-05/T-07/T-09 | `Workbooks/Writing/EvaluationResultsWriter.cs`; tests | candidates、criteria、reason/evidence、review results as string cells |
| W-08 | W-06/W-07 | `Workbooks/Validation/OutputPackageValidator.cs`; tests | Open XML、sheet/column/limits、non-app part preservation |
| W-09 | W-08 | `Workbooks/Writing/AtomicOutputCommitter.cs`; fault tests | temp→flush→reopen→validate→same-volume rename |
| W-TR | W-01〜W-09 | traceability | workbook evidence serialized |
| GATE-W | W-TR | `artifacts/test/gate-workbook.json` | input immutable、output valid、fault tests PASS |

## 10. Phase 3B — Copilot lane

| Task | Dependencies | Files | Completion |
|---|---|---|---|
| A-01 | GATE-2 | `Platform/Copilot/CopilotClientFactory.cs`; tests | current fixed SDK、logged-in user auth、auth status、no secret input |
| A-02 | T-03/T-07 | `Platform/Copilot/EvaluationSchemaFactory.cs`; tests | report/Prompt result schemas closed to snapshot criteria/range |
| A-03 | A-02/T-08 | `Platform/Copilot/SubmitEvaluationTool.cs`; tests | process-local, external I/O 0、one structured submission |
| A-04 | A-01/A-03/T-06 | `Platform/Copilot/EphemeralEvaluationRunner.cs`; tests | one unit/session、tool-only result、normal body ignored |
| A-05 | A-04 | `Platform/Copilot/RetryAndCleanupCoordinator.cs`; tests | schema retry 1、transient retry 2、delete before next、cancel interlock |
| A-06 | A-01/A-05 | `Platform/Logging/SafeLogger.cs`; tests | closed code/count/hash only、content/token canary 0 |
| A-07 | A-01〜A-06 | `Platform.Tests/Copilot/CapabilityBoundaryTests.cs` | shell/fs/Web/GitHub write/MCP tool 0、unexpected permission reject |
| A-08 | A-01〜A-07 | optional authenticated synthetic smoke | if authenticated, model list→one inference→delete; otherwise explicit SKIP |
| A-TR | A-01〜A-08 | traceability | fake-client contract is required; live smoke status recorded separately |
| GATE-AI | A-TR | `artifacts/test/gate-copilot.json` | unit/contract/capability tests PASS; no external policy required |

A-08は実装completionの必須blockerではない。actual workbookを使わず固定synthetic textだけを送る。認証が利用可能なら実行し、利用不可なら`SKIPPED_NOT_AUTHENTICATED`を正確に記録する。

## 11. Phase 4 — Run orchestration

| Task | Dependencies | Files | Completion |
|---|---|---|---|
| R-01 | GATE-W/GATE-AI | `Core/Runs/EvaluationPlanBuilder.cs`; tests | rows×questions×report/prompt units、optional fields、max count |
| R-02 | R-01/A-05 | `Core/Runs/EvaluationScheduler.cs`; tests | concurrency 1/3、progress、cancel、no send after cancel |
| R-03 | R-02/W-09 | `Core/Runs/EvaluationOrchestrator.cs`; tests | snapshot→preview confirmation→evaluate→review/output |
| R-04 | R-03 | `Core/Runs/RunSummary.cs`; tests | success/failure/skipped/pending counts、no content |
| R-TR | R-01〜R-04 | traceability | run evidence serialized |
| GATE-RUN | R-TR | `artifacts/test/gate-run.json` | normal/failure/cancel/offline/cleanup tests PASS |

## 12. Phase 5 — Avalonia UI

Each UI task owns its View, code-behind, ViewModel, and component tests.

| Task | Dependencies | Screen / completion |
|---|---|---|
| U-01 | GATE-1 | shell、navigation、Japanese styles、keyboard/focus baseline |
| U-02 | GATE-W | start + file selection: scope warning、file hash、sheet/header inspection |
| U-03 | W-04/T-02 | column mapping: question groups、answer/Prompt/considerations/identifier/ignore |
| U-04 | T-03〜T-05 | evaluation settings: presets、free edit、criteria、score range、snapshot preview |
| U-05 | T-06/A-01/R-01 | send preview: selected fields、excluded identifiers、auth/model、unit count、confirm |
| U-06 | R-02/T-09 | execution/review: progress/cancel、candidate、criteria、reason/evidence、decisions |
| U-07 | R-03/R-04/W-09 | completion + composition root: input unchanged、output path、counts |
| U-08 | U-01〜U-07 | keyboard-only、focus order、color independence、200% tests |
| U-TR | U-08 | traceability | UI evidence serialized |
| GATE-UI | U-TR | `artifacts/test/gate-ui.json` | synthetic journey and accessibility checks PASS |

## 13. Phase 6 — End-to-end, packaging, docs

| Task | Dependencies | Files / completion |
|---|---|---|---|
| E-01 | GATE-UI | synthetic 1/2/10-question E2E | file→mapping→preset→fake AI→review→xlsx |
| E-02 | GATE-UI | hostile input E2E | encrypted/corrupt/formula injection/PII preview/cancel/disk full |
| E-03 | GATE-UI | Windows 11 x64 local E2E | actual self-contained app, synthetic workbook, Open XML output |
| E-04 | E-03 | local performance | 531 rows、Copilot wait excluded、environment/result recorded |
| P-01 | E-03 | `scripts/publish-windows.ps1`; layout tests | win-x64 self-contained folder、CLI/runtime/ICU/assets |
| P-02 | P-01 | `scripts/package-windows.ps1`; package tests | unsigned local zip + SHA-256、traversal/link 0 |
| D-01 | E-01〜P-02 | `README.md` | install、login、7-step tutorial、privacy、troubleshooting、limits |
| D-02 | D-01 | `docs-dev/architecture.md`; `docs-dev/excel-contract.md`; `docs-dev/customization.md` | current implementation only |
| D-03 | D-01/D-02 | documentation contract tests | links、paths、unsupported platform claims 0 |
| E-TR | E-01〜D-03 | traceability | all v2.0 AC mapped to evidence |
| GATE-ACCEPTANCE | E-TR | `artifacts/test/gate-acceptance.json` | AC-001〜AC-012 PASS or evidence-backed limitation |

## 14. Final deliverables

- Windows 11 x64 self-contained application folder.
- Local zip package and SHA-256.
- Source and locked dependencies.
- Unit、integration、synthetic E2E、Windows local E2E results.
- Editable two-question presets.
- Input `.xlsx` read-only processing.
- Candidate score and human review workflow.
- Output `.xlsx` with Config、Results、Run sheets.
- README and developer contract docs.

No production certificate、notarization、external legal approval、education baseline artifact is required to complete this plan.

## 15. Gate definitions

| Gate | Required positive evidence | Required negative evidence |
|---|---|---|
| GATE-0 | v2.0/v3.0 baseline、traceability、review | hidden external dependency 0、src file 0 before gate |
| GATE-1 | restore/build/test hosts、architecture | forbidden reference/floating package 0 |
| GATE-2 | domain/preset/fixture positives | invalid mapping/config/result accepted 0 |
| GATE-W | valid output and input hash | input mutation、formula cell、partial final output 0 |
| GATE-AI | auth contract、closed schema/tool、fake integration | secret input、ambient capability、content log 0 |
| GATE-RUN | normal/cancel/failure/offline | send-after-cancel、cleanup bypass、0-point coercion 0 |
| GATE-UI | seven-step synthetic journey | confirmation bypass、identifier send、keyboard blocker 0 |
| GATE-ACCEPTANCE | AC-001〜AC-012 | NOT_RUNをPASS扱い0、unsupported platform claim 0 |

## 16. Traceability

| Acceptance | Owner tasks |
|---|---|
| AC-001 | G0-01/G0-04/D-01 |
| AC-002 | T-02/W-03/W-04/U-03/E-01 |
| AC-003 | T-04/U-04/E-01 |
| AC-004 | T-03/T-04/T-07/A-02/E-01 |
| AC-005 | T-07/T-08/W-07/U-06/E-01 |
| AC-006 | T-02/R-01/W-07/E-01 |
| AC-007 | T-06/U-05/A-06/E-02 |
| AC-008 | T-09/W-07/U-06/E-01 |
| AC-009 | W-02/W-05/W-09/E-02/E-03 |
| AC-010 | W-08/W-09/E-02/E-03 |
| AC-011 | A-01/U-05/E-03 |
| AC-012 | F-01〜F-05/E-03/P-01 |

Detailed requirement-to-test mappings are maintained in `docs-dev/traceability.md` after G0-04.

## 17. Execution rules

1. 各taskはdirect dependency、own files、対応testだけを変更する。
2. production codeはtestまたはtestable contractと同じtaskで追加する。
3. 1つのnegative fixtureは1つのprimary failure reasonを持つ。
4. student content、Prompt本文、reason/evidence、tokenをtest log、exception、artifactへ保存しない。
5. actual Forms workbookはrepositoryへcommitしない。必要なら利用者指定pathからread-onlyで実行し、header、件数、hash以外をlogへ出さない。
6. library versionとAPIは実装時に公式sourceで確認してexact lockする。
7. live Copilot smokeが認証不足ならSKIPを記録し、fake contract testsで実装を完成できる。実データを代用送信しない。
8. 既存の無関係な未commit変更をrevert、stage、commitしない。
9. 各task後にdiagnostics、対象test、`git diff --check`を実行し、対象fileだけcommitする。
10. 実測していない性能、OS対応、署名、security、教育精度を主張しない。

## 18. Future scope

次は初版完成後の別要求である。

- macOS/Linux/Arm64 support and package signing.
- signed organizational policy and managed OAuth App.
- formal educational baseline and subgroup validation.
- institutional privacy/legal approval integration.
- row printing.
- protected workbook support.
- signed installer and public distribution.

将来scopeを追加しても、ReportDefinitionの作成にlocal launcher identity、teacher/product approvalを要求しない原則は維持する。
