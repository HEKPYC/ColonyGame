# Settlement Prototype

## Implemented now

Open the existing `Assets/Scenes/SettlementPrototype.unity` in **Unity 6000.6.4f1** after script import. No scene regeneration, package change, or ProjectSettings change is required. MainMenu/profile routing is unchanged. Runtime state resets on leaving Play Mode.

The playable bootstrap starts with 400 claimed Tiles (X/Z 0..19), completed 5 x 5 Town Hall at (7,7), four unemployed adults, and **10 Wood** stored logically in Town Hall. Town Hall remains **Level 4 TEST OVERRIDE**, not final new-game progression. Build (B), rotation (R), camera controls, Tile outlines, distinct schematic catalog visuals, citizen selection, entrances, recovery, and peaceful wildlife remain.

The catalog still offers 13 previews with cumulative 3/7/11/13 availability at Town Hall levels 1/2/3/4. Only the **first Builder's Hut** has approved playable construction. Other types and later Huts display deferred-construction feedback; no unspecified costs or instant normal construction are introduced. `TryPlacePrototypeBuilding` retains immediate completed placement exclusively as the existing domain test/fixture API, including all footprint/influence regressions. The old runtime instant placement behavior is superseded by the playable bootstrap.

## Domain and resource architecture

| File | Responsibility |
| --- | --- |
| `Assets/ColonyGame/World/SettlementBootstrap.cs` | Central prototype balance, extensible ResourceType/ResourceInventory transactions, storage identity, first-Hut start/work/completion, authoritative workplace assignment and live roster queries. |
| `Assets/ColonyGame/World/AmbientWoodGathering.cs` | Task-specific boundary area, seeded/injectable random source, outbound/gather/return tasks and arrival-only delivery. |
| `Assets/ColonyGame/World/SettlementState.cs` | Claimed Tile set, occupancy, oriented buildings, construction state/progress and one-time influence. |
| `Assets/ColonyGame/World/PlayerInteraction.cs` | Central context-sensitive player intent: selected target, available action, discrete press, continuous hold and suppression. |
| `Assets/ColonyGame/World/Citizen.cs` | Immutable identity/attributes, mutable profession/workplace and current task separate from movement. |
| `Assets/ColonyGame/World/CitizenSimulation.cs` | Ordinary walking and safety/recovery; task controller can own authorized movement without changing idle eligibility. |
| `Assets/ColonyGame/World/CitizenNavigation.cs` | Shared cardinal routing and weighted recovery searches. |
| `Assets/ColonyGame/World/PeacefulWildlife.cs` | Wilderness-only wildlife, continuous expansion evacuation and nearest-citizen flee trigger. |
| `Assets/ColonyGame/Scripts/WorldPresentation/SettlementPrototypeView.cs` | Runtime composition, input/picking and completion-triggered presentation/reconciliation. |
| `Assets/ColonyGame/Scripts/WorldPresentation/PrototypeHud.cs` | Compact Wood HUD, Town Hall roster/overview, Hut work/assignment and Help panels with shared UI pointer blocking. |
| `Assets/ColonyGame/Scripts/WorldPresentation/PrototypeHelp.cs` | Presentation-only Help state and structured sections, reading existing balance/catalog data without mutating gameplay. |
| `Assets/ColonyGame/Scripts/WorldPresentation/CitizenView.cs` | Replaceable identity visuals, world selection and Builder apron/tool-belt overlay. |
| `Assets/ColonyGame/Scripts/WorldPresentation/SchematicBuildingView.cs` | Oriented entrance/perimeter and incomplete/completed schematic visuals. |

The world assembly has no Unity references. Resource transactions identify resource type, signed quantity and unique transaction ID; reject negative totals/overflow; and retry matching IDs without charging/crediting twice. A conflicting retry fails. Current storage identity is Town Hall, separate from inventory operations. Only Wood is functional. Successful gather results remain pending on the task until actual return. No stacks, player inventory, other-resource production or transfer logistics exist.

## Citizen identity, management and assignment

Four stable prototype IDs belong to Edwin/Owen (Male) and Clara/Nora (Female). Seed 1147 generates ages 18..45 and independent integer Strength, Dexterity, Intelligence, Endurance, Charisma 1..10. Attributes have no gameplay effects. Initial free positions beside Town Hall are (6.5,6.5), (12.5,6.5), (6.5,12.5), (12.5,12.5), with deterministic claimed/free fallbacks when occupied.

Click Town Hall for a separate compact management panel: test level, current claimed count, population, employed/unemployed counts, Wood, Town Hall storage, and scrollable roster containing every citizen exactly once. Every row displays actual name/sex/age/profession/workplace and all five attributes. Clicking a row opens the same citizen detail/selection used by clicking the world figure. Both query the same domain Citizen objects and refresh profession/workplace immediately.

Click a completed Hut, choose **Assign Worker**, then explicitly choose any eligible unemployed adult. Incomplete/foreign/non-Hut buildings reject assignment. The citizen's workplace ID is authoritative; Hut worker and roster queries read it. The current panel allows one chosen worker per Hut; final staffing limits are TBD. Name, ID, sex, age, attributes, tunic/hair/base identity remain unchanged. A brown work apron and belt are added to that model. Other citizens remain unemployed. Assigned Builders take no new ambient trips; already pending Wood returns before ordinary behavior resumes. There are no NPC construction jobs yet.

All building, citizen and roster panels use existing UI Toolkit styling and pointer guards. Placement has priority over world selection. Ordinary world picking prefers a citizen silhouette, otherwise the footprint under the ground ray. B clears inspection; Escape cancels placement and closes inspection. Closing/deselecting a site stops player work.

## Gathering and prototype balance

`BootstrapSettings` centralizes these replaceable prototype values:

| Setting | Default |
| --- | --- |
| Starting Wood | 10 |
| First Hut cost | 20 Wood |
| Successful trip yield / chance | 2 Wood / 0.90 |
| Maximum Wilderness depth | 10 Tiles |
| First Hut required active work | 30 seconds |
| Gathering pause | 3 seconds |
| Cooldown after return | 18-32 seconds |
| First dispatch delay / per-citizen stagger | 4 seconds / 6 seconds (4/10/16/22) |
| Concurrent ambient trips | At most 2 |
| Preferred candidate depth / sampling window | Depth 1-4 / nearest 24 reachable candidates |
| Citizen detection radius for wildlife | 4 Tiles |

Citizen movement defaults remain Inspector-configurable: 0.8 Tiles/second and idle pauses 1-3 seconds. Trip travel time depends on actual route length. The cooldown gives time for ordinary life between trips; income requires visible walking, gathering and returning, never passive town income.

Unemployed active adults alone are eligible. Tasks are separate from profession: Idle/Wandering -> GatheringWoodOutbound -> GatheringWood -> ReturningWood -> ordinary behavior. Gathering rolls once after its pause, records 2 or 0 pending Wood, then routes to Town Hall. Deposit occurs exactly once on arrival. The oriented Town Hall exterior entrance is preferred. If another reserved footprint blocks it, a reachable claimed/unoccupied cell immediately adjacent to Hall is used as an outdoor delivery area; this grants no interior traversal.

Depth is **multi-source cardinal BFS from actual claimed boundary cells**. Adjacent Wilderness has depth 1; cells beyond depth 10 are excluded. This follows irregular protrusions/holes/corners and updates when claim count changes; no Town Hall radius or bounding-box membership is used. Actual task paths also avoid reserved footprints and maintain 0.28-Tile body clearance. Ordinary idle policy remains claimed/unoccupied only. Nearby candidate sampling is a practical forest preference, independent of decorative tree objects, and never removes trees. Rendered Wilderness now extends beyond the full task-depth limit.

Safety recovery overrides gathering when a footprint covers a citizen. Interrupted task records preserve pending Wood, then replan return after recovery. Changed/claimed outbound destinations cancel gathering and return without inventing income. When no return route exists, the task retains pending Wood and waits/retries after access is restored. Already pending returns take precedence over new ambient work. Mandatory Builder work is a future scheduler extension.

## Wildlife

Squirrel, Badger, Deer and Fox remain peaceful, decorative, resource-free, and Wilderness-only. Default population remains two of each with seed 731, no respawning or combat. The current threat is the nearest active citizen.

Approaching within **4 Tiles** interrupts ordinary pause or an active wandering segment. Safe destinations increase threat distance; when a deeper Wilderness alternative exists it is preferred over sideways escapes, then candidates rank by greater threat distance followed by depth. Segment/body checks reject claimed traversal. Running uses existing Inspector-configurable **1.8 x 0.65 = 1.17 Tiles/second**. Citizen fleeing continues until **6 Tiles** away (2-Tile safety margin), then ordinary pause/wandering resumes. These are prototype tuning values.

Expansion evacuation has priority: claiming underneath an animal starts continuous cardinal travel to the nearest valid Wilderness exit and a short deeper run. Claimed traversal permission ends as soon as the body is fully clear; citizen threat handling does not overwrite the evacuation route. Only impossible/broken states use warning-bearing relocation/dormancy. No animals die and no resources are awarded. The original abstract RequestFlee fixture remains covered by its existing tests.

## Construction and influence

Blueprint/Planned -> Under Construction -> Completed is the shared lifecycle. The current preview is an unpersisted blueprint; Town Hall starts Completed. Confirming a valid first Hut with sufficient stock immediately consumes **20 Wood once** and reserves all oriented **3 x 3** footprint cells, including its entrance/perimeter. Repeated input cannot create a second first Hut or charge again. It appears as foundation/posts/frame rather than a completed building. Influence is zero until completion.

Select the site and **hold E (Interact / Action)**. This construction context uses the continuous form of universal interaction. The adapter passes active player work to the plain-C# contribution API; no fake player model/controller exists. The panel shows progress and remaining active seconds. Releasing E, closing/deselecting, opening Build or pressing Escape freezes stored progress. **15 active seconds + any pause + 15 active seconds** completes the 30-second requirement. Excess work is capped; later calls cannot complete or apply influence again.

Completion switches visuals and applies only individual footprint-plus-3 Tiles once. For origin **(17,8)**, claims go **400 -> 427**, adding only X 20..22/Z 5..13; (20,4), (20,14), (23,8) remain Wilderness. Citizen connectivity and wildlife evacuation reconcile immediately. Starting Hall remains exempt, including repeated influence calls. Footprints remain normal outdoor obstacles; the exterior entrance is not reserved or an additional general placement requirement.

The previous entrance/recovery foundation is retained: canonical south lower-middle entrance rotates with footprint through all four quarter turns, with visible opening/posts/threshold. Shared weighted recovery favors fewer occupied cells, then travel cost with bounded entrance preference; safety routes may exceptionally cross reserved cells only along that citizen's recovery whitelist. Largest connected claimed/free component is the ordinary anchor, with deterministic Town Hall access preference on ties. Covered/sealed citizens move continuously without position jumps. Impossible/broken routes alone warn and relocate/dormant; no interiors, house ownership, night schedules, or normal footprint traversal are added.

## Approved future design and TBD

Future physical player interaction, range/tool requirements, manual resource transport, inventories, dialogue, fishing, exploration and direct-action quests remain future systems. Both Player and Builder actor kinds share the same domain contribution/progress foundation, so physical player input and NPC work can later supply work without duplicating construction. No NPC job dispatch/travel/work loop or other building costs are invented.

Before Warehouse, Town Hall stores resources. After the first completed Warehouse, it will become the primary storage/logistics center; transfer behavior, Courier automation, capacities and delivery priorities remain TBD. All normal later construction belongs to assigned Builder NPCs. Final costs/durations, worker limits, construction range, upgrades, cancellation/refunds, final cadence and polished UI/art remain TBD. There are no functional professions beyond Builder, other resources/production, families/children, needs, relationships, day/night, interiors, predators/combat, quests, upgrades, demolition, roads, streaming, saving or player inventory.

## Universal player interaction

**E = Interact / Action**, the canonical context-sensitive player intent rather than a dedicated construction key. One plain-C# PlayerInteraction owns the selected building/citizen target, derives the available interaction from current state, and receives press/hold intent through one keyboard adapter in SettlementPrototypeView. No unrelated system checks E independently. Future physical-player target resolution can use the same path; no player model, distance/tool checks, dialogue, storage logistics or item interactions are added now.

| Selected valid context | Input | Current result |
| --- | --- | --- |
| Town Hall | Press E | Open settlement management. |
| Completed Builder's Hut | Press E | Open workplace management; assignment still needs Assign Worker and an explicit citizen choice. |
| Citizen | Press E | Show existing citizen information without changing identity or employment. |
| Incomplete first Builder's Hut | Hold E | Contribute active manual work through the existing construction API. |
| No target / unsupported future building | E | No action. |

Discrete contexts use the press edge, not repeated held frames. Construction consumes held time; press-only intent without hold does not add work. Release freezes accumulated progress. On completion the available action becomes workplace management, and continuing the same hold neither repeats completion nor automatically opens/assigns a worker.

Existing left-click inspection remains available. The prototype uses the selected target, without physical range requirements. Closing building management preserves the target; a fresh E press reopens it. Closing a construction panel stops an existing held action until a fresh E press or mouse selection reopens it. Opening Help preserves the target but suppresses all E intent, including the closing Help frame. Placement/Build mode also suppresses interaction. Pointer guards still prevent selection or placement underneath visible panels.

Future citizen dialogue/quests and other approved physical-player interactions will extend this same primary action. They are not currently available.

Manual interaction checks:

1. Click Town Hall, close its panel, then press E: management reopens. Holding E thereafter must not repeatedly recreate the panel or change resources/territory.
2. Click a completed Hut, close management, then press E. Confirm no citizen is automatically assigned; use Assign Worker and choose a specific citizen. Press E again and verify the assigned worker remains unchanged.
3. Click each citizen and press E. Verify existing information and unchanged ID/name/sex/age/attributes/profession. Mouse inspection must still work normally.
4. Select an incomplete first Hut and hold E. Confirm continuous work, interruption/resume, exactly one completion/influence, then a new E press opens workplace management. Continuing the completing hold must not assign anyone.
5. Close the incomplete site's panel while holding E: work stops. Release and press E again to reopen/resume. Open Help and press/hold E in each context: no interaction or work. H/Escape close Help without clearing the target or contributing work on that closing frame.
6. With Build/placement active, press E: no interaction/assignment/work. Retest rotation, mouse placement, UI click-through guards and contextual Escape. No scene regeneration is required.

## Help & Hints

Press **H** or click the compact **Help [H]** HUD button to toggle the same Help state. **Escape** while Help is open closes only Help; the panel also has a **Close** button. H/Escape are consumed on the closing frame so they cannot also cancel placement or trigger a gameplay shortcut. Help starts closed.

The runtime UI Toolkit overlay uses the existing prototype styling, a readable title, section headings, scrollable content, Close button, and prototype-balance footer. It is sized to the viewport rather than a full-screen opaque backdrop. Transparent space outside the visible panel retains world input. Its visible panel participates in the existing pointer-blocking set: clicks cannot select/place underneath, wheel input scrolls Help without camera zoom, and middle-drag over it cannot pan the camera.

Sections: **Start Here** (prominent Town Hall and first-Hut hints), **Controls**, **Gameplay / Prototype Balance**, **Building**, the four **Town Hall newly unlocked building groups**, **Citizens**, and **Wildlife**. Balance values come from BootstrapSettings; names/footprints/unlocks come from BuildingCatalog. The reference describes only current functionality, including the Level 4 TEST OVERRIDE and deferred subsequent construction. It does not imply Town Hall upgrades or future systems are implemented.

Documented controls are the actual source controls: WASD/arrow camera movement, middle-drag pan, wheel zoom, left-click contextual selection/placement, B Build toggle, R quarter-turn rotation, E universal/context-sensitive Interact / Action (press for inspection/management, hold for first-Hut work), H Help toggle, and contextual Escape. While Help is open, B/R/E and keyboard camera movement are blocked. Citizen/wildlife/gathering simulation continues at its usual rate; there is no time-scale or pause change. Manual work requires its active input, so Help suppresses E contributions while preserving accumulated construction progress.

Opening Help temporarily hides Build/placement/citizen/building inspection panels without clearing their underlying state, selected citizen, managed building, worker-choice state, or placement orientation. Closing restores the applicable panels. The compact HUD remains available; Build is disabled while Help is open. The Help state/content class has no Unity rendering or simulation ownership; content is rendered as individual sections rather than a giant input-handler text blob.

Manual Help checks:

1. Let Unity import PrototypeHelp.cs and PrototypeHelpStateTests.cs and generate their `.meta` files. Run all 117 World EditMode tests; no scene regeneration is required.
2. Enter SettlementPrototype: Help is initially closed and **Help [H]** is visible. Open/close with H and the button; open then close using Escape and Close. Confirm Escape closes Help without clearing a prior placement or inspection.
3. Scroll through every section. Verify actual controls, Town Hall roster/storage hint, first-Hut sequence, prototype values, cumulative Level 1-4 unlock groups/footprints, Level 4 test label, citizen and wildlife rules. Check readability and access to Close at normal and smaller window sizes.
4. Over the panel, scroll at both content ends: the camera must not zoom. Middle-drag must not pan; clicks on title/content/padding/Close must not select citizens/buildings or place beneath it. Transparent outside space should remain usable.
5. Select a rectangular placement preview, rotate it, then open Help. Press B/R/E while reading: Build state, orientation and work must not change. Close Help and verify the original placement/rotation continues. Retest normal B/R/E after closing.
6. Open Town Hall or citizen inspection, then Help. Confirm large panels hide and return with the same selection on close. Repeat from Hut worker-choice UI and a partially worked construction site; progress must remain stored and E must not add work while Help is open.
7. Keep Help open long enough to observe citizens/wildlife walking and gathering deliveries in the HUD. Confirm no simulation pause. Close and resume inspection/construction normally. Check Console and smoke-test existing menu/profile flows.

## Validation and manual Unity acceptance

Source validation uses Unity's bundled compiler/runtime and installed NUnit. The 72 existing territory/catalog/placement/wildlife/citizen/entrance/recovery tests remain unchanged. Added SettlementBootstrapTests and CitizenWildlifeFleeTests cover resource retry/overflow/negative checks, first-Hut placement/work/influence, explicit identity-preserving employment/live roster, gathering authorization/boundary/arrival-only delivery/random injection/recovery, and citizen-triggered wildlife safety/evacuation. **117 NUnit tests passed, 0 failed (109 existing + 8 interaction tests)**. PlayerInteractionTests cover press versus hold, Town Hall/workplace/citizen contexts, completion context changes, suppression, explicit assignment and unrelated-state preservation. The new presentation-state tests cover initial state, shared toggle/key consumption, contextual Escape, simultaneous-key priority and unchanged settlement/citizen/construction state. Static content strings and pixel positions are not tested. Domain, Unity presentation/Editor source, and the separate test assembly compile without compiler warnings or errors against installed Unity 6000.6.4f1 assemblies. `git diff --check` passes; this does not establish Editor import or Play Mode visual correctness.

The known Editor licensing/sandbox limitation remains; licensing/security settings are unchanged. Use the normal Editor:

1. Exit Play Mode, allow import/compilation, and let Unity generate `.meta` files for the new PlayerInteraction source/test files. Retain these before any later commit. Do not regenerate the existing scene/materials. Run all World EditMode tests in Test Runner and check Console.
2. Open SettlementPrototype and enter Play Mode. Confirm Wood **10**, 400 claims, one completed Hall, four unemployed adults, and TH Level **4 TEST OVERRIDE**. Click Town Hall: total 4/employed 0/unemployed 4, storage Town Hall, four unique complete roster entries. Click each roster row and each world figure; compare all five actual attributes and name/sex/age.
3. Observe staggered trips: walking into Wilderness, short pause, visible return, then **+2** only at Hall access on success; failure gives +0. Inspect citizen Task in the detail panel. Ordinary citizens between trips remain claimed-only. Trip destinations stay within the actual 10-Tile boundary depth; decorative trees remain intact. Wait for Wood to reach at least **20**.
4. Observe wildlife near gathering citizens. Confirm all four species can interrupt pauses/walks, run away/deeper at the faster speed, avoid claims, and resume wandering after safety. Check for position jumps or ordinary emergency warnings.
5. Before 20 Wood, preview the Hut and confirm clear insufficient-resource feedback with no reservation/charge. At 20+, choose Hut, move origin to **(17,8)**, optionally rotate R, and confirm placement. Exactly 20 Wood is debited, footprint is 3 x 3 with correctly rotated entrance, incomplete frame appears, and claims remain **400**. Attempt overlaps/second-Hut confirmation; no extra charge. Other catalog entries remain previews with deferred-construction feedback.
6. Select the site and hold **E for about 15 seconds**. Confirm roughly 50%/15 seconds remaining. Release for at least 10 seconds; progress must freeze. Deselect/close/reselect and check it persists. Resume E for the remaining work; completed visual appears and claims become **427 once** at the test origin. Holding E further changes neither progress nor influence. Check citizen recovery and wildlife expansion evacuation remain continuous; new trees vanish with no Wood reward.
7. Click completed Hut -> **Assign Worker** -> explicitly choose a citizen. Confirm that exact person becomes Builder, receives the Hut workplace and visible work apron/belt, retaining identity, sex, age and all attributes. Hut shows that worker; Town Hall roster and world info immediately agree; other citizens stay unemployed. A Builder finishes already pending return, then stops receiving ambient trips.
8. Retest UI padding/buttons/scroll, transparent-space world clicks, B/R/E/Escape, camera pan/zoom, Tile outlines/Gizmos, and placement priority. UI actions must not place/select behind panels or zoom the world while scrolling UI. Click another object or close site while E is held and verify work stops.
9. Restart Play Mode: initial state and deterministic identities reset. Smoke-test MainMenu/profile/settings without changed routing. Check Console for compilation/runtime/shader/material/input errors and duplicate AudioListeners. Manually exercise the same cases at different window sizes.

Generic placement and chained multi-type expansion remain domain fixture tests. The playable runtime intentionally waits for approved future recipes/NPC construction before allowing subsequent normal buildings.