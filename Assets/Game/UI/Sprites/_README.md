# UI Sprite Library

All assets under this folder are auto-processed by `Assets/Game/Scripts/Editor/UISpriteImportPostprocessor.cs` with these settings:

- Texture type: Sprite
- Pixels per unit: 100
- Sprite mesh type: Tight
- Filter mode: Bilinear
- Mip maps: off
- Alpha is transparency: on
- Compression: CompressedHQ, no crunch (painted assets band visibly under crunch)

See [ADR 0007](../../../../docs/architecture/adr/0007-ui-art-style-vintage-hotel.md) for the full asset style contract.

## Folder layout (matches ADR 0007 atlases)

| Folder | Atlas | Contents |
|---|---|---|
| `Banners/` | `Atlas_Banners` | Hero strips, section headers, the OLD TOWN HOTEL wordmark |
| `Cards/` | `Atlas_Cards` | 9-sliced card / panel / modal backgrounds and progress bars |
| `Buttons/` | `Atlas_Cards` (or split if size grows) | 9-sliced button skins: primary / secondary / danger |
| `Icons/` | `Atlas_Icons` | State badges, bed types, preferences, inventory items, dishwasher, top-bar glyphs, mood emojis |
| `Portraits/` | `Atlas_Portraits` | Guest + worker circle-cropped portraits |
| `Nav/` | `Atlas_Nav` | Bottom-nav tab icons (active + inactive variants) |

## Naming

`[category]_[name]_[variant]_[size].png` — e.g. `icon_state_ready.png`, `portrait_guest_business.png`, `button_primary.png`, `banner_lounge.png`.

## When a new asset arrives

1. Drop it into the right folder.
2. The postprocessor applies the import settings automatically (no manual fiddling).
3. If it's a 9-slice (card / button / modal / progress bar), open it in Inspector and set the **Border** to the value from ADR 0007 § "9-Slice / Sprite Import Standards".
4. Add it to the matching Sprite Atlas when ready.
