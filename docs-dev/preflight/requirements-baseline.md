# Requirements baseline integration record

| 項目 | 値 |
|---|---|
| Task | G-RB |
| 状態 | **完了・baseline content commit anchored** |
| Requirement | `docs/requirements-definition.md` v1.2 |
| Plan | `work/20260831-implementation-plan.md` v2.1 |
| Base commit | `bc12522864235b9089dd474560d6222973eb2e92` |
| Baseline content commit | `a06dc33639fbb904b14bc0ed0ae853bd7841e1a8` |
| 記録日 | 2026-08-31 |

## Requirement owner correction

要求所有者は、次を明示的に訂正した。

1. 実際のreason/evidenceはreport種類ごとにアプリ内で指定するため、実装前に作成しない。
2. missing flagsはreport種類ごとにアプリ内で指定する。
3. 最大rubric item数、evaluator/item ID最大長、target languageはreport作成時に変更できる設定とする。
4. これらの設定に教員責任者・製品責任者approvalを要求しない。
5. local application launcherの本人性・roleはapplication scope外とする。

反映時の解釈はADR-0009に固定した。実際のreason/evidence本文は学生回答ごとにmodelが生成し、ReportDefinitionが保存するのはinstruction/validation rule/maxだけである。GitHub OAuth account確認はCopilot service authorizationのために維持し、local launcher identityへ転用しない。

## Decision disposition

| Decision/artifact | 判定 | v1.2への反映 |
|---|---|---|
| ADR-0002 / DEC-02 | APPROVED | workbook internal manifest digest + signed detached whole-file completion record |
| ADR-0003 / DEC-03/04 | APPROVED | value/static projection、external/data-table formula拒否、closed Open XML mutation allowlist |
| ADR-0004 / DEC-05/06/21 | APPROVED AS AMENDED | ROUND、status mapping、calibration-selected metric。authenticated actorはADR-0009により撤回 |
| ADR-0005 / DEC-07/08 | APPROVED | evidence-only student tool argument exception、result tool permission bypass、other requests Reject |
| ADR-0005 old DEC-09 | SUPERSEDED | fixed release constants / EXT-01 corpus / educator-product approvalを撤回 |
| ADR-0009 / DEC-09/23 | APPROVED | runtime ReportDefinition、dynamic closed schema、hard ceilings、launcher identity out of scope |
| ADR-0006 / DEC-12〜14 | APPROVED | strict JWS/JCS、AES-GCM checkpoint profile |
| ADR-0007 | APPROVED | signed runtime endpoint manifest、expiry/rollback、telemetry/experimentation deny |
| ADR-0008 / DEC-15/16/20 | APPROVED FOR CANDIDATE MATRIX ONLY | final Wayland/print/package choice remains G-17 blocked |
| G-09 / DEC-22 | APPROVED | P1-01〜P1-10 IMPLEMENT、DEFER 0 |

## Runtime ReportDefinition boundary

- ReportDefinitionはlocal runtime settingであり、signed operational policyではない。
- report type/revision、target language、Prompt/rubric/anchor、reason/evidence policies、missing flags、item/ID/field limits、weights/rounding/aggregate ASTを持つ。
- student answer、actual generated reason/evidence、OAuth/token、local launcher、approverを持たない。
- run開始時にcanonical snapshot/hashへ固定し、generated schema、checkpoint、cache、manifestへ結合する。
- productは教育用途のlimitを決めず、hard ceilingとactual serializer preflightだけを強制する。

## Artifact identity

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `docs/requirements-definition.md` | 96,304 | `CBBD60FDD8112B2E09E8E38F17C6869F3302E60910F798D3F1FDA49F4858BDF9` |
| `work/20260831-implementation-plan.md` | 39,469 | `22B438267FF356F84A9CC175C9E73805A4C7D576D1EE20E0167E0C652D7FDAA5` |
| `eng/schemas/report-definition-v1.schema.json` | 10,234 | `BB68EB90549C6487088FD5F96D61D94578850622FEB3ED9978FE1976649C997C` |
| `docs-dev/adr/0009-runtime-report-definition.md` | 9,713 | `0E7C10B71BED12B89927E5B16B4EF0216ECA2FE1C092140565961E95B3192D63` |
| `docs-dev/preflight/report-definition-contract.md` | 9,101 | `0E329457B9B2290CD6F25E7269DF5F90AB9A51D2300B232B94B8C074556775BD` |
| `docs-dev/preflight/implementation-task-file-map.md` | 23,757 | `3C0C0D69EA65F8F4BD46B0C8ECC3BE929515673A32C64188C4A0518337BD8D50` |
| synthetic JA schema vector | 2,903 | `9658BB80242B5E4F996A17E151F1F11DC3E469BE84D37BA599C0B7F35023D734` |
| synthetic EN schema vector | 2,236 | `9BC498D5347A9849DEC122208B16AA3FAC1912634AB8B2816137CB0A0107E98B` |

The byte counts and SHA-256 values identify the exact artifact bytes stored in baseline content commit `a06dc33639fbb904b14bc0ed0ae853bd7841e1a8`. At anchor-record update time, `git diff --quiet <anchor> -- <artifact>` confirmed that all eight listed working-tree artifacts were identical to their commit-tree versions. SHA-256 is an integrity value, not an organizational signature or signer identity.

## Block release table

| Surface | Previous blocker | v1.2 result | Remaining blocker |
|---|---|---|---|
| G-05 / DEC-09 | EXT-01 corpus + educator/product approval | **UNBLOCKED and complete** | none at decision level |
| G-10 | lesson-specific evaluation design hash | **contract-complete**: generic ReportDefinition schema/contract/2 synthetic vectorsを確定。production/operational実装完了は主張しない | GATE-0には別途EXT-02〜09、G-11〜G-18が必要 |
| T-03/T-06/C-01 | fixed release field constants | dynamic config + actual serializer preflight | GATE-0 before production implementation |
| L-12 | EXT-01 aggregate formula | runtime closed AST | GATE-0 |
| A-03/A-10/A-14 | allowed local user/launcher identity | GitHub account/org authorization only | EXT-03/04/05/09 and GATE-0 |
| R-10/R-11 | authenticated actor | explicit action/timestamp + optional unverified label | GATE-0 |
| GATE-0 | all Phase 0 evidence | **still BLOCKED** | EXT-02〜09 as applicable、G-11〜G-18 |

## Gate integrity

- `src/` production code remains prohibited until GATE-0 PASS.
- Plan v2.1のtask表で省略したfuture pathは`docs-dev/preflight/implementation-task-file-map.md`をnormative mapとして解決し、alternate pathを実装時に発明しない。
- This baseline does not claim production implementation, live Copilot behavior, Office recalculation, OS support, signing, notarization, accessibility, performance, educational validity, or legal approval.
- Specific report definitions are created after launch and therefore are not GATE-0 inputs.
- Institutional real-data governance remains separate and is not removed by the launcher-identity correction.

## Completion condition

Requirement content integration and owner-directed semantic correction are complete. G-RB is anchored by baseline content commit `a06dc33639fbb904b14bc0ed0ae853bd7841e1a8`. This record update is intentionally a later commit so the recorded hash does not create a self-reference. The anchor does not satisfy G-11〜G-18 or imply GATE-0 PASS.
