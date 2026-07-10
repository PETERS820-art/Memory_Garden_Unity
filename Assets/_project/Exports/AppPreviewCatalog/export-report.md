# App Preview Catalog Export Report

- Schema version: `app-preview-catalog-v0`
- Generated at (UTC): `2026-07-10T10:31:38.6111099Z`
- Export directory: `Assets/_project/Exports/AppPreviewCatalog`
- Scan mode: asset/prefab scan plus currently loaded scene layout snapshot.

## Scan Scope
- `Assets/_project/ScriptableObjects`
- `Assets/_project/Data`
- `Assets/_project/Prefabs/Environment/SpaceBlocks`
- `Assets/_project/Prefabs/DisplayFurniture`
- `Assets/_project/Prefabs/MemoryItems`
- `Garden layout scene: Assets/_project/Scenes/00_Prototype/_02_VR_test_Displayzone.unity`

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
- layout block instances exported: 2
- layout connections exported: 1
- layout furniture placements exported: 13
- layout slot placements exported: 9
- layout item placements exported: 4
- warnings: 11

## Warning Summary
- `duplicate_blockInstanceId`: 1
- `duplicate_blockTypeId`: 5
- `duplicate_slotId`: 1
- `unplaced_item`: 4

### Warning Details
- `duplicate_blockInstanceId` | `Assets/_project/Scenes/00_Prototype/_02_VR_test_Displayzone.unity` | Duplicate blockInstanceId 'Block_01' found in 02 scene. Exporting 'Block_01--01-ENVIRONMENT_MemoryDisplayZoneRoot_PlacementAreas_PF_SB_Palace1_ConnectedBlocks_PF_SB_Palace2' to keep layout preview IDs unique.
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_01 1.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_01_1' to keep v0 preview records unique.
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_01 2.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_01_2' to keep v0 preview records unique.
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_01 3.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_01_3' to keep v0 preview records unique.
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_01.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_01' to keep v0 preview records unique.
- `duplicate_blockTypeId` | `Assets/_project/ScriptableObjects/SpaceBlocks/SBD_Block_02.asset` | Block type 'Block_01' appears multiple times. Using previewAssetKey 'preview-block-SBD_Block_02' to keep v0 preview records unique.
- `duplicate_slotId` | `Assets/_project/Prefabs/DisplayFurniture/PF_DF_plith_001.prefab` | Duplicate slotId 'Slot_Center' found in 02 scene. Exporting 'Slot_Center--01-ENVIRONMENT_MemoryDisplayZoneRoot_PlacementAreas_PF_SB_Palace1_SlotRoot_RSP_19198168_Slots_Slot_Center' to keep layout preview IDs unique.
- `unplaced_item` | `Assets/_project/Prefabs/MemoryItems/PF_MI_cube_001.prefab` | MemoryObject 'PF_MI_cube_001' is not assigned to a MemoryDisplaySlot.
- `unplaced_item` | `Assets/_project/Prefabs/MemoryItems/PF_MI_doll_001.prefab` | MemoryObject 'PF_MI_doll_001' is not assigned to a MemoryDisplaySlot.
- `unplaced_item` | `Assets/_project/Prefabs/MemoryItems/PF_MI_sculpture_001.prefab` | MemoryObject 'PF_MI_sculpture_001' is not assigned to a MemoryDisplaySlot.
- `unplaced_item` | `Assets/_project/Prefabs/MemoryItems/PF_MI_vermiu_001.prefab` | MemoryObject 'PF_MI_vermiu_001' is not assigned to a MemoryDisplaySlot.

## Layout Preview
- file: `Assets/_project/Exports/AppPreviewCatalog/garden-layout-preview.json`
- sourceScene: `Assets/_project/Scenes/00_Prototype/_02_VR_test_Displayzone.unity`
- blockInstances: 2
- connections: 1
- furniturePlacements: 13
- slotPlacements: 9
- itemPlacements: 4
- layout warnings: 6

## Layout Warning Focus
- `missing_blockInstanceId`: 0
- `missing_blockTypeId`: 0
- `missing_portId`: 0
- `missing_furnitureId`: 0
- `missing_slotId`: 0
- `missing_itemId`: 0
- `duplicate_blockInstanceId`: 1
- `duplicate_furnitureId`: 0
- `duplicate_slotId`: 1
- `missing_layout_anchor`: 0
- `unanchored_furniture`: 0
- `unplaced_item`: 4

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