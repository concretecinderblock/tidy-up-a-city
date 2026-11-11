# Art bible — Tidy Up a City

1. **Style** — Soft storybook old town: rounded, slightly chunky shapes; gentle hand-made textures (wood grain, plaster, tiles, cobbles) with soft normal-map relief; nothing sharp-edged or flat-grey.
2. **Palette** (source of truth: `Scripts/Core/Palette.cs`)

| Role | Name | Hex |
|---|---|---|
| Floors | Honey oak | `#A0784F` |
| Walls | Cream plaster | `#EADBC0` |
| Furniture | Walnut | `#6B4A32` |
| Props / greenery | Sage | `#6E8F6A` |
| Roofs / warm accent | Terracotta | `#C9714D` |
| Stone / street | Warm stone | `#B8AE9C` |
| Shadow tint | Cool slate | `#6B7A8F` |
| Highlight (glow, sparkle, Tidy Sense) | Amber | `#F2B134` |
| Text on light | Ink | `#3D2A1C` |
| Text on dark / paper | Cream | `#FFF4DF` |

Location tints stay in the same warm family: Bakery cream and teal, Library honey and forest green, Magic Quarter lilac and plum.

3. **Mood** — Late-afternoon sun, warm lamps lit in every room, cool soft shadows. Nothing is ever dark or grey: restoration adds life (NPCs, music), never colour.

## Rules

- Textures are generated (`Tools/Tidy Up/Art/Generate Textures`), mostly light values so materials tint them; 10–20% value variation, never noisy.
- Meshes are generated with rounded edges (`MeshGen`); UVs are in metres, so every texture has the same scale on every object.
- One material per colour × texture; shared across objects (SRP Batcher).
- Real models can replace any item through the CSV `prefab` column; keep their palette within this table.
