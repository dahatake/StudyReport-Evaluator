# GATE-0 preflight result

| 項目 | 値 |
|---|---|
| Task | GATE-0 |
| 判定 | **BLOCKED** |
| Phase 1 authorization | **NOT GRANTED** |
| Requirement baseline | `docs/requirements-definition.md` v1.2 |
| Plan | `work/20260831-implementation-plan.md` v2.1 |
| Baseline content commit | `a06dc33639fbb904b14bc0ed0ae853bd7841e1a8` |
| Baseline anchor record commit | `921edc0eb2be13edad1db0688a63bb8fa555fa6d` |
| Evaluation snapshot HEAD | `df98ea0e29c6b66afd30a38ee369f53821a8436c` |
| G-18 traceability commit | `df98ea0e29c6b66afd30a38ee369f53821a8436c` |
| Production `src/` file count | `0` |
| 記録日 | 2026-08-31 |

## Decision rule

GATE-0は、次の全条件を同時に満たした場合だけ`PASS`とする。

1. G-RBで承認・固定した要求改版がcommitへ結合されている。
2. `docs/requirements-definition.md` v1.2第17節の8項目がすべて`PASS`である。
3. G-10〜G-18の各直接依存が完了している。
4. `src/`配下のproduction fileが0件である。
5. test-only、synthetic、local、candidate、`NOT_RUN`、外部入力待ちをproductionまたはcross-platformの`PASS`へ読み替えていない。

現在は、要求baselineと`src/`ゼロ件は成立しているが、第17節は2/8項目だけがpreflight水準で`PASS`し、6項目がblocking resultである。したがって論理積はfalseで、最終判定は**BLOCKED**である。`NOT_RUN`は`PASS`ではなく、local overrideも設けない。

## Status vocabulary

| Status | Gate meaning |
|---|---|
| `PASS` | 当該GATE-0項目が要求するPhase 0証拠を検証済み。後続production実装やrelease受入の完了を意味しない。 |
| `BLOCKED` | 必須external input、owner decision、または直接依存が不足している。GATE-0をblockする。 |
| `NOT_RUN` | 必要な実行証拠をまだ生成していない。GATE-0ではblocking resultとして扱う。 |
| `PARTIAL_BLOCKED` | test-onlyまたは限定環境の部分証拠だけが存在する。行全体は`BLOCKED`であり、部分証拠を一般化しない。 |

## Requirement revision and evidence inventory

| Task | Current result | Evidence identity | Boundary |
|---|---|---|---|
| G-RB | `PASS` | `docs-dev/preflight/requirements-baseline.md`; commit `921edc0eb2be13edad1db0688a63bb8fa555fa6d`; SHA-256 `7F5C6276E3F01B3C368A23439002603B53E025C6DA7DE65A01B7445A364C63F9` | v1.2 content is anchored to commit `a06dc33639fbb904b14bc0ed0ae853bd7841e1a8`; the hash is an integrity anchor, not an organizational signature. |
| G-10 | `PASS` | `docs-dev/preflight/report-definition-contract.md`; commit `a06dc33639fbb904b14bc0ed0ae853bd7841e1a8`; SHA-256 `0E329457B9B2290CD6F25E7269DF5F90AB9A51D2300B232B94B8C074556775BD` | Generic runtime ReportDefinition contract only; production schema factory, serializer preflight, UI, and storage are not implemented. |
| G-11 | `PASS` | `docs-dev/preflight/fixture-plan.md`; commit `2554b7d7a148717430059ab54302c5d3df6b9400`; SHA-256 `12F382204F2BE47B8D235118998EDEFDCEC2FA8321D0A400078CCF46782DFED6` | Synthetic fixture specifications only; production generators and full E2E fixtures remain later tasks. |
| G-12 | `PARTIAL_BLOCKED` | `docs-dev/adr/0006-signed-records.md` and four test-only JWS fixtures; commit `b4e32ab5eb83f612ac06bb1c22f9564694e89642`; ADR SHA-256 `E508FC47B46F6E2033C77CFA08C83B9FA8AF32DF8C6BF6E11960588A1366C18D` | EXT-03 production issuer, trust root, policy, OAuth/org/model, retention/location/cost input is absent. Test keys and synthetic policy values are never production trust. |
| G-13 | `BLOCKED` | `docs-dev/preflight/governance-inputs.md`; commit `2b16986a1d3f22439aabf96df22d1680aebbf8d7`; SHA-256 `5EFB21695D26F8DB1F73DD1D21717CE70F209E0D0826CC4F7C02917276777CA6` | EXT-04の4 rowsとEXT-06の4 rowsはすべて`NOT_PROVIDED`。external SHA-256、owner reference、validity、region、本文を捏造していない。 |
| G-14 | `PARTIAL_BLOCKED` | `tests/fixtures/evaluation/metric-spec-test.json`; commit `e715fe53a57f1e3ec432e03eb9045340a1c8b8d0`; SHA-256 `88582D01922DC0382AA97A01005D15DD23A90DDE7C49489317D1379137A3EA52` | Synthetic test oracle and test-only thresholds only; EXT-05 target-course baseline、metric spec、pre-threshold、subgroup policyはabsent。 |
| G-15 | `BLOCKED / NOT_RUN` | `spikes/CopilotContract/*` and `docs-dev/preflight/copilot-contract.md` are absent | G-12/EXT-03が直接blocker。ambient GitHub、Copilot CLI、`gh`、environment credentialを探索・代用していない。 |
| G-16 | `PASS` | `docs-dev/preflight/document-pipeline.md` and isolated spike; commit `618da0900cdeafb8d62df00bacc089494b3a66e8`; record SHA-256 `CE189797BCE1500911B3BBBF114E6D2F598AE3BE62B64F82F005F705658CF063` | Measured Windows x64 Open XML/ICU/Excel/LibreOffice preflight only; production writer、macOS/Linux、release RC evidenceではない。 |
| G-17 | `BLOCKED / NOT_RUN` | `spikes/PlatformPreflight/*`, `docs-dev/preflight/platform-matrix.md`, and `eng/platform-matrix.json` are absent | EXT-07 signing、notarization、Arm64、Office/print runner evidenceがabsent。候補matrixを採用済みplatform claimへ昇格しない。 |
| G-18 | `PASS` | `docs-dev/traceability.md`; commit `df98ea0e29c6b66afd30a38ee369f53821a8436c`; SHA-256 `56972193AADFAFE9BA066F083BD45028B9158079E789AB11C920964F74AAE9F2` | 145 explicit P0 IDsをexactly onceでmapping。mapping completenessはupstream blockerを解消しない。 |

## Requirements section 17 evaluation

| Item | Required evidence | Actual result | Gate effect |
|---|---|---|---|
| 17-01 generic ReportDefinition/schema/resource contract | G-10 | `PASS` at preflight contract level. Runtime report values、approval、launcher identityを外部入力にしていない。 | none |
| 17-02 non-PII synthetic workbook/hostile/Unicode/formula vectors | G-11 | `PASS` at plan v2.1 fixture-specification level. G-11のsynthetic/PII boundaryとcase contractsを検証済み。 | none |
| 17-03 signed Copilot operations policy/org/model/OAuth conditions | G-12/G-15 and EXT-03 | `BLOCKED`. Test-only JWSは存在するが、production policy/trust/org/model/OAuth/retention/location/cost evidenceは`NOT_PROVIDED`。 | **BLOCK** |
| 17-04 notice/contact/retention/lawful basis | G-13 and EXT-04 | `BLOCKED`. `EXT-04-NOTICE`、`EXT-04-LAWFUL-BASIS`、`EXT-04-RETENTION-ACCESS`、`EXT-04-CONTACT-REDRESS`の4 rowsはすべて`NOT_PROVIDED`。 | **BLOCK** |
| 17-05 human baseline/metrics/pre-threshold | G-14 and EXT-05 | `BLOCKED`. Synthetic metric oracleはproduction educational validity、target-course baseline、approved thresholdを証明しない。 | **BLOCK** |
| 17-06 EU applicability/classification/responsibility/conformity | G-13 and EXT-06 | `BLOCKED`. EU適用・非適用のいずれも推測せず、external legal applicability decisionは`NOT_PROVIDED`。 | **BLOCK** |
| 17-07 exact SDK/CLI/hash/allowlist/session live canary | G-15 | `NOT_RUN`. G-12/EXT-03が未充足のため、approved auth/org/model/manifest条件なしにlive canaryを開始していない。 | **BLOCK** |
| 17-08 Office/LibreOffice/Open XML/ICU/RID cross-platform evidence | G-16/G-17 and EXT-07 | `PARTIAL_BLOCKED`. G-16の限定Windows spikeは`PASS`だが、G-17 cross-platform/signing/notary/Arm64/print/package matrixは`NOT_RUN`。 | **BLOCK** |

### Aggregate

| Check | Actual |
|---|---:|
| Section 17 items | 8 |
| Passing items | 2 |
| Blocking items | 6 |
| `NOT_RUN` items counted as PASS | 0 |
| Local/test-only evidence promoted to production evidence | 0 |
| GATE-0 result | **BLOCKED** |

## External input disposition

| External input | Current status | Required next evidence |
|---|---|---|
| EXT-02 | Satisfied for G-11 preflight scope | Keep synthetic-only boundary; T-08 and later E2E remain future work. |
| EXT-03 | `NOT_PROVIDED` | Verified production issuer/trust/policy、OAuth App client ID/scope、GitHub org、approved model、retention/location/cost、validity/revocation metadata. |
| EXT-04 | `NOT_PROVIDED` | Verified notice、lawful basis、retention/access、contact/correction/appeal metadata. |
| EXT-05 | `NOT_PROVIDED` | Verified human baseline、metric specification、measurement-before-threshold、subgroup policy metadata. |
| EXT-06 | `NOT_PROVIDED` | Verified EU applicability decision and every classification/responsibility/conformity row it makes required. |
| EXT-07 | `NOT_PROVIDED` | Production-equivalent signing/notarization、Arm64、Office/LibreOffice、print、clean setup/uninstall runner evidence. |

EXT-08 independent signed release reviewとEXT-09 dump-policy attestationはGATE-0の第17節判定へ代入しないが、後続release/real-data gateで未充足のままである。本記録はそれらを`PASS`としない。

## Fail-closed assertions

- `src/`は存在しないか、配下file数が0でなければならない。評価時の実測は`0`である。
- Phase 1のF-01以降、正式production/test project、配布物、実データ処理を開始しない。
- G-15の代わりにambient GitHub/Copilot CLI/`gh`資格情報、environment token、個人accountを使用しない。
- G-12 test key/fixtureをproduction issuer、機関承認、Business/Enterprise条件として扱わない。
- G-14 test-only閾値を教育評価責任者の事前登録値として扱わない。
- G-16 Windows x64結果をmacOS、Linux、Arm64、production package、印刷、署名、notarizationへ一般化しない。
- ReportDefinitionの作成・保存・実行確認にlocal launcherの本人性、role、teacher/product approvalを追加しない。
- 外部artifact本文、秘密、token、署名用private key、実在学生dataを不足証拠の代替としてrepositoryへ保存しない。
- GATE-0 failureにlocal overrideを設けない。

## Re-evaluation conditions

GATE-0は次の順でのみ再評価する。

1. EXT-03を外部ownerから受領し、production trustとtest trustを分離してG-12を完了する。
2. 完了したG-12を直接依存としてG-15 live synthetic canaryを実行し、exact SDK/CLI/hash、auth、tool、permission、telemetry、session deletion、usageを検証する。
3. EXT-04/EXT-06を外部ownerから受領し、本文をrepositoryへ保存せずG-13のrequired metadata rowsを検証する。
4. EXT-05を外部ownerから受領し、結果を見る前のmetric/threshold/subgroup policyとhuman baselineを検証してG-14を完了する。
5. EXT-07のproduction-equivalent runner/trust inputsでG-17を実行し、各OS/CPU/display/print/package rowを独立判定する。
6. 変更されたrequirement、evidence、blockerをG-18へ反映し、145-ID completenessと第14/17節mappingを再検証する。
7. 第17節8項目がすべて`PASS`し、approved requirement revision commitが不変で、`src/` file countが0であることを再確認する。

要求を満たせない場合は、不足証拠を補う、要求を正式改版する、または対象platform/feature/projectを停止する。未実行や限定証拠を読み替えて先へ進めない。

## Final disposition

**GATE-0 = BLOCKED.** Phase 1は許可されない。次の許可作業は、上記external inputsの受領・検証、直接blockされているG-12/G-13/G-14/G-15/G-17の完了、対応するG-18更新、およびGATE-0再評価だけである。
