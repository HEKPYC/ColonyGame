# Settlement Prototype

## Scope and setup

This vertical slice provides the initial Tile world, completed starting Town Hall, inspection camera, prototype House placement, immediate completion, and one-time territory expansion. It adds no construction resources, timers, builders, residents, upgrades, demolition, roads, economy, or world persistence. The approved world design remains unchanged.

Use Unity **6000.6.4f1**. For the existing, manually verified scene, **do not regenerate the scene or materials**. Exit Play Mode, allow scripts to compile/import, then open `Assets/Scenes/SettlementPrototype.unity` and enter Play Mode. Unity must generate `HousePlacementTests.cs.meta` on import; retain it with the new test source.

For a new checkout without the scene, use **ColonyGame > Create Settlement Prototype Scene** once. The command creates the scene and eight URP Unlit prototype materials, adds the prototype after MainMenu in the build scene list, preserves other entries, and refuses to overwrite an existing prototype. Future scenes use the same updated runtime view automatically; no setup-command changes are needed.

MainMenu, profiles, settings, packages, and project settings are unchanged by this slice. MainMenu remains the launch scene; profile selection still opens the existing placeholder. Inspect SettlementPrototype directly for now.

## Domain architecture

All paths below are relative to the repository root.

| File | Responsibility |
| --- | --- |
| `Assets/ColonyGame/World/ColonyGame.World.asmdef` | Pure C# domain assembly without Unity engine references. |
| `Assets/ColonyGame/World/TileCoordinate.cs` | Integer X/Z identity, equality, and hashing. |
| `Assets/ColonyGame/World/SettlementState.cs` | Individual claimed Tiles, building state/list, authoritative occupancy, House validation/placement, and one-time influence. |
| `Assets/ColonyGame/Scripts/WorldPresentation/SettlementPrototypeView.cs` | Mouse-to-Tile conversion, preview, placement requests, runtime ground/building/forest presentation, and debug counts. |
| `Assets/ColonyGame/Scripts/WorldPresentation/PrototypeCamera.cs` | Existing Input System pan/zoom with inspection bounds following rendered territory extent. |
| `Assets/ColonyGame/Editor/SettlementPrototypeSetup.cs` | Existing non-overwriting scene/material setup. |
| `Assets/ColonyGame/Tests/Editor/SettlementStateTests.cs` | Four existing initial-state tests. |
| `Assets/ColonyGame/Tests/Editor/HousePlacementTests.cs` | Seven focused House placement/influence tests. |

`SettlementTerritory` owns a private `HashSet<TileCoordinate>`. Membership defines ownership; a rendered rectangle does not. It copies/deduplicates constructor input and does not expose the mutable set. Influence adds individual Tiles through the domain assembly.

`BuildingState` stores identity, type, origin, width/depth, orientation in 90-degree increments, completion, starting-building exemption, occupied coordinates, and whether influence has applied. Houses receive distinct IDs and occupy 3 x 3 Tiles. Orientation is preserved as state (currently 0 degrees); a rotation interface and future rectangular building types are deferred. The starting Town Hall retains ID `starting-town-hall` and its 5 x 5 footprint.

`SettlementState` owns a read-only building list and authoritative occupied-Tile set. `ValidateHousePlacement` is read-only: all nine footprint Tiles must already be claimed and none may belong to another building. `TryPlacePrototypeHouse` rejects invalid placement without mutation; valid placement creates a completed House and applies its influence immediately. Rendering, camera movement, and preview validation never apply influence.

`ApplyBuildingInfluence` accepts only a building belonging to that settlement. It returns the number of newly claimed Tiles, marks influence applied even when that number is zero, and returns zero on subsequent calls. The starting Town Hall is explicitly exempt and never expands the initial territory. No resources are awarded. State resets when Play Mode ends; no save format or save/load system is introduced.

## Coordinates and runtime presentation

- Initial claimed range: **X = 0..19, Z = 0..19**, inclusive: **400 Tiles**.
- Starting Town Hall origin: **(7, 7)**; X/Z = 7..11, inclusive: **25 occupied Tiles**.
- One logical Tile is drawn as exactly **one 1 x 1 world cell** on the X/Z plane. Y is visual height. Final physical scale remains undecided.
- Each ground quad fills `[X, X+1]` and `[Z, Z+1]`.
- Uniform runtime grid strips mark every integer boundary: 0.04-unit interior width, elevated 0.01 units to avoid coplanar artifacts, clipped at outer rendering edges. There are no major lines, chunks, or grouped cells.

**The grid is game presentation and remains visible independently of Editor Gizmos, including standalone builds.** Ground uses claimed, Wilderness, occupied-footprint, and grid materials. Every building's reserved footprint is highlighted in gold. Compact primitive House/Town Hall markers leave the footprint cells readable; visuals do not define occupancy.

Initially the renderer covers X/Z = -6..25, inclusive. After placement it rebuilds a finite visual area extending six Tiles beyond the actual claimed extent. This visual bounding rectangle does not claim land: holes and Wilderness beside protrusions retain their domain membership and appearance. Camera inspection bounds follow that area so chained expansion remains accessible.

Sparse deterministic trees exist only on unclaimed rendered Tiles. Refresh disables the previous presentation immediately, rebuilds from domain state, and removes decoration from newly claimed Tiles. Trees have no retained colliders, resource entities, harvesting, or persistence. The preview uses nine separated green/red cells so individual boundaries remain readable.

## Controls and placement

Focus Game View before using controls.

- **WASD / arrow keys**: pan.
- **Middle mouse drag**: ground-plane pan.
- **Mouse wheel**: orthographic zoom, sizes 5..28.
- **H**: toggle House placement mode.
- **Left click**: place when the preview is valid; mode stays active for additional Houses.
- **Escape / H**: cancel placement mode.

The mouse ray intersects the ground plane and floors local X/Z to the footprint origin. The preview covers origin X..X+2 and Z..Z+2. **Green** means all nine Tiles are claimed and free. **Red** means some footprint Tile is Wilderness or occupied by the Town Hall/another House. Invalid clicks change nothing; influence cannot make an initially invalid placement legal. The debug panel displays cursor origin, validity/reason, Claimed Tiles, Building count (including Town Hall), original dimensions, and camera controls. Clicking over the panel does not place a House.

## Influence and the clean-edge test

A completed House claims its **3 x 3 footprint plus three Tiles outward on every side**: a **9 x 9** influence area.

For origin `(x, z)`, influence is inclusive X = x-3..x+5 and Z = z-3..z+5. Already claimed Tiles are deduplicated. Territory remains an arbitrary set, never a resized settlement rectangle.

To reproduce **400 -> 427**:

1. Start a fresh Play Mode session. Confirm **Claimed Tiles: 400**, **Buildings: 1**, and Town Hall occupancy 25.
2. Press **H**. Move the cursor until the debug label shows origin **(17, 8)** and a green preview. Its footprint occupies X = 17..19, Z = 8..10, entirely inside the initial right boundary.
3. Left click once. Confirm **Claimed Tiles: 427**, **Buildings: 2**, and a completed House marker.
4. Count the new protrusion: **X = 20..22, Z = 5..13**, exactly **3 x 9 = 27 newly claimed Tiles**. The full influence is X = 14..22, Z = 5..13.
5. Verify Wilderness above/below that protrusion remains unclaimed (for example `(20, 4)` and `(20, 14)`), as does `(23, 8)`. Trees on newly claimed Tiles disappear without resources. Grid cells retain their original dimensions and uniform lines.
6. Move the cursor to **(20, 8)**. It was illegal before expansion but is now green. Place a second House: **454 claimed Tiles**, **3 buildings**. Its footprint uses the first House's newly claimed territory; expansion can continue outward.

Additional manual checks:

- In a fresh session, place at `(3, 3)`: all influence was already claimed, so count remains **400** and buildings becomes **2**.
- In a fresh session, try `(18, 8)`: one footprint column is Wilderness; red preview and invalid clicks leave **400 / 1**.
- Try overlapping Town Hall at `(6, 6)`, or overlapping an existing House: red preview and no mutation.
- Toggle/cancel repeatedly, move the preview through Wilderness, pan/zoom, and refresh the presentation through further placements. Counts must change only after successful placement.
- Disable Game View Gizmos and inspect the grid, including all 25 Town Hall cells and nine House footprint cells. Verify camera controls and common desktop aspect ratios.
- Restart Play Mode and verify the initial **400 / 1** state returns. Check Console for missing materials, shader/input errors, camera warnings, or duplicate AudioListeners.
- Open MainMenu and smoke-test existing profile/settings behavior. A standalone build using the existing scene list should also show the runtime grid when SettlementPrototype is opened; no menu-to-prototype routing was added.

## Automated validation and remaining Editor checks

In Unity, choose **Window > General > Test Runner > EditMode** and run `SettlementStateTests` and `HousePlacementTests` (11 test methods). Coverage includes original 400-Tile/Town Hall/irregular-set behavior, valid interior placement and immediate completion, partial-Wilderness rejection without mutation, Town Hall/House overlap rejection, zero-growth interior influence, exact 27-Tile protrusion with surrounding Wilderness preserved, repeated influence/validation idempotence, the starting exemption, chaining with unique identities, and rejection of another settlement's building.

Source-side results for this slice: **11 passed, 0 failed**, using actual NUnit assertions with Unity's bundled Mono compiler/runtime outside the Editor. The clean-edge test confirms exactly **400 -> 427**, with only X = 20..22 / Z = 5..13 newly claimed. Domain and Unity presentation/Editor source compilation against installed Unity 6000.6.4f1, URP, and Input System assemblies also passed.

The known Unity licensing/sandbox limitation prevents reliable automated Editor execution. It was not repeatedly retried, and licensing/security configuration was not changed. Source compilation and external NUnit execution do not verify Editor import, rendered previews, Play Mode interaction, or player builds. Complete the import, Editor Test Runner, and manual checks above in the normal Editor. New test metadata must be generated there before a future commit.

## Prototype limitations

Houses complete instantly without construction, costs, workers, residents, upgrades, or demolition. There is no world persistence, profile/world integration, physical player, rotation UI, final UI/art, or world streaming. Materials remain simple Unlit placeholders; the debug panel is IMGUI and separate from the UI Toolkit menu. Successful placement rebuilds the finite ground mesh and decorative objects; very large chained settlements may require later targeted rendering optimization. Thin lines at distant zoom can become subpixel; zoom closer when counting individual cells. No final Town Hall level or undecided mechanics are inferred.
