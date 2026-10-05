# ColonyGame: World and Settlement Design

## Status, authority, and scope

This is the authoritative current high-level design reference for ColonyGame's world, settlement, population, resources, production, professions, buildings, wildlife, and early-game progression. It records the approved direction supplied by the project owner. Follow `AGENTS.md` when implementing it.

Status terms used throughout this document:

- **Approved design decision**: a confirmed rule, constraint, definition, association, or value. Approval describes the design, not whether it has been implemented.
- **Planned future system**: an intended capability whose implementation belongs to a later playable vertical slice. Only the explicitly stated aspects are approved; unspecified details remain TBD.
- **TBD / undecided**: no decision or balance value has been approved. Future agents must not fill gaps with assumed mechanics, defaults, formulas, AI behavior, or building functionality.

This document does not authorize gameplay implementation. It does not change the existing menu, profiles, settings, scenes, assets, packages, or Unity settings. All terminology and future agent-created project content must be in English. Preserve Unity 6.6 / 6000.6.4f1 compatibility unless an upgrade is explicitly approved.

## 1. Core world model

**Approved design decisions**

- The logical world uses a square grid. Its smallest logical cell is a **Tile** in code and technical documentation.
- An individual Tile must never be called a Chunk. **Chunk** remains available as a future technical term for groups of Tiles used for streaming, rendering, optimization, or world partitioning. No Chunk dimensions or implementation are approved.
- A new settlement begins with **exactly 20 x 20 claimed Tiles**, with an already existing Town Hall inside that predefined territory. The starting Town Hall triggers no completion expansion at world creation.
- The early prototype may use a simple, flat, top-down environment. Core simulation is independent of that presentation.

**Approved future player role**: the player physically participates in colony management, direct world interaction, exploration, manual resource transport, and quests. Future examples include taking resources from Town Hall/Warehouse, carrying/delivering to NPCs/sites, talking to citizens, personally catching fish, and performing world objectives. Quests may require talking to N/specific citizens, catching N fish, deliveries, and world interactions. No physical Player Controller/model, inventory, tools, carrying, dialogue, fishing, or quests are implemented in this slice. Player-provided construction work is a domain operation with a replaceable prototype input adapter.

**Planned future presentation**: a fully 3D settlement that the player can explore directly, potentially switching between first-person and third-person perspectives. Specific controls and switching behavior are TBD.

## 2. Settlement Territory and Wilderness

**Approved design decisions**

- The world is conceptually divided into claimed **Settlement Territory** and surrounding **Wilderness**.
- The player may physically move only inside claimed Settlement Territory.
- Idle citizens wander only inside claimed Settlement Territory. Specific future tasks/professions may authorize Wilderness travel; ordinary idle wandering does not grant that permission.
- Citizens on authorized Wilderness trips must never travel farther than **10 Tiles beyond the current settlement boundary**.
- Building footprints are obstacles for normal outdoor citizen movement, including their perimeter Tiles. Buildings have defined entrances/access points for citizen interaction. Citizens approach an adjacent exterior access Tile and use an explicit entrance transition rather than treating the footprint as walkable outdoor space. The exterior access Tile is separate from the reserved footprint and normally must be claimed and unoccupied. Actual building entry/exit and interiors remain future systems.
- A building may be placed only when its **entire reserved footprint** is already inside existing claimed Settlement Territory.
- Placing a building partly or fully outside the territory to claim new land is prohibited.

Citizen Wilderness distance is conceptually measured relative to the **nearest actual claimed Settlement Territory boundary**, respecting its irregular shape. It is not measured from the original 20 x 20 area, Town Hall, settlement center, or rectangular bounding box.

Implemented prototype metric: multi-source cardinal breadth-first depth from all actual claimed boundary cells. The first exterior Wilderness cell is depth 1; only cells through depth 10 are authorized. This respects protrusions, holes, and corners without claiming a bounding rectangle. Route search also requires unoccupied body clearance and a reachable return access area. The cached area refreshes after claims change; blocked task routes yield to recovery or replan a return while preserving pending Wood. This is the current prototype metric, not a final 3D pathfinding commitment.

## 3. Territory expansion

**Approved design decisions**

Every building initially constructed and completed during gameplay expands Settlement Territory through influence covering **its entire reserved footprint plus 3 Tiles in every direction**. Influence applies **once on initial completion**. Only influenced Tiles previously outside Settlement Territory become newly claimed.

Expansion uses actual claimed Tiles. Do not expand the settlement's entire bounding rectangle or substitute a rectangular settlement radius. Territory representation must support irregular borders, extensions, and protrusions. This is a state requirement, not approval of a particular storage structure.

The rule applies to every building type initially completed during gameplay. Placement eligibility is checked against existing territory before completion influence applies. The starting Town Hall is exempt at world creation: the predefined initial territory remains exactly 20 x 20 Tiles. Normal expansion starts with subsequent gameplay construction and completion.

**Upgrades never apply additional territory influence**: the footprint is unchanged and its original influence has already applied. Initial completion applies influence once; later upgrades apply none.

**Claimed Settlement Territory is permanent under the current design.** Demolition or destruction of a building does not return its previously claimed Tiles to Wilderness and does not shrink territory. Future combat-related destruction follows this principle unless explicitly redesigned. Exact demolition costs, refunds, ruins, rebuilding, and destruction behavior remain TBD.

Building influence areas may overlap without bonuses; already claimed Tiles simply remain claimed. Reserved building footprints must not overlap (section 20).

Approved examples along an existing straight boundary:

| Placement | Influence width parallel to boundary | Newly claimed outward depth | Result |
| --- | --- | --- | --- |
| A 3 x 3 House directly against the boundary, with its entire footprint already claimed | 3 + 3 + 3 = 9 Tiles | 3 Tiles | 3 Tiles deep x 9 Tiles wide |
| A 2 x 2 Guard Tower with one claimed Tile between its footprint and the boundary | 2 + 3 + 3 = 8 Tiles | 3 minus the one already-claimed Tile = 2 Tiles | A protrusion extending 2 Tiles beyond the old boundary |

These examples add only previously unclaimed Tiles and do not turn irregular territory into a bounding rectangle. Upgrades do not repeat influence; demolition/destruction do not revoke territory; the starting Town Hall does not expand the predefined initial area.

## 4. Wilderness and ambient Wood gathering

**Approved design / implemented now**: unemployed adult citizens occasionally make authorized Wilderness trips for small supplementary Wood income; no Lumberjack is required. Assigned Builders do not take ordinary ambient trips. Profession and current task are separate: Idle/Wandering, GatheringWoodOutbound, GatheringWood, ReturningWood, and Recovery.

The visible loop is outbound travel, a gathering pause, one success roll, and a return to the Town Hall outdoor access area. Successful Wood is pending during return and deposited only on arrival. A failed trip returns normally with zero Wood. Ordinary wandering remains settlement-only. Recovery/safety takes priority, followed by returning pending Wood, mandatory assigned work, ambient gathering, and idle behavior. Mandatory Builder construction tasks are future work.

**Prototype balance**: start with 10 Wood; successful trip yields 2 Wood with 90% success; maximum Wilderness depth is 10 Tiles from the current actual irregular boundary. The implemented cardinal boundary distance and path eligibility are described in section 2. Destinations are reachable Wilderness cells, without requiring a rendered tree. Defaults: 3-second gathering pause, 18-32-second cooldown after return, staggered initial delays of 4/10/16/22 seconds, and at most two simultaneous trips. Current destination sampling prefers nearby reachable cells within depth 1-4, among the nearest 24 candidates. These configurable cadence choices are prototype tuning, not final balance.

Early Wilderness is primarily forest. Decorative trees are not persistent harvest nodes; trips do not chop, destroy, reserve, or regenerate them. Newly claimed vegetation disappears visually, but territory expansion awards no resources. Lumber Mill + Lumberjack remains the future scalable Wood source; ambient income should eventually be economically insignificant.

**TBD**: final gathering balance, physical carrying/animations, broader profession eligibility, and more advanced scheduling/interruption behavior. The old permission for the initial Builder to gather while idle is superseded by this milestone's unemployed-only rule.

## 5. Resources

**Approved resource vocabulary**

| Category | Resources |
| --- | --- |
| Materials | Wood, Stone, Metal, Wool |
| Food / agricultural resources | Beetroot, Wheat, Potato, Carrot, Fish, Meat |

**Planned future system**: resource definitions should be data-driven and extensible. These resources are not an exhaustive permanent list; additional types may be approved later.

The player should eventually have a persistent, understandable UI panel or HUD showing quantities of relevant/unlocked resources. Resource UI presentation is TBD; see section 31. Only Wood is functional now: the prototype starts with 10. Town Hall stores it before Warehouse; ambient trips deliver there and the first Hut consumes from that inventory. Other starting quantities and economic prices remain TBD.

## 6. Global building levels and reserved footprints

**Approved design decisions**

- The maximum building level is **Level 4**.
- Where levels apply, upgradeable buildings use **Levels 1 through 4**. Do not assume or implement Levels 5+.
- A building's maximum level is capped by the **current Town Hall level**: Town Hall Levels 1, 2, 3, and 4 cap other buildings at Levels 1, 2, 3, and 4 respectively. This cap does not define upgrade costs or effects or imply every building is upgradeable.
- Building unlock level and maximum building level are separate concepts (section 20). Farm unlocks at Town Hall Level 1 and can reach Level 1, 2, 3, or 4 as the Town Hall reaches those levels. Laboratory cannot exist before Town Hall Level 4 because its type unlocks only then.
- This ceiling does not require inventing four sets of upgrade effects for every building. Undefined level behavior remains TBD.
- The maximum physical footprint is reserved at initial placement. **Upgrades never increase the reserved footprint.**
- Higher levels may change visuals, production, capacity, available functionality, progression capability, or other building-specific properties only as those effects are designed.

Whether each individual building is upgradeable and its currently unspecified effects must not be inferred solely from the global level rule. Additional levels require explicit later approval.

## 7. Universal production progression

**Approved design decisions**

Resource-producing buildings use this common model unless a later approved design explicitly defines an exception. It applies to the Farm, Lumber Mill, Mine, Fisherman's Hut, Butcher, and future production buildings unless explicitly overridden.

**Assigned workers are required for active scalable production.** Resource-producing buildings do not generate their normal scalable output merely because they exist. An appropriate citizen with the corresponding profession must be assigned:

| Production building | Required corresponding profession |
| --- | --- |
| Farm | Farmer |
| Lumber Mill | Lumberjack |
| Mine | Miner |
| Fisherman's Hut | Fisher |
| Butcher | Butcher |

Without an appropriate assigned worker, a building performs no normal scalable resource production. The formula describes an **active production process** and does not remove this requirement. Exact worker counts, extra workers from upgrades, efficiency/stat modifiers, work schedules, and absence behavior remain TBD.

Each producible resource definition has at minimum:

- Resource Type;
- Resource Unlock Building Level;
- Base Production Cycle Duration.

Availability:

`BuildingLevel >= ResourceUnlockLevel`

For an available resource, after each completed production cycle:

`ProductionAmount = BuildingLevel - ResourceUnlockLevel + 1`

A resource starts at exactly **1 unit per cycle** at its unlock level; every subsequent building level adds **1 unit per cycle**. Below its unlock level it is unavailable; do not apply the amount formula as an available output.

| Resource unlock level | Building L1 | Building L2 | Building L3 | Building L4 |
| --- | --- | --- | --- | --- |
| 1 | 1 per cycle | 2 per cycle | 3 per cycle | 4 per cycle |
| 2 | Unavailable | 1 per cycle | 2 per cycle | 3 per cycle |
| 3 | Unavailable | Unavailable | 1 per cycle | 2 per cycle |
| 4 | Unavailable | Unavailable | Unavailable | 1 per cycle |

Building upgrades increase output per cycle. Building level **does not automatically reduce Base Production Cycle Duration**. Production definitions should eventually be data-driven rather than hardcoded separately into production systems.

**Production scheduling remains TBD.** Multiple unlocked resources do not imply simultaneous production. For example, Farm Level 4 might later use concurrent crops, one selected crop, player priorities, Farmer-selected work, divided worker time, or another model; none of these options is approved.

Production inputs, interrupted cycles, partial progress, storage blocking, worker travel time, offline/background production, pauses, upgrades during cycles, resource delivery, and output destinations remain TBD. The formula decides none of these mechanics or the unresolved worker details.

## 8. Farm

**Approved**: reserved footprint **3 x 4 Tiles**; associated profession **Farmer**; products Beetroot, Wheat, Potato, and Carrot.

| Resource | Unlock building level | Base Production Cycle Duration |
| --- | --- | --- |
| Beetroot | 1 | 15 seconds |
| Wheat | 2 | 30 seconds |
| Potato | 3 | 45 seconds |
| Carrot | 4 | 60 seconds |

Approved production progression:

| Farm level | Beetroot | Wheat | Potato | Carrot |
| --- | --- | --- | --- | --- |
| 1 | 1 / 15 seconds | Unavailable | Unavailable | Unavailable |
| 2 | 2 / 15 seconds | 1 / 30 seconds | Unavailable | Unavailable |
| 3 | 3 / 15 seconds | 2 / 30 seconds | 1 / 45 seconds | Unavailable |
| 4 | 4 / 15 seconds | 3 / 30 seconds | 2 / 45 seconds | 1 / 60 seconds |

This follows the universal formula. The table states each available resource's output and cycle duration, without choosing how multiple product cycles are scheduled.

**TBD**: Farm upgrade costs, construction time, additional upgrade benefits, visual progression, and field presentation.

## 9. Wood and Lumber Mill

**Approved**: reserved footprint **5 x 5 Tiles**; associated profession **Lumberjack**.

The Lumber Mill becomes the primary scalable Wood source. Small ambient citizen Wood gathering may continue after the mill exists, but should become economically insignificant compared with mill production in the mid-game and late-game.

Lumber Mill Wood production uses the universal production progression model.

**TBD**: Wood unlock level, Wood Base Production Cycle Duration, and Lumber Mill level-specific functionality. No values are to be invented.

## 10. Mine

**Approved**: reserved footprint **4 x 4 Tiles**; associated profession **Miner**.

Stone and Metal must not initially appear as ordinary visible deposits around the starting settlement. Their availability and environmental representation are introduced through Mine progression. Mine production uses the universal model.

**TBD**: Stone unlock level/cycle duration, Metal unlock level/cycle duration, mining-resource visuals, and detailed Mine progression.

## 11. Fisherman's Hut

**Approved**: reserved footprint **3 x 5 Tiles**; associated profession **Fisher**; produces Fish under the universal model.

The complex includes a small pond/lake **within the same reserved 3 x 5 footprint**. The pond occupies no extra territory. Fishing does not require individually persistent fish entities.

**TBD**: Fish unlock level, Fish Base Production Cycle Duration, and level-specific progression.

## 12. Butcher and livestock

**Approved**: reserved footprint **3 x 6 Tiles**; associated profession **Butcher**.

The working building and livestock enclosure/pen form one complex within the same reserved 3 x 6 footprint. The enclosure does not extend beyond it.

Domestic livestock includes **Cow, Pig, Chicken, and Sheep**. They live in the Butcher's enclosure and do not freely roam the world or Wilderness.

The Butcher produces **both Meat and Wool**, each under the universal production model. Wool is not assigned to a separately invented building or profession.

**TBD**: Meat unlock level/cycle duration, Wool unlock level/cycle duration, livestock population, exact animal counts, breeding, slaughter/shearing representation, and production animations.

## 13. Wildlife

**Approved categories and constraints**

| Category | Animals | Design constraints |
| --- | --- | --- |
| Potentially hostile Wilderness wildlife | Wolf, Bear | Hostility and threat capability are intended; exact predator/combat mechanics are TBD. |
| Decorative Wilderness wildlife | Fox, Badger, Squirrel, Deer | Provides no resources, does not attack citizens, is not attacked by citizens, and primarily makes the world feel alive. |
| Domestic livestock | Cow, Pig, Chicken, Sheep | Remains within the Butcher's enclosure; not freely roaming Wilderness wildlife. |

**Approved peaceful ambient wildlife rules**

- Foxes, Badgers, Squirrels, and Deer are decorative: they provide no resources, cannot be hunted/attacked, and do not attack citizens.
- Peaceful ambient wildlife normally moves only through Wilderness and never voluntarily enters claimed Settlement Territory. Eligibility follows actual irregular claimed territory, not the original rectangle or a Town Hall radius. If expansion claims an animal's location, territory still expands immediately and unchanged. The animal evacuates continuously at a faster run speed toward a nearby sensible Wilderness exit, temporarily traversing claims only to escape. This permission ends as soon as the animal reaches valid Wilderness; it continues a short distance deeper before resuming normal wandering. Teleportation is reserved for an impossible/broken-state emergency with a development warning. Evacuation never kills animals or awards resources.
- Implemented citizen-triggered fleeing interrupts ordinary pause/wandering and reacts to the nearest active citizen. Valid escapes increase threat distance and prefer deeper actual Wilderness when available; within those candidates, greater threat distance ranks before depth. Fleeing never voluntarily enters claims. Prototype detection radius is 4 Tiles; safety distance is 6 Tiles, preventing rapid toggling; running uses the configurable existing 1.8 multiplier. At safety, ordinary wandering resumes. Expansion evacuation routes have priority over citizen fleeing. These are prototype tuning choices, not final wildlife balance.
- The Wilderness-only policy belongs to peaceful ambient wildlife. Future Wolves/Bears may enter Settlement Territory and follow different movement/combat rules; their implementation remains deferred.
- Cow, Pig, Chicken, and Sheep are separate future livestock associated with the Butcher's enclosure and do not roam Wilderness.
- Schematic prototype geometry/colors are replaceable presentation choices, not permanent design rules.

**TBD**: final spawning/population balance, ecology, detailed movement/flee tuning and citizen detection, predator AI, and combat behavior. No hunting or resource yield from decorative wildlife is approved.

## 14. Citizen identity

**Approved design decisions**

Citizens are persistent individuals, not disposable generic units. They include adult men, adult women, and children. Each has a persistent underlying identity and base appearance.

Assigning or changing profession must preserve that same person and underlying appearance. Profession presentation changes clothing, tools, and equipment; it must not replace the citizen with a different person or completely different base character. This remains valid when full 3D models replace prototype visuals.

**Planned future building access**: citizens can become logically inside a building through its designated entrance and later exit through the entrance/access area. Houses may use this for citizens going home and remaining inside at night. This does not require outdoor pathfinding across the building footprint. Interiors, home assignment, night schedules, day/night behavior, and sleeping are not implemented or otherwise specified here.
## 15. Citizen characteristics

**Planned future model capabilities**: name/identity, sex, age/life stage, family relationships, profession, skills/aptitudes, individual characteristics/statistics, current needs/state, work assignment, and home assignment.

Citizens have persistent identifiers, first names, sex, age, profession state, and base attributes. Approved base attributes are **Strength, Dexterity, Intelligence, Endurance, and Charisma**, represented as integers from **1 through 10**. These attributes currently have no gameplay effects; efficiency, suitability, combat, social, leveling, and experience formulas remain undefined. Future relationships can reference persistent citizen identifiers without replacing those identities.

**TBD**: additional characteristics, skills, needs, happiness, aging rates, skill/attribute effects, and profession suitability rules. Do not invent additional statistics or formulas.

## 16. Families and population

**Planned future system; approved direction**

Citizens form a persistent settlement population. Adults may form families and have children. Children are persistent citizens who eventually mature into adults. Population should support growth, generational continuation, and recovery after deaths.

This serves believable settlement gameplay; unnecessary biological simulation is not required.

**TBD**: relationships, partner selection, family formation, child generation, aging duration, family limits, housing requirements, and population caps.

## 17. Visitors and recruitment

**Planned future system; approved direction**

People may occasionally arrive from outside the settlement. Visitors are not automatically citizens. The player should eventually be able to attract or recruit suitable visitors. Recruitment supplements families and children as a population-growth and recovery mechanism.

**TBD**: arrival frequency, recruitment requirements/costs, visitor characteristics, reputation requirements, and recruitment UI.

## 18. Citizen death and population continuity

**Planned future system; approved constraints**

Citizens may eventually die from Wolves, Bears, or future hostile dangers. Because individuals persist, death should matter. It must not automatically force an unrecoverable population decline.

Recovery can eventually come through families having children, children becoming adults, and recruitment of outside visitors. These are intended recovery mechanisms, not defined automatic replacement behavior or guarantees with invented timing.

**Population deadlock prevention remains explicitly TBD.** Visitor/recruitment systems may eventually provide recovery when family-based growth is impossible, but no extreme-loss fail-safe is approved. Do not define guaranteed emergency visitors, automatic replacement citizens, minimum protected population, recruitment guarantees, or game-over conditions.

**TBD**: death consequences, family effects, profession replacement, morale effects, graves/memorials, inheritance, and exact recovery behavior when no suitable adults or citizens remain.

## 19. Professions

**Approved/planned profession vocabulary**

Builder, Courier, Cook, Miner, Lumberjack, Farmer, Fisher, Butcher, Warrior (Swordsman and Archer specializations), Scientist, and Mage.

Citizens may also have no profession. Profession is an assignment/state associated with an existing citizen, not the citizen's underlying identity. Approval of these names does not imply their complete behavior has been designed or implemented.

## 20. Building unlock progression and reserved footprints

**Approved building unlock progression through Town Hall Level 4**

The Town Hall is the starting settlement building, with a **5 x 5 Tile** reserved footprint. It exists from settlement creation and is not part of the normal build-menu unlock list.

| Required Town Hall level | Newly unlocked building / site | Maximum reserved footprint in Tiles | Associated profession/category where specified |
| --- | --- | --- | --- |
| 1 | Builder's Hut | 3 x 3 | Builder |
| 1 | House | 3 x 3 | Not specified |
| 1 | Farm | 3 x 4 | Farmer |
| 2 | Tavern | 4 x 5 | Cook |
| 2 | Butcher | 3 x 6 | Butcher |
| 2 | Fisherman's Hut | 3 x 5 | Fisher |
| 2 | Guard Tower | 2 x 2 | Warrior dispatch is planned; staffing is TBD |
| 3 | Warehouse | 4 x 4 | Courier (planned) |
| 3 | Lumber Mill | 5 x 5 | Lumberjack |
| 3 | Mine | 4 x 4 | Miner |
| 3 | Barracks | 3 x 5 | Warrior |
| 4 | Laboratory | 6 x 6 | Scientist |
| 4 | Mage Tower + Place of Power | Shared 5 x 5 site | Mage |

A building type becomes available when the Town Hall reaches its required level. **Unlocks are cumulative**: Level 1 provides the initial three building types; Level 2 retains those and adds its four types; Level 3 retains all previous types and adds its four types; Level 4 makes all currently designed build-menu types available. No Town Hall Level 5+ content is currently designed; do not invent it.

The Mage Tower and its Place of Power occupy the same overall **5 x 5 reserved building footprint/site**. The Place of Power belongs to the Mage Tower site and is not separately placeable. Only the Mage Tower itself visually changes as the building progresses/upgrades; the Place of Power remains part of that same site (section 29).

This is the single approved building-type unlock list. Building-level caps are defined separately in section 6 and do not replace unlock requirements. Costs, construction times, Town Hall upgrade quest requirements, worker counts, building limits, prerequisites between individual buildings, and unspecified upgrade effects remain TBD unless already explicitly approved elsewhere. Do not invent additional buildings or prerequisites.

Reserve the entire maximum footprint from initial placement. Upgrades never enlarge it.

### Approved rotation and placement requirements

Rectangular footprints may rotate in **90-degree increments**. Square buildings naturally retain their dimensions when rotated. Rotation changes neither footprint area, building functionality, nor territory influence rules. Detailed visual orientation is presentation-specific.

| Building | Approved dimension examples in Tiles |
| --- | --- |
| Farm | 3 x 4 or 4 x 3 |
| Fisherman's Hut | 3 x 5 or 5 x 3 |
| Butcher | 3 x 6 or 6 x 3 |
| Barracks | 3 x 5 or 5 x 3 |
| Tavern | 4 x 5 or 5 x 4 |

The currently approved fundamental placement requirements are:

1. The **entire reserved footprint** is inside already claimed Settlement Territory.
2. Every footprint Tile is free of another building's reserved footprint: **reserved footprints must not overlap**.

Influence areas may overlap without bonuses. Do not invent additional general placement gates. Special-building requirements may be added only through later explicit design. Physical Tile dimensions and detailed entrance/traversal presentation remain TBD; these do not authorize new placement prerequisites.

### No gameplay road system

**ColonyGame has no gameplay road system.** Roads are not required for building placement, operation, citizen movement, production, resource transportation, Courier logistics, or settlement expansion. There is no general road-access, connectivity, or adjacency requirement.

Do not design or implement Road Tiles, Road buildings, road construction, road connectivity requirements, road maintenance, road-dependent movement bonuses, road-dependent logistics, or road adjacency requirements. Citizens navigate through available traversable space using the movement/pathfinding system. Buildings function without road connectivity. Courier logistics must not depend on a constructed road network.

Purely decorative paths or worn trails may potentially exist as future visual presentation. They must not become a required gameplay system unless the design is explicitly changed.

## 21. Town Hall

**Approved**: reserved footprint **5 x 5 Tiles**; already present inside the exactly 20 x 20 predefined starting territory; a major progression gate. It triggers no initial completion expansion at world creation. Final starting position and level remain TBD; the prototype uses origin (7,7) and Level 4 TEST OVERRIDE.

Town Hall is the settlement overview/management center and current logical resource storage. It displays resources, population, and the citizen roster (section 31); it does not assign the first Builder. Before a completed Warehouse exists, resources are stored in Town Hall.

Town Hall upgrades are not simply instant purchases. They involve an associated quest chain. After the relevant quest progression is completed, the Town Hall itself is upgraded through construction.

Building-type unlocks through Town Hall Level 4 are approved in section 20. The current design is limited to Town Hall Levels 1 through 4; no Level 5+ content is defined.

**TBD**: quests, quest requirements, upgrade costs, construction times, and other unspecified level-specific effects.

## 22. Builder's Hut

**Approved**: reserved footprint **3 x 3 Tiles**; associated profession **Builder**. The first Hut is built manually by the **player**, before any Builder exists. This supersedes the earlier Town Hall assignment / Builder-builds-first-Hut draft.

**Implemented now / prototype balance**: the first Hut costs **20 Wood**, debited once from Town Hall storage on valid confirmed placement. It reserves its oriented footprint and entrance immediately, prevents overlaps, and appears as an incomplete construction frame. It applies no influence while incomplete. A selected site accepts player-provided active work through the prototype **hold E** adapter. **30 seconds total active work** completes it; release, deselection, or closing the panel pauses work without resetting progress. Completion applies universal +3-per-side influence exactly once and reconciles citizens/wildlife.

Construction has a shared Blueprint/Planned -> Under Construction -> Completed foundation. The current placement preview is the unpersisted blueprint stage; Town Hall starts Completed. Player work and future Builder NPC work use the same domain contribution/progress API. There is no passive construction timer.

Only a completed Hut offers worker assignment. Through that workplace, the player explicitly chooses an eligible unemployed adult, without automatic stat selection. The same citizen becomes Builder and gains that Hut's ID as workplace. Citizen ID, name, sex, age, all attributes, and base visual identity remain unchanged; an added work apron/tool belt distinguishes the profession. The citizen's workplace relationship is authoritative; the Hut and roster query it rather than duplicating worker state. The prototype exposes one worker choice per Hut; final worker limits remain TBD. Already pending Wood is returned before employment excludes subsequent ambient trips.

**Approved future design**: all normal subsequent buildings are constructed by assigned Builder NPCs: blueprint -> supplied resources -> Builder job -> entrance/access approach -> work -> completion -> influence once. Other building costs and work durations are not designed, so normal subsequent construction is not implemented. All catalog previews remain available; the immediate generic placement API remains a domain test fixture, not the playable construction flow.

After the Hut exists, construction progression depends on it. Its maximum level remains capped by current Town Hall level under the global Level 1-4 rule.

**TBD**: costs/work requirements for other buildings, NPC construction speeds, final worker limits/range, upgrades and level-specific capability, cancellation/refunds, physical player range/tools/inventory/material delivery.

## 23. House

**Approved**: reserved footprint **3 x 3 Tiles**; supports settlement population and family life.

**TBD**: citizen capacity, family capacity, upgrade benefits, and housing requirements.

## 24. Warehouse

**Approved**: reserved footprint **4 x 4 Tiles**; associated planned profession **Courier**; associated with settlement storage, logistics, and resource movement/distribution.

**Approved future storage rule**: after the first completed Warehouse, it becomes the primary settlement storage/logistics center. Inventory operations are independent of the storage building identity so this transition does not require rewriting resource transactions. Warehouse transfer/logistics and Courier behavior are not implemented.

Courier resource transportation/distribution must not depend on a constructed road network. Citizens navigate through available traversable space. **TBD**: capacity, Courier behavior, delivery priorities, storage rules, and upgrade effects; road dependency is not an open design option.

## 25. Tavern

**Approved**: reserved footprint **4 x 5 Tiles**; associated profession **Cook**; associated with food/meal-related settlement functionality.

**TBD**: meal production, recipes, hunger/food needs, ingredient usage, Tavern capacity, and social functionality.

## 26. Barracks

**Approved**: reserved footprint **3 x 5 Tiles**; associated profession category **Warrior**, with possible **Swordsman** and **Archer** specializations.

**TBD**: training, equipment, capacity, combat, and progression.

## 27. Guard Tower

**Approved**: reserved footprint **2 x 2 Tiles**.

**Planned functionality**: detect or warn about hostile threats, inform the settlement/player of danger, and allow Warriors to be dispatched toward threats.

**TBD**: detection radius, warnings, dispatch behavior, combat interaction, and tower progression. No automatic combat or staffing behavior is inferred.

## 28. Laboratory

**Approved**: reserved footprint **6 x 6 Tiles**; associated profession **Scientist**.

**Planned functionality**: research/progression systems. Detailed research mechanics are TBD. No technology tree is approved; do not invent one.

## 29. Mage Tower and Place of Power

**Approved design decisions**

- Associated profession: **Mage**.
- Mage Tower and Place of Power are **one building complex**, constructed together in the same location, with one reserved **5 x 5 Tile** footprint.
- Place of Power is a component/feature of the complex, not a separately placeable building. The player does not place it separately.
- During upgrades, the Mage Tower itself is the component whose visual representation changes through building levels.
- Do not invent separate Place of Power levels, footprint, placement rules, or construction progression.

**TBD**: magic system, Mage functionality, Place of Power functionality, and magical progression.

## 30. Intended early-game flow

**Approved current bootstrap; implemented in the standalone prototype**:

1. Exactly 20 x 20 claimed Tiles, a completed Town Hall, surrounding forest Wilderness, four unemployed adults, and 10 Wood in Town Hall storage.
2. Unemployed adults occasionally gather in authorized nearby Wilderness; wildlife flees approaching citizens. Successful returns deposit 2 Wood with a 90% per-trip success chance.
3. The player inspects resources/population and all citizens through Town Hall management.
4. At 20 Wood, the player places the first Builder's Hut site, paying 20 once. No influence applies yet.
5. The player provides 30 seconds of active construction work, with interruption/resume. Completion applies influence once.
6. Through the completed Hut, the player chooses an unemployed adult as Builder. Identity/attributes persist; workplace and profession clothing change.

**Approved future continuation**: normal subsequent construction belongs to assigned Builder NPCs; resource costs and work requirements still need design. Building types unlock cumulatively through Town Hall Levels 1-4 (section 20), with maximum building levels capped separately (section 6). Town Hall upgrades require quest-chain progression followed by construction. Upgrades do not repeat influence; demolition/destruction do not revoke claims.

Profile/menu routing is unchanged. SettlementPrototype is still a standalone scene that resets on leaving Play Mode. Town Hall Level 4 is a test override, not the final new-game starting level.

## 31. Resource and settlement management UI

**Implemented now**: compact always-visible Wood HUD; clicking Town Hall opens a separate management panel with Level 4 TEST OVERRIDE, actual claimed Tile count, total/employed/unemployed population, current Wood, Town Hall storage identity, and all four citizens exactly once. Each roster entry shows actual name, sex, age, profession, workplace, Strength, Dexterity, Intelligence, Endurance, and Charisma. Roster selection opens the same citizen information used by world selection. Profession/workplace updates immediately in both; there is no copied citizen model.

Town Hall provides high-level overview. Profession assignment belongs to the corresponding completed workplace, with only Builder's Hut -> Builder implemented now. Future workplace associations remain those in section 20; Tavern -> Cook, Warehouse -> Courier, and Barracks/military -> Warrior are future systems. Placement retains priority over world picking; all management panels participate in UI pointer blocking.

**Approved future design**: show quantities of relevant/unlocked resources without inspecting each producer, with extensibility beyond the current functional Wood resource.

**TBD**: final screen layout, icons, grouping/sorting, locked-resource visibility, tooltips, animations, and polished UI.

## 32. Long-term architectural intent

**Approved architectural constraints**

- Territory state must not depend on its rendering.
- Citizen identity must not depend on a particular character model; profession state must not depend on a clothing model.
- Building state must not depend on a building model.
- Production must not depend on UI; simulation state must not depend on a specific camera.
- Resource definitions should be data-driven where practical, and common production progression logic must not be duplicated independently for every building.
- Wilderness simulation must not require every decorative object to become a simulation entity.
- Replacing prototype visuals with full 3D assets should not require rewriting core simulation rules.
- Follow `AGENTS.md`: separate simulation and presentation; prefer plain C# domain logic where practical, using MonoBehaviour when Unity lifecycle/scene integration requires it.
- Use straightforward, maintainable systems and small playable vertical slices. Do not overengineer speculative systems or create large inheritance hierarchies for systems that do not yet exist.

Current prototype implementation details are documented in SettlementPrototype.md; no final storage format, 3D pathfinding architecture, streaming scheme, or speculative class hierarchy is prescribed.

## 33. TBD / Open Design Questions

Everything below remains unresolved. These questions do not authorize agents to choose answers.

### Production balance

| Resource | Unlock building level | Base Production Cycle Duration |
| --- | --- | --- |
| Wood | TBD | TBD |
| Stone | TBD | TBD |
| Metal | TBD | TBD |
| Fish | TBD | TBD |
| Meat | TBD | TBD |
| Wool | TBD | TBD |

Farm unlock levels and cycle durations are approved in section 8; they are not open balance values here.

### Buildings

- Construction costs/work requirements other than the first Hut (20 Wood, 30 seconds active player work), upgrade costs/times, and undefined level-specific benefits.
- Worker counts, building-count limits, and prerequisites between individual buildings remain TBD unless explicitly approved elsewhere. Do not invent them from the unlock table.
- Which buildings are upgradeable where applicability has not been specified.
- House citizen/family capacity and housing requirements; Warehouse and Barracks capacities.
- Farm additional benefits, visuals, and field presentation; Lumber Mill functionality; Mine progression and resource visuals; Fisherman's Hut progression.
- Builder construction speeds, worker limits, range, and level-specific capabilities.
- Courier behavior, delivery priorities, storage rules, and Warehouse upgrades.
- Tavern recipes, meal production, ingredient usage, food needs, capacity, and social functionality.
- Barracks training, equipment, progression, and combat integration.

### Town Hall

- Exact quest chains, quest requirements, upgrade requirements/costs, construction times, and other progression unlocks not explicitly approved elsewhere. Building-type unlock levels through Town Hall Level 4 are approved in section 20 and are not TBD. No Level 5+ content is defined.

### Citizens

- Exact characteristics/statistics, skill system and effects, needs system, happiness system, aging speed, and profession suitability.
- Work/home assignment details not otherwise defined.

### Population

- Relationships, partner selection, family formation rules, child generation rules, child aging rules, family limits, housing requirements, and population limits.
- Visitor frequency, visitor characteristics, recruitment rules/costs, reputation requirements, and recruitment UI.
- Death consequences, family/morale effects, profession replacement, graves/memorials, and inheritance.

### Combat / wildlife

- Predator spawning, predator behavior/AI, combat rules, Warrior behavior, and Guard Tower detection radius, warnings, dispatch rules, and combat interaction.
- Wildlife movement, population, and ecology.
- Domestic livestock counts/population, breeding, slaughter/shearing representation, and production animations.

### Advanced systems

- Research system, magic system, Mage functionality, Place of Power functionality, and magical progression.
- No technology tree or separate Place of Power progression is defined.

### UI

- Exact Resource UI presentation: position, icons, grouping, sorting, locked-resource visibility, tooltips, and animations.
- First-person/third-person controls and perspective-switching behavior.

### Additional genuine ambiguities requiring later decisions

- **Tile scale and world extent**: what physical size does a logical Tile represent in 3D, and what are the world's outer limits, if any?
- **Final boundary/navigation behavior**: the current prototype uses actual cardinal boundary depth and task replanning/recovery (section 2). Final 3D navigation and additional task policies remain TBD.
- **Influence geometry edge cases**: how are diagonal corners, world edges, disconnected claimed areas, or holes treated beyond the approved straight-boundary examples?
- **Starting Town Hall details**: its exact position within the predefined territory and its initial level remain TBD. It applies no initial completion expansion.
- **Demolition, destruction, and rebuilding details**: costs, refunds, ruins, rebuilding rules, and detailed destruction behavior remain TBD. Permanent claimed territory and no repeated upgrade influence are settled rules.
- **Placement presentation and special cases**: entrance/traversal representation and detailed visual orientation remain TBD. No extra general placement requirement is approved beyond claimed territory and non-overlapping footprints. Any future special-building requirements need explicit design; roads are not an unresolved prerequisite.
- **Player boundary enforcement**: how is the prohibition on physical movement outside claimed territory enforced without defining an unapproved movement mechanic?
- **Ambient gathering operation**: final balance, physical carrying animation, additional profession eligibility, and advanced scheduling remain TBD. Current unemployed-adult eligibility, 2 Wood / 90% yield, 10-Tile depth, and prototype cadence are specified in section 4. Assigned Builders are excluded.
- **Bootstrap beyond this slice**: current initial conditions and first-Hut recipe/work are specified in sections 22 and 30. Subsequent recipes, cancellation/refunds, and construction progression remain TBD.
- **Production operation**: concurrent cycles, selected products, player priorities, Farmer-selected work, divided worker time, or another schedule remain undecided. Worker count per building, additional workers from upgrades, efficiency/stat modifiers, work schedules, absence behavior, travel time, inputs, interruptions/partial progress, storage blocking, delivery/output destinations, upgrades during cycles, and offline/background production remain TBD. Appropriate assigned-profession workers are already required.
- **Population deadlock prevention**: how should recovery work after extreme population loss when family growth is impossible? Emergency visitors, replacement citizens, minimum protected population, recruitment guarantees, and game-over conditions are not approved. The exact fail-safe remains TBD.
- **Non-production levels**: what level behavior applies to each building where the global ceiling is known but building-specific functionality is not? Do not infer a resource output rule for Tavern meals without an approved definition.

### Contradiction and ambiguity review

No direct contradiction was identified among the explicit rules. Unspecified operational details above must remain open. In particular:

- Full-footprint placement inside existing territory and expansion on completion are compatible: the footprint is already claimed, while influence can claim surrounding Wilderness.
- Visually removing trees during expansion grants no resources; separate ambient citizen trips are the approved source of small early Wood income.
- Universal production defines output and level availability for an active process, with an appropriate assigned-profession worker required. Scheduling, worker counts/attendance details, and inputs remain undecided.
- The initial territory is exactly 20 x 20 Tiles. The already existing Town Hall causes no expansion at creation; subsequent initially completed buildings do, upgrades do not, and demolition/destruction never revoke claimed territory.
- Unemployed adults provide ambient bootstrap Wood. The player constructs the first Hut (20 Wood / 30 active seconds), then assigns Builder through the completed Hut. This supersedes the earlier Town Hall assignment and idle-Builder gathering draft.
- Non-overlapping reserved footprints and 90-degree rotation are approved. Influence overlap adds no bonus. Road connectivity is neither a placement nor an operational requirement.
