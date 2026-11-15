# サムネイルの生成条件

内蔵 `image_gen` で生成し、同じツールでフラットな配色へ調整しました。Workshopの容量制限に合わせて512 × 512のPNGへ書き出しています。最終画像は `image.png`、ゲーム画面のプレビューは `previews/01-language-settings.jpg` です。プレビューはv0.5.7の分離テストでゲームから直接取得した画面です。

## 最初の生成プロンプト

```text
Use case: ads-marketing
Asset type: Square Steam Workshop thumbnail for an existing language-learning mod named Bilingual Spire for Slay the Spire 2.
Primary request: Create a polished, distinctive thumbnail communicating that holding the Z key reveals a translation while reading a card game. Readable at small thumbnail size.
Subject: Two overlapping fantasy playing-card shapes with large clean glyphs A and あ, a small keyboard keycap marked Z, and a simple translation arrow connecting the cards. Original graphic, no characters or official game logos.
Style/medium: Clean illustrated game-mod cover with bold outlines, mostly flat color areas and very restrained texture. A dark slate blue backdrop and warm parchment/gold accents consistent with a fantasy card-game UI.
Composition/framing: 1:1 square. Clearly spaced title and compact central card symbol with large glyphs; uncluttered and high contrast.
Text (verbatim): "BILINGUAL SPIRE" and "Hold Z to translate". Card glyphs: "A" and "あ". Keycap: "Z". No other text.
Constraints: 1024 by 1024 square image. Keep text accurate and legible. Use a small color palette and simple areas that compress well as PNG under 1MB. Opaque background. No watermark. No official endorsement or borrowed game art.
```

## 調整プロンプト

```text
Make a losslessly compressible flat-color Steam Workshop thumbnail from this image. Preserve the exact composition, objects, silhouette, all lettering and spelling, including BILINGUAL SPIRE, A, あ, Z and Hold Z to translate. Change only the rendering style to a crisp flat vector-like illustration using at most 12 solid colors. Remove ALL paper texture, grain, noise, gradients and soft shading from EVERY region including the background, title, cards and keycap. Use uniform flat fills, with hard-edged simple shadows only. Keep the same dark blue, gold, parchment and red visual theme, but drastically reduce raster complexity so the PNG file is less than 1 MB. Prefer 512x512 square output. Opaque background. No additional text or decoration.
```
