# schema不正（No.14）の根本原因と effort 記録（No.13）

## 依頼

- No.13: reasoning effort をジョブログに記録する。
- No.14: proxy 経由の実行で多発した schema 不正の根本原因を調査し、解消するまで修正する。

## 調査（証拠）

| 実験 | 条件 | 結果 |
|---|---|---|
| アプリ通し（一時診断コード、commitしない） | proxyなし、通常評価 claude-sonnet-5・medium | 通常評価 7 attempt 中 3 件 `InvalidPayload ROOT_MISSING_PROPERTY` |
| 同 tool引数の記録 | 同上 | 失敗3件とも root `EvaluatorId` が無い。`Criteria` は正しい |
| SDK直接probe（同じPrompt・payload、並列3） | medium | 10/30 不正（全件 `EvaluatorId` 欠落） |
| 同 | effort 未指定 | 2/30 不正（全件 `EvaluatorId` 欠落） |
| 同（修正後 parser） | medium | 30/30 受理（全件 `EvaluatorId` 省略） |
| 固有評価 probe（`SpecialEvaluationId`） | claude-sonnet-5・medium | 30/30 受理（欠落なし） |
| アプリ通し 3 回（修正後） | proxyなし、通常評価 claude-sonnet-5・medium | 45/45 attempt 成功、通常評価 15/15 が1回目で成功 |

- proxy は原因ではない（proxy なしで再現）。
- Copilot CLI は tool 引数を schema 検証しない。結果 tool は terminal のため、モデルに同一 session 内の再提出機会はない。
- `EvaluatorId` はアプリが payload で指定した単一値の定数（schema は単一値 enum）。
- `session.Rpc.Model.GetCurrentAsync()` は設定した effort を返すだけ（`auto`／未指定は `null`）。実効 effort は観測できない。

## 根本原因

claude-sonnet-5 が、結果 tool の root 必須項目 `EvaluatorId`（アプリが既知の定数）を一定確率で省略する。`medium` で頻度が上がる。

## 修正

- `EvaluationSchemaFactory`: root `required` を `["Criteria"]` に変更（`EvaluatorId` は properties と enum に残す）。
- `SubmitQuantificationTool.TryParseArguments`: 省略時は期待値で補う。返された値は従来どおり一致検証（`EVALUATOR_ID_MISMATCH`）。未知・重複項目の拒否は維持。
- Prompt は変更しない（返された場合も受理・検証するため不要。Prompt 契約文は画面の Prompt preview にも表示される）。
- 採用しなかった案: `EvaluatorId` の削除（Prompt 契約文の変更が必要で、返したモデルは未知項目で拒否される）。

## effort 記録

- `AttemptUsageSnapshot.RequestedReasoningEffort`（ジョブログの各 attempt）。値は `medium` または `null`（それ以外の文字列は `null` に落とす）。
- nullable 追加のみで `SchemaVersion` は 1 のまま。旧レコードは `null` として読む。

## 範囲外の観測（未対応・所有者判断）

- 利用者の `~\.copilot\installed-plugins\azure-skills\azure\hooks\hooks.json` の hook が、アプリの評価 session でも `userPromptSubmitted`／`sessionStart`／`preToolUse`／`postToolUse` で実行された（診断 event log で確認）。hook の入力には Prompt（回答本文を含む）と tool 引数が含まれる。アプリは `EnableFileHooks=false`、`PluginDirectories=[]`、`Hooks=null` を指定済み。

## 敵対的レビュー

| No. | 軸 | 重大度 | 指摘箇所 | 問題の説明 | 対応 |
|---|---|---|---|---|---|
| 1 | 内容の妥当性 | Major | detailed-design §5.1 初稿 | 「Prompt hash を変えないため」と記載したが、コードに prompt hash は存在しない（`PromptHash` 等の検索で 0 件）。根拠のない理由 | 理由を「返された場合も受理・検証するため変更不要」に修正 |
| 2 | 目的適合性 | Major | 固有評価 `SpecialEvaluationId` | 同じ定数 ID を返させる固有評価は通常評価 model（claude-sonnet-5・medium）で動くため、同じ欠落が起きるか未確認だった | probe で同条件 30/30 受理を確認。変更なしとし、設計へ記録 |
| 3 | 整合性 | Minor | ADR-0005 「全property required」 | 新しい任意項目と矛盾して見える | ADR-0005 は HISTORICAL / SUPERSEDED（旧 `submit_evaluation`）。変更不要 |
| 4 | 品質・運用性 | Minor | `JobUsageTracker` の `"medium"` literal | `SdkEphemeralCopilotTransport.MediumReasoningEffort` と重複 | 未修正。Usage 名前空間は Copilot 名前空間に依存していないため、依存を増やさない |
| 5 | 品質・運用性 | Minor | session → ジョブログの配線 | `SdkEphemeralCopilotSession` の effort 配線に unit test がない（既存の `RequestedModelKey` も同様に unit test なし） | 未修正。実機 3 run の jsonl で normal=`medium`、auto=`null` を確認済み |
| 6 | 整合性 | Minor | Prompt と schema | Prompt は evaluator ID を返すよう指示し、schema は任意 | 意図どおり（返せば検証、省略なら補完）。設計に明記 |
| 7 | 根拠性 | Minor | 「medium で増える」 | 10/30 対 2/30 の 1 回の比較（n=30） | 記述を観測値の提示に留め、断定的な因果は書かない |
| 8 | 目的適合性 | Minor | reference／similarity の `QuestionId` | 同種の定数 ID | `auto` で失敗の観測なし（修正後の実機 3 run で参照回答・類似度 30 attempt 全件成功）。YAGNI で変更しない |

サマリー: Critical 0 / Major 2（修正済み）/ Minor 6。合格判定: PASS

## push 後の CI（`ff4ae22`）

- CI run 36056059441: macOS 2 job は success。Windows job は deterministic tests が成功（TRX: 1881 passed、failed 0）したが、単一 EXE の step が `P07_FAIL stage=p06-trx code=P07_TRX_TEST_NOT_PASSED` で失敗した。1 回目と `gh run rerun --failed` 2 回の計 3 回とも同じ結果。P07 が失敗したため、後続の unsigned MSIX と repository integrity の step は実行されていない。
- ローカル再現（detached worktree `C:\GitHub\sre-ci4`、`ff4ae22`）: ci.yml と同じ順序（restore --locked-mode → build Release → publish -SingleFile → package → test-windows-singlefile）で実行し `PASS_REQUIRED`（7/7、sourceStatusEntryCount 0）。`test-windows-msix-unsigned.ps1` は `PASS_MECHANISM`。`git diff --check HEAD~1 HEAD` の exit は 0。
- 判断: P06 の 7 シナリオは起動・展開・cache に関するもので、今回の変更（評価 tool の schema と parser、ジョブログの項目）は通らない。記録だけの commit `9aab53f` でも同じ code で失敗していた（`work/20260924-1815-RemainTaskExecutionRecord3.md` の RR-00、BLOCKED）。以上から、既知の P07 の断続的な失敗と判断した。ただし CI は P06 の生 TRX を残さないため、失敗したシナリオは分からない。テストも ci.yml も変えていない。
