# 2026-09-25 Uniform Reasoning Effort 実装計画

## 目的

採点の一貫性を上げるため、1回のrunに含まれるLLM operationが同じreasoning effortを使うようにする。対象は通常評価、固有評価、参照回答生成。類似度評価は別agentがLLMからローカル計算へ置換中のため、この計画では触れない。

## Fact-check

### SDK 1.0.11の仕様確認

- `%USERPROFILE%\.nuget\packages\github.copilot.sdk\1.0.11\README.md`の`CreateSessionAsync(SessionConfig)`説明では、`ReasoningEffort`は対応model向けの設定で、例として`low`、`medium`、`high`、`xhigh`、`max`を挙げ、`ListModelsAsync()`で対応可否を確認するよう記載している。
- `lib\net10.0\GitHub.Copilot.SDK.xml`では、`SessionConfigBase.ReasoningEffort`はreasoning effort対応modelにだけ適用される。RPC `SwitchToAsync`の説明では、CAPIの値はmodel-definedで選択modelに対して検証され、BYOK providerは追加値を定義し得る。`none`はreasoningを無効化し、未指定ならoverrideしない。
- 同XMLは`ModelSupports.ReasoningEffort`、`ModelInfo.SupportedReasoningEfforts`、`ModelInfo.DefaultReasoningEffort`を公開している。
- したがって、アプリ側は`auto`や非対応modelへeffortを送らず、model一覧から選択modelの対応値だけを使う必要がある。実効effortはSDKから完全には観測できず、アプリが指定した値を記録する。

### ローカルmodel一覧の測定

`CopilotClientFactory`を使う一時console probeを`work\effort-model-probe`に作成し、prompt送信なしで`ListModelsAsync()`だけを試みた。結果は`creation=CliUnavailable`で、この環境では同梱CLIを利用できず、実modelごとの`SupportedReasoningEfforts`と`DefaultReasoningEffort`は測定できなかった。probeは削除対象の一時作業物とする。

### `auto`の扱い

`auto`はrouterであり、SDK docs上もmodel-specificなeffort対応値を確認できない。既存実装でも`auto`へeffortを送るとsession作成が拒否される前提でunsetにしていた。したがって、run全体でreasoning effortを揃えるには、参照回答も通常評価と同じ選択modelを使う必要がある。選択model自体が`auto`の場合は、全operationを未指定（null）で揃える。

## 方針

1. run開始時に選択modelからrun-level reasoning effortを1つ解決する。
2. 既定希望値は`low`とする。所有者が「低いeffortでよい」と述べており、`low`はreasoningを維持する最小の通常値である。`none`はreasoningを無効化するため既定にはしない。
3. fallback順序は`none < minimal < low < medium < high < xhigh < max`。希望値が非対応なら最も近い対応値を選ぶ。同距離では高い側を優先し、`none`へ落ちにくくする。
4. 選択modelが`auto`、非対応、対応effort未列挙、一覧にない場合はnullをrun-level値とし、Reference／Normal／Specialすべてでunsetにする。
5. 解決処理は`IEnumerable<ModelInfo>`＋希望値のpure functionにする。将来、別agentのshared per-run model cacheへ移動しても同じ関数を呼べる。
6. 設定UIには今回は項目を増やさない。希望値は定数`low`にし、保存形式の互換性を保つ。利用者が誤ってmodel-defined値を入力する導線を作らない。

## 実装手順

1. `ReasoningEffortPolicy`を追加し、safeなeffort識別子判定、既定希望値、fallback順序、`ModelInfo`列挙からの解決を実装する。
2. 認証時の`CopilotModelAvailability`へ`SupportedReasoningEfforts`と`DefaultReasoningEffort`を取り込み、実行開始時の選択modelから`ReasoningEffortPolicy`を呼ぶ。
3. `EphemeralEvaluationRunner`、`ReferenceAnswerEvaluationRunner`、`SpecialEvaluationRunner`へreasoning effort引数を追加し、`SessionConfig.ReasoningEffort`へrun-level値を設定する。transportはsessionごとの`ListModelsAsync()`をやめ、`auto`だけは安全のためnullへ戻す。
4. `QuantificationRunRequest`に`ReasoningEffort`を追加し、`QuantificationRunBoundary`でnormal runner、reference runner、special runnerへ同じ値を渡す。
5. 参照回答生成のmodelを固定`auto`からrunの選択modelへ変更する。Similarity runnerには触れない。
6. checkpointへ`ReferenceModelId`（run model）と`ReasoningEffort`を保存し、resume admissionでmodel、ReferenceModelId、ReasoningEffortの一致を確認する。不一致は`CHECKPOINT_MODEL_MISMATCH`として開始前に止める。
7. Run sheet、Reference sheet、ジョブログcontext／attempt、実行条件表示へrun-level effortを記録・表示する。ジョブログは`medium`固定ではなくsafeなeffort文字列を許可する。
8. detailed-design、README、features、CHANGELOG、implementation-statusを日本語で更新する。
9. unit testsを更新し、`dotnet build .\StudyReportEvaluator.slnx -c Release`と`dotnet test .\StudyReportEvaluator.slnx -c Release`で検証する。

## 互換性とリスク

- 新規partial checkpointは`ReferenceModelId`がrun modelになり、`ReasoningEffort`がnull以外なら保存される。古いcheckpointの扱いはresume admissionで明示的なmodel／effort一致条件に従う。
- SDKの値はmodel-definedであり、将来`minimal`やBYOK固有値が出ても、safeな文字列として記録できる。ただしfallback順序にない値は希望値と完全一致する場合だけ優先し、それ以外は順序内の値から決定する。
- `auto`を選んだrunではeffortはnullで統一されるが、routerの実効model・実効effortは引き続き観測できない。
