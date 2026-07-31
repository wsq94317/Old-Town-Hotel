# Furniture Prefab Workflow

## Guest rooms

- Gameplay furniture definitions remain in `FurnitureCatalog`; do not change stable `kindId` values.
- Art is configured in `Assets/Game/Resources/HotelArt/FurnitureVisualCatalog.asset`.
- Every furniture kind reserves four visual variants. Variant `0` contains the current low-poly placeholder.
- Assign a future model to an empty variant, then optionally set per-renderer material overrides and local position, rotation, or scale corrections.
- The room furniture screen lists every configured variant. Cosmetic changes do not alter price, wear, faults, or guest appeal.
- The selected `visualVariantId` is saved per furniture instance. Missing variants safely fall back to variant `0`.

## Public areas

- Public-area furniture prefabs live under `Assets/Game/Prefabs/Furniture/Scene/Floor*`.
- Each placed prefab instance has a `SceneFurnitureSlot` component with a stable placement ID and four reserved options.
- Option `0` is the existing model. Additional options can use another prefab, material overrides, and transform corrections.
- `TrySelect` applies a configured option at runtime, so a later facility-renovation UI can use the same component directly.

## Rebuilding

Use `Old Town Hotel > Art > Export All Furniture Prefabs` after adding new procedural scene furniture. The exporter preserves existing prefab assets and configured variant slots; it only creates missing assets and reconnects new scene furniture.
