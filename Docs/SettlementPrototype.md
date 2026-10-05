# Settlement Prototype

## Scope and scene setup

This slice implements only a logical Tile grid, initial territory, already-completed Town Hall, finite decorative Wilderness, and an inspection camera. It does not implement citizens, resources, production, construction, expansion, roads, networking, player characters, or world saving/loading. The approved world design remains unchanged.

In Unity **6000.6.4f1**:

1. Open the project and allow all new scripts, assembly definitions, and tests to import. Unity generates their `.meta` files; keep those with the corresponding assets.
2. Choose **ColonyGame > Create Settlement Prototype Scene** once. Save any modified scene when prompted.
3. The command creates `Assets/Scenes/SettlementPrototype.unity`, eight URP Unlit prototype material assets in `Assets/ColonyGame/PrototypeMaterials/`, and their Unity-generated metadata. It adds the prototype after MainMenu in the build scene list while preserving other scene entries. It refuses to overwrite an existing prototype scene and reuses existing prototype materials without modifying them.
4. Open SettlementPrototype and enter Play Mode. Focus Game View to use the camera controls.

The scene contains a Settlement Prototype root and an orthographic Prototype Camera with one AudioListener. The view generates its finite ground mesh, Town Hall primitives, and forest decoration at runtime. No prefabs or final art are required. Materials are saved and referenced by the scene so they are included in builds.

MainMenu, profiles, and audio/settings behavior remain intact. Selecting a profile still opens the existing placeholder. Profile-to-world integration is deferred. MainMenu remains the launch scene; inspect this separate prototype directly in Editor for now.

## Files and architecture

All paths below are relative to the repository root.

| File | Responsibility |
| --- | --- |
| `Assets/ColonyGame/World/ColonyGame.World.asmdef` | Pure domain assembly with no Unity engine references. |
| `Assets/ColonyGame/World/TileCoordinate.cs` | Integer X/Z identity, value equality, and hashing. |
| `Assets/ColonyGame/World/SettlementState.cs` | Claimed-Tile set, completed Town Hall instance, and initial-state factory. |
| `Assets/ColonyGame/Scripts/WorldPresentation/SettlementPrototypeView.cs` | Renders domain state using ground/materials and primitives; exposes lightweight development counts. |
| `Assets/ColonyGame/Scripts/WorldPresentation/PrototypeCamera.cs` | Input System inspection pan/zoom, independent of simulation. |
| `Assets/ColonyGame/Editor/SettlementPrototypeSetup.cs` | Non-overwriting Editor scene/material setup and build-list insertion. |
| `Assets/ColonyGame/Tests/Editor/ColonyGame.World.Tests.asmdef` | Editor test assembly using the installed Unity Test Framework. |
| `Assets/ColonyGame/Tests/Editor/SettlementStateTests.cs` | Focused NUnit logical tests. |

`SettlementTerritory` owns a private `HashSet<TileCoordinate>`, constructed from individual claimed Tiles. Membership is authoritative; neither a rectangle nor rendered GameObjects define ownership. Construction copies and deduplicates supplied Tiles. Enumeration does not expose the mutable set. Arbitrary irregular/disconnected sets and holes are representable, but no territory-expansion operation is implemented.

`BuildingState` holds the Town Hall's stable ID (`starting-town-hall`), type (`TownHall`), integer origin, footprint dimensions, orientation (0 degrees), completed status, and enumerable occupied coordinates. Creating it does not modify territory. No levels, upgrades, construction, generic building placement, or influence behavior are implemented.

`SettlementState` owns this territory and Town Hall state. It is recreated on each prototype run. Plain values and enumerable coordinates provide a straightforward basis for future serialization without choosing a world-save format now. Rendering consumes state; it does not create authoritative citizen, tree, or resource entities.

## Coordinates and prototype presentation scale

A Tile is identified by integer `(X, Z)`. These logical axes map to Unity's horizontal X/Z plane; Y is presentation height. For this prototype only, one Tile is drawn as one Unity unit. That is a visual scale choice, not a final approved physical Tile size.

- Initial claimed range: **X = 0..19, Z = 0..19**, inclusive: exactly **400 Tiles**.
- Town Hall origin: **(7, 7)**; reserved dimensions **5 x 5**.
- Town Hall occupied range: **X = 7..11, Z = 7..11**, inclusive: exactly **25 Tiles**.
- Orientation: **0 degrees**, already completed.
- A rendered Tile fills exactly `[X, X+1]` and `[Z, Z+1]`. Uniform thin grid strips mark every integer boundary; there are no major lines or larger cell groups.
- Territory visual bounds are `[0, 20]` on each axis; Town Hall visual footprint bounds are `[7, 12]`, with center `(9.5, 9.5)`. This chooses the lower of the two near-center alignments in the even-sized territory. Its half-unit offset from territory center `(10, 10)` is intentional and deterministic.
- No Town Hall influence is applied; all other coordinates are unclaimed.

## Visual presentation and Wilderness

Ground rendering covers **X/Z = -6..25**, inclusive: a finite 32 x 32 visual area with six Tiles of Wilderness beyond each side of the settlement. These are rendering limits, not authoritative world limits or citizen travel limits.

A single ground mesh uses four material groups: lighter green claimed ground, dark green Wilderness, golden Town Hall footprint Tiles, and uniform dark grid lines. Each ground quad fills one 1 x 1 Tile. Grid lines are centered on every integer X/Z boundary with the same 0.04-unit interior width and no every-N-Tile emphasis. They sit 0.01 units above ground to avoid coplanar depth artifacts; outer edges are clipped to the rendered area. A compact cream/dark-roof Town Hall marker leaves its 5 x 5 reserved footprint easy to count. The marker is illustrative and does not define occupancy or final building scale.

Sparse deterministic cylinder/sphere trees appear only on unclaimed rendered Tiles. Their compact canopies and lower height reduce obstruction of individual grid cells; their positions and decorative role are unchanged. They are disposable decoration, have no retained colliders, and have no harvesting, targeting, resource-node, wildlife, or persistence behavior. No trees enter the authoritative domain state. No infinite world, streaming, technical chunks, or resource deposits are present.

## Camera controls

- **WASD or arrow keys**: pan on the X/Z plane.
- **Middle mouse drag**: ground-plane pan.
- **Mouse wheel**: orthographic zoom, clamped to sizes 5..28.

The camera is angled 70 degrees downward and starts centered on the settlement. Focus panning is bounded to X/Z -6..26 to keep this finite prototype accessible. These are presentation-only inspection limits; the camera is not a physical player and does not implement territory movement enforcement. Mouse/keyboard reads use the already installed Input System; no new input package or changes to the existing input asset are required.

## Validation

In Unity, choose **Window > General > Test Runner**, then run the **EditMode** `SettlementStateTests` tests. They verify:

1. Exactly 400 unique claimed Tiles and all expected 0..19 coordinates.
2. Unclaimed surrounding and distant coordinates.
3. A completed, unrotated 5 x 5 Town Hall with the exact 25 expected coordinates, entirely within territory, and no expansion beyond 400.
4. Irregular individual territory membership, holes, negative coordinates, deduplication, and ownership of input data.

Environment results: these four NUnit test methods were compiled and executed successfully using the installed NUnit library and Unity's bundled Mono compiler/runtime. They ran directly outside the Unity Test Runner because the domain has no engine dependency. Domain and Unity presentation/Editor source compilation were also checked against installed Unity/URP/Input System assemblies. This does not verify Unity asset import, scene rendering, or player builds.

Manual Play Mode checks:

- Confirm the development panel and Settlement Prototype Inspector show **400 claimed Tiles**, **20 x 20**, **5 x 5 Town Hall**, and **25 occupied Tiles**. The startup Console log also reports coordinates/counts.
- Inspect the full claimed border, all 25 golden footprint Tiles, the raised Town Hall, and surrounding forest. Confirm no forest decoration appears inside claimed territory.
- Test keyboard pan, middle-drag, and zoom, including camera bounds, at common desktop aspect ratios. Confirm grid lines remain readable at inspection zoom levels.
- Restart Play Mode and confirm identical initial state and decorative placement.
- Inspect the Console for missing materials, shader/input errors, camera warnings, or duplicate AudioListeners.
- Confirm MainMenu is still first in the build scene list, followed by SettlementPrototype; open MainMenu and check existing profile/settings flows remain unchanged.

## Known limitations and remaining Editor step

Unity automation previously could not reliably connect to licensing in this sandbox. This task therefore provides source and the explicit Editor setup command without retrying licensing or modifying machine/security settings. **The saved prototype scene, material assets, metadata, and build-list entry must be generated by running the setup command in your normal Editor.** New source/assembly/test `.meta` files are generated on import.

The prototype has static initial domain state, finite visuals, no world persistence, no profile integration, and no physical player. Materials are simple Unlit placeholders with no final lighting or artwork. Ground geometry and tree objects are generated at startup; no general optimization framework is added. The small on-screen development panel is IMGUI, separate from the existing UI Toolkit menu. No final Tile size, starting Town Hall level, or undecided world mechanics are inferred.

## Uniform Tile grid presentation update

The original ground code already used one-unit logical spacing and had no intentional 4 x 4, 5 x 5, or every-N-Tile major lines. It exposed thin inset gaps over a separate backing surface. Those small gaps can resolve unevenly at an angled camera view and different zoom levels; without a captured render, the precise cause of the reported stronger grouping is not confirmed.

The updated renderer fills each Tile and draws explicit equal-width strips along every integer boundary, using the same spacing and line material across territory, Wilderness, and the Town Hall footprint. There are no secondary grid divisions. The Town Hall remains a separate building state covering X/Z 7..11, with 25 highlighted cells; only its visual marker was reduced to leave grid boundaries visible. Forest decoration remains lightweight and presentation-only.

**Do not regenerate the existing scene or materials.** Exit Play Mode, allow the changed script to compile, then re-enter Play Mode in SettlementPrototype. All grid geometry and primitive markers are rebuilt at startup using the scene's existing material references. The existing setup command also creates future scenes using this updated runtime view without any setup-code changes.

Count 20 individual cells along both settlement axes and 5 along both footprint axes. Check equal spacing/line treatment across all ground categories, pan/zoom readability, unchanged debug counts (400 and 25), and compact trees. At very distant zoom, subpixel lines still require visual inspection at your Game View resolution; zoom closer when counting cells. No camera controls, domain state, scene/material assets, or approved design rules changed.
