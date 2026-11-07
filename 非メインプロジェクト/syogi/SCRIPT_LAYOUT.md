# スクリプト構成

`Assets/Scripts` は機能別に配置しています。

| フォルダー | 役割 |
| --- | --- |
| `Grass` | 草のグリッド、描画、餌の探索 |
| `Herbivores` | 草食動物の生成、移動、採食、資源、状態表示 |
| `Animals` | 動物共通のHP・資源・被弾・生存個体登録 |
| `Carnivores` | 肉食動物の生成、追跡、描画、射撃、捕食、状態表示 |
| `Cards` | カードデータ、所持・選択の保存、カード画面 |
| `Barrage` | 弾幕の合成、弾、プレイヤー、照準用ターゲット |
| `View` | カメラ操作、戦闘画面の操作 UI、テキストスクロール |
| `Navigation` | シーン遷移、ステージ解放、戦闘終了 |
| `Anime` | 既存の演出スクリプトとシェーダー |

Unity の参照を維持するため、スクリプトを移動するときは対応する `.meta` も一緒に移動します。
`tests` のプロジェクトが直接参照するスクリプトは、移動時に `.csproj` のパスも更新してください。

既存のコンソールテストはリポジトリ直下から実行します。Unity の Play Mode 確認とは別の検証です。

```powershell
dotnet run --project tests/GrassGrid/GrassGrid.csproj
dotnet run --project tests/CarnivoreCombat/CarnivoreCombat.csproj
dotnet run --project tests/BarrageComposition/BarrageComposition.csproj -- syogi/Assets/CardDropSamples.asset
```
