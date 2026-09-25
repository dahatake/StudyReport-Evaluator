# 2026-09-26 並列度・reasoning effortの実測記録

## 目的

並列度の既定値・上限と、run-level reasoning effort（希望`low`）の妥当性を、合成Promptだけで確認する。学生データ・実設定のPromptは使っていない。

## 方法

- 一時console probe（commitしない。セッション作業領域に作成）から、SDK `GitHub.Copilot.SDK 1.0.11`と、ビルド出力に同梱されたCLI（`copilot.exe`。`--version`は`1.0.88`を報告）を直接使った。
- Prompt: `Reply with exactly: OK`。tool 0件、permission全拒否、attemptごとに新しいsession ID、送信後に`DeleteSessionAsync`。
- model: `claude-sonnet-5`、reasoning effort: `low`。
- cold: attemptごとにclient作成→`StartAsync`→`GetAuthStatusAsync`→`ListModelsAsync`→session作成→送信→削除→停止（変更前の方式）。
- shared: 1つのclientを起動・認証した後、attemptごとにsessionだけを作成→送信→削除（変更後の方式）。

## 結果

### model一覧（`ListModelsAsync`、token消費なし）

- reasoning effort対応modelは、すべて`SupportedReasoningEfforts`に`low`を含む（例: claude-sonnet-5／claude-opus系は`low,medium,high,xhigh,max`、gpt-5.x系は`none,low,…`、gemini-3.5/3.6-flashは`minimal,low,medium,high`）。したがって希望`low`は、対応modelでは常に`low`へ解決され、fallbackは起きない。
- `auto`、claude-haiku-4.5、gemini-3.7/3.8-flashは`supports.reasoningEffort=false`で、run全体が未指定になる。
- `DefaultReasoningEffort`はどのmodelも空だった。

### cold（変更前）とshared（変更後）

| 方式 | 1 attemptの所要時間 |
|---|---|
| cold（3回） | 11.0〜14.0秒（うちCLI起動〜model一覧が約2.6〜2.9秒、session作成〜応答が7.2〜9.7秒） |
| shared、並列1（12回） | 中央値5.0秒 |

### shared clientの並列度別スループット

| 並列度 | 件数 | 所要時間 | スループット | 1件の中央値／最大 | 失敗 |
|---:|---:|---:|---:|---:|---:|
| 1 | 12 | 65.8秒 | 10.9件/分 | 5.0秒／9.9秒 | 0 |
| 4 | 24 | 48.3秒 | 29.8件/分 | 7.1秒／11.9秒 | 0 |
| 8 | 24 | 31.0秒 | 46.4件/分 | 9.7秒／12.3秒 | 0 |
| 16 | 24 | 29.3秒 | 49.2件/分 | 17.1秒／21.1秒 | 0 |

## 判断

- 並列8でスループットはほぼ頭打ちになり（16は+6%）、1件あたりの待ち時間は16で約2倍になる。1つのCLI processを共有するため、並列度を上げるほどprocess内・server側で待ちが増えると考えられる。
- 既定値は **8**（実測の頭打ち点）とする。上限は **16** とし、利用者が明示した場合だけ使う。SDKにsession並列数の上限はないため、上限値はアプリの判断である。
- 並列度はtoken消費と回答内容を変えない（同じPrompt・model・effortを送るだけ）。rate limitで再試行が増えるとtokenが増えるため、rate limit時はAIMDで有効並列度を半減し、backoff付きで再試行する。
- 制約: 測定は短いPromptだけで、採点用の長いPromptと応答では1件の所要時間が長くなる。16並列では1件の待ちがSDKの応答待ち60秒に近づき、timeoutと再試行が増える恐れがあるため、既定にはしない。rate limitはこの測定の範囲では発生しなかった。
