# Revised GATE-0 result — single-input implementation

| 項目 | 値 |
|---|---|
| Gate | GATE-0 |
| Requirement | `docs/requirements-definition.md` v2.0 |
| Plan | `work/20260831-implementation-plan.md` v3.0 |
| Scope decision | ADR-0010 |
| Evaluation snapshot HEAD | `34e2dc334bba2bb9fef291ca76b176611f75d475` |
| Previous gate result | v1.2/v2.1 `BLOCKED` record — **SUPERSEDED** |
| Current result | **PASS** |
| Phase 1 authorization | **GRANTED** |
| Production `src/` files at evaluation | `0` |
| Record date | 2026-08-31 |

## Decision rule

Requirement v2.0 section 15、plan v3.0 phase dependencies、ADR-0010 section 8を統合し、次の7条件をGATE-0の論理積として評価する。

1. Requirement v2.0 and plan v3.0 are committed.
2. ADR-0010 is approved and committed.
3. 単一Excel入力、editable preset、AI候補と人の確認、およびsynthetic fixtureのcontractが明文化されている。
4. The current baseline, exact task file map, and traceability are committed.
5. Independent document review reports zero unresolved blocker/high defects.
6. Production `src/` contains zero files at gate evaluation.
7. The initial implementation has zero hidden external blockers.

All seven conditions pass. Revised GATE-0 therefore grants Phase 1 implementation authorization.

Requirement v2.0 section 15 condition 3の「合成fixture仕様」は、section 14に列挙した入力組合せ、境界、negative scenarioと、plan T-10／current path mapに固定した生成物の種類・pathを指す。JSONおよびworkbook fixtureの実体生成とexpected valueの固定はGATE-1後のT-10で行い、その生成物自体をGATE-0の事前条件にはしない。

This result applies only to requirement v2.0 / plan v3.0. It does not retroactively change the correct historical `BLOCKED` result for v1.2 / v2.1.

## Evidence identity

| Evidence | Commit | SHA-256 | Result |
|---|---|---|---|
| `docs/requirements-definition.md` v2.0 | `740736f0ccc5df2dbf94989227b50a0ea34f480e` | `D401EEC4778E5A088D1674F65F5E036C4957713BADAC86B5D162584ABFB9FE31` | PASS |
| `work/20260831-implementation-plan.md` v3.0 | `23c48392a124541386f7d0d880a576943b1ef0ad` | `63E0669916B27E0ADF2FD1A44435EE20EC664B57D624EA949DCD2B3AED0C504E` | PASS |
| `docs-dev/adr/0010-single-input-local-scope.md` | `84484fb912a474b9ec36e0e240407af3e7cfea01` | `E6849DBFD0FE7C86782440E3C1AB99920AAE369F79D6D3D0B88DE7B2377038E6` | PASS |
| `docs-dev/preflight/requirements-baseline.md` | `3f40760bb05ee6fdc3837368cb835ec75ececd7d` | `36A9815BAAFE22AB69614BC48FAF8E27A4FDF352AFF68A3956F2DBEAC7D66535` | PASS |
| `docs-dev/preflight/implementation-task-file-map.md` | `3f40760bb05ee6fdc3837368cb835ec75ececd7d` | `519044E8CA218C24AF27EB3A85B1D5C376C7FCBF4C08E87FC16E42B56C987E63` | PASS |
| `docs-dev/traceability.md` | `34e2dc334bba2bb9fef291ca76b176611f75d475` | `53115D459F1EE8CCADFAD951F67792092354F4263A459D27CDBBC9639D464E26` | PASS |

The hashes identify exact repository bytes. They are not organizational signatures, legal approvals, production signing identities, or proof of educational validity.

## Gate checks

| Check | Evidence | Actual result | Gate result |
|---|---|---|---|
| Current canonical requirement | v2.0 identity above | committed | PASS |
| Current implementation plan | v3.0 identity above; `SCOPE-01`〜`SCOPE-10` distinct from documentation tasks | committed | PASS |
| Current scope decision | ADR-0010 identity above | approved in the requirement-owner session and committed | PASS |
| Input/preset/review contract | requirement v2.0 sections 2、4〜8、13 | one `.xlsx`; editable presets; candidate/human boundary | PASS |
| Synthetic fixture specification | requirement v2.0 section 14; plan T-10; current path map | fixed-seed JSON surfaces、1/2/10 questions、optional columns、security negatives specified; generation remains a post-GATE task | PASS |
| Baseline and exact paths | G0-03; 72 expected task/gate rows | 72 unique; missing 0; extra 0 | PASS |
| Acceptance traceability | G0-04 | 12 AC, 18 mandatory surfaces, 12 test surfaces | PASS |
| Document diagnostics | Markdown diagnostics | errors 0 | PASS |
| Adversarial review — requirement/plan | independent read-only review | 4 findings corrected; focused re-review defects 0 | PASS |
| Adversarial review — ADR | independent read-only review | 1 low wording variance corrected; unresolved defects 0 | PASS |
| Adversarial review — baseline/map | independent read-only review | defects 0 | PASS |
| Adversarial review — traceability | independent read-only review | defects 0 | PASS |
| Sole user-provided information | INPUT-01 | one Forms response `.xlsx` at runtime | PASS |
| Hidden external implementation input | baseline and 72-task dependency scan | 0 | PASS |
| Production source before gate | recursive file count under `src/` | 0 | PASS |

## Sole-input boundary

The only information or artifact the user must provide to operate the initial application is one standard, non-encrypted Forms response `.xlsx`.

The following are built into the application or selected at runtime and are not pre-implementation inputs:

- two editable question presets;
- editable report and Prompt evaluation templates;
- editable criteria/viewpoints;
- default report score range 0–30;
- default Prompt score range 1–10;
- editable column mappings for required report answers and optional Prompt/considerations;
- candidate score and human review workflow.

Existing Copilot CLI login is an interactive runtime prerequisite, not project information supplied to the developer. The user performs authentication directly with GitHub; no credential value is provided to the application project.

## Former external gates

EXT-03 through EXT-09 are outside the current initial scope. Their absence does not block implementation. This GATE-0 result does not claim that any former external policy, privacy, education, legal, platform, signing, review, or dump-policy input was provided or verified.

| Former surface | Current treatment |
|---|---|
| Managed organization policy and production trust | Future requirement; not implemented in initial version |
| Institutional privacy/legal approval verification | Future requirement; user responsibility notice remains |
| Formal educational baseline and thresholds | Future requirement; no accuracy or validity claim |
| Cross-platform runners and signing | Future requirement; Windows 11 x64 local scope only |
| Independent signed release record | Future requirement; internal adversarial review only |
| Dump-policy attestation | Future requirement; no institutional real-data mode claim |

## Authorized implementation scope

GATE-0 authorizes plan v3.0 Phase 1 and its dependency-ordered successors:

1. Foundation and locked projects.
2. Domain, editable presets, mapping, payload, validation, and review contracts.
3. Workbook and Copilot lanes.
4. Run orchestration.
5. Seven-step Avalonia UI.
6. Windows x64 E2E, self-contained folder publish, unsigned local zip, README, and developer documentation.

Authorization does not include:

- macOS, Linux, or Windows Arm64 support claims;
- production code signing or signed installer;
- protected workbook decryption;
- automatic grade finalization from AI candidates;
- managed organizational policy enforcement;
- formal legal compliance or educational validity claims.

## Runtime and testing boundaries

- Production implementation may now create `src/` and production/test projects listed in the current path map.
- Development and required E2E use fixed-seed synthetic workbooks and fake Copilot adapters.
- Optional authenticated smoke sends only fixed synthetic text. If no logged-in Copilot account is available, it is recorded as `SKIPPED_NOT_AUTHENTICATED` and does not block implementation completion.
- An actual Forms workbook is never committed and is not needed to build or test the application.
- When a user selects an actual workbook, it is read-only and identifiers are excluded from Copilot payloads.
- `NOT_RUN` is not treated as PASS except that the explicitly optional A-08 smoke may be skipped with its exact nonblocking status.

## Final disposition

**GATE-0 = PASS. Phase 1 authorization = GRANTED.**

Implementation must follow the 72-task ownership map and phase gates. The unrelated existing changes in `.gitignore`, `README.md`, and `.vscode/settings.json` remain outside this gate record and must not be staged, reverted, or overwritten by this task.
