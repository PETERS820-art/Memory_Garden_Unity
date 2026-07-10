# App Preview Catalog Export Report

- Schema version: `app-preview-catalog-v0`
- Generated at (UTC): `2026-07-10T10:09:36.4460387Z`
- Export directory: `Assets/_project/Exports/AppPreviewCatalog`
- Scan mode: asset/prefab scan plus currently loaded scene layout snapshot.

## Scan Scope
- `Assets/_project/ScriptableObjects`
- `Assets/_project/Data`
- `Assets/_project/Prefabs/Environment/SpaceBlocks`
- `Assets/_project/Prefabs/DisplayFurniture`
- `Assets/_project/Prefabs/MemoryItems`

## Export Counts
- block definitions scanned: 5
- space block prefabs scanned: 7
- furniture prefabs scanned: 10
- furniture placements scanned: 0
- memory item prefabs scanned: 4
- memory item data assets scanned: 0
- blocks exported: 5
- furniture records exported: 10
- slots exported: 17
- items exported: 4
- doorway ports exported: 11
- layout block instances exported: 0
- layout connections exported: 0
- layout furniture placements exported: 0
- layout slot placements exported: 0
- layout item placements exported: 0
- warnings: 6

## Warning Summary
- `duplicate_blockTypeId`: 5
- `missing_layout_blocks`: 1

### Warning Details
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_01 1.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_01_1' to keep v0 preview records unique.
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_01 2.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_01_2' to keep v0 preview records unique.
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_01 3.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_01_3' to keep v0 preview records unique.
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_01.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_01' to keep v0 preview records unique.
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_02.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_02' to keep v0 preview records unique.
- `missing_layout_blocks` | `(untitled scene)` | No MemorySpaceBlock instances were found in currently loaded scenes or the selected root GameObject.

## Layout Preview
- file: `Assets/_project/Exports/AppPreviewCatalog/garden-layout-preview.json`
- sourceScene: `(untitled scene)`
- blockInstances: 0
- connections: 0
- furniturePlacements: 0
- slotPlacements: 0
- itemPlacements: 0
- layout warnings: 1

## Layout Warning Focus
- `missing_blockInstanceId`: 0
- `missing_blockTypeId`: 0
- `missing_portId`: 0
- `missing_furnitureId`: 0
- `missing_slotId`: 0
- `missing_itemId`: 0
- `missing_layout_anchor`: 0
- `unanchored_furniture`: 0
- `unplaced_item`: 0

## Procedural-Only Content
- `preview-block-SBD_Block_01_1` rebuilt from grid and wall-edge data
- `preview-block-SBD_Block_01_2` rebuilt from grid and wall-edge data
- `preview-block-SBD_Block_01_3` rebuilt from grid and wall-edge data
- `preview-block-SBD_Block_01` rebuilt from grid and wall-edge data
- `preview-block-SBD_Block_02` rebuilt from grid and wall-edge data

## Good GLB Candidates Later
- furniture: `preview-furniture-DF_plith_001` (stand)
- furniture: `preview-furniture-DF_shelf_001` (shelf)
- furniture: `preview-furniture-DF_shelf_003` (shelf)
- furniture: `preview-furniture-DF_wallshelf_001` (wallShelf)
- furniture: `preview-furniture-PF_DF_carpet_001` (lowTable)
- furniture: `preview-furniture-PF_DF_frame_001` (frame)
- furniture: `preview-furniture-PF_DF_lamp_001` (stand)
- furniture: `preview-furniture-PF_DF_lamp_002` (stand)
- furniture: `preview-furniture-PF_DF_plith_002` (stand)
- furniture: `preview-furniture-PF_DF_shelf_002` (shelf)

## Prefabs Not Recommended For Direct App Use
- `Assets/_project/Prefabs/Environment/SpaceBlocks/*` should stay metadata-driven and procedural in the App.
- `Assets/_project/Prefabs/DisplayFurniture/*` should be simplified to primitive or later GLB previews.
- `Assets/_project/Prefabs/MemoryItems/*` should stay lightweight and should not carry XR or Memory Mode logic.

## React App Next Step
- Yes: wire the React Three.js App to read these preview catalogs next.
- Start with `block-preview-catalog.json` for procedural block generation, then join `furniture-preview-catalog.json` on block identity and anchors.
- Keep `preview-asset-manifest.json` as the lookup layer for renderer choice, silhouette fallback, and later GLB upgrades.