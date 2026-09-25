# Changelog

StudyReport Evaluatorの利用者に影響する変更をこのファイルへ記録します。版番号は[Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)に従い、公開済み版の内容は変更しません。

## [Unreleased]

主画面と詳細設定を分け、設定の再利用と設問・結果の確認を改善し、通常評価モデルとして`auto`を選べるようにした、ソース候補`0.8.6`の**未公開**変更です。追加の4画面実機確認は入力画面で対象行を一意に特定できず停止（FAIL）しており、**製品UIの実機検証済み・公開可能とは扱いません**。Narrator・本人による操作確認・隔離ユーザーでの設定保存も外部条件待ちです。採点アルゴリズムと、Base＋Special＋全有効設問の配点合計を正確に100点とする検証は従来どおり維持します。中断・再開の導線を追加した後に崩れていた実行画面の配置と、再開モード以外で余分に出ていた再検証の通知も修正しました。再起動後に再開するための手順を利用者ガイドへ補い、開発用MSIXの検証scriptが同梱文書を見つけられず失敗していた問題も修正しました。明示モデルでの実行と、実行中にCopilot CLIが終了した場合の扱いを実機で追加確認し、開発者向けの実装状況を更新しました（この確認自体は製品コードの変更なし）。また、AIの応答待ちをCopilot SDKの既定と同じ60秒に揃え、対応するモデルではreasoning effortを`medium`に指定するようにし、送信中の通信失敗が再試行されない問題とCopilot CLIの起動前確認が遅い問題を修正しました。さらに、claude-sonnet-5などで通常評価がschema不正（`AI_OUTPUT_INVALID`）になる問題を修正し、試行ごとに指定したreasoning effortをジョブログへ記録するようにしました。
### Added

- 実行中／終了後に、GitHub Copilot SDKから観測できた入力・出力token、推論／cache内訳、`nano-AI units`、premium request消費量を、実行・結果画面とジョブ単位のローカルJSON Linesログで確認できるように追加。未取得値は0とせず、請求確定額・通貨・アカウント全体の利用量として扱わない。再試行ごとの試行番号・相関ID・終端結果（成功・スキーマ不正・通信失敗・timeout・認証要求・取消・後始末失敗）もジョブ単位のログへ記録し、数値観測の状態（部分取得・観測完了）は変更しない。項目ごとの取得元（イベント／最終RPC／最後の呼び出しのみ）と、モデル内訳とセッション総量の不一致も表示し、不一致を配賦・補正で一致させない。
- ジョブログ（JSON Lines）の各試行に、アプリが指定したreasoning effort（`RequestedReasoningEffort`。`medium`、未指定は`null`）を記録。`auto`や非対応モデルで実際に使われたeffortはSDKから観測できないため記録しません。
- run内で同梱Copilot CLI processを共有し、attemptごとに一時sessionを分けたまま`ListModels`結果を再利用する性能改善を追加。rate limitは専用statusとしてbackoff付きで再試行し、有効並列度を下げます。quota exhaustedは無限再試行せずpartialを残して停止します。
- 実行を中断した後、保存済みpartial checkpointから再開準備を行う導線を追加。再開元の選択、開始前の入力／採点設計／モデル／runtime／checkpoint構造の項目別確認、入力・モデルだけの明示的な条件合わせ込み、window close時の有限な中断待機を提供。再開は引き続き利用者が「定量化を開始」を選ぶまで開始しません。
- 同じウィンドウ内に、共通／入力詳細／通常評価／固有評価／読込Promptの5カテゴリの[設定画面](docs/settings.md)を追加。
- 共通設定と任意の採点定義1件の手動保存・読込を追加。保存先はOSのLocalApplicationData配下`StudyReportEvaluator\setting.txt`（Windowsでは通常`%LOCALAPPDATA%\StudyReportEvaluator\setting.txt`）、UTF-8の平文JSON（設定用schema `1`、暗号化なし）。保存定義はExcel読込後に明示適用し、ID・順序・設問文・Prompt・配点を保持。入力との整合を検証し、適用失敗・取消では現在の編集と設定ファイルを保持。
- Windows 11 x64向け.NET 10 self-containedのunsigned単一EXEとSHA-256 sidecarを将来の主配布候補に追加し、ZIP／sidecarは代替経路を維持。EXE・ZIP・非公開開発用MSIXの3形式をmatrix v2で限定し、公開4 asset・clean-host証跡・承認付き公開の条件を満たさない場合は公開しない契約を追加。
- Execution画面に、検証済み同梱Copilot CLIだけを直接起動する「GitHubにログイン」と取消操作を追加。認証成功は既存の状態確認で明示的に再確認。
- 採点設計にBase・Special・類似度減点係数の現在値と短い採点計算式を常時表示。詳しい式の説明と、入力欄・選択欄・主要操作の設定内容・値の範囲・計算への影響を示すホバーヘルプを追加。

### Changed

- Similarity評価を行ごとのLLM呼び出しからローカル決定的計算へ変更。NFKC正規化、空白・句読点・記号除去、文字n-gram包含率、Dice/Jaccard、最長共通substring被覆率を組み合わせ、コピー＆ペースト寄りの表層一致を0〜1で算出します。互換性のため`.Similarity_AI_Raw`列名は維持し、同じ設問の他学生回答との最大類似度`.Similarity_Peer_Max`と相手行`.Similarity_Peer_Row`を情報列として追加しました。Similarityは不正行為の証明ではありません。旧checkpoint schema 1は再開時に不一致として拒否し、新schema 2でローカル結果を保存します。
- 入力・採点設計・実行・結果の4ステップを保ち、主画面の要点と設定での詳細編集を分離。「基礎配点」「評価方法」「評価項目」などの日本語ラベルへ整理。設問は全カードの同時表示から、残領域に応じたコンパクト一覧とページ切替へ変更し、全件への到達と固定式ガイドが編集領域を圧迫しない配置を両立。
- 並列度の既定値を1から4、選択上限を3から8へ変更。学生行はsource row順の連続prefixだけをcheckpointへ保存しながら複数行を先行処理し、出力順序とresume correctnessを維持します。
- 警告・ナビゲーション・前後操作を固定し、本文14 DIP・主要操作の最小44 DIPを維持。通常サイズは外側スクロール不要で横に見切れない配置とし、長文・全文path・候補一覧は局所スクロール、狭小・拡大時は再配置と必要な本文縦スクロールで対応。
- 結果をページ一覧と選択行の詳細・override編集に分離し、元の行番号でも移動可能に。修正版は「検証して出力」で別名workbookへ保存し、入力・既存final／partialを上書きしない。
- 利用者ガイドと[8枚の説明画像](images/README.md)を更新し、[Fluent System Iconsの出典・固定revision・LICENSE／NOTICE](docs/third-party-notices.md)を明記。画像は合成／fake状態で、実認証・実保存・実機検証の証拠ではない。
- 要求定義書（v4.6）を改訂し、「SDKがmodelのtoken上限を公開しない」をerrorではなく正規の状態として扱うように変更。通常評価modelは`auto`を含む列挙された全modelから選択でき、上限不明modelではmodel相対の容量検査だけを適用外とします。app所有の絶対上限とattempt上限、送信後の失敗扱い、`auto`では実際のrouting先modelを記録しないことを明記しました。
- [はじめに](docs/getting-started.md)の「checkpointから再開」に、アプリを再起動した後に再開する場合の手順を追記。中断前に設計を**設定を保存**で保存しておき、再起動後に保存定義タブの**現在の入力に適用**を選んでから再開元を指定します。保存していない設計の編集は、再起動後に同じ設計として判定されません。
- [開発者向けの実装状況](dev/docs/implementation-status.md)に実機確認の結果を追記（製品の動作は変更なし）。明示選択したモデル（`claude-sonnet-5`）で全attemptが完了し、ジョブログのモデル識別子が選択と一致すること、実行中にCopilot CLIが終了した場合は該当attemptを後始末失敗としてretryせず、結果を技術エラーとして保存することを確認しました。また、Copilot CLIの通信が止まって送信が時間切れになった場合に、同じ評価を新しいsessionで再試行して成功すること、schema不正の後に1回再試行することも確認しました。
- 要求定義書（v4.6）の§7.6を改訂し、AIの応答待ちはCopilot SDKの`SendAndWaitAsync`の既定（60秒）に従うことを明記。起動・認証確認・session作成を含む1回の試行（attempt）全体の上限は、応答待ちの60秒を先に打ち切らないよう120秒のままとします（attempt全体を60秒にすると、実機では起動に約10〜25秒かかり応答待ちが途中で打ち切られたため）。
- 通常評価・固有評価で、SDKのモデル一覧がreasoning effort対応と示し、`medium`を列挙したモデルにはreasoning effortを`medium`に指定するように変更（答案の定量化にhigh以上は使いません）。`auto`と非対応モデル（例：claude-haiku-4.5）は指定するとsession作成が失敗するため指定せず、ランタイムの既定に任せます。`auto`固定の参照回答生成・類似度評価も同様で、実際に使われるeffortはSDKから確認できません。これを理由に実行を拒否したり、別モデルへ切り替えたりはしません。
- 利用者の手動SHA-256比較を任意の推奨とし、sidecar公開とCI／公開判定での最終bytes照合は必須のまま維持。単一EXE候補の公開前にfresh Windows 11 x64標準userでCH-01〜CH-06を実測し、candidate run／source／version／bytes／hashへ拘束する手順へ変更。

### Fixed

- 通常評価モデルに`auto`を選んだままでは定量化を開始できなかった問題を修正。同梱Copilot CLIはrouterである`auto`のtoken上限を公開しないため、希望モデル未指定の初期選択が`auto`になると`MODEL_PROMPT_LIMIT_UNAVAILABLE`で恒久的に開始できませんでした。上限が不明なモデルでもrunを開始できるようにし、モデル相対のcontext budget検証だけを適用対象外にします。既定値の推定や別モデルへの自動切替は行いません。app側のリクエスト絶対上限とretry込みattempt上限は従来どおり常に検証します。実効モデルの上限は実行画面に「SDK未公開・事前検証なし」または実際のtoken数として表示します。`auto`を選んだ場合、実際にroutingされたモデルは記録されず、再開時も同一モデルへroutingされる保証はありません。
- 保存済みの明示出力先を再起動・入力変更後も保持するよう修正。空欄は未指定（`null`）への変更とし、その場合だけ新しい入力に隣接する`result`を都度算出。算出先を指定値として保存せず、入力未選択なら「入力後に決定」と表示。
- 保存した希望modelが明示状態確認の候補にない場合は未選択とし、別modelへ自動fallbackしないよう修正。確認失敗だけでは希望IDを消去しない。
- 現在runの開始時の条件・予約済みpathと、前回結果・overrideを次回編集から分離。画面往復や設定編集で現在runを再構成・自動同期せず、設定表示中の完了でも結果へ強制移動しないよう修正。
- 画面・カテゴリ・ページの切替で最新draft、数値として未確定の入力文字列、対象ID・選択を保持。不正入力を確定値で上書きせず、ページ外を含むエラーの対象欄へ移動できるよう修正。
- 主回答列の変更時は、選択sheetの設問text行（1行目または2行目）との交差セルを設問文へ即時反映し、空セルは空欄と必須エラーで通知。設問text行の変更だけでは再読込まで既存文を保持し、再読込でもsheet・手入力文・配点・評価設定を保持。空見出しの代替文や設問追加時の古い見出しの混入を防ぎ、長いsheet名・補助列名は折り返して表示。
- 中断・再開の導線を追加した後の実行画面で、狭い画面ではログイン手順の説明が表示範囲の外に出る、通常サイズでは重要な状態表示が「…」で切れる、Tabキーで読取専用の欄に先に移ってしまう、といった問題を修正。ログインの領域と再開の領域を分け、再開元のpath欄→「参照…」の順を入力画面と揃えました。結果画面の「この結果は表示時の採点draftから自動再評価しない」の注記も切らずに表示します。
- 再開モードを使っていないときに、入力や採点設計を変えるたびに実行条件の再検証の通知が余分に出ていた問題を修正。通知は再開モードで条件が変わったときだけ出します。
- 開発用MSIXの検証script（`scripts/test-windows-msix-unsigned.ps1`）が、同梱文書1件と説明画像3件のentry名を`\`区切りで探していたため、package内に存在しても「missing」として失敗していた問題を修正。ZIP形式のentry名に合わせて`/`区切りにしました。一般利用者向けの配布物は変わりません。
- AIへの送信中に通信が途切れ、Copilot CLIが接続の時間切れなどをsession errorとして返した場合に、再試行せず`AI_RUNTIME_FAILED`としていた問題を修正。通信失敗として新しいsessionで最大2回再試行し、再試行後も失敗した場合は`NETWORK_FAILED`とします。
- Copilot CLIの起動前確認（同梱CLIのSHA-256照合）に1回あたり約12秒かかっていた問題を修正（約0.4秒に短縮）。「Copilot 状態を確認」が15秒の確認時間内に終わらず失敗することがある主な原因とみられます。
- 通常評価で、モデル（実測ではclaude-sonnet-5）が結果toolの引数からアプリが指定したevaluator IDを省略すると、schema不正として再試行し、再試行でも省略されると`AI_OUTPUT_INVALID`になっていた問題を修正。evaluator IDはアプリが保持している値なので、省略された場合はアプリが補います。返された値が異なる場合は従来どおりschema不正です。reasoning effort `medium`では省略が増えることを実測しています（同じPromptで`medium` 10/30、未指定 2/30）。

## [0.8.1] - 2026-09-04

### Added

- Windows 11 x64向けの.NET 10 self-containedデスクトップアプリ。
- GitHub Copilot CLIを同梱し、Knowledge／Custom evaluator、Reference、固有評価、Similarityによる動的な定量化。
- student row単位のatomic checkpoint、中断後の再開、4 sheet構成のfinal workbook、criterion overrideの別名出力。
- アプリケーション版の単一正本、開発者向け版管理手順、版操作・検証tool。
- 利用者ガイド、現行UI画像、Prompt起動ガイド。
- Windows 11 x64向けself-contained ZIPとSHA-256 sidecarを、clean checkoutの回帰検証を通過した場合だけ公開するdelivery pipeline。
- 公開前のdraft作成workflowと、承認必須のprotected publish workflowへの分離。

### Changed

- 標準`.xlsx`をread-onlyで扱い、入力を変更せず別fileへatomic出力する契約へ統一。
- AIへ送るworkbook値をcurrent rowの選択済みsourceへ限定し、application logへ回答・Prompt・reason・evidence・credentialを記録しない境界を明文化。
- 開発者向け文書を`dev/docs/`へ分離し、利用者向けREADMEと`docs/`を要求v4.3へ同期。
- 初回公開のWindows配布物をself-contained ZIPとsidecarへ確定し、開発用unsigned MSIXは一般配布しない検証専用の位置づけへ変更。
- macOSの署名・公証済みDMG提供は現版scope外とし、sourceと静的契約のみ維持。
- 初回公開版を`0.8.1`へ確定。公開前候補は`0.8.0`へ再baseline済み。

### Fixed

- Windows runnerの`PATH`に複数のGit実行pathがある場合も、版・tag・clean tree検証とpublish toolが単一のapplication commandを選択できるよう修正。

<!-- 公開時は Unreleased の項目を `## [X.Y.Z] - YYYY-MM-DD` へ移し、空の Unreleased を先頭へ残します。 -->
