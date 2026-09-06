# StudyReport Evaluator

標準`.xlsx`のレポート回答をGitHub Copilotで定量化し、入力を変更せず別の`.xlsx`へ結果を作成するWindowsデスクトップアプリです。

> [!WARNING]
> 生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません

AIが作る値は確認対象です。最終的な評点と利用判断は、授業目的や所属組織の方針に基づいて利用者が行ってください。Similarityは不正行為の証明ではなく、低いSimilarityも回答品質を保証しません。

## できること

- native pickerまたはfull pathから標準`.xlsx`をread-onlyで読み込む
- 質問、主回答列、補助列、Knowledge／Custom evaluator、固有評価を設定する
- Base、Question、Specialの絶対配点とSimilarity penalty weightを設定する
- QuestionごとのReference、通常評価、固有評価、Similarityを実行する
- Reference完了ごと・student row完了ごとに`.partial.xlsx`へcheckpointする
- finalを自動作成し、必要ならcriterion override反映版を別名で出力する

## 対応環境

| 項目 | 対応内容 |
|---|---|
| OS / architecture | Windows 11 x64 |
| 配布 | .NET 10 self-contained。公開`v0.8.1`はunsigned ZIP、未公開`0.8.4`候補ではunsigned単一EXEを追加。各形式にSHA-256 sidecar |
| 入力 | 標準Office Open XML `.xlsx` 1file |
| AI runtime | 配布物へ同梱したGitHub Copilot CLI。PATH上の別CLIへfallbackしません |
| AI login | 利用者本人のGitHub Copilot対話login。GUI起動とは別条件です |
| Spreadsheet runtime | Microsoft Excel、Office、LibreOfficeはアプリ実行に不要 |

`.xls`、`.xlsb`、CSV、PDF、`.xlsm`等のmacro-enabled file、password／rights-protected／暗号化workbook、安全境界に違反するpackageは対象外です。

## 公開状況・SHA-256確認・起動

現在の公開版は`0.8.1`です。[GitHub Releases](https://github.com/dahatake/StudyReport-Evaluator/releases)で現在取得できる配布物は、`v0.8.1`の`StudyReportEvaluator-win-x64.zip`と`StudyReportEvaluator-win-x64.zip.sha256`です。**この公開版には単一EXE配布も「GitHubにログイン」buttonもありません。**

> [!IMPORTANT]
> **現在の作業ツリーの製品候補は`0.8.4`（UNRELEASED・未公開）です。** `0.8.3`時点の単一EXE publish/packageとP06の実EXE試験7件は開発環境で成功し、独立レビューも指摘0件でした。P07は7件・9観測から`PASS_DEVELOPMENT`の証跡を生成済みですが、版と同梱文書の変更後に最終再検証が必要です。ログインUIの確認は自動試験の範囲で、**clean-host試験と本人loginは`NOT_RUN`**です。追加ソフト未導入のOS-only環境での動作や実認証、新EXEの公開完了を示すものではありません。
>
> README等の同梱文書や製品版が変わればEXEのbytesも変わるため、公開判定には最終EXEの再package・再検証が必要です。

### 単一EXEから起動する（未公開候補・今後の主導線）

今後の主配布名は`StudyReportEvaluator-win-x64.exe`と`StudyReportEvaluator-win-x64.exe.sha256`です。現時点ではソース側で作成した未公開候補であり、公開条件を満たす最終成果物の公開後に主導線となります。未公開のdownload URLは案内しません。

目標とする起動操作は、**取得済みの`StudyReportEvaluator-win-x64.exe`をダブルクリック → 入力画面**です。ダブルクリックを1起動gestureと数え、download、任意の手動hash比較、Windowsの警告への操作、本人loginは含めません。

- 標準userがofflineでGUI、Excel読込、mapping、採点設計を利用できることを要求しています。**clean-hostでの実証は未完了**です。
- GUI起動に.NET Runtime／SDK、PowerShell、Node.js／npm、Git、GitHub CLI（`gh`）、別Copilot CLI、Microsoft Excel／Office／LibreOffice、IDEの導入を要求しないself-contained設計です。
- 手動展開、setup script、terminalへのcommand入力、管理者昇格、repository、隣接DLL／manifest、既存CLI cache・認証情報、sidecarをGUI起動の前提にしません。
- GUI起動時にloginやAI評価は自動開始しません。AIのnetwork・account・model・組織policy上の許可は別条件です。

### 現在の公開版を起動する（v0.8.1 ZIP・今後も代替として維持）

1. 上記GitHub Releasesの`v0.8.1`から`StudyReportEvaluator-win-x64.zip`をdownloadします。
2. 任意・推奨のhash確認を行う場合は、同じreleaseの`StudyReportEvaluator-win-x64.zip.sha256`も取得し、下記の方法で照合します。
3. ZIPを新しいdirectoryへ展開します。
4. `StudyReportEvaluator-win-x64\StudyReportEvaluator.App.exe`を起動します。

単一EXEが公開された後も、ZIPとsidecarの名前および展開後の起動方法は代替経路として維持します。公開済み`v0.8.1`の配布物を候補版の内容へ差し替えることはありません。

### SHA-256の確認（利用者の手動比較は任意・推奨）

EXE／ZIPそれぞれのsidecar公開と、CI・公開判定での最終配布bytesとのhash完全一致確認は必須です。一方、**利用者の手動hash比較は任意の推奨で、sidecarは起動に必要なfileではありません。**

確認する場合は、配布fileのSHA-256が、同じrelease／候補のsidecarの先頭64文字と完全一致することを確かめます。不一致なら実行せず、正式配布元からの再取得を確認してください。同じ配布元のhash一致だけでは、発行者の真正性やSmartScreen reputationは保証されません。

PowerShell 7が既にある場合の公開ZIPのSHA-256確認例（任意）です。PowerShell 7の導入は必須ではなく、Windows 11標準の`certutil`を使う確認方法は[はじめに](docs/getting-started.md)を参照してください。手動比較を起動の必須操作にはしません。

```powershell
(Get-FileHash -Algorithm SHA256 .\StudyReportEvaluator-win-x64.zip).Hash
```

### Windowsの警告・実行拒否

公開ZIPと候補EXEはunsignedです。SmartScreen、Smart App Control（SAC）、企業policyによる警告・実行拒否があり得ます。code signing済み、installer形式、SmartScreen reputation確立済み、すべての端末で無警告・無条件に1操作で起動できるとは表示しません。

入手元を確認できない場合やhashが不一致の場合は実行しないでください。保護機能が実行を拒否した場合は停止し、所属組織の管理者に確認してください。保護機能の無効化、MOTW除去、証明書の自動trust、execution policy変更、UAC回避は案内しません。

### 単一EXEの標準展開と残存cache

候補EXEの内容は.NET標準hostが、通常は標準userの一時領域`%TEMP%/.net/<app>/<bundle-id>/`へ展開します。展開先の書込権限と空き容量が必要です。cacheは終了後も残り、再利用され得ます。**1ファイル配布は「ディスク上も1ファイル」「痕跡なし」ではありません。** 独自の展開・cache管理UIや自動掃除は追加しません。

cacheはアプリ配置用で、入力／final／partialやCLI credential storeとは別です。既定の結果保存先は入力fileに隣接する`result`であり、抽出cacheではありません。配布・展開・起動のために利用者workbookを移動・削除しません。結果の扱いは[出力と再開](#出力と再開)を参照してください。

## 5分クイックスタート

> **画面・追加操作の対象版:** 以下の画像と、利用者ガイドの設問text自動同期・固定表示の採点計算式・幅に応じて折り返す設問カードは、**現在の作業ツリーのUNRELEASED（未リリース）`0.8.4`候補**を対象にしています。本READMEの単一EXE起動例とlogin buttonの説明も同候補向けです。公開`0.8.1`でこれらの追加UI／動作を前提にしないでください。画像はsynthetic data／fake結果による説明用で、clean-hostや本人認証の証跡ではありません。リンク先の手順の対象版と更新状況は[ガイド](#ガイド)を参照してください。

### 1. 入力

![標準xlsxをnative pickerまたはpathからread-onlyで読み込む入力画面の上部。synthetic dataを使用](images/01-input-workbook.png)

1. **ファイルを選択**でnative pickerを開くか、**ファイル path（標準 .xlsx）**へfull pathを入力します。
2. **read-only で読込**を選びます。
3. 回答sheet、質問文の行1/2、回答開始・終了行を確認します。
4. 質問ごとの主回答列と補助列を確認・変更します。

mapping候補は確定値ではありません。workbookの見出しと授業設計に合わせてください。

### 2. 定量化設計

- Base pointsは既定60、Special pointsは既定0です。
- Question pointsは設問ごとの絶対配点です。
- Similarity penalty weightは0〜1で、既定0.1です。
- 通常評価はKnowledgeまたはCustom evaluatorを使います。
- 学生Prompt等を別sourceで評価する場合は固有評価を追加します。
- `Base + Special + enabled Question points = 100`になるよう設定します。

Custom Promptには許可されたplaceholderだけを使います。通常Custom evaluatorでは`{回答}`と`{評価項目}`、固有評価では`{回答}`が必須です。

### 3. 実行

1. **Copilot 状態を確認**を選びます。
	- 未認証の場合、**未公開`0.8.4`候補のみ**の**GitHubにログイン**を明示的に選ぶと、検証済みの同梱native CLIが認証用console／ブラウザーを開きます。本人のaccountでCLI／ブラウザー上の対話を完了してください。アプリへpassword、token、device codeを入力する必要はありません。
	- 完了後は、既存の**Copilot 状態を確認**をもう一度選びます。取消・失敗後や再起動後も同じbuttonで再確認します。login processの起動・終了だけを認証成功とは扱いません。
	- **公開`v0.8.1`にはlogin buttonがありません。** 従来どおり同梱CLIで本人loginを行い、既存の状態確認buttonで確認します。
2. 表示されたmodelから通常評価用modelを選びます。ReferenceとSimilarityは利用者が選択せず、固定で`auto`を使います。
3. Concurrencyを1〜3から選びます。
4. 新規runでは出力directory、再開では`.partial.xlsx`のpathを確認します。
5. 技術検証を通過したら**定量化を開始**を選びます。

AI処理には、利用可能なGitHub Copilot account、本人認証、network接続、利用可能model、組織policy上の許可が別途必要です。GUI表示やlogin完了だけでAI利用可能とは判断しません。起動引数、Prompt適用、状態確認からloginやAI評価を暗黙に開始せず、login後もmodelを自動変更したりAI評価を自動開始したりしません。

候補版の**ログインを取り消す**またはアプリ終了で終了するのは、このアプリが開始・所有した当該login CLI processだけです。ブラウザーや他のCLI、保存済みcredentialを終了・削除しません。loginの二重開始・評価中の開始を防ぎ、取消・失敗後もExcel読込、mapping、設計編集、checkpoint確認は利用できます。CLI欠落・不一致時は配布物の再取得／展開状態を確認し、PATH上の別CLI導入やhash検証の緩和で回避しません。

### 4. 結果・出力

![自動final path、row別配点・減点・Final scoreを確認する結果画面。fake scoreを使用](images/06-results-review.png)

finalまたはpartial path、status、Question earned、Special earned、Similarity penalty、Final raw、Final scoreを確認します。criterionのAI raw、effective raw、normalized valueも確認できます。

criterion overrideを使う場合、range内の値を入力して未使用の別名`.xlsx`へ任意出力します。既存finalを上書きしません。

## 点数の読み方

有効Questionを$q=1..N$とします。

$$
BasePoints + SpecialPoints + \sum_{q=1}^{N}QuestionPoints_q = 100
$$

$$
QuestionEarned_q=QuestionPoints_q\times QuestionRate_q
$$

$$
SimilarityPenalty_q=QuestionPoints_q\times Similarity_q\times SimilarityPenaltyWeight
$$

$$
FinalRaw=BasePoints+\sum_q QuestionEarned_q+SpecialEarned-\sum_q SimilarityPenalty_q
$$

Final scoreはFinal rawを0〜100へ収めた表示用の値です。

- 空の主回答: AI callなし、Question earnedとSimilarityは0相当
- 非空回答の技術的AI失敗: 対象値はblank
- 必要な値がblank: Final raw / Final scoreもblank

0とblankは意味が異なります。詳しくは[機能と点数](docs/features.md)を参照してください。

## 出力と再開

新規runの初期出力先は入力fileに隣接する`result` directoryです。単一EXEでも、この初期出力先をEXEの配置先や.NETの抽出cacheへ変更しません。

- final: `eval-yyyyMMdd-HHmm[-NN].xlsx`
- partial: `eval-yyyyMMdd-HHmm[-NN].partial.xlsx`

既存final/partialがある場合は、共通の次suffixを使って上書きを避けます。cancelまたはprocess終了後は、Execution画面で**既存checkpointから再開**を選び、保存済みcomplete rowの次から再開できます。

finalは入力workbookの全sheetを保持し、次を追加します。

- `Quantification_Config`
- `Quantification_References`
- `Quantification_Results`
- `Quantification_Run`

同名sheetが入力にある場合は既存sheetを変更せず、` (2)`等の一意名を使います。partialは入力全体と`Quantification_Checkpoint`を含みます。

## データとprivacy

workbook由来でAIへ送る値は、処理に必要なcurrent rowの選択済みprimary／supporting／special sourceだけです。処理に応じてQuestion text、Prompt、criterion metadata、Reference、closed schema metadataも送ります。他row、非選択列、workbook pathは通常payloadへ含めません。

application logは回答、Prompt、Reference、reason、evidence、credentialを受け取るfree-text surfaceを持ちません。

本人認証とcredential保管は同梱CLI／ブラウザーに委譲します。アプリはpassword、PAT、token、device codeを入力・収集・解析・保存・log出力しません。runtime抽出cache、利用者workbook、CLI credential storeは別のものとして扱い、アプリがcacheの再帰削除やcredentialの削除・logoutを行うことはありません。

final/partialには入力全体、Prompt、Reference、AI結果が含まれ得ます。入力と同等以上に機密なfileとして扱ってください。詳しくは[データとprivacy](docs/privacy-and-data-handling.md)を参照してください。

## Promptファイルから起動する

任意の高度な起動方法です。通常のGUI起動にcommand入力は不要です。次の単一EXE例は**現在の作業ツリーの`0.8.4`未公開候補**向けで、公開`v0.8.1`の配布名ではありません。

```text
StudyReportEvaluator-win-x64.exe --input <xlsx-path> --prompt <txt-path> [--prompt <txt-path> ...]
```

ZIP版の従来の起動名と引数は変わりません。公開`v0.8.1`のZIPでは次を使います。

```text
StudyReportEvaluator.App.exe --input <xlsx-path> --prompt <txt-path> [--prompt <txt-path> ...]
```

- `--input`は0または1回
- `--prompt`は0回以上、指定順を維持
- 相対pathは起動時のcwdが基準。EXE配置先や抽出cacheを基準に変更しません。日本語・空白を含むpathも扱います
- Prompt fileはstrict UTF-8の`.txt`、1〜32,767文字
- Imported Promptを選び、適用先を選んで**Promptを適用**した時だけtemplateへcopy
- 起動引数やPrompt適用ではloginもAI処理も自動開始しません。AI処理はExecution画面の明示操作まで開始しません。候補版のlogin buttonも別の明示操作が必要です

詳しい例は[Promptファイルから起動](docs/prompt-launch.md)を参照してください。

## 制限と非保証

- macOS、Linux、Windows Arm64は初版対応対象外です。
- installer、code signing、notarizationを提供しません。
- development MSIXは非公開の開発用検証だけで、一般利用者向けinstallerではありません。
- 自動更新・差分更新、online bootstrap、独自cache管理／自動掃除、常駐service、file associationは追加しません。更新時は正式に公開された配布物を利用者が取得します。
- 保存済みfinal workbookのアプリへの再importと、definition profileだけの独立save/loadは提供しません。
- AI品質、教育的妥当性、公平性、法的適合性、組織policy適合性、不正行為を保証・判定しません。
- 未実測の処理時間、token数、費用を保証しません。

## ガイド

利用者ガイドは単一EXE候補・login導線・任意の手動hash比較へ同期しています。公開版と未公開候補を区別し、業務手順と画像は各ガイドの対象版を確認してください。

- [はじめに](docs/getting-started.md)
- [機能と点数](docs/features.md)
- [Custom evaluator](docs/custom-evaluator-guide.md)
- [Promptファイルから起動](docs/prompt-launch.md)
- [データとprivacy](docs/privacy-and-data-handling.md)
- [トラブルシューティング](docs/troubleshooting.md)

開発・保守の手順は[開発者向けガイド（repository）](https://github.com/dahatake/StudyReport-Evaluator/blob/main/dev/README.md)を参照してください。開発用のSDKやPowerShellは一般利用者のGUI起動要件ではありません。

## ライセンス

[MIT License](LICENSE)
