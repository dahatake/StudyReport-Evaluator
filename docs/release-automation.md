# Windows Release自動化（FR-121 / INT-002）

## 実行前の条件

- リポジトリの `publish` environment に必須reviewerを設定し、自己承認と
  管理者による迂回の可否も運用方針に従って制限する。YAMLのenvironment指定だけでは
  GitHub側の承認保護を作成できない。本変更は外部設定を変更しない。
- 対象は候補開始時のmain先端と一致する既存stable annotated tag `vX.Y.Z`。
  `dev/version.ps1 verify` と日付入りCHANGELOG節が必要。tag作成・pushは行わない。
- Windows x64 runner、PowerShell Core 7.5+、固定.NET SDK 10.0.400、
  Python 3.13（標準ライブラリのみ）を使用する。新しいPATや外部サービスは不要。

## 候補を作る

1. `release.yml` を対象tagで手動実行する。read-onlyの `candidate` ジョブは、
   **ビルド前**にGitHubからmain tipとannotated tag objectを取得・固定する。
   ビルド中にmainが進んでも、この記録を新しいmainで置き換えない。
2. 既存のRelease build、Core/Appテスト、ZIP検証、P07 single-file検証、
   unsigned MSIX mechanism、C03 Candidate/C02を順に実行する。
   正式包装の必須結果をスキップ・開発用結果で代替しない。
3. 内部adapterがC02を再実行し、Core/AppのTRX実行結果と包装証跡から
   `request.json` を導出する。workflow入力でPASSフラグを指定する方法はない。
4. 別の `draft` ジョブだけが `contents: write` を持つ。ビルド／テストは行わず、
   共通 `invoke-release-automation.ps1` がtag object、source、runとlocal bytesを検証する。
   Releaseがないときだけdraftを作り、EXE/ZIPと各SHA-256 sidecarの4ファイルを添付する。
   downloadした実bytesまで再確認する。候補から最終matrixや公開資格は生成しない。

内部control、public bytes、内部automation observationsは元のActions runの
別々のimmutable artifactとして30日保持する。Releaseのbodyはcandidate identity、
正規化package証跡のSHA-256、4 asset descriptorを束縛するJSONであり、
内部証跡全文・秘密値・MSIX binaryは公開しない。

## exact EXEを承認して公開する

1. 元candidate runのEXEを対象clean hostで人が検証し、既存の
   `windows-singlefile-clean-host` 証跡を用意する。CH-01–06必須、ADV-01/02は任意。
   Windows hosted runnerやfixtureの合格はclean-host実測の代わりにならない。
2. `publish-release.yml` にtag、元candidate run ID、clean-host JSONを渡す。
   JSONは環境変数経由で64 KiB以下のUTF-8ファイルにし、shellへ展開しない。
3. read-onlyの `validate` ジョブが元runの成功・repository/workflow/sourceを確認し、
   元のcontrol/public bytes/automation observationsを取得する。
   既存C03 Final/C02がcandidateとexact EXEの人手証跡を照合し、
   closed schemaVersion 2の3行とpublish/statusを検証する。
   EXEやZIPを再ビルド・再包装しない。
4. `publish` environmentの承認後、別のwriterジョブが同じ共通driverを実行する。
   保存したmain snapshotを維持したままtag object/commit/runを再確認し、
   Releaseの由来、exact asset set、downloadした4ファイル全bytesとsidecarを照合する。
   最終matrixのEXE/ZIPだけpublish=true、MSIXはpublish=false/PASS_MECHANISM。
   publicへのPATCHはこの照合後に一度だけ行い、応答後もremote状態を再確認する。

candidate/publishのwriterは同じ
`release-${repository}-${tag}` concurrency group、`cancel-in-progress: false`。
同じホスト上の直接呼出しもrepository/tag/API単位のOS mutexで直列化し、
待機後はremote状態を最初から照合する。ETagだけに並行変更の拒否を任せない。
read-onlyジョブにはRelease書込権限を与えず、run metadata/artifact参照に必要なジョブだけ
`actions: read` を持つ。checkoutにtokenを永続化しない。

## 再実行・停止の扱い

- 同一source、版、candidate run、証跡、4ファイル全bytesの完全一致だけがnoop成功。
  同じファイル名やAPI digestの一致だけでは成功にしない。
- public Releaseをdraftへ戻さない。既存Release/assetの削除・上書き・自動修復はしない。
  旧方式のbody、別candidate、欠損、余分なasset、違うbytesは明示停止する。
- upload途中で止まったdraftも再実行で補完しない。管理者が由来と状態を調査して
  別途判断する。新たなrun IDでの再buildは同一candidateの再実行とは扱わない。
- 404だけを不存在とする。通信断や503は不存在／成功にしない。
  publish応答喪失時は既にpublicの場合もあるため、結果の停止段階を見て再確認する。
  publicの完全一致を再確認できた次回実行は変更なしで成功する。
- driverの `result.json` は成功／停止の双方でtag、commit、candidate_run_id、stageを記録する。
  workflowは `always()` でprivate result artifactを保存する。
  runner強制終了・driver開始前の失敗はActionsのジョブログで確認する。

## ローカル検証（外部操作なし）

```powershell
python -m pytest -q tests/packaging/test_release_automation.py
python scripts/ledger.py run --cases IT-006,IT-007,IT-008,IT-009,IT-010 --no-record
python scripts/verify.py --quick
```

先行System TestのHTTP fakeは本番と同じdriverへ
`-RequestPath -ResultPath -GitHubApiUrl http://127.0.0.1:<port>` で接続する。
driverにghフォールバックはなく、GitHubへの全呼出しは注入したtransportに従う。
normalized requestは既存validatorの観測境界であって新しい公開証跡schemaではない。
fixtureは合成bytesを実行せず、正式x64包装・clean-host実測・GitHubのenvironment設定・
実Actions実行の証明にはならない。実環境での承認設定とrunner実行は別途必要。

### ARM64と実x64の検証境界（AC-103）

既存`app-unit`は次の既存selectorのローカル実行モードを使う。
新たな証跡schemaや配布RIDは導入しない。

```powershell
python scripts/release_test_selection.py --run-local --results-directory work/local-trx --logger "trx;LogFileName=local.trx"
```

- native OSアーキテクチャを選び、エミュレートされたプロセスのx64を
  実x64ホストと取り違えない。ARM64では本物のApp単体・結合試験を実行する。
  子プロセスの失敗はそのまま非0とし、標準TRX引数をそのまま転送する。
- 従来の8環境失敗のうち、ZIP入力のzero-byte/reparse拒否2件と
  single-fileの不正入力・reparse/hardlink拒否2件は、
  実スクリプトの読み取り専用admissionをホスト制約より先に実行する。
  書込み直前のx64制約は維持する。assertや入力拒否条件は省略しない。
- sample構造検証はCPU非依存。private `SampleReport.xlsx`があれば実行する。
  探索先はworktreeの`sample/`、`STUDYREPORT_SAMPLE_DIR`、主worktreeの`sample/`。
  不在なら`Private input NOT_RUN`と試験名を表示する。同じ完全な構造assertは
  既存合成Workbook基盤でも常に実行し、実sample合格とは区別する。
- 実x64専用は既存の性能中央値証跡、通常Release起動、
  ZIP publish/起動/再現性の3試験。ARM64では各試験名を`x64 NOT_RUN`と列挙する。
  性能試験の実Workbook read/write/validation処理は別のportable試験で実行するが、
  x64性能JSONやx64合格結果は生成しない。未実行をPASSとは記録しない。
- x64ローカルプロファイルは3試験を除外せず、通常起動に必要なReleaseを先にbuildする。
  CIとcandidateはローカルモードを使わず、既存の`dotnet test`と包装・起動検証を維持する。
  CI/candidate/publishはOSとプロセス両方のx64を要求し、ARM64上のx64エミュレーションも拒否する。
- candidate adapterはCore/App全必須観測の合格に加え、実x64起動・性能の
  **名前付きTRX結果の存在とPassed**を必須にする。欠落・NotExecuted・NOT_RUN・Failedは停止する。
  ZIP/single-file包装、clean-host検証、Final matrixの既存必須条件も維持する。
  ローカルのportable成功や合成bytesから正式publishable証跡を作ることはできない。
