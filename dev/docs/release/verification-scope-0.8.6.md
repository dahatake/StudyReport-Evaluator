# v0.8.6の検証範囲

公開`v0.8.6`は、clean-host試験CH-01〜06と本人loginを実施しないまま公開しました。development MSIXは非公開の開発検証専用です。以下の履歴は先生の操作手順ではなく、管理者・開発者が確認範囲を判断するための情報です。現行候補`0.8.8`にも別の公開前確認が必要で、文書の更新だけで配布物や確認状況は変わりません。

先生向けの要約は[はじめに：公開版の確認状況](../../../docs/getting-started.md#公開版の確認状況)にあります。この文書は、そこから詳細を切り出した正本です。

以下は2026-09-07、版更新前の**製品`0.8.4`で得た検証履歴**です。**T01〜T38は対象範囲でREVIEWED**です。文書・実成果物・full required gateの実績を、版更新後の`0.8.6`の最終成果物や未完了のnative確認のPASSへ読み替えません。

| 対象 | 確認状況 |
|---|---|
| 文書・画像contract（T36） | **21/21成功**。文書の整合性確認であり、native操作の成功ではない |
| ZIP実成果物・MSIX静的検証（T37） | **ZIP実検証3件＋MSIX静的検証6件＝9/9成功**。`0.8.4`時点の対象範囲に限定 |
| 単一EXEの対象検証（T38） | **114/114成功**。開発hostのP06実EXE試験も**7/7成功**。fresh OS-onlyや本人loginの確認とは別 |
| full required gate（T39） | **Core 190 + App 1702 = 1892/1892成功、skip 0**。`0.8.4`の結果であり、追加native確認を含むT39全体の完了ではない |
| development MSIX | **`PASS_MECHANISM`**。開発用の方式検証に限定し、一般配布・署名・公開成功を示さない |
| 追加native probe | **NOT COMPLETE（FAIL）**。3回の試行後、最終試行はUIAの行を一意に特定する段階で停止。アプリ不具合の確定ではない |
| Space／Narrator・本人walkthrough・隔離利用者環境のnative保存 | **NOT_RUN** |
| fresh Windows 11 x64のclean-host試験CH-01〜06／本人login | **NOT_RUN** |

**T39全体は`BLOCKED`です。** 追加native probeで観測できたのは、**120 DPI、実client 1475 × 1000 pixels（1180 × 800 DIP）**での入力読込、入力hashの保持、設定fileが存在しないことまでです。主画面4ステップと設定を通した確認は完了していません。既存P06の実EXE観測まで未実施とするものではありません。

開発環境の実EXE試験やUIのfake認証試験を、追加ソフト未導入のOS-only試験、MOTW／Windows保護の実測、本人認証成功の代わりにはしません。新EXEの公開判定には最終成果物と一致するCH-01〜06の必須PASSが必要であり、現時点では公開条件を満たしていません。

## 対象版と配布物の関係

本ガイド（`docs/getting-started.md`）は配布物へ同梱するsourceです。同梱文書や製品版を変更すると最終EXE／ZIPのbytesとSHA-256も変わるため、**公開判定は対象版の最終成果物・sidecarと、その成果物に対応する検証に基づきます。** 過去のPASSでこの条件やclean-host条件を省略しません。

## 2026-11-25の再確認

- `gh release list`で、最新の公開版が`v0.8.6`（asset 4件）、`v0.8.7`はDraftのままであることを確認しました。
- `Directory.Build.props`の`VersionPrefix`が`0.8.8`であることを確認しました。
- 公開`v0.8.6`の単一EXEを取得し、SHA-256が同じReleaseのsidecarと一致すること、同梱Copilot CLIのみで（PATH上のCLIなしで）起動してウィンドウが応答することを確認しました。ログイン、AI評価、fresh Windows環境での確認は含みません。
- 2026-11-07以降のソース変更は、チェックポイントのデコード／検証結果の再利用、類似度計算のn-gram表現の高速化、単一EXEの発行プロファイルへの文書画像2点（`teacher-workflow.svg`、`teacher-result-review.svg`）の同梱追加です。利用者向けの動作の記述が変わる変更は見つかりませんでした。
