# App Garden Logic Audit

Read-only audit date: 2026-07-08

Scope:
- Unity -> App Garden logic audit only
- No DTO definition
- No sync protocol
- No prefab ingestion assumption on the App side
- Goal: help a React Three.js App approximate Unity's spatial authoring rules with low poly previews and constrained editing

Primary files reviewed:
- `Assets/_project/Scripts/SpaceSystem/SpaceBlockDefinition.cs`
- `Assets/_project/Scripts/SpaceSystem/MemorySpaceBlock.cs`
- `Assets/_project/Scripts/SpaceSystem/SpaceSegmentDefinition.cs`
- `Assets/_project/Scripts/SpaceSystem/SpaceSegmentKit.cs`
- `Assets/_project/Scripts/Editor/SpaceBlockBuilderWindow.cs`
- `Assets/_project/Scripts/Editor/SpaceSegmentKitBuilderWindow.cs`
- `Assets/_project/Scripts/SpaceSystem/SpaceOpeningPort.cs`
- `Assets/_project/Scripts/SpaceSystem/SpaceConnection.cs`
- `Assets/_project/Scripts/SpaceSystem/SpaceConnectionManager.cs`
- `Assets/Scripts/MemoryGarden/Placement/MemoryDisplayFurniture.cs`
- `Assets/Scripts/MemoryGarden/Placement/MemoryDisplaySlot.cs`
- `Assets/_project/Scripts/SpaceSystem/RoomSlotPlacementMetadata.cs`
- `Assets/_project/Scripts/SpaceSystem/RoomSlotGridUtility.cs`
- `Assets/_project/Scripts/SpaceSystem/WallSegmentSlot.cs`
- `Assets/_project/Scripts/MemorySystem/MemoryItemData.cs`
- `Assets/_project/Scripts/MemorySystem/MemoryObject.cs`
- `Assets/_project/Scripts/Editor/MemoryItemDataAssetBuilder.cs`

## A. Unity Current Spatial System Overview

Unity currently has two related but different space layers:

1. Segment authoring layer
- `SpaceSegmentDefinition` describes a reusable segment piece.
- `SpaceSegmentKit` is the lookup catalog for those pieces.
- `SpaceBlockBuilderWindow` lets a designer paint placements on a block grid and save them as `SpaceBlockDefinition`.

2. Baked block layer
- `MemorySpaceBlock` is the scene/prefab instance container.
- A block contains placed floor, wall, ceiling, threshold, beam, and overlay placements.
- Wall placements can carry doorway overlays and doorway ports.

3. Furniture and slot layer
- Furniture is not a wall segment.
- Furniture is placed onto block floor or wall surfaces using `RoomSlotPlacementMetadata`.
- Furniture owns `MemoryDisplaySlot` children, and items snap to those slots.

4. Item layer
- `MemoryItemData` holds content identity and story fields.
- `MemoryObject` adds placement logic plus VR-specific grab/observe behavior.

For the App, the clean mental model is:
- `block` = spatial container with grid and wall edges
- `segment placement` = authored structural piece on a block
- `opening port` = connection candidate on a wall opening
- `furniture placement` = authored object attached to block floor or wall
- `item placement` = runtime occupancy of a furniture slot

The App should model these as separate layers. It should not flatten them into "all prefabs in one scene graph".

## B. Block Generation Logic

### 1. How a block is composed

`SpaceBlockDefinition` stores:
- `blockId`
- `gridWidth`
- `gridDepth`
- `gridSize`
- `placements`

Each `SpaceSegmentPlacementRecord` stores:
- `placementId`
- `segmentId`
- `category`
- `gridX`
- `gridZ`
- `side`
- `rotationY`
- `footprint`
- `overlaySegmentId`
- `isConnectorCandidate`

The builder turns those records into a `MemorySpaceBlock` with child roots such as:
- `FloorSegments`
- `WallSegments`
- `CeilingSegments`
- `OpeningOverlays`
- `ConnectorPorts`
- `FurniturePlacementPoints`

In practice:
- floor, ceiling, threshold, and beam placements are grid-area placements
- wall placements are wall-edge placements
- doorway is not an independent wall category
- doorway is usually an `OpeningOverlay` attached onto a wall placement

### 2. How `gridWidth`, `gridDepth`, `gridSize` affect dimensions

Unity block size is grid-driven:
- world width = `gridWidth * gridSize`
- world depth = `gridDepth * gridSize`
- one floor cell = `gridSize x gridSize`
- wall span length = `footprint.x * gridSize`

Placement center logic is grid-centered around block origin:
- floor-like placements use cell center
- wall placements use wall edge center
- north/south walls span along X
- east/west walls span along Z

App implication:
- do not derive size from prefab bounds
- treat block dimensions as authored data first
- use prefab or preview mesh only as a visual skin

### 3. How wall side and segment placement are expressed

Wall placement uses:
- `category = Wall`
- `side = North | South | East | West`
- `gridX`, `gridZ`
- `footprint.x` as wall span length in grid units

Wall overlap is not checked with rectangle math. Unity converts a wall placement into edge keys:
- North: `H:x:z+1`
- South: `H:x:z`
- East: `V:x+1:z`
- West: `V:x:z`

That means App wall editing should also be edge-based, not free polygon editing.

### 4. Where `blockTypeId` and `blockInstanceId` currently live

There are two related IDs:

1. Block type-like ID
- `SpaceBlockDefinition.blockId`
- `RoomSlotGridUtility.GetBlockTypeId` prefers `block.blockDefinition.blockId`
- if absent, it falls back to `block.name`

2. Block instance-like ID
- `MemorySpaceBlock.spaceBlockId`
- `RoomSlotGridUtility.GetBlockInstanceId` prefers `block.spaceBlockId`
- if absent, it falls back to `block.name`

Important nuance:
- `blockId` is closer to catalog/type identity
- `spaceBlockId` is closer to placed instance identity

### 5. How App should represent blocks with low poly geometry

Recommended App preview approach:
- block floor: one procedural plane per occupied floor cell, or one merged floor slab
- wall: one plane or thin box per wall edge span
- ceiling: optional slab or translucent plane
- doorway/opening: cut or highlight on a wall plane, plus a port marker

Recommended App block preview hierarchy:
- block bounds box
- floor layer
- wall layer by side
- doorway markers
- slot markers
- furniture markers or low poly furniture meshes

Do not copy Unity's exact baked child hierarchy. Rebuild from records.

## C. Doorway / Connection Rules

### 1. Key `SpaceOpeningPort` fields

Unity doorway connection depends on:
- `openingId`
- `openingType`
- `connectionKind`
- `widthUnits`
- `height`
- `wallSide`
- `gridPosition`
- `connectorAnchor`
- `isOccupied`
- `connectedPort`
- `OwningBlock`

### 2. How doorway width, height, wall side, and position are defined

Doorway overlays become ports when attached or baked:
- `widthUnits` comes from overlay display width, rounded to integer grid units
- `height` comes from overlay display height
- `wallSide` comes from the wall placement record
- `gridPosition` comes from wall placement `gridX/gridZ`

So the doorway is anchored to wall placement data, not discovered from mesh topology.

### 3. How two blocks are judged connectable

`SpaceOpeningPort.CanConnectTo` requires:
- different ports
- neither port occupied
- same `openingType`
- same `connectionKind`
- same `widthUnits`
- nearly same `height`
- both belong to baked `MemorySpaceBlock`
- not on the same block
- facing each other when auto-align is off

App implication:
- connection UI should be a constrained "connect compatible ports" action
- it should not permit arbitrary wall-to-wall linking

### 4. How auto-align of block B works

`SpaceConnectionManager` auto-align logic:
- take port A anchor forward
- rotate block B so port B faces opposite to port A
- place block B so port B anchor lands one snap length away from port A
- current default snap length is `2f`

Important consequence:
- connection is driven by port anchors and forward vectors
- App should store or derive a lightweight port transform for each doorway preview

### 5. Overlap / collision warning behavior

Unity overlap warning is coarse:
- it uses `_BlockBounds` / block world bounds
- it checks block B moved pose against other `MemorySpaceBlock` bounds
- it produces a warning, not a detailed collision manifold

App should mirror the same intent:
- simple AABB or OBB overlap warning is enough for preview editing
- no need for mesh boolean or exact physics collision during connection authoring

### 6. What App should allow

Allowed:
- select a doorway port on block A
- show compatible ports on other blocks
- preview connect
- auto-align block B to block A
- reject or warn on bounds overlap
- explicitly allow "force connect anyway" only as an override

Not allowed:
- dragging doorway width freely
- rotating a doorway independently from its wall side
- connecting arbitrary wall faces with no port compatibility check
- stretching connection corridors by arbitrary non-grid edits

## D. Slot / Furniture Rules

### 1. Difference between item placement slot and wall segment slot

`WallSegmentSlot`:
- structural wall authoring slot
- belongs to a block wall placement
- stores `segmentId`, `overlayId`, `side`, `segmentIndex`
- can carry doorway overlays
- is about architecture

`MemoryDisplaySlot`:
- item placement slot
- belongs under a furniture object
- stores `slotId`, `slotType`, accepted sizes, occupancy
- is about placing memory items

These are different systems and should stay different in the App.

### 2. Slot and furniture identity

Furniture identity:
- `MemoryDisplayFurniture.furnitureId`

Slot identity:
- `MemoryDisplaySlot.slotId`
- `slotType`
- `acceptedItemSizes`

Placement metadata also mirrors:
- `RoomSlotPlacementMetadata.furnitureId`
- `RoomSlotPlacementMetadata.slotIds`

Current authoring helper behavior:
- `RoomSlotPlacementToolWindow` auto-generates `furnitureId` like `DF_<blockTypeId>_<slotPlacementId>` when needed
- it auto-generates slot IDs like `Slot_<slotPlacementId>_01`

### 3. Floor slot vs wall slot expression

Furniture placement surface is described by `RoomSlotPlacementMetadata.surfaceType`:
- `Floor`
- `Wall`

Floor placement uses:
- `gridX`
- `gridZ`
- `widthUnits`
- `depthUnits`
- half-grid fields such as `floorGridXHalf`, `floorGridZHalf`, `floorWidthHalf`, `floorDepthHalf`

Wall placement uses:
- `wallSide`
- `wallGridPosition`
- `widthUnits`
- `heightOffset`
- `wallLayerIndex`
- `wallLayerCount`
- `wallSurfaceHeight`

Important nuance:
- room slot placement uses half-grid for floor authoring
- this is finer than the main SpaceBlock segment grid

### 4. How a slot/furniture placement belongs to room, block, and grid

Belongs to block by:
- `RoomSlotPlacementMetadata.blockTypeId`
- `RoomSlotPlacementMetadata.blockInstanceId`

Belongs to spatial anchor by:
- floor: half-grid rectangle on block floor
- wall: wall side + wall grid position + width span + wall layer

Practical App model:
- room can be treated as the block container unless multi-room nesting is introduced later
- furniture should belong to `blockInstanceId`
- furniture placement should carry its own floor or wall anchor record
- item slots should belong to furniture, not directly to block

### 5. How App should show occupied and empty slots

Recommended marker states:
- empty: outlined cyan or neutral marker
- occupied: filled marker or item silhouette
- invalid for selected item: muted or red marker
- compatible target: highlighted marker

The App should derive occupancy from item placement state, not from mesh inspection.

### 6. How App should restrict item placement

At minimum, mirror Unity checks:
- slot is not occupied
- item placement is enabled
- item placement is currently allowed
- item's allowed slot types include slot type, or item allows all
- slot accepted sizes include item size, or slot allows all

App should not allow:
- dropping items anywhere on furniture surface
- wall furniture receiving floor-only items by manual drag
- placing multiple items onto the same slot

## E. Memory Item Rules

### 1. Where `itemId` comes from

Runtime item identity comes from:
- `MemoryObject.ItemId`
- which proxies `MemoryItemData.ItemId`

Authoring source:
- `MemoryItemDataAssetBuilder` creates `MemoryItemData` assets from FBX files
- it sets `itemId` to the FBX file name without extension
- it also assigns the data asset back onto the matching prefab

So for App use, `itemId` should be treated as content identity authored from asset naming, not a runtime-generated GUID.

### 2. How item size, allowed slot type, and current slot are expressed

Item placement fields live on `MemoryObject`:
- `enablePlacement`
- `itemSizeMode`
- `manualItemSize`
- `allowedSlotTypes`
- `preferredHeightOffset`
- `alignToSlotRotation`
- `currentSlot`

Item size rules:
- manual when `itemSizeMode = Manual`
- otherwise derived from bounds
- thresholds currently map roughly to `Small`, `Medium`, `Large`, `Tall`

Current slot:
- direct reference to `MemoryDisplaySlot`
- not a stored string on `MemoryObject`

### 3. What App item marker should keep

Recommended App item marker fields:
- `itemId`
- `itemName`
- `itemSize`
- `allowedSlotTypes`
- `currentFurnitureId`
- `currentSlotId`
- `enablePlacement`
- `preferredHeightOffset`
- optional `emotionType`

This is enough to render occupancy and validate moves without copying Unity runtime interaction code.

### 4. What item logic should stay VR-only

Do not carry these runtime behaviors into the App model:
- `XRGrabInteractable`
- held state
- observe angle / observe timer
- `MemoryModeManager` enter/exit behavior
- snap coroutine smoothing
- rigidbody lock/unlock during placement
- gaze inspection gating

The App only needs the structural placement rules, not the VR interaction stack.

## F. Low Poly Preview Asset Recommendations

### 1. Prefabs that are useful as preview references

Useful as visual references only:
- `Assets/_project/Prefabs/SegmentKit/Wall/*`
- `Assets/_project/Prefabs/SegmentKit/Floor/*`
- `Assets/_project/Prefabs/SegmentKit/Ceiling/*`
- `Assets/_project/Prefabs/SegmentKit/OpeningOverlay/*`
- `Assets/_project/Prefabs/DisplayFurniture/*`

Useful as structural references:
- `Assets/_project/Prefabs/Environment/SpaceBlocks/*`

### 2. Prefabs that should not be directly used by the App

Not suitable for direct App consumption:
- baked `SpaceBlocks` prefabs as-is
- full `DisplayFurniture` prefabs as-is
- `MemoryItems` prefabs as-is

Reasons:
- Unity scene hierarchy is optimized for authoring/runtime, not web rendering
- many behaviors depend on Unity components and bounds logic
- App needs lightweight geometry plus explicit metadata, not MonoBehaviours

### 3. Suggested preview asset naming

Recommended preview naming:
- `preview-block-<blockTypeId>`
- `preview-wall-<styleId>-<width>x<height>`
- `preview-floor-<styleId>-<width>x<depth>`
- `preview-opening-<styleId>-<width>x<height>`
- `preview-furniture-<furnitureKey>`
- `preview-item-<itemId>`

Keep App preview IDs separate from Unity prefab names, but preserve source linkage in metadata.

### 4. Recommended preview asset formats

Best fit by asset class:

1. Structural block preview
- procedural geometry first
- no GLB required for walls/floors/ceiling

2. Furniture preview
- low poly GLB for recognizable silhouettes
- PNG thumbnail for catalog cards

3. Item preview
- low poly GLB or billboard/icon depending on distance and UI mode

Recommended mix:
- blocks and connectors: procedural geometry
- furniture: GLB + thumbnail
- items: GLB + thumbnail

### 5. Minimum metadata for each block preview

Each block preview should carry at least:
- `blockTypeId`
- `blockInstanceId`
- `gridWidth`
- `gridDepth`
- `gridSize`
- list of structural placements
- list of doorway ports
- list of furniture placements
- list of slot markers
- preview bounds

## G. Logic the App Should Recreate

Recreate these rules:
- block size from `gridWidth/gridDepth/gridSize`
- floor occupancy by grid rectangle
- wall occupancy by edge keys
- doorway compatibility by width, height, type, kind, owner block, occupancy
- auto-align by port anchor transform
- overlap warning by block bounds
- furniture anchoring by floor half-grid or wall side/grid position
- slot compatibility by slot type, accepted item size, and occupancy
- item placement by `itemId -> furnitureId -> slotId`

If the App reproduces those rules, it will feel close to Unity even with simple low poly previews.

## H. Logic the App Should Not Recreate

Do not recreate:
- Unity prefab hierarchy exactly
- `Undo` behavior
- renderer-bounds based alignment details for every mesh
- runtime GameObject parenting for merged blocks
- XR grab / release behavior
- gaze observation and Memory Mode
- rigidbody / collider snap physics
- material migration or painterly shader behavior

Those are Unity runtime/editor concerns, not App authoring model concerns.

## I. Suggested App Garden Feature Directory Structure

Suggested feature split:

```text
app/features/garden/
  block-model/
  block-preview/
  block-editing/
  connection-model/
  connection-preview/
  furniture-model/
  furniture-placement/
  slot-model/
  item-model/
  item-placement/
  preview-assets/
  validation/
  mock-data/
```

Suggested responsibilities:
- `block-model`: grid, wall edge, placement shape, IDs
- `block-preview`: low poly procedural generation
- `block-editing`: constrained placement tools
- `connection-model`: ports, matching, alignment
- `furniture-placement`: floor/wall anchoring rules
- `validation`: overlap, occupancy, compatibility

## J. Suggested App Mock Data Fields

Do not treat this as final transport shape. This is only a planning checklist for mock data.

Block mock data should be able to express:
- type identity
- instance identity
- grid dimensions
- structural placements
- wall side and wall spans
- doorway ports
- bounds

Furniture mock data should be able to express:
- furniture identity
- preview asset key
- owning block instance
- surface type
- floor half-grid or wall anchor
- slot list
- optional light/frame counts

Slot mock data should be able to express:
- slot identity
- owning furniture identity
- slot type
- accepted item sizes
- occupied item identity or null
- local anchor transform

Item mock data should be able to express:
- item identity
- display name
- size
- allowed slot types
- current furniture identity
- current slot identity
- optional emotion type

Connection mock data should be able to express:
- source block/port
- target block/port
- connector width/height/length
- auto-aligned target transform
- overlap warning state

## K. If Unity Later Exports a Preview Catalog

Unity-side exporter tools that would help later:

1. Block preview catalog exporter
- exports `SpaceBlockDefinition`
- exports normalized placement records
- exports doorway port records
- exports simplified bounds

2. Furniture placement catalog exporter
- exports `RoomSlotPlacementMetadata`
- exports `furnitureId`
- exports `slotIds`
- exports floor/wall anchoring data

3. Item preview catalog exporter
- exports `MemoryItemData` summary
- exports item size and allowed slot types from `MemoryObject`

4. Preview asset manifest exporter
- maps Unity prefab or asset name to App preview asset key
- separates procedural-preview candidates from GLB candidates

These are editor exporters only. They should export normalized metadata, not raw prefab internals.

## L. Current Blockers and Risks

1. Structural authoring and furniture placement use related but different grid rules
- SpaceBlock segment authoring uses whole-cell placement and wall edge spans
- room slot placement adds half-grid floor resolution
- App must keep both layers, not collapse them

2. East/west wall authoring is asymmetric in some builder paths
- editable wall slot generation excludes corners on east/west rebuilds
- room slot utility treats wall spans at full wall-length abstraction
- App should model wall spans from resolved edge anchors, not literal Unity child count

3. Doorway is mesh-overlay driven, not topological
- Unity infers doorway ports from overlay metadata and naming/size rules
- App cannot discover this by looking at a wall mesh alone

4. Some IDs have fallback behavior
- `blockTypeId`, `blockInstanceId`, `furnitureId`, and `slotId` may fall back to object names if authoring data is incomplete
- App mock data should not depend on fallback names long term

5. Occupancy is component-state based in Unity
- `currentSlot` and `isOccupied` are live references/state
- App should derive occupancy from explicit placement state instead of trying to mirror live Unity references

6. Current repo does not yet contain a web-ready preview catalog
- there is no existing App-facing export layer in the audited scope
- low poly preview generation will need either procedural generation or a later exporter pass

## Recommended App Strategy

If the goal is "feel like Unity without loading Unity prefabs", the best approach is:

1. Rebuild blocks procedurally from grid records.
2. Represent walls as constrained edge spans, not arbitrary meshes.
3. Represent doorway ports as explicit metadata with anchors and compatibility fields.
4. Represent furniture as anchored preview objects with child slot markers.
5. Represent items as slot occupancy records with a lightweight preview.

That will stay close to Unity's editing logic while remaining simple enough for React Three.js.
