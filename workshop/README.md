# Steamワークショップ配布

[Bilingual Spire](https://steamcommunity.com/sharedfiles/filedetails/?id=3840138909) の公開用ファイルです。

- `description.bbcode`：英語・日本語の紹介文。`@MOD_VERSION@` は配布ZIPのバージョンに置き換えます。
- `metadata.json`：タイトル、タグ、対応ブランチ、依存MOD。
- `item-id.txt`：既存のWorkshop項目ID。更新時も同じ項目を使います。
- `image.png`：512 × 512、1MB未満のサムネイル。
- `image-prompt.md`：画像の生成条件。

配布内容はGitHub ReleasesのDLL・JSONと同一です。ゲーム本体のDLL、セーブ、ログ、開発用テストはアップロードしません。

## 更新手順

検証を通過したリリースZIPと、その `.zip.sha256` を用意します。ゲームとMODのバージョンが変わったら紹介文の動作確認環境も更新してください。

```sh
python3 scripts/prepare-workshop.py .artifacts/release-v0.5.7/BilingualSpire-v0.5.7-macOS.zip \
  --visibility public --change-note 'Update to v0.5.7'
```

作業フォルダは `.artifacts/workshop/BilingualSpire` です。公開IDを `mod_id.txt` に設定するため、作業フォルダを作り直しても同じ項目を更新します。別の項目IDや想定外の配布ファイルがある場合は停止します。`--visibility` を省略すると非公開用の設定になるので、公開項目の更新時は `public` を明示してください。

[Mega Crit公式アップローダー](https://github.com/megacrit/sts2-mod-uploader/releases) のMac版を展開し、Steamへログインした状態で、そのディレクトリから実行します。

```sh
./ModUploader upload -w /absolute/path/to/sts2-mod/.artifacts/workshop/BilingualSpire
```

終了コードと成功ログを確認し、Steamから取得したDLL・JSONが配布元と一致することを確認します。新しい項目は作らず、保存されたIDを使って更新します。

## 確認した範囲

2026-11-15、macOS Apple Silicon、ゲームpublic-beta v0.113.0で確認しました。

- 公式アップローダーv0.2.0による非公開登録と、Steam APIによるサブスクライブ・ダウンロード。
- Steam取得ファイルとGitHub Releases v0.5.7のDLL・JSONのバイト一致。
- Steam取得フォルダをゲーム本体の `mods_STEAMTEST` からSteamWorkshop種別で読み込み、Release版の初期化と言語1表示が成功すること。
- 起動テストはSteam接続を切り、テスト用セーブを使って実行。既存のSteam用セーブと非Steam用セーブが変わっていないことをSHA-256で確認。

Steam接続を有効にした状態での通しプレイは、この検証には含みません。v0.5.7のキー操作・表示・再起動の回帰テストは[READMEの動作確認](../README.md#動作確認)を参照してください。
