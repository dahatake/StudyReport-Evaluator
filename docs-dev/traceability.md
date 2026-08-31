# P0 requirement traceability

| 項目 | 値 |
|---|---|
| Task | G-18 |
| Requirement baseline | `docs/requirements-definition.md` v1.2 |
| Plan | `work/20260831-implementation-plan.md` v2.1 |
| Baseline content commit | `a06dc33639fbb904b14bc0ed0ae853bd7841e1a8` |
| Snapshot HEAD before this record | `e715fe53a57f1e3ec432e03eb9045340a1c8b8d0` |
| P0 IDs tracked | 145 |
| Production implementation | **NOT STARTED — GATE-0 BLOCKED** |
| Record date | 2026-08-31 |

## Scope and status vocabulary

This record maps every explicit P0 requirement ID to its planned implementation and evidence tasks, current Phase 0 evidence, and unresolved blocker. It does not claim that a mapped future task has been implemented or tested.

| Status | Meaning |
|---|---|
| `CONTRACT_READY` | A Phase 0 contract, synthetic fixture, or isolated spike exists; the production requirement is still unimplemented. |
| `PARTIAL_BLOCKED` | Some test-only or local evidence exists, but a required external or live input is absent. |
| `PLANNED` | Exact implementation/test ownership exists in plan v2.1, but production work is prohibited until GATE-0 passes. |
| `BLOCKED` | A direct external input or prerequisite is absent; `NOT_RUN` is not treated as PASS. |

All `PLANNED` and `CONTRACT_READY` rows remain unimplemented. A Phase 0 artifact is design or preflight evidence only. It cannot satisfy the later production, E2E, release, external approval, or independent review obligation.

## Phase 0 evidence inventory

| Task | Status | Evidence | Commit | SHA-256 / blocker |
|---|---|---|---|---|
| G-RB | COMPLETE | `docs-dev/preflight/requirements-baseline.md` | `921edc0eb2be13edad1db0688a63bb8fa555fa6d` | baseline content anchored to `a06dc33639fbb904b14bc0ed0ae853bd7841e1a8` |
| G-10 | COMPLETE | `docs-dev/preflight/report-definition-contract.md` | `a06dc33639fbb904b14bc0ed0ae853bd7841e1a8` | `0E329457B9B2290CD6F25E7269DF5F90AB9A51D2300B232B94B8C074556775BD` |
| G-11 | COMPLETE | `docs-dev/preflight/fixture-plan.md` | `2554b7d7a148717430059ab54302c5d3df6b9400` | `12F382204F2BE47B8D235118998EDEFDCEC2FA8321D0A400078CCF46782DFED6` |
| G-12 | PARTIAL_BLOCKED | `docs-dev/adr/0006-signed-records.md`; four test-only JWS fixtures | `b4e32ab5eb83f612ac06bb1c22f9564694e89642` | EXT-03 production issuer, trust, policy, OAuth/org/model conditions not provided |
| G-13 | BLOCKED | `docs-dev/preflight/governance-inputs.md` | `2b16986a1d3f22439aabf96df22d1680aebbf8d7` | EXT-04 and EXT-06 not provided |
| G-14 | PARTIAL_BLOCKED | `tests/fixtures/evaluation/metric-spec-test.json` | `e715fe53a57f1e3ec432e03eb9045340a1c8b8d0` | EXT-05 external baseline, approved metric spec, thresholds, subgroup policy not provided |
| G-15 | BLOCKED / NOT_RUN | no spike or live evidence created | none | Direct prerequisite G-12 is blocked by EXT-03; no approved auth/org/model/manifest inputs |
| G-16 | COMPLETE | `docs-dev/preflight/document-pipeline.md`; isolated Open XML spike | `618da0900cdeafb8d62df00bacc089494b3a66e8` | document-pipeline record SHA-256 `CE189797BCE1500911B3BBBF114E6D2F598AE3BE62B64F82F005F705658CF063` |
| G-17 | BLOCKED / NOT_RUN | candidate contract only in `docs-dev/adr/0008-platform-print-package.md` | none | EXT-07 signing, notarization, Arm64, Office/print runners not provided |
| G-18 | THIS RECORD | `docs-dev/traceability.md` | pending | Mapping completeness is independent of upstream PASS; blockers remain explicit |

## Objectives and acceptance criteria

| Requirement | Planned implementation / evidence tasks | Current evidence | Status / unresolved blocker |
|---|---|---|---|
| OBJ-001 | W-01〜W-12; O-01〜O-11; R-12; E-01 | G-03; G-11; G-16 | CONTRACT_READY; production and E2E unimplemented |
| OBJ-002 | T-02〜T-04; L-01〜L-07; C-01〜C-11; R-12 | G-05; G-10; G-11 | CONTRACT_READY; GATE-0 |
| OBJ-003 | L-10〜L-14; O-04〜O-08; E-07 | G-04; G-10; G-16 | CONTRACT_READY; G-17/E-07 still required |
| OBJ-004 | R-09〜R-11; V-01〜V-06; UI-01/UI-06/UI-09; E-12/E-13 | G-04; G-13; G-14 | PARTIAL_BLOCKED; EXT-04/05/06/09 and later tests |
| OBJ-005 | G-17; P-01〜P-09; E-09 | G-08 candidate matrix only | BLOCKED; EXT-07 and real OS/CPU evidence absent |
| AC-001 | W-01〜W-03; O-09〜O-11; E-01 | G-11 fixture specs; G-16 local spike | CONTRACT_READY; input-preservation E2E absent |
| AC-002 | L-10〜L-14; O-05/O-08; E-07 | G-11 formula vectors; G-16 two-engine bounded spike | CONTRACT_READY; production formula/E2E absent |
| AC-003 | A-11/A-12; C-12; G-15; E-05 | G-07 network contract | BLOCKED; G-15 live process/network canary absent |
| AC-004 | G-14; V-01〜V-06; E-12 | G-14 test-only oracle | PARTIAL_BLOCKED; EXT-05 absent, no educational validity claim |
| AC-005 | T-01; L-11; O-05; R-10; E-14 | G-04 state/adoption contract | CONTRACT_READY; production cross-product/E2E absent |
| AC-006 | G-12〜G-14; A-14; E-12; REL-03 | G-12 test-only fixtures; G-13 register; G-14 test-only spec | BLOCKED; EXT-03/04/05/06/09 and operations approval absent |
| AC-007 | W-12; O-01/O-08; E-01 | G-03 mutation contract; G-16 isolated package evidence | CONTRACT_READY; full preservation E2E absent |

## Business flow

| Requirement | Planned implementation / evidence tasks | Current evidence | Status / unresolved blocker |
|---|---|---|---|
| BUS-001 | A-14; UI-01; E-13 | G-13 governance register | BLOCKED; EXT-03/04/05/09 and UI absent |
| BUS-002 | A-05〜A-10; UI-02; E-04 | G-06/G-07 contracts | BLOCKED; EXT-03 and G-15 absent |
| BUS-003 | W-01〜W-09; UI-03; E-01/E-02 | G-11 intake fixtures; G-16 spike | CONTRACT_READY; implementation absent |
| BUS-004 | T-02; L-01; UI-04; E-03 | G-10/G-11 synthetic definitions | CONTRACT_READY; implementation absent |
| BUS-005 | T-03/T-06; L-02/L-03/L-07; UI-05 | G-10 schema/contract/vectors | CONTRACT_READY; implementation absent |
| BUS-006 | L-04〜L-06; UI-04; E-03 | G-05/G-10/G-11 contracts | CONTRACT_READY; implementation absent |
| BUS-007 | C-04; V-01〜V-06; UI-06; E-04/E-12 | G-14 test-only metric spec | PARTIAL_BLOCKED; G-15 and EXT-05 absent |
| BUS-008 | T-06; A-13; R-04; UI-07 | G-05/G-10 hard-limit contract | CONTRACT_READY; implementation absent |
| BUS-009 | R-03〜R-06/R-12; UI-08; E-14 | plan/task map only | PLANNED; GATE-0 |
| BUS-010 | R-09〜R-11; UI-09; E-13 | G-04 review transition contract | CONTRACT_READY; implementation absent |
| BUS-011 | O-01〜O-11; UI-11; E-01/E-07 | G-02/G-03/G-16 | CONTRACT_READY; implementation absent |
| BUS-012 | O-11; R-12; UI-11; E-14 | G-02 completion contract | CONTRACT_READY; implementation absent |

## Functional requirements — workbook and mapping

| Requirement | Planned implementation / evidence tasks | Current evidence | Status / unresolved blocker |
|---|---|---|---|
| FR-001 | W-01〜W-03; E-01 | G-11 workbook vectors | CONTRACT_READY; OS file-identity implementation absent |
| FR-002 | W-03; O-10/O-11; E-01 | G-02 output-pair contract | CONTRACT_READY; implementation absent |
| FR-003 | W-11; R-07; O-09〜O-11 | G-02/G-03 contracts | CONTRACT_READY; implementation absent |
| FR-004 | W-04〜W-06; E-03 | G-11 hostile package vectors | CONTRACT_READY; scanner absent |
| FR-005 | W-04/W-08; E-02 | G-11 protected-input vectors | CONTRACT_READY; classifier/E2E absent |
| FR-006 | T-05; W-05/W-09; E-03 | G-11 boundary vectors | CONTRACT_READY; budget scanner absent |
| FR-007 | W-07〜W-09; O-03; E-03 | G-03/G-11 formula and relationship vectors | CONTRACT_READY; scanner/projection absent |
| FR-008 | O-01/O-02; W-12 | G-03; G-16 isolated mutation spike | CONTRACT_READY; production writer absent |
| FR-009 | W-12; O-01/O-08; E-01 | G-03; G-16 isolated relationship evidence | CONTRACT_READY; full package preservation absent |
| FR-010 | W-09/W-10; O-03 | G-03; G-11 clean fixture spec | CONTRACT_READY; projection writer absent |
| FR-011 | W-11; O-02 | G-11 naming vectors | CONTRACT_READY; implementation absent |
| FR-012 | W-09/W-10; O-03 | G-11 clean fixture spec | CONTRACT_READY; implementation absent |
| FR-013 | W-09/W-10 | G-11 row-boundary vectors | CONTRACT_READY; implementation absent |
| FR-014 | W-09; UI-03 | G-11 fixture metadata contract | CONTRACT_READY; reader/UI absent |
| FR-016 | T-10; O-09/O-11; E-01 | G-02/G-16 atomic-boundary contract evidence | CONTRACT_READY; fault-tested implementation absent |
| FR-017 | T-07; O-04/O-10/O-11; E-01 | G-02 detached completion contract | CONTRACT_READY; signed production record blocked by G-12 |
| FR-020 | T-02; L-01; UI-04 | G-10/G-11 1/2/10-question vectors | CONTRACT_READY; implementation absent |
| FR-021 | T-02; L-01; UI-04 | G-10 mapping/report contract | CONTRACT_READY; implementation absent |
| FR-022 | T-02/T-03; L-04/L-07; O-04 | G-10 snapshot/hash contract | CONTRACT_READY; implementation absent |
| FR-023 | T-02; L-04/L-06 | G-10/G-11 vectors | CONTRACT_READY; implementation absent |
| FR-024 | T-02; L-08/L-09; UI-04 | G-10/G-11 vectors | CONTRACT_READY; implementation absent |
| FR-025 | T-02; L-04〜L-06; O-03 | G-05/G-10 data-boundary contract | CONTRACT_READY; implementation absent |
| FR-026 | T-02; L-01/L-04; O-03 | G-10/G-11 vectors | CONTRACT_READY; implementation absent |
| FR-027 | L-01; UI-04; E-03 | G-10 launcher-identity boundary | CONTRACT_READY; UI confirmation absent |

## Functional requirements — ReportDefinition and Copilot

| Requirement | Planned implementation / evidence tasks | Current evidence | Status / unresolved blocker |
|---|---|---|---|
| FR-030 | T-03; L-02; UI-05 | G-10 schema/contract/vectors | CONTRACT_READY; production storage/UI absent |
| FR-031 | L-02; E-03 | G-10/G-11 template vectors | CONTRACT_READY; parser absent |
| FR-032 | T-03/T-06; L-03; C-01 | G-10 maximum-instance contract | CONTRACT_READY; serializer preflight absent |
| FR-033 | L-03; UI-05 | G-10 synthetic anchors | CONTRACT_READY; validator/UI absent |
| FR-034 | L-02〜L-06; C-01〜C-04; UI-06 | G-05/G-10 protocol contract | BLOCKED; G-15 live canary absent |
| FR-035 | T-03; L-07; O-04; R-06 | G-10 snapshot/hash contract | CONTRACT_READY; implementation absent |
| FR-036 | L-04; C-03/C-10/C-11; E-03/E-04 | G-05 capability/evidence contract; G-11 injection vectors | BLOCKED; G-15 live capability proof absent |
| FR-037 | L-02〜L-04; C-01/C-02 | G-05/G-10 dynamic reason/evidence contract | CONTRACT_READY; implementation absent |
| FR-039 | T-03/T-06; C-01; UI-05 | G-10 schema/max-instance vectors | CONTRACT_READY; implementation absent |
| FR-040 | F-02/F-10; A-12; G-15; P-08 | G-05/G-07 source contract only | BLOCKED; exact adopted SDK/CLI/hash and live canary absent |
| FR-041 | A-01〜A-03; G-12; E-04 | G-06 test-only signature fixtures | PARTIAL_BLOCKED; EXT-03 production trust/policy absent |
| FR-042 | A-05; E-04 | G-07 endpoint contract | BLOCKED; EXT-03 OAuth App input absent |
| FR-043 | A-06〜A-09; G-17; E-06 | G-06 vault/checkpoint contract | BLOCKED; G-17 real vault evidence absent |
| FR-044 | A-10; E-04 | G-07/G-10 account-versus-launcher boundary | BLOCKED; EXT-03 and live account/org proof absent |
| FR-045 | A-13; G-15; E-04 | G-07 model binding contract | BLOCKED; approved model and live catalog absent |
| FR-046 | C-04; C-07; E-04/E-05 | G-05 session lifecycle contract | BLOCKED; G-15 live session evidence absent |
| FR-047 | A-12; C-03/C-10; G-15 | G-05 permission/capability contract | BLOCKED; live Empty/tool/Reject canary absent |
| FR-048 | C-01〜C-03/C-11 | G-05/G-10 closed dynamic schema contract | CONTRACT_READY; implementation/live canary absent |
| FR-049 | T-06; C-01/C-02/C-11 | G-05/G-10 maximum-instance contract | CONTRACT_READY; implementation absent |
| FR-050 | C-05/C-07/C-11 | G-05 retry lifecycle contract | BLOCKED; G-15 live cleanup semantics absent |
| FR-051 | T-09/T-11; C-06; R-04 | G-05 retry classification contract | CONTRACT_READY; implementation absent |
| FR-052 | T-06; R-04; G-15 | G-05/G-10 hard-limit contract | PARTIAL_BLOCKED; SDK/CLI measured lower limits unknown |
| FR-053 | T-06; A-13; R-04; UI-07 | G-10 display/budget contract | CONTRACT_READY; UI/runtime implementation absent |
| FR-054 | C-09/C-13; UI-11; E-11 | G-05 usage-only contract | BLOCKED; G-15/E-11 live usage evidence absent |
| FR-055 | C-07/C-08; G-15; E-05 | G-05 deletion/canary contract | BLOCKED; live session storage/delete evidence absent |
| FR-056 | R-06/R-12; E-06 | G-02/G-10 identity binding contracts | CONTRACT_READY; implementation absent |
| FR-057 | R-07/R-12; E-14 | G-05 offline disposition contract | CONTRACT_READY; implementation absent |

## Functional requirements — privacy, similarity, output, and review

| Requirement | Planned implementation / evidence tasks | Current evidence | Status / unresolved blocker |
|---|---|---|---|
| FR-060 | L-04/L-06; C-03; E-03/E-04 | G-05/G-10 data allowlist contract | CONTRACT_READY; implementation absent |
| FR-061 | L-05/L-06; UI-04 | G-11 synthetic PII vectors | CONTRACT_READY; detector/preview absent |
| FR-062 | R-08; C-12; E-05 | G-05/G-07 no-content contract | BLOCKED; G-15 live log/session evidence absent |
| FR-063 | R-01/R-02; C-07/C-08; E-06 | G-06 checkpoint/vault contract | BLOCKED; G-17 vault and EXT-09 dump evidence absent |
| FR-064 | A-12; C-12; G-15; E-05 | G-05/G-07 telemetry-deny contract | BLOCKED; live process/network proof absent |
| FR-070 | L-08/L-09; E-07 | G-11 Unicode oracle; G-16 app-local ICU spike | CONTRACT_READY; cross-OS bit-exact evidence absent |
| FR-071 | L-09; O-05; E-07 | G-04/G-11/G-16 | CONTRACT_READY; output implementation absent |
| FR-072 | L-13; O-05; E-07 | G-04 formula priority contract | CONTRACT_READY; production/recalc test absent |
| FR-073 | A-04; L-13; O-04; E-07 | G-04 signed-calibration contract | BLOCKED; external course calibration record absent |
| FR-074 | L-13/L-14; O-08; E-07 | G-04 graph non-connection contract | CONTRACT_READY; validator absent |
| FR-075 | O-07; UI-01/UI-09; D-09; E-13 | G-04 notice contract | PLANNED; UI/workbook/README evidence absent |
| FR-080 | L-03/L-12; C-02; O-05 | G-04/G-05 result and aggregate contracts | CONTRACT_READY; implementation absent |
| FR-081 | T-03; L-10〜L-12; O-04/O-05 | G-04/G-10/G-16 | CONTRACT_READY; production formulas absent |
| FR-082 | O-05; R-10; UI-09 | G-04 review/adoption contract | CONTRACT_READY; implementation absent |
| FR-083 | T-01; L-11; O-05 | G-04 necessary-and-sufficient adoption contract | CONTRACT_READY; 500+ cross-product test absent |
| FR-084 | C-02; R-07; O-05 | G-04/G-05 failure contract | CONTRACT_READY; implementation absent |
| FR-085 | L-10〜L-14; O-08; E-07 | G-04 closed AST; G-16 ROUND range evidence | CONTRACT_READY; production validator/E2E absent |
| FR-086 | O-05/O-08; G-16/G-17; E-07 | G-16 Windows Excel/LibreOffice bounded spike | PARTIAL_BLOCKED; macOS/Linux/adopted RC evidence absent |
| FR-087 | O-03/O-08; E-03/E-07 | G-03; G-11 injection vectors; G-16 string-cell spike | CONTRACT_READY; production/E2E absent |
| FR-088 | L-14; O-08; E-03/E-07 | G-03/G-04 forbidden-formula contract | CONTRACT_READY; production scan absent |
| FR-089 | O-08〜O-11; E-01 | G-02/G-03/G-16 | CONTRACT_READY; fault/E2E implementation absent |
| FR-090 | O-06; UI-09; E-14 | G-16 minimal presentation evidence only | PLANNED; production presentation absent |
| FR-091 | O-07; D-03; E-13 | G-04/G-10 explanation contract | PLANNED; workbook documentation absent |
| FR-100 | R-09; UI-09; E-13 | G-04 review-state contract | CONTRACT_READY; implementation absent |
| FR-101 | R-10; UI-09; E-14 | G-04 review transition contract | CONTRACT_READY; implementation absent |
| FR-102 | C-02; R-11; UI-09 | G-05 exact evidence contract | CONTRACT_READY; implementation absent |
| FR-103 | R-11; UI-10; G-17; E-09 | G-08 privacy-first print candidate contract | BLOCKED; EXT-07/print runner and approved adapter absent |
| FR-104 | R-10; O-05; UI-09 | G-04/G-10 optional unverified label contract | CONTRACT_READY; implementation absent |
| FR-105 | G-13; UI-01; O-07; D-09; E-13 | G-13 contact/redress row is NOT_PROVIDED | BLOCKED; EXT-04 contact/correction/appeal evidence absent |

## Non-functional requirements

| Requirement | Planned implementation / evidence tasks | Current evidence | Status / unresolved blocker |
|---|---|---|---|
| NFR-SEC-001 | A-12; C-03/C-10; L-04; E-04 | G-05 capability contract | BLOCKED; G-15 live capability proof absent |
| NFR-SEC-002 | W-04〜W-08; L-04; C-01〜C-11; O-03/O-08; E-03/E-04 | G-03/G-05/G-11 vectors | CONTRACT_READY; implementation absent |
| NFR-SEC-003 | A-05〜A-09; R-08; P-08 | G-06/G-12 test-only trust separation | PARTIAL_BLOCKED; production trust/vault evidence absent |
| NFR-SEC-004 | F-02/F-10; P-08; REL-01 | plan/task map only | PLANNED; no production dependencies/SBOM yet |
| NFR-SEC-005 | G-17; P-02〜P-04/P-08/P-09; E-09 | G-08 candidate contract | BLOCKED; EXT-07 signing/notarization evidence absent |
| NFR-SEC-006 | A-11; C-12; P-02〜P-04; E-05 | G-07 TLS contract | BLOCKED; G-15/G-17 live evidence absent |
| NFR-SEC-007 | A-11; G-15; C-12; D-06 | G-07 source snapshot/manifest contract | BLOCKED; signed production manifest and live capture absent |
| NFR-SEC-008 | D-08; REL-03/REL-04 | G-13 contact evidence not provided | BLOCKED; real security contact/support input absent |
| NFR-SEC-009 | G-12; A-05/A-10; P-08; E-04 | G-06/G-07 policy contract | BLOCKED; EXT-03 OAuth configuration/approval absent |
| NFR-PRI-001 | G-13; A-03/A-14; UI-01/UI-12; D-06/D-09 | G-13 register | BLOCKED; EXT-04 metadata absent |
| NFR-PRI-002 | G-13; A-14; E-12; REL-03 | G-13 lawful-basis row is NOT_PROVIDED | BLOCKED; EXT-04 absent |
| NFR-PRI-003 | L-04〜L-06; C-03; E-03/E-04 | G-05/G-10/G-11 | CONTRACT_READY; implementation absent |
| NFR-PRI-004 | G-12; A-03/A-13/A-14; REL-03 | G-12 test-only policy fixtures | BLOCKED; EXT-03 production model/retention approval absent |
| NFR-PRI-005 | G-11; T-08; UI-13; E-01〜E-14 | G-11 synthetic-only fixture plan | CONTRACT_READY; later suites/UI absent |
| NFR-REL-001 | T-10; W-01〜W-03; O-09〜O-11; R-01/R-02/R-06; E-01/E-06 | G-02/G-06/G-16 contracts | CONTRACT_READY; implementation/fault E2E absent |
| NFR-REL-002 | T-01; C-02; R-07; O-05 | G-04/G-05 state contract | CONTRACT_READY; implementation absent |
| NFR-REL-003 | V-04/V-05; UI-06; E-12 | G-14 trial-separation fixture | PARTIAL_BLOCKED; EXT-05 and repeated live evaluation absent |
| NFR-REL-004 | T-03; L-07; R-06; V-03 | G-10 snapshot/hash contract; G-14 invalidation list | CONTRACT_READY; implementation absent |
| NFR-REL-005 | R-05; UI-08; E-10/E-14 | plan/task map only | PLANNED; UI/runtime absent |
| NFR-REL-006 | G-17; E-10; P-09 | G-08 environment matrix contract | BLOCKED; EXT-07 RC runners and benchmark absent |
| NFR-REL-007 | R-03/R-05; C-13; UI-08 | plan/task map only | PLANNED; runtime/UI absent |
| NFR-REL-008 | C-13; E-11; REL-02 | no measured values claimed | BLOCKED; G-15/E-11 approved live environment absent |
| NFR-REL-009 | C-07/C-08; G-15; E-05/E-06 | G-05 deletion/canary contract | BLOCKED; live SDK/CLI storage evidence absent |
| NFR-FAI-001 | G-14; V-01〜V-06; E-12 | G-14 test-only metric oracle | PARTIAL_BLOCKED; EXT-05 target-course baseline absent |
| NFR-FAI-002 | G-14; V-01/V-05/V-06 | G-14 exact preregistered test metrics | PARTIAL_BLOCKED; external metrics/thresholds absent |
| NFR-FAI-003 | G-14; V-02/V-06 | G-14 synthetic subgroup negative control | PARTIAL_BLOCKED; lawful/needed external subgroup policy absent |
| NFR-FAI-004 | G-11/G-14; V-02; E-12 | G-14 five counterexample categories | PARTIAL_BLOCKED; external baseline cases absent |
| NFR-FAI-005 | L-07; V-03/V-06; E-12 | G-14 change-invalidation list | PARTIAL_BLOCKED; production gate implementation absent |
| NFR-FAI-006 | V-03/V-06; A-14; E-12 | G-14 real-data blocker and failing subgroup control | PARTIAL_BLOCKED; EXT-05 absent |
| NFR-ACC-001 | F-09; UI-01〜UI-14; E-08/E-14 | G-08 manual matrix contract | PLANNED; UI absent |
| NFR-ACC-002 | F-09; UI-13; G-17; E-08/E-09 | G-08 AT/platform contract | BLOCKED; EXT-07 real OS/AT runners absent |
| NFR-ACC-003 | F-09; UI-01〜UI-14; E-08/E-14 | G-08 display matrix contract | PLANNED; UI absent |
| NFR-ACC-004 | UI-01〜UI-14; E-13/E-14; D-09 | requirements wording only | PLANNED; UI/docs absent |

## Setup requirements

| Requirement | Planned implementation / evidence tasks | Current evidence | Status / unresolved blocker |
|---|---|---|---|
| SET-001 | G-17; P-01〜P-04; E-09 | G-08 package candidate contract | BLOCKED; EXT-07 clean runners absent |
| SET-002 | G-17; P-01〜P-04/P-08/P-09; E-09 | G-08 trust/path contract | BLOCKED; signed production artifacts absent |
| SET-003 | G-17; P-02〜P-04; E-09 | G-08 non-privileged candidate rule | BLOCKED; setup artifacts/runners absent |
| SET-004 | P-02〜P-07; E-09 | G-08 repair/upgrade/uninstall contract | PLANNED; packaging prohibited by GATE-0 |
| SET-005 | P-01〜P-04; E-09 | G-08 diagnostic acceptance contract | BLOCKED; application/CLI candidate absent |
| SET-006 | G-07; P-02〜P-04; E-05/E-09 | G-07 proxy/TLS contract | BLOCKED; live network/setup evidence absent |
| SET-007 | P-05〜P-07; E-09 | G-08 uninstall contract | PLANNED; packaging prohibited by GATE-0 |
| SET-008 | D-07; D-09; D-12 | documentation plan only | PLANNED; implementation and docs absent |

## Unnumbered mandatory verification surfaces

### Automated tests in requirements section 14.1

| Requirement section item | Planned evidence task(s) | Current status |
|---|---|---|
| 14.1-01 workbook golden/preservation | G-11; T-08; W-12; O-08; E-01 | Fixture specification complete; implementation/E2E NOT_RUN |
| 14.1-02 original/TOCTOU | W-01〜W-03; E-01 | NOT_RUN |
| 14.1-03 protection classification | G-11; W-04/W-08; E-02 | Fixture specification complete; implementation/E2E NOT_RUN |
| 14.1-04 hostile package | G-11; W-04〜W-08; E-03 | Fixture specification complete; implementation/E2E NOT_RUN |
| 14.1-05 mapping/collision | G-11; L-01; W-11; E-03 | Fixture specification complete; implementation/E2E NOT_RUN |
| 14.1-06 ReportDefinition/Prompt | G-10/G-11; T-03/T-06; L-02/L-03; C-01/C-02; E-03 | Contract complete; implementation/E2E NOT_RUN |
| 14.1-07 policy/OAuth/Copilot | G-12/G-15; A-01〜A-14; C-01〜C-11; E-04 | BLOCKED by EXT-03; live canary NOT_RUN |
| 14.1-08 session/telemetry | G-15; C-07/C-08/C-12; E-05 | BLOCKED by G-12/EXT-03; NOT_RUN |
| 14.1-09 similarity | G-11/G-16; L-08/L-09; E-07 | Synthetic/local preflight complete; cross-OS E2E NOT_RUN |
| 14.1-10 formula injection | G-11/G-16; O-03/O-08; E-03/E-07 | Synthetic/local preflight complete; production/E2E NOT_RUN |
| 14.1-11 formula cross-product | G-04/G-16; L-10〜L-14; O-05/O-08; E-07 | Contract/local spike complete; 500+ production test NOT_RUN |
| 14.1-12 formula Office E2E | G-16/G-17; E-07 | Windows spike partial; required adopted OS matrix BLOCKED |
| 14.1-13 resume/no-double-send | R-06/R-12; E-06 | NOT_RUN |
| 14.1-14 log inspection | R-08; C-12; E-05 | BLOCKED by G-15; NOT_RUN |
| 14.1-15 network E2E | G-15; A-11/A-12; C-12; E-05 | BLOCKED by EXT-03; NOT_RUN |
| 14.1-16 accessibility E2E | G-17; UI-13; E-08 | BLOCKED by EXT-07/application absence; NOT_RUN |
| 14.1-17 cross-OS E2E | G-17; E-09 | BLOCKED by EXT-07; NOT_RUN |
| 14.1-18 performance/resource | G-17; E-10/E-11 | BLOCKED by EXT-03/EXT-07; NOT_RUN |
| 14.1-19 similarity calibration gate | G-14; A-04; L-13; E-07 | BLOCKED by external calibration record/EXT-05; NOT_RUN |

### Educational release gate in requirements section 14.2

| Item | Planned evidence task(s) | Current status |
|---|---|---|
| 14.2-01 independent double scoring/adjudication | G-14; V-01/V-06 | Test-only protocol exists; EXT-05 baseline absent |
| 14.2-02 fixed Prompt/model and repeated runs | G-14; V-04/V-05 | Test-only trial policy exists; real approved design absent |
| 14.2-03 exact/adjacent/kappa/difference/rank/repeat metrics | G-14; V-01/V-05 | Test-only hand oracle PASS; external preregistration absent |
| 14.2-04 subgroup error/missingness | G-14; V-02/V-06 | Synthetic negative control PASS; external lawful policy absent |
| 14.2-05 counterexamples | G-11/G-14; V-02 | Categories preregistered; external cases absent |
| 14.2-06 pre-threshold/critical-difference definition | G-14; V-03/V-06 | Test-only threshold cannot be reused; EXT-05 absent |
| 14.2-07 failure remediation/no auto-grading promotion | V-03/V-06; A-14 | Planned; no production evaluation exists |
| 14.2-08 change invalidation | G-14; V-03/V-06 | Invalidation keys preregistered; implementation absent |
| 14.2-09 separate similarity calibration | G-04/G-14; A-04; P1-06 | Contract separated; external pair set/labels absent |
| 14.2-10 Copilot operations approval separate from launcher identity | G-12; A-03/A-10/A-14; REL-03 | BLOCKED by EXT-03; launcher identity not substituted |

### Definition of Done in requirements section 14.3

| DoD condition | Planned evidence | Current status |
|---|---|---|
| Every P0 has test or approval evidence | G-18; all `*-TR`; REL-05 | Mapping complete in this record; execution evidence incomplete |
| Supported-OS CI/E2E successful | G-17; E-07〜E-10 | BLOCKED / NOT_RUN |
| Known Critical/High vulnerability disposition | F-10; P-08; REL-01 | NOT_RUN; no release dependency set exists |
| Formula/external-link/input differences zero | GATE-OUTPUT; E-01/E-07 | G-16 isolated spike only; production NOT_RUN |
| Allowed package differences only | W-12; O-08; E-01 | G-16 isolated spike only; production NOT_RUN |
| Content-free logs/session/cache/network inspection | G-15; E-05/E-06 | BLOCKED / NOT_RUN |
| README and docs match real implementation/screens | D-01〜D-12 | NOT_RUN; mock/unsupported claims prohibited |
| Education/privacy/security operations approvals | REL-03 | BLOCKED by EXT-03/04/05/06/07/09 |
| Independent signed review, blocker/high zero | EXT-08; REL-04 | BLOCKED; no release candidate |

## Requirements section 17 implementation-start gate

| Item | Required evidence | Current result | Gate effect |
|---|---|---|---|
| 17-01 generic ReportDefinition/schema/resource contract | G-10 | PASS at preflight contract level | none |
| 17-02 non-PII synthetic workbook/hostile/Unicode/formula vectors | G-11 | PASS at fixture-specification level | none |
| 17-03 signed Copilot operations policy/org/model/OAuth conditions | G-12/G-15; EXT-03 | BLOCKED; test-only trust is not production trust | BLOCK |
| 17-04 notice/contact/retention/lawful basis | G-13; EXT-04 | BLOCKED; all required rows NOT_PROVIDED | BLOCK |
| 17-05 human baseline/metrics/pre-threshold | G-14; EXT-05 | BLOCKED; synthetic metric spec is not educational evidence | BLOCK |
| 17-06 EU applicability/classification/responsibility/conformity | G-13; EXT-06 | BLOCKED; applicability itself NOT_PROVIDED | BLOCK |
| 17-07 exact SDK/CLI/hash/allowlist/session live canary | G-15 | NOT_RUN; direct prerequisite G-12 blocked | BLOCK |
| 17-08 Office/LibreOffice/Open XML/ICU/RID cross-platform evidence | G-16/G-17; EXT-07 | G-16 local document spike PASS; G-17 BLOCKED/NOT_RUN | BLOCK |

## Current disposition

- All 145 explicit P0 IDs extracted from `docs/requirements-definition.md` v1.2 are represented exactly once in the requirement tables above.
- Exact future file ownership remains normative in `docs-dev/preflight/implementation-task-file-map.md`; this record does not invent alternate production paths.
- G-15 is not started because G-12/EXT-03 is unresolved. Ambient GitHub/CLI credentials are not inspected or substituted.
- G-17 is not started because EXT-07 is unresolved. Current Windows x64 availability does not substitute for the required OS/CPU/signing/notary/print matrix.
- GATE-0 is **BLOCKED**. No local override exists, and `src/` must remain absent or contain zero files.
- Next permissible gate action is to serialize a BLOCKED GATE-0 result. Phase 1 tasks remain prohibited until the missing external evidence is supplied and all blocked preflight tasks pass.
