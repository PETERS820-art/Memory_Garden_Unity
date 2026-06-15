# Memory Garden Unity

Unity XR prototype project for `Memory Garden`.

## Requirements

- Unity Hub
- Unity `2022.3.62f3c1`
- Git
- Git LFS

## Clone And Open Locally

Use Git LFS when cloning this repository. Some project assets such as textures, audio, models, and HDRI files are stored with LFS.

```bash
git lfs install
git clone https://github.com/PETERS820-art/Memory_Garden_Unity.git D:\unity_project\MemoryGarden_Unity_0_0
cd D:\unity_project\MemoryGarden_Unity_0_0
git lfs pull
```

Then open the project in Unity Hub:

1. Open Unity Hub.
2. Click `Open` or `Add project from disk`.
3. Select `D:\unity_project\MemoryGarden_Unity_0_0`.
4. Use Unity editor version `2022.3.62f3c1`.

If Hub does not have that version installed yet, install `2022.3.62f3c1` first and then reopen the project from Hub.

## What Is Included

This repository includes the files needed to open and work on the project:

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `.meta` files

## What Is Not Included

These folders and files are intentionally ignored because Unity or local tools regenerate them:

- `Library/`
- `Temp/`
- `Obj/`
- `Build/`
- `Builds/`
- `Logs/`
- `UserSettings/`
- `.vs/`
- `.vscode/`
- `.idea/`
- `.vsconfig`
- `*.csproj`
- `*.sln`

If these are missing after clone, that is expected.

## First Open Notes

- The first project open may take a while because Unity will rebuild `Library/`.
- If large assets look missing, make sure Git LFS is installed and run `git lfs pull`.
- This project uses URP, OpenXR, and XR Interaction Toolkit.
- If materials, models, or HDRI assets look pink or missing, first re-check `git lfs pull`.

## Package Note

The project depends on this Git package:

- `cn.unity.uos.launcher`: `https://cnb.cool/unity/uos/UOSLauncher.git`

If Unity reports package download errors on first open, check whether this URL is reachable from your network.

## Main Editor Tools

The custom editor tools in this repo live under the Unity menu:

`Tools/Memory Garden/*`

### Build Memory Item Data Assets

Menu: `Tools/Memory Garden/Build Memory Item Data Assets`

Use this when new Memory Item FBX files are added under:

`Assets/_project/Art/Models/MemoryItems`

What it does:

- Creates or updates `MemoryItemData` assets beside the FBX files in each item's `Data/` folder.
- Fills default fields such as `itemId`, `itemName`, and `emotionType`.
- Tries to auto-assign the generated data asset onto the matching memory item prefab.

Typical workflow:

1. Import or update Memory Item FBX files.
2. Run `Build Memory Item Data Assets`.
3. Open the generated `*_Data.asset` files and edit story text, emotion, and other metadata.

### Build Memory Item Prefabs

Menu: `Tools/Memory Garden/Build Memory Item Prefabs`

Use this to generate or rebuild interactable memory item prefabs from FBX sources.

What it does:

- Creates prefabs under `Assets/_project/Prefabs/MemoryItems`.
- Builds a root object, model container, collider, rigidbody, `XRGrabInteractable`, `MemoryObject`, and respawn setup.
- Tries to assign the matching `MemoryItemData` automatically.

Typical workflow:

1. Select one or more Memory Item FBX files or source folders in the Project window.
2. Open `Build Memory Item Prefabs`.
3. Use `Build Selected Memory Item Prefabs` for targeted generation.
4. Use `Build All Memory Item Prefabs` for missing prefabs only.
5. Use `Rebuild Existing Prefabs` only when you intentionally want to overwrite previously generated prefabs.

### Segment Kit Builder

Menu: `Tools/Memory Garden/Segment Kit Builder`

Use this when SegmentKit FBX source assets have changed and the environment building kit needs to be regenerated.

Default source/output folders:

- Source: `Assets/_project/Art/Models/SegmentKit`
- Prefabs: `Assets/_project/Prefabs/SegmentKit`
- Definitions: `Assets/_project/ScriptableObjects/SegmentKit`

Typical workflow:

1. Put or update SegmentKit FBX assets in the source folder.
2. Run `Create Missing Folders` if needed.
3. Run `Build / Update All Segment Prefabs`.
4. Run `Generate / Update Segment Definitions`.
5. Run `Generate / Update Default SegmentKit Asset`.
6. Run `Validate Segment Naming` if the generated output looks incomplete or naming might be off.

### Space Block Builder

Menu: `Tools/Memory Garden/Space Block Builder`

Use this to assemble room blocks from SegmentKit content.

Main modes:

- `QuickRectangularBlock`: fast room/block generation from a selected SegmentKit.
- `GridPaintMode`: hand-place floor, wall, ceiling, and opening segments on a 1m authoring grid, then save/bake reusable block data.

QuickRectangularBlock workflow:

1. Choose a `Segment Kit`.
2. Set `Width Units`, `Depth Units`, and optional `Wall Style Filter`.
3. Click `Build New SpaceBlock`.
4. Select generated `WallSegmentSlot` objects in the hierarchy if you need to swap wall variants in the Inspector.

GridPaintMode workflow:

1. Choose a `Segment Kit`.
2. Set `Block Id`, grid size, and category filter.
3. Pick a segment from the palette.
4. Enable `Scene Placement`.
5. Hover in Scene view to preview placement.
6. Left-click to place, press `R` to rotate, and use `Shift + Click` or `Scene Delete Mode` to remove placed segments.
7. Save with `Save Block Definition`.
8. Bake with `Bake Block Prefab` when the block is ready to reuse.

### Memory Mode Painterly Materials

The Memory Mode painterly look uses emotion-driven material assets under:

`Assets/_project/Art/Memory Materials`

Current workflow:

- Each `MemoryObject` reads its `emotionType` from the linked `MemoryItemData`.
- `MemoryModeShaderManager` resolves that emotion through `EmotionMaterialLog`.
- When Memory Mode starts, non-focused stylizable renderers receive runtime painterly materials automatically.
- The focused memory item keeps its original materials.
- You do not need to manually replace all scene materials with the painterly shader.

Tuning workflow:

1. Enter Play Mode.
2. Trigger Memory Mode by focusing/picking up a memory item.
3. Edit the active `MMS_*` material in `Assets/_project/Art/Memory Materials`.
4. The runtime painterly materials will live-sync from that source material while Memory Mode stays active.

## Collaboration Tips

Before starting work:

```bash
git pull
git lfs pull
```

Before pushing changes:

```bash
git status
git add .
git commit -m "Your message"
git push
```
