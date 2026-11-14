# Bilingual Spire

SteamのMac版 **Slay the Spire 2** で、2つの言語を切り替えたり併記したりできる語学学習用MOD。
ゲーム収録の公式翻訳を利用し、外部の翻訳APIは使いません。

v0.5.0 / 動作確認済みのゲームバージョン: v0.113.0 / macOS Apple Silicon

## ZIPから導入

[BilingualSpire-v0.5.0-macOS.zip をダウンロード](https://github.com/guppi-tw/sts2-bilingual-spire/releases/download/v0.5.0/BilingualSpire-v0.5.0-macOS.zip)
（[リリース一覧](https://github.com/guppi-tw/sts2-bilingual-spire/releases)）

SteamのMac版Slay the Spire 2がインストールされていれば使えます。
ビルド、.NET SDK、BaseLibの追加インストールは不要です。動作確認はゲームv0.113.0・Apple Siliconで行っています。

1. ゲームを終了し、上のZIPをダウンロードして展開します。GitHubが自動で付ける `Source code (zip)` は導入用ではありません。
2. SteamライブラリでSlay the Spire 2を右クリックし、「管理 → ローカルファイルを閲覧」を開きます。
3. Finderで `SlayTheSpire2.app` を右クリックして「パッケージの内容を表示」を選び、`Contents → MacOS` を開きます。
4. その中に `mods` フォルダを作ります。すでにある場合はそのフォルダを使います。
5. ZIPから出てきた `BilingualSpire` フォルダを、丸ごと `mods` の中に入れます。配置後の構成は次のとおりです。

```text
SlayTheSpire2.app/
  Contents/
    MacOS/
      mods/
        BilingualSpire/
          BilingualSpire.dll
          BilingualSpire.json
```

6. Steamからゲームを起動し、「設定 → MOD設定」で「Bilingual Spire」を有効にします。再起動を求められた場合は再起動してください。
7. F8で表示モード、F9で言語の組み合わせを変更できます。Macで反応しない場合は **fn + F8 / fn + F9** を使います。

MOD有効時の進行状況は通常プレイと別に保存されます。初めてMODを使う場合は、
`~/Library/Application Support/SlayTheSpire2/` をバックアップしてから起動してください。
通常プレイの進行が表示されなくても、元のセーブが消えたわけではありません。このMODは進行データの移行を行いません。

更新時はゲームを終了し、`mods/BilingualSpire` を新しいZIP内の同名フォルダに置き換えます。
言語や表示モードの設定は別の場所に保存されているため引き継がれます。
無効にする場合はMOD設定でオフにして再起動します。アンインストールする場合はゲーム終了後に `mods/BilingualSpire` を取り除きます。

## 使い方

| 操作 | 動作 |
| --- | --- |
| F8 | 言語1のみ → 言語2のみ → 両方表示 → 言語1のみ… |
| F9 | 右上の言語選択欄を開く・閉じる |
| 設定欄の「今すぐ切り替え」 | マウスで表示モードを切り替える |

Macで反応しない場合は **fn + F8 / fn + F9** を使ってください。
初期の組み合わせは英語・日本語です。F9からゲーム収録の16言語を選べます。
表示モードと言語の組み合わせは自動保存され、次回起動時に復元されます。

- カード、メニュー、レリック、イベント、ツールチップなどの翻訳文を更新します。
- 併記では言語1を上、言語2を下に表示します。空行で分かれた文章は、原文の各段落のすぐ下に対応する翻訳を置きます。
- 段落内の改行や文字色を保ちます。段落数が違う場合や表などを安全に分割できない場合は、全文の下に翻訳を表示します。
- 長いイベント本文は枠内をスクロールできます。選択肢は画面内に残します。
- 本体の言語設定を変更すると、その言語をMODの「言語1」に反映します。併記中なら併記を続けます。言語2と同じ言語を選ぶと、言語1と言語2を入れ替えます。
- F8では本体の保存済み言語設定を変更しません。

対応言語は英語、日本語、中国語簡体字・繁体字、ドイツ語、スペイン語（中南米・スペイン）、フランス語、インドネシア語、イタリア語、韓国語、ポーランド語、ポルトガル語（ブラジル）、ロシア語、タイ語、トルコ語です。

MODの設定ファイルは `~/Library/Application Support/SlayTheSpire2/mod_configs/BilingualSpire.json` に保存されます。
表示モードが未保存の場合は、本体の設定言語で起動します。

## ソースからビルド

開発する場合や自分でビルドする場合の手順です。通常の導入は上のZIPを使ってください。
必要なものは、SteamでインストールしたMac版Slay the Spire 2と.NET 9 SDKです。
検証に使用したSDKは9.0.321で、使用するバージョンは [global.json](global.json) に指定しています。
このMODはDLLとJSONで動作し、BaseLib・PCK・MegaDotエディターは不要です。

ゲームを終了してから実行してください。

```sh
git clone https://github.com/guppi-tw/sts2-bilingual-spire.git
cd sts2-bilingual-spire
./scripts/deploy.sh -c Release
./scripts/launch.sh
```

`deploy.sh` はビルド後、Steamの標準ライブラリにある次のフォルダへMODを配置します。

```text
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/
  SlayTheSpire2.app/Contents/MacOS/mods/BilingualSpire/
```

ゲーム内の「設定 → MOD設定」で「Bilingual Spire」を有効にしてください。
無効にする場合も同じ画面からオフにして再起動します。
`launch.sh` はSteam経由でゲームを起動します。

別のSteamライブラリを使う場合は、プロジェクト直下にGit管理対象外の `local.props` を作ります。

```xml
<Project>
  <PropertyGroup>
    <Sts2Path>/path/to/steamapps/common/Slay the Spire 2</Sts2Path>
  </PropertyGroup>
</Project>
```

Intel Mac用の参照先は `Sts2Architecture` を `x86_64` にして指定できますが、動作確認はApple Siliconのみです。
付属の起動・テストスクリプトはmacOSの標準Steam配置を対象とします。
ゲームのDLLはインストール先から参照し、このリポジトリには含めません。

読み込みを確認するには次を実行します。

```sh
./scripts/check-log.sh
```

## 動作確認

2026-11-15にゲームv0.113.0で、次を実ゲーム内のテストと画面画像で確認しました。

- 6種類のカード、強化後の数値、非公開表示、メニュー、F8/F9、16言語の切り替え
- 英日・仏韓の併記、新しく表示された文への適用、重複の防止、単独表示への復帰
- 自己啓発本、Waterlogged Scriptorium、ドール・ルーム、ネオーの英日表示
- イベント本文末尾までのスクロール、選択肢の位置、ドール・ルームの選択後のページ更新、ネオーの3段階の会話
- 本体の言語設定との連動、段落ごとの対訳、文字装飾の保持、段落数が異なる場合の全文併記
- 3モードの再起動後の復元と、復元後のF8操作・保存。本体の初期言語を毎回フランス語にして、英語・日本語への復元漏れを検出

全イベント・全言語ペア・戦闘を含む通しプレイの網羅確認はしていません。
狭い欄は文字を縮めるため、読みにくい場合はF8で単独表示に戻せます。
画像内の文字、翻訳キーがない固定文、他MODの独自表示には対応しない場合があります。
会話・説明文・選択肢を優先し、キャラクターなどの固有名詞の併記は必須としていません。

ゲーム更新後は再ビルドと動作テストが必要です。

## テストの実行

ゲームを終了してから実行してください。

```sh
python3 scripts/smoke-test.py
```

Debugビルドを配置し、Steam接続を無効にした実ゲームをヘッドレスで起動します。
一時的な非Steam用プロフィールでテストし、既存の非Steam用データは終了時に復元します。
Steam用セーブが変わっていないことと、非Steam用データの復元をSHA-256で検証します。
MODの設定ファイルもテストごとの一時フォルダに分離します。

イベント検証では保存しないテスト用ランを作り、画面表示と選択肢の操作を検査します。
その後ゲームを3回再起動し、各モードの復元とF8による次のモードへの変更・保存を検査します。
全プロセスの終了コードと成功マーカーが揃った場合だけ成功とします。

画面画像も取得する場合は次を使います。

```sh
python3 scripts/smoke-test.py --visual
```

最初のテストだけゲームウィンドウを開き、終了後は閉じます。再起動テストはヘッドレスです。
ログと画像は `.artifacts/` に保存します。既存データの退避先は `.artifacts/isolated-test-*/original-default` です。
強制終了などで退避データが残っている場合は、復元するまで `.artifacts/` を削除しないでください。

テスト後は通常用のReleaseビルドを配置してください。Releaseビルドにはテストコードを含みません。

```sh
./scripts/deploy.sh -c Release
```

## コードの構成

| 場所 | 役割 |
| --- | --- |
| `LanguageSwitcher.cs` | F8/F9、表示モード、本体の言語設定との連動 |
| `Localization/` | 翻訳キーと表示文の追跡、段落ごとの対訳、フォント調整 |
| `Patches/` | ゲームへのHarmonyパッチ、カード・イベントの表示更新 |
| `Settings/` | 言語ペアと表示モードの保存 |
| `UI/` | 言語選択欄と通知 |
| `Development/` | 実ゲーム内で動くDebug専用テスト |
| `scripts/` | ビルド・配置・起動・テスト |

ゲームの翻訳キーと変数から表示文を再生成します。カードは本体の表示更新処理も呼び、数値やキーワードの構成を維持します。
対象を特定できない文はそのまま残します。

## 開発資料

- [MODのセットアップ](https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup)
- [MODファイルの構成](https://github.com/Alchyr/ModTemplate-StS2/wiki/Modding-Basics)
- [MegaDot公式配布](https://megadot.megacrit.com/)

インストール済みゲームの公開XMLドキュメントとILSpyでAPIを確認できます。
ローカルツールのILSpyは `dotnet tool restore` で準備し、`dotnet ilspycmd` で利用します。
