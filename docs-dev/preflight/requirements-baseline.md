# Requirements baseline integration record

| 項目 | 値 |
|---|---|
| Task | G0-03 |
| 状態 | **CURRENT BASELINE** |
| Requirement | `docs/requirements-definition.md` v2.0 |
| Plan | `work/20260831-implementation-plan.md` v3.0 |
| Scope decision | `docs-dev/adr/0010-single-input-local-scope.md` |
| Requirement content commit | `740736f0ccc5df2dbf94989227b50a0ea34f480e` |
| Plan content commit | `23c48392a124541386f7d0d880a576943b1ef0ad` |
| ADR commit | `84484fb912a474b9ec36e0e240407af3e7cfea01` |
| Previous baseline | v1.2 / plan v2.1 — **SUPERSEDED** |
| 記録日 | 2026-08-31 |

## Requirement owner direction

要求所有者は、実装と利用に必要な提供物をForms回答のExcelファイルだけに限定し、提示した設問・論点・Prompt例を編集可能な初期表示として実装するよう指示した。

確認質問へ回答が得られなかった項目はADR-0010と要求v2.0の安全側既定に固定した。

- 1回答者または1提出を1行とする。
- 設問ごとに必須レポート回答列、任意学生Prompt列、任意Prompt考慮事項列をmappingする。
- Prompt考慮事項が存在する場合は最優先基準として実Promptへの反映度を評価する。
- レポート候補点は既定0〜30、Prompt候補点は既定1〜10。
- AI点は候補であり、人が採用または上書きするまで確認済み点を空欄にする。
- 既存Copilot CLIのlogged-in user credentialsを使用する。
- 毎run、identifier除外と送信previewを確認する。
- 初版の検証済みtargetはWindows 11 x64だけとする。

## Artifact identity

| Artifact | Bytes | SHA-256 | Content commit |
|---|---:|---|---|
| `docs/requirements-definition.md` | 24,658 | `D401EEC4778E5A088D1674F65F5E036C4957713BADAC86B5D162584ABFB9FE31` | `740736f0ccc5df2dbf94989227b50a0ea34f480e` |
| `work/20260831-implementation-plan.md` | 19,726 | `63E0669916B27E0ADF2FD1A44435EE20EC664B57D624EA949DCD2B3AED0C504E` | `23c48392a124541386f7d0d880a576943b1ef0ad` |
| `docs-dev/adr/0010-single-input-local-scope.md` | 8,562 | `E6849DBFD0FE7C86782440E3C1AB99920AAE369F79D6D3D0B88DE7B2377038E6` | `84484fb912a474b9ec36e0e240407af3e7cfea01` |

SHA-256はexact bytesのintegrity anchorであり、組織電子署名、法務承認、本人確認を意味しない。

## Current scope

### Required user-provided information

| ID | Required value | Timing |
|---|---|---|
| INPUT-01 | Forms回答の標準Office Open XML `.xlsx` 1ファイル | アプリ利用時のfile selection |

開発、build、unit test、synthetic E2Eには実際の利用者workbookを要求しない。固定seedのsynthetic workbookを使用する。実workbookが提供された場合もrepositoryへcommitせずread-onlyで扱う。

### Runtime prerequisite, not a provided project input

AI評価時は、利用端末でGitHub Copilot CLIへ対話的にlogin済みである必要がある。loginは利用者がGitHub画面で直接行い、credential値を開発者、chat、repositoryへ提供しない。未認証時はAI評価を停止するが、buildおよびsynthetic fake-client E2Eは完了できる。

### Not required for initial implementation

次は初版scope外であり、未提供をblockerにしない。

- EXT-03 operational policy、org approval、managed OAuth App、production trust
- EXT-04 privacy／lawful-basis artifacts
- EXT-05 educational baseline／threshold／subgroup policy
- EXT-06 legal／EU classification artifact
- EXT-07 cross-platform／signing／notarization／print runners
- EXT-08 independent signed release review
- EXT-09 dump-policy attestation
- macOS、Linux、Windows Arm64実機
- production certificate、private key、signed installer
- 特定授業のPrompt／rubric／reason／evidence corpus
- local launcher identity／role approval

これらを不要とすることは、存在、承認、妥当性、法的適合、platform supportを主張することではない。

## Carried-forward safety invariants

旧v1.2の全scopeを維持せず、要求v2.0が明示する次だけを現行実装へ引き継ぐ。

1. 入力workbookを変更しない。
2. 暗号化／破損／macro形式をfail-closedで拒否する。
3. identifier、他行、他設問をCopilotへ送らない。
4. student／model文字列をExcel formulaとして書かない。
5. Copilot capabilityを評価用structured toolへ限定する。
6. sessionを評価単位ごとに分離し、明示削除する。
7. AI候補と人の確認済み点を分離する。
8. student content、reason、evidence、tokenをlogへ保存しない。
9. local launcher identityを認証・推測しない。
10. 実測していないOS、署名、性能、教育精度を主張しない。

## Superseded artifacts

ADR-0001〜0009、旧G-RB、旧G-18、旧GATE-0、G-12〜G-17 preflight artifactsは設計履歴として保持するが、要求v2.0のcurrent gate判定には使用しない。旧文書内の`BLOCKED`、EXT dependency、cross-platform release条件を新baselineへ伝播しない。

`docs-dev/traceability.md`と`docs-dev/preflight/gate-result.md`はG0-04／GATE-0で現行版へ置換する。旧commitはGit履歴から監査可能である。

## Gate readiness

| Check | Result |
|---|---|
| Requirement v2.0 committed | PASS |
| Plan v3.0 committed | PASS |
| ADR-0010 committed | PASS |
| Sole user input explicitly defined | PASS |
| Hidden external implementation input | 0 |
| Production `src/` files at this record | 0 |
| Current path map | `docs-dev/preflight/implementation-task-file-map.md` |
| Current traceability | G0-04 pending |
| Revised GATE-0 result | pending |

## Completion boundary

G0-03は、要求・計画・scope decisionのidentityと現行dependency boundaryを固定する。production実装、live Copilot結果、実workbook評価、Windows E2Eはまだ開始・実証していない。G0-04と改訂GATE-0がPASSするまで`src/`を作成しない。
