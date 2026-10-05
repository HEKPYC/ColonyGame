# Settlement Prototype

## Scope and setup

This prototype provides a shared building-definition catalog, generic placement, quarter-turn rotation, contiguous Tile surfaces with subtle cell outlines, distinct schematic sites, peaceful decorative wildlife, four adult citizens, and a compact left-side UI Toolkit interface. Buildings still complete immediately and apply territory influence once. It introduces no costs, construction time, assigned workers, production, upgrades, quests, demolition, roads, save/load, or final art. The authoritative design records approved wildlife rules and citizen identity, attributes, and outdoor movement policies.

Use Unity **6000.6.4f1**. Exit Play Mode and let the changed scripts compile/import. Open the existing `Assets/Scenes/SettlementPrototype.unity` and enter Play Mode; **do not regenerate the scene or materials**. The existing serialized material references are sufficient. Let Unity generate metadata for the new Citizen.cs, CitizenNavigation.cs, CitizenSimulation.cs, CitizenView.cs, and CitizenTests.cs files, as well as `PrototypeHud.cs`, the new `Resources` folder, and `Resources/PrototypeTheme.tss`; retain them before a future commit. The theme imports the Unity default runtime theme and is loaded from Resources for standalone builds. Existing asset GUIDs are preserved.

For a checkout without the prototype scene, use **ColonyGame > Create Settlement Prototype Scene** once. The existing non-overwriting command creates the scene and eight URP Unlit materials and adds the prototype after MainMenu in the build scene list. Future scenes automatically use this updated runtime view; the setup command needs no changes.

MainMenu, profiles, settings, packages, project settings, scene assets, camera movement/zoom mathematics, and existing materials are unchanged. Camera wheel/middle-drag input is ignored over visible UI panels so scrolling the build list does not move the world. MainMenu remains the launch scene; profile selection still opens its placeholder. Open this separate prototype directly for testing.

## Catalog and Town Hall test override

`BuildingCatalog` is the single authoritative prototype source for type identifiers, English display names, base width/depth, required Town Hall level, and player-placeability. Definitions are immutable plain C# objects in the domain assembly. The enum has explicit stable values; existing TownHall and House identifiers are preserved. `MageTower` identifies one shared site, never a separately placeable Place of Power.

| Type identifier | Display name | Base footprint | Required Town Hall level | Player-placeable |
| --- | --- | --- | --- | --- |
| TownHall | Town Hall | 5 x 5 | Starting; stored as 0 | No |
| BuildersHut | Builder's Hut | 3 x 3 | 1 | Yes |
| House | House | 3 x 3 | 1 | Yes |
| Farm | Farm | 3 x 4 | 1 | Yes |
| Tavern | Tavern | 4 x 5 | 2 | Yes |
| Butcher | Butcher | 3 x 6 | 2 | Yes |
| FishermansHut | Fisherman's Hut | 3 x 5 | 2 | Yes |
| GuardTower | Guard Tower | 2 x 2 | 2 | Yes |
| Warehouse | Warehouse | 4 x 4 | 3 | Yes |
| LumberMill | Lumber Mill | 5 x 5 | 3 | Yes |
| Mine | Mine | 4 x 4 | 3 | Yes |
| Barracks | Barracks | 3 x 5 | 3 | Yes |
| Laboratory | Laboratory | 6 x 6 | 4 | Yes |
| MageTower | Mage Tower + Place of Power | Shared 5 x 5 | 4 | Yes |

**The prototype initializes Town Hall at Level 4 solely to test every current building type. This is a temporary testing override, not the intended final new-game progression.** `SettlementState.TownHallLevel` is read-only; no upgrading operation exists. `CreatePrototype(townHallLevel)` can initialize lower-level test settlements without an upgrade system. Supported prototype levels are 1 through 4.

Availability still requires `IsPlayerPlaceable` and `RequiredTownHallLevel <= TownHallLevel`. Unlocks are cumulative: TH1 has 3 options, TH2 has 7, TH3 has 11, TH4 has 13. Town Hall never appears in the list and the domain rejects additional Town Hall placement. The current Town Hall level and its testing override are visible in the debug panel. Building upgrade levels/effects are not implemented.

## Domain and presentation architecture

All paths below are relative to the repository root.

| File | Responsibility |
| --- | --- |
| `Assets/ColonyGame/World/ColonyGame.World.asmdef` | Domain assembly without Unity engine references. |
| `Assets/ColonyGame/World/TileCoordinate.cs` | Integer X/Z Tile equality and hashing. |
| `Assets/ColonyGame/World/BuildingCatalog.cs` | Immutable definitions, stable type identifiers, oriented footprints, and cumulative unlock filtering. |
| `Assets/ColonyGame/World/SettlementState.cs` | Claimed Tiles, building instances/list, occupancy, Town Hall level, generic validation/placement, and influence. |
| `Assets/ColonyGame/Scripts/WorldPresentation/SettlementPrototypeView.cs` | Build list, selection/rotation input, mouse-to-Tile conversion, preview, domain requests, runtime rendering, labels, and counts. |
| `Assets/ColonyGame/Scripts/WorldPresentation/PrototypeCamera.cs` | Existing controls/bounds/math, with a UI picking callback to gate wheel/middle-drag input. |
| `Assets/ColonyGame/Scripts/WorldPresentation/PrototypeHud.cs` | Runtime UI Toolkit HUD, left build list, placement status, and hierarchy-based pointer picking. |
| `Assets/ColonyGame/Resources/PrototypeTheme.tss` | Default UI Toolkit runtime theme included through Resources; no menu-theme changes. |
| `Assets/ColonyGame/Editor/SettlementPrototypeSetup.cs` | Existing non-overwriting scene/material setup. |
| `Assets/ColonyGame/Tests/Editor/SettlementStateTests.cs` | Four original initial-state/irregular-territory tests. |
| `Assets/ColonyGame/Tests/Editor/HousePlacementTests.cs` | Seven original House regressions, now using the generic API. |
| `Assets/ColonyGame/Tests/Editor/BuildingCatalogTests.cs` | Sixteen catalog, availability, generic placement, rotation, and influence tests. |

`SettlementTerritory` owns a private `HashSet<TileCoordinate>`. It copies/deduplicates supplied coordinates and does not expose the mutable set. Actual membership is authoritative; rendered bounding rectangles never claim land.

`BuildingState` references its immutable catalog definition and records unique identity, integer origin, actual oriented dimensions, orientation, immediate completion, starting exemption, and influence-applied state. `OccupiedTiles` and `InfluenceTiles` derive from the oriented footprint. The starting Town Hall retains `starting-town-hall`, origin `(7, 7)`, and its 25 occupied Tiles.

`ValidateBuildingPlacement(type, origin, orientation)` is read-only and checks known type, player-placeability, unlock level, valid quarter-turn orientation, claimed membership for the entire oriented footprint, and non-overlap. Unknown types, invalid angles, and unsafe coordinate arithmetic are rejected. `TryPlacePrototypeBuilding` rejects invalid placement without mutation; successful placement creates a completed instance and applies influence immediately. One implementation serves every catalog type; there are no per-type placement scripts.

`ApplyBuildingInfluence` accepts only a building belonging to that settlement, adds individual influenced Tiles, and marks influence applied even when no new Tiles were claimed. Repeated calls return zero without changing territory. The starting Town Hall is explicitly exempt. Preview, validation, camera motion, and presentation refresh do not apply influence. No resources are awarded.

## Citizen foundation

The prototype creates exactly four adults: Edwin and Owen (Male), Clara and Nora (Female). All start Unemployed. Seed 1147 reproducibly generates ages 18..45 inclusive and independent integer Strength, Dexterity, Intelligence, Endurance, and Charisma values 1..10. Attributes have no gameplay effects. Initial free positions are beside the four corners of Town Hall at (6.5,6.5), (12.5,6.5), (6.5,12.5), and (12.5,12.5); already occupied positions are replaced by deterministic free claimed positions.

Citizen.cs contains identity/data and continuous X/Z positions; CitizenSimulation.cs owns population and movement. Prototype IDs are stable and unique within this settlement instance; relationships can later reference them. No automatic partners or households exist. Future profession/aging/save systems can extend the domain without tying identity to a visual GameObject.

CitizenNavigation.cs provides lightweight cardinal BFS routes and nearest-destination search. SettlementIdleMovementPolicy requires actual claimed, unoccupied Tiles and conservative 0.28-Tile body clearance. All footprints, including Town Hall and Farm, are obstacles. Unclaimed holes and areas beyond irregular protrusions remain inaccessible. Normal interpolation between cardinal cell centers cannot cut occupied corners. Idle destinations are randomly chosen from reachable routes up to six steps away, so a blocked direct line produces a detour instead of permanent failure.

Prototype Inspector tuning: Citizen Walk Speed defaults to 0.8 Tiles/second, Citizen Minimum Pause to 1 second, and Citizen Maximum Pause to 3 seconds. Routes persist across frames and visible positions move continuously. Citizens occasionally pause, then choose another reachable destination. A citizen with no reachable destination waits safely unless construction has disconnected its area; that case now triggers the recovery described below. Simulation accepts an ICitizenMovementPolicy: future tasks may supply different eligibility and obey the already approved Wilderness distance rules. No job or Wilderness travel behavior is implemented now.

CitizenView.cs creates upright schematic figures: Male citizens have teal ordinary tunics and separate trouser legs; Female citizens have purple tunics with a wider hem and longer hair. These are base presentation cues, not profession clothing. Models turn toward movement. A runtime cyan annulus marks selection; no Gizmos, physics pushing, crowd avoidance, animation packages, or saved assets are added.

Outside placement mode, left-click a figure to select it using presentation-only ray/bounds picking. Clicking empty world deselects. The compact left UI Toolkit panel shows name, sex, age, Unemployed, and all five attributes, with no assignment/inventory/needs/relationship controls. Opening Build clears citizen selection; selecting a building also clears it. Placement takes priority over citizen picking. The information panel participates in existing UI pointer blocking; transparent layout remains non-blocking. Escape also deselects.

Citizens never block placement or change validation/influence. After successful placement, citizen reconciliation immediately refreshes navigation connectivity. A citizen covered by a new footprint or sealed into a disconnected pocket keeps its exact position and receives a continuous recovery route to connected free claimed ground. A new obstacle invalidating an otherwise ordinary path cancels that path toward the current cell center before choosing another route. See the unified building access/recovery foundation below.

Only an impossible/broken recovery uses emergency relocation to a valid claimed unoccupied cell with a clear development warning. If none exists, warning-bearing dormancy preserves identity; later reconciliation may reactivate the citizen. No resource awards occur. Territory expansion makes new claimed free cells eligible without changing policy. Citizens and wildlife do not interact; existing wildlife code is unchanged.

New domain tests in CitizenTests.cs cover population/sex/unemployment/IDs, deterministic ages and attributes including range endpoints, valid starting positions and previous buildings, Town Hall detours, irregular claims, body clearance, continuous wandering/pauses, expansion availability, independent continuous footprint recovery, unsafe-route cancellation, recovery restrictions, continuous fractional-position recovery, emergency relocation/dormancy, isolated cells, policy injection, and invalid input.

Citizen Play Mode acceptance:
1. Let Unity import the five new files and generate their .meta files. Open the existing SettlementPrototype scene; no regeneration is required.
2. Verify four citizens, two teal Male figures and two purple Female figures, with no spawn inside footprints. Check the HUD count.
3. Observe walking and pauses for at least a minute. Check detours around Town Hall and several placed footprints; normal movement must never enter Wilderness.
4. Click each figure. Verify the cyan ring and all required information. Click empty ground to deselect. Click panel content/padding and confirm no selection or placement underneath.
5. Open Build with B and its button; citizen information must disappear. Select a building; clicks on citizens now perform placement rather than selection. Retest UI scrolling and camera input gating.
6. Catch a citizen with a valid building footprint. Placement must succeed with no position jump; watch the citizen walk out to free claimed ground, then resume wandering. At initial citizen position (6.5,6.5), a House at origin (4,4) is a valid example if placed before the citizen moves away.
7. Expand at the boundary and confirm citizens can subsequently use new free claims while avoiding Wilderness holes. Retest wildlife wandering and visible evacuation.
8. Restart Play Mode; names, ages, attributes, IDs, and initial positions are reproducible. Check Console for unexpected errors or emergency warnings; run all 72 EditMode tests.

Citizen visuals, selection, information, ordinary wandering, and general navigation were manually verified by the user. The new entrance and trapped-pocket recovery behavior still needs manual Unity verification. No households, families, children, aging, immigration, professions/assignments, Builder behavior, needs, inventories, resources, combat, or citizen-triggered wildlife fleeing are implemented.

## Building access and trapped-citizen recovery

BuildingEntrance.cs supplies deterministic domain data for every type, including Town Hall: a footprint-edge Tile, cardinal outward facing, and adjacent exterior access Tile. The prototype convention selects the lower central Tile of the canonical south edge; this is a replaceable access convention, not a final architectural requirement. BuildingState.Entrance derives the data from definition, origin, and rotation. Quarter-turns rotate both the Tile and facing consistently with the site's Unity Y rotation. ExteriorTile is FootprintTile + Facing.

The exterior access Tile is separate from the reserved footprint. IsExteriorAccessible checks current claimed membership and non-occupancy. Access is advisory: blocked entrances never alter placement validation, footprints, influence, or unlocks. Normal citizen navigation still rejects every occupied Tile, including doorway and perimeter Tiles. A gate is a future transition point, not a public path across the site.

SchematicBuildingView derives an opening from actual domain entrance data, splits only the relevant perimeter segment, and adds two short posts and a blue threshold entirely inside/on the footprint boundary. Existing building geometry is unchanged. Gate geometry rotates with the site; no scene regeneration or saved asset changes are required.

SettlementIdleMovementPolicy refreshes actual cardinal walkable components after placement. The largest component is the ordinary connected settlement area; equal-size ties prefer the Town Hall exterior access component, then stable Z/X ordering. The anchor is Town Hall access when it belongs to that component, otherwise its deterministic first Tile. This avoids using a sealed Town Hall courtyard as the recovery destination for the rest of the settlement. No original rectangle, Town Hall radius, or bounding-box approximation defines connectivity. Ordinary ticks use this cached connectivity.

CitizenSimulation uses one recovery system for direct footprint overlap and disconnected pockets, even when a pocket still contains multiple ordinary walkable Tiles. CitizenNavigation extends its existing cardinal search with a small weighted search whose costs are ordered as:
1. Fewest occupied Tiles crossed.
2. Short travel plus a bounded soft preference for crossing near the designated entrance.
3. Shortest route to break remaining ties.

Thus any entirely ordinary route wins over occupied traversal, regardless of its length. Door preference never adds extra occupied Tiles; minimal routes may cross elsewhere when necessary. The destination must be normally walkable in the selected connected component. Recovery traverses claimed Tiles only and explicitly whitelists the selected route (plus initial body overlap), never granting arbitrary building access. Citizens move continuously at their existing walk speed, with no wandering pauses during recovery. A later placement replans from the current fractional position without a jump.

Recovery ends immediately when the body is clear on connected settlement ground. Route permissions are discarded; normal building blocking and idle wandering resume. Merely reaching another disconnected free pocket does not end recovery prematurely. Emergency relocation is reserved for routing failure/impossible states, preserves identity/state, selects valid connected claimed ground, and emits a clear development warning. If no safe Tile exists, warning-bearing dormancy remains. Ordinary recoverable hut traps produce no relocation warnings.

Citizen.InsideBuildingId is a reserved nullable location link and remains null. A future explicit entry system can path to ExteriorTile, transition through the entrance using the building's stable ID, hide the outdoor view while logically inside, and later exit at access. No EnterBuilding/ExitBuilding behavior, interiors, home assignment, night schedules, or sleeping is implemented. Task-specific movement policy remains separate from entrances and recovery; wildlife is unchanged.

BuildingAccessRecoveryTests.cs adds fourteen focused tests. All previous 58 tests are retained; the surrounded-footprint test now expects walking rather than relocation, and the recovery permission test checks the route whitelist. Coverage includes all entrances/access Tiles, rotation, blocked-access placement, ordinary footprint blocking, the sealed Builder's Hut pocket, narrow passages, minimal crossing, ordinary detours, entrance preference, irregular claims, blocked Town Hall access, additional construction, and warning-bearing broken-state fallback.

Manual Unity checks:
1. Let Unity import BuildingEntrance.cs and BuildingAccessRecoveryTests.cs and generate their .meta files. Keep the existing scene and materials.
2. Inspect every site's gate. Rotate Farm through all four orientations and confirm the opening rotates with the exact footprint.
3. Watch ordinary citizens walk around footprints and fences; no doorway/perimeter Tile becomes normally walkable.
4. Surround a citizen with Builder's Huts, leaving a narrow passage first, then sealing it. A passage should preserve ordinary navigation; sealing should start continuous recovery without teleporting or rejecting placement.
5. Watch the citizen leave the pocket, cross only the necessary obstruction, and resume ordinary movement on reaching connected space. Repeat with direct overlap and another placement during recovery.
6. Check that normal citizens never enter Wilderness, selection/info UI and Build input work, and wildlife wandering/evacuation remain intact. Check Console for unexpected errors or emergency relocation warnings.
7. Run all 72 EditMode tests. New entrance/recovery visuals remain subject to this manual verification.
## Controls, selection, and rotation

Focus Game View before using keyboard/mouse controls.

- **B** or **Build / Close list**: open/close the compact left-side list. It starts closed; toggling it preserves any active selection.
- **Click a building entry**: select it at 0 degrees and automatically collapse the list. Entries show name, base footprint, and required Town Hall level. Scroll vertically if needed; successful placement does not reopen the list.
- **R**: rotate the selected preview 90 degrees clockwise: 0 -> 90 -> 180 -> 270 -> 0.
- **Left click on ground**: place if valid; selection remains active for repeated placement.
- **Cancel button**: cancel selection/preview.
- **Escape**: cancel placement and close the list. Press B to reopen it.
- **WASD / arrow keys**: pan; **middle mouse drag**: ground-plane pan; **wheel**: zoom, sizes 5..28.

H no longer selects House. Choose House from the same list as other types. Selecting another entry resets orientation; successful placement retains the current orientation.

The permanent HUD is confined to the upper-left and shows Claimed Tiles, Buildings (including Town Hall), Town Hall Level, compact camera help, and Build. The interface is at most 280 pixels wide, with a relative maximum for small windows. A compact placement panel below it shows the selected name, actual oriented footprint, VALID/INVALID with reason and cursor origin, rotation/cancel help, and Cancel. The center remains available for world input; UI scale is not tied to world zoom.

Rectangles swap dimensions on 90/270-degree orientations; squares retain their shape while orientation is stored. The footprint origin remains the lower X/Z corner. The existing ground-plane mouse ray and floored local X/Z coordinate conversion are unchanged. Preview cells exactly follow the selected definition/orientation, with separated green/red cells only during placement. Normal ground has subtle shader-based cell outlines.

The generated UIDocument root and transparent left layout use `PickingMode.Ignore`. Only small, visibly filled HUD/list/status panels and their interactive children are picked. World placement checks `panel.Pick` at a `RuntimePanelUtils.ScreenToPanel` position and walks picked ancestors to identify those panels; it uses no fixed screen-coordinate exclusion rectangles. Hidden panels cannot block input. Pointer-down consumption is retained for the current frame so collapsing a list during selection cannot also place a building underneath it. Camera wheel/middle-drag reads use the same picking check; actual UI controls consume their intended input, while empty root/layout space permits world clicks. Runtime building labels remain presentation-only IMGUI labels.

The previous prototype had no UI Toolkit hierarchy for its build interface: its IMGUI overlay used a 580-pixel-wide HUD and fixed HUD/list exclusion rectangles. Those broad exclusions blocked world placement even over unused space within the rectangles. The new runtime hierarchy replaces that path without changing MainMenu's separate UI Toolkit interface.

## World, visuals, and influence

Initial territory remains **X/Z = 0..19**, inclusive: **400 Tiles**. Starting Town Hall occupies **X/Z = 7..11**, inclusive: **25 Tiles**. A logical Tile is presented as one 1 x 1 world cell; final physical scale remains undecided.

The ground mesh contains contiguous per-Tile 1 x 1 quads in three surface groups: claimed territory, Wilderness, and occupied footprints. Adjacent faces meet edge-to-edge with no gaps, black separators, backing surface, or grid overlay. Each quad has local 0..1 UVs. The new Resources shader uses derivatives to antialias a thin outline inside each cell, darkening the surface tint by at most 16%. Outlines fade at approximately 3..7 pixels per cell to avoid distant shimmer. Pixel width is approximate and depends on projection and GPU derivatives. This remains one efficient mesh, not one GameObject/MonoBehaviour per ground Tile. Gold occupied areas, green/red previews, compact building markers, and labels remain distinguishable. Normal Tile presentation is identical with Gizmos enabled or disabled.

The former runtime grid was made of explicit 0.04-unit strips raised 0.01 units over the ground at every integer boundary. Their projected width changed with camera zoom and could become subpixel, producing the distracting line appearance independently of Gizmos. Those strips and their fourth mesh/material group remain removed. Outlines add no geometry, LineRenderers, per-Tile objects, or Gizmos. Three runtime ground materials use the shader; saved materials remain unchanged. Zoom scales the ordinary ground geometry naturally; no world zoom math or Tile size was changed. Temporary preview cell separation exists only in active placement mode.

Ground initially covers X/Z = -6..25. After successful placement it rebuilds a finite area six Tiles beyond the claimed extent; camera inspection bounds follow that extent. The rectangle is a rendering extent only: unclaimed holes and Wilderness beside protrusions remain unclaimed. Presentation refresh immediately disables the old visual root and rebuilds terrain/buildings/decoration from domain state. Deterministic trees appear only on unclaimed Tiles, so newly claimed decoration disappears without resource rewards.

For actual oriented dimensions `(width, depth)` and origin `(x, z)`, completed-building influence covers inclusive **X = x-3..x+width+2**, **Z = z-3..z+depth+2**. Influence dimensions are `(width+6) x (depth+6)`:

| Building / orientation | Footprint | Full influence |
| --- | --- | --- |
| House | 3 x 3 | 9 x 9 |
| Guard Tower | 2 x 2 | 8 x 8 |
| Farm, 0 degrees | 3 x 4 | 9 x 10 |
| Farm, 90 degrees | 4 x 3 | 10 x 9 |
| Laboratory | 6 x 6 | 12 x 12 |

The entire footprint must be claimed **before** placement. Influence cannot legalize a footprint extending into Wilderness. Existing claims are deduplicated, territory stays irregular, and later buildings may use newly claimed territory. No costs, resources, builders, timers, or production accompany immediate completion.

## Schematic sites and peaceful wildlife

All 14 sites, including Town Hall, use catalog dimensions. The complete site rotates in quarter-turns and translates to the actual oriented origin. Four inset perimeter pieces mark the entire footprint with outer edges inside it. All decorations remain inside.

| Building | Schematic identity |
| --- | --- |
| Town Hall | Cream civic hall, stone tower, approach |
| Builder's Hut | Workshop, material stack, workbench |
| House | Terracotta dwelling and chimney |
| Farm | Four crop rows |
| Tavern | Ochre hall, table, barrel/sign marker |
| Butcher | Hut and empty divided yard |
| Fisherman's Hut | Wood hut, blue pond, dock |
| Guard Tower | Stone column and cream battlement |
| Warehouse | Storage hall and three crates |
| Lumber Mill | Workshop and log stacks |
| Mine | Rock bank, dark entrance, track |
| Barracks | Hall, training court, post and red banner |
| Laboratory | Light research wing and blue instrument |
| Mage Tower + Place of Power | Purple tower, blue power pad/crystal on one shared site |

These cues implement no building functionality. SchematicBuildingView.cs owns building presentation and a cached runtime palette. PeacefulWildlifeView.cs renders animals independently of terrain rebuilds. PrototypeTileCells.shader is included through Resources. Runtime materials are destroyed on teardown; scene and saved material assets remain unchanged.

PeacefulWildlife.cs contains plain C# species, positions, bounds, habitat policy, animal state, and simulation. PeacefulWildlifeTests.cs contains twelve tests covering population, irregular claims/body clearance, continuous wandering/pauses, evacuation without relocation, independent faster outward running, nearest irregular exits, repeated expansion, return to safe wandering, warning-bearing emergency recovery, dormant recovery, abstract flee selection, and invalid inputs.

Default population is two each of Squirrel, Badger, Deer, and Fox (eight total), seed 731. Wildlife Per Species in the view Inspector accepts 0..12, limited by available spawn cells. Squirrel is small with an upright ginger tail; Badger is low gray with white head stripes; Deer is larger brown with long legs, neck, and antlers; Fox is low ginger with white muzzle/tail tip.

The peaceful habitat checks actual claimed Tiles, including holes and protrusions. A conservative 0.48-Tile body half-extent must remain within represented Wilderness bounds and intersect no claimed Tile. Cardinal cell-center destinations prevent diagonal corner cutting. Animals move continuously at 0.65 Tiles/second, pausing 0.8..2.8 seconds between segments; initial pauses vary.

Expansion claims territory normally and immediately, with no wildlife influence on shape, placement, or Tile counts. Reconciliation preserves affected animals' world-space positions and starts Evacuating movement instead of relocation. A lightweight cardinal breadth-first search through actual claimed Tiles finds the shortest cell route to nearby valid Wilderness; ties prefer greater actual boundary clearance. Movement first aligns within the current cell, follows that route without pauses, then runs one additional valid cardinal step deeper into Wilderness where available. Destinations always have body clearance. No Town Hall radius or rectangular settlement approximation is used.

Only an animal caught by newly expanded claims gets temporary permission to traverse claimed Tiles along its escape route. The permission ends immediately when its entire conservative body bounds reach valid Wilderness: movement switches from Evacuating to Fleeing for the remaining safe route. It cannot enter claims again or use them as a wandering shortcut. Arrival returns to Wandering with ordinary pauses and speed. Additional expansion replans from the actual current position without teleportation; multiple animals have independent routes.

Walking remains 0.65 Tiles/second. Wildlife Run Speed Multiplier in the view Inspector defaults to 1.8 (1.17 Tiles/second), and accepts 1.1..3 as prototype tuning. Evacuating and Fleeing share continuous movement and the run multiplier. No citizen detection is implemented.

Teleportation is retained only as emergency recovery if no evacuation route exists despite an available safe destination. It emits a clear development warning through the Unity view. If no valid Wilderness exists, the warning-bearing emergency fallback is dormancy with identity retained, followed by recovery on later reconciliation. Neither fallback kills animals or awards resources. Searches run during expansion/recovery, not repeatedly each frame for dormant animals.

RequestFlee accepts an abstract threat only between walking segments. It selects a valid neighbor increasing threat distance, preferring greater depth from the nearest actual claimed Tile square. This prototype ranking does not define the citizen distance metric. No citizen detection or automatic trigger exists. Habitat eligibility is a separate policy: future Wolves/Bears may use settlement-capable policies, rather than inheriting peaceful restrictions. No predators, livestock, reproduction, hunting, resources, combat, or citizen-triggered wildlife reactions are implemented.

Additional Play Mode checks:
- Import the shader/scripts/tests and let Unity generate their .meta files; retain these before committing. Check Console and ensure ground is not pink. Do not regenerate scenes/materials.
- Inspect all sites and four rotations of rectangular buildings. Check that perimeter, crops, pond, empty yard, and shared power site remain inside the oriented footprint.
- Observe all four species, continuous walking/pauses for one minute, and Wildlife count in the existing compact HUD.
- Expand toward an animal or destination. Confirm no position jump at placement, immediate faster running through the new boundary, a short deeper Wilderness run, then ordinary wandering. Repeat for several animals and a second expansion while an animal is still evacuating. Confirm no emergency warnings under ordinary conditions and no rewards.
- Sweep zoom with Gizmos on/off; check subtle outlines and distant fading. Retest UI scrolling/passthrough, placement snapping, and camera controls.

The preceding building/wildlife milestone, including evacuation, was manually verified by the user. Citizen rendering/input and recovery require new Unity Play Mode verification; source compilation does not establish visual correctness.
## Validation and manual Unity steps

Source-side results: **72 NUnit tests passed, 0 failed**, using Unity's bundled Mono compiler/runtime and the installed NUnit library outside the Editor. All original tests remain covered through the generic API. Tests verify all 14 catalog definitions, exact names/footprints/unlock levels/placeability, 3/7/11/13 cumulative options, lower-level placement rejection, every player type through the generic path, actual rotated occupancy/validation/influence, starting exemption, idempotence, irregular membership, House chaining, and invalid type/angle/coordinate rejection. Domain and Unity presentation/Editor source compilation against installed Unity 6000.6.4f1/URP/Input System assemblies passed.

The known licensing/sandbox limitation means source-side validation does not verify Unity import, menu input, rendered previews/labels, Play Mode, or player builds. Licensing/security configuration was not changed or repeatedly retried. Complete these checks in the normal Editor:

1. Exit Play Mode, allow imports/compilation, and retain new source/test `.meta` files. Do not recreate the existing scene/materials.
2. Use **Window > General > Test Runner > EditMode** and run `SettlementStateTests`, `HousePlacementTests`, and `BuildingCatalogTests` plus PeacefulWildlifeTests, CitizenTests, and BuildingAccessRecoveryTests (72 tests).
3. Open SettlementPrototype and enter Play Mode. Confirm only the compact upper-left HUD is visible initially; press B or Build to open the left list. Confirm **400 claimed Tiles**, **1 building**, **25 Town Hall occupied Tiles**, and **TH 4 (test override)**. Verify all 13 player types appear, with accurate footprints/unlock levels, and Town Hall/standalone Place of Power do not appear.
4. Select Farm. Confirm the list collapses automatically and only compact placement information remains on the left. Clicking the selection entry must not place a building behind the list. Press R and verify 3 x 4 -> 4 x 3 -> 3 x 4, oriented cells, selection label, and complete schematic site orientation after placement. Verify all four orientation states and a square building's unchanged shape.
5. Try Farm origin `(16, 17)`: 0 degrees must be red/outside; 90 degrees must be green. In a fresh session, Farm at `(4, 7)` is valid at 0 degrees and invalid/occupied at 90 degrees because it would overlap Town Hall. Invalid clicks must not change counts.
6. Reproduce the original House edge test in a fresh session: select House, place origin **(17, 8)**. Confirm **400 -> 427**, **1 -> 2 buildings**, and only X = 20..22, Z = 5..13 newly claimed (3 x 9 = 27). `(20, 4)`, `(20, 14)`, and `(23, 8)` remain Wilderness. Place House at `(20, 8)` next and confirm **454 Tiles / 3 buildings**.
7. In separate fresh sessions, select Guard Tower at `(18, 8)` and expect **424 Tiles** (24 new); unrotated Farm at `(17, 8)` and expect **430 Tiles** (30 new); rotated Farm at `(16, 8)` and expect **427 Tiles** (27 new); Laboratory at `(14, 7)` and expect **436 Tiles** (36 new). Check full influence shapes against the table and retained Wilderness above/below protrusions.
8. Place several different types, inspect their name labels and reserved footprint cells, and attempt overlaps against Town Hall and other types. Selection, rotation, cancellation, and repeated rendering must not change territory. Trees disappear on new claims with no resource rewards.
9. Check B/buttons, Escape cancellation, repeated placement, and scrollable left list at typical desktop aspect ratios. Click Build, Cancel, entries, and list padding: no building should be placed underneath. Scroll inside the list: the list scrolls and the world does not zoom; outside it, wheel zoom and middle-drag work normally. Confirm empty space under the transparent root/left layout permits valid placement. Toggle Gizmos ON/OFF and sweep the zoom range: normal ground has subtle cell outlines without black gaps or raised strips, with distant fading. Preview snapping and oriented extent remain precise.
10. Restart Play Mode and confirm the initial state returns. Check Console for missing material/shader/input/camera errors and duplicate AudioListeners. Smoke-test MainMenu/profile/settings flows. A normal player build should preserve contiguous subtly outlined ground and the left-side menu; no menu-to-prototype routing was added.

Lower Town Hall filtering is tested in the domain; the runtime prototype intentionally has no level-changing control or upgrade system.

## Prototype limitations

Every building produces a distinct schematic site with a label, without its future functionality. No Warehouse logistics, Farm production, Tavern behavior, livestock, fishing, detection, combat, research, magic, or Place of Power mechanics are implemented. There are no resources, costs, timers, assigned workers, households, upgrades, demolition, roads, saving, physical player, or final UI/art. The prototype resets when Play Mode ends.

The build/HUD interface is runtime UI Toolkit and remains separate from MainMenu. Building-name labels remain IMGUI. Building labels can overlap each other in crowded settlements; near a screen edge they may clip. Ground and decoration rebuild on successful placement; large chained settlements may need later rendering optimization. There is no streaming or modding pipeline. Final Town Hall starting level remains undecided; Level 4 here is solely a testing override.
