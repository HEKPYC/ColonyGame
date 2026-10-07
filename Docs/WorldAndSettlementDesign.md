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

**Approved primary player control**: **E = Interact / Action**, one universal/context-sensitive player intent rather than separate keys for every activity. Press triggers discrete interactions; hold provides continuous interaction where appropriate. The current prototype operates on the selected valid target: Town Hall management, completed building/workplace management, citizen information, or held manual construction work on an initial L1 Hut site. Existing mouse inspection remains available. Workplace interaction never automatically assigns a citizen; assignment requires an explicit choice. Help and placement input priority suppress world interaction. Future physical-player targeting, dialogue/citizen quests, storage, items, fishing and quest objects will extend this action; those systems are not implemented now. Physical range requirements remain deferred until a Player Controller exists.

**Planned future presentation**: a fully 3D settlement that the player can explore directly, potentially switching between first-person and third-person perspectives. Specific controls and switching behavior are TBD.

## 2. Settlement Territory and Wilderness

**Approved design decisions**

- The world is conceptually divided into claimed **Settlement Territory** and surrounding **Wilderness**.
- The player may physically move only inside claimed Settlement Territory.
- Idle citizens wander only inside claimed Settlement Territory. Specific future tasks/professions may authorize Wilderness travel; ordinary idle wandering does not grant that permission.
- Citizens on authorized Wilderness trips must never travel farther than **10 Tiles beyond the current settlement boundary**.
- Building footprints are obstacles for normal outdoor citizen movement, including their perimeter Tiles. Buildings have defined entrances/access points for citizen interaction. Citizens approach an adjacent exterior access Tile and use an explicit entrance transition rather than treating the footprint as walkable outdoor space. The exterior access Tile is separate from the reserved footprint and normally must be claimed and unoccupied. Logical residence entry/exit is implemented at night; rendered interiors remain future work.
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

**Claimed Settlement Territory is permanent under the current design.** Demolition or destruction of a building does not return its previously claimed Tiles to Wilderness and does not shrink territory. Future combat-related destruction follows this principle unless explicitly redesigned. Completed ordinary building demolition refunds floor(50%) of every actually invested resource (initial construction plus completed upgrades). Claims remain permanent. Ruins and combat destruction remain future work.

Building influence areas may overlap without bonuses; already claimed Tiles simply remain claimed. Reserved building footprints must not overlap (section 20).

Approved examples along an existing straight boundary:

| Placement | Influence width parallel to boundary | Newly claimed outward depth | Result |
| --- | --- | --- | --- |
| A 3 x 3 House directly against the boundary, with its entire footprint already claimed | 3 + 3 + 3 = 9 Tiles | 3 Tiles | 3 Tiles deep x 9 Tiles wide |
| A 2 x 2 Guard Tower with one claimed Tile between its footprint and the boundary | 2 + 3 + 3 = 8 Tiles | 3 minus the one already-claimed Tile = 2 Tiles | A protrusion extending 2 Tiles beyond the old boundary |

These examples add only previously unclaimed Tiles and do not turn irregular territory into a bounding rectangle. Upgrades do not repeat influence; demolition/destruction do not revoke territory; the starting Town Hall does not expand the predefined initial area.

## 4. Wilderness and ambient Wood gathering

**Approved design / implemented now**: unemployed adult citizens occasionally make authorized Wilderness trips for small supplementary Wood income; no Lumberjack is required. Assigned Builders do not take ordinary ambient trips. Profession and current task are separate: Idle/Wandering, GatheringWoodOutbound, GatheringWood, ReturningWood, and Recovery.

The visible loop is outbound travel, a gathering pause, one success roll, and a return to the Town Hall outdoor access area. Successful Wood is pending during return and deposited only on arrival. A failed trip returns normally with zero Wood. Ordinary wandering remains settlement-only. Recovery/safety takes priority, followed by returning pending Wood, mandatory assigned work, ambient gathering, and idle behavior. Assigned Builder jobs are implemented and take priority over ambient/idle work; night return-home schedules stop new work.

**Prototype balance**: start with 10 Wood; successful trip yields 3 Wood with 90% success; maximum Wilderness depth is 10 Tiles from the current actual irregular boundary. The implemented cardinal boundary distance and path eligibility are described in section 2. Destinations are reachable Wilderness cells, without requiring a rendered tree. Defaults: 3-second gathering pause, 18-32-second cooldown after return, staggered initial delays of 4/10/16/22 seconds, and at most two simultaneous trips. Current destination sampling prefers nearby reachable cells within depth 1-4, among the nearest 24 candidates. These configurable cadence choices are prototype tuning, not final balance.

Early Wilderness is primarily forest. Decorative trees are not persistent harvest nodes; trips do not chop, destroy, reserve, or regenerate them. Newly claimed vegetation disappears visually, but territory expansion awards no resources. Lumber Mill + assigned Lumberjack is the implemented scalable Wood source; ambient income should eventually be economically insignificant.

**TBD**: final gathering balance, physical carrying/animations, broader profession eligibility, and more advanced scheduling/interruption behavior. The old permission for the initial Builder to gather while idle is superseded by this milestone's unemployed-only rule.

## 5. Resources

**Approved resource vocabulary**

| Category | Resources |
| --- | --- |
| Materials | Wood, Stone, Iron, Wool |
| Food / agricultural resources | Beetroot, Wheat, Potato, Carrot, Fish, Meat |

**Planned future system**: resource definitions should be data-driven and extensible. These resources are not an exhaustive permanent list; additional types may be approved later.

**Implemented**: one authoritative inventory supports all ten listed resources; Metal is replaced by Iron. Start with 10 Wood and zero other resources. Town Hall shows actual amounts when obtainable from a completed appropriately leveled producer (Wood always), otherwise "Requirements are not met yet". Warehouse does not duplicate totals; physical hauling/logistics remains future work. Recipes, cycles and generated costs are in [SettlementPrototype.md](SettlementPrototype.md).

## 6. Global building levels and reserved footprints

**Approved / implemented**

- Town Hall and all normal building types support Levels 1 through 5.
- A normal building cannot exceed current Town Hall level. Unlock level and maximum building level are separate.
- TH5 adds no new building type; it permits L5 upgrades, production/Builder progression and Town Hall capacity 8.
- Reserve the maximum footprint initially; upgrades never enlarge it or repeat influence.
- Undefined effects remain TBD. Do not invent TH6+ content.

## 7. Universal production progression

**Implemented**: a completed producer requires one explicitly assigned adult of the corresponding profession at valid access: Farm/Farmer, Lumber Mill/Lumberjack, Mine/Miner, Fisherman's Hut/Fisher, Butcher/Butcher. No automatic workers.

Quantity per completed cycle = building level - resource unlock level + 1. Base cycle duration stays fixed. All unlocked products run independently/concurrently. Work occurs during Day after travel. Partial cycles persist overnight and during absence; output goes directly to the single inventory. Upgrades retain old capacity/output until completion. No inputs or physical delivery costs are invented.

| Product | Producer | Unlock level | Cycle seconds | L1 / L2 / L3 / L4 / L5 quantity |
| --- | --- | --- | --- | --- |
| Beetroot | Farm | 1 | 15 | 1 / 2 / 3 / 4 / 5 |
| Wheat | Farm | 2 | 30 | - / 1 / 2 / 3 / 4 |
| Potato | Farm | 3 | 45 | - / - / 1 / 2 / 3 |
| Carrot | Farm | 4 | 60 | - / - / - / 1 / 2 |
| Meat | Butcher | 1 | 30 | 1 / 2 / 3 / 4 / 5 |
| Wool | Butcher | 2 | 45 | - / 1 / 2 / 3 / 4 |
| Fish | Fisherman's Hut | 1 | 30 | 1 / 2 / 3 / 4 / 5 |
| Wood | Lumber Mill | 1 | 30 | 1 / 2 / 3 / 4 / 5 |
| Stone | Mine | 1 | 45 | 1 / 2 / 3 / 4 / 5 |
| Iron | Mine | 4 | 60 | - / - / - / 1 / 2 |

**Future / TBD**: inputs, hauling, storage blocking/capacity, extra workers, non-Builder stat efficiency, priorities and offline production.

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
| 5 | 5 / 15 seconds | 4 / 30 seconds | 3 / 45 seconds | 2 / 60 seconds |

All unlocked crops run concurrently under section 7.

Farm starts at 50 Wood and 6 minutes base work. Upgrade costs follow section 22/SettlementPrototype. Additional benefits and polished field visuals remain TBD.

## 9. Wood and Lumber Mill

**Approved**: reserved footprint **5 x 5 Tiles**; associated profession **Lumberjack**.

The Lumber Mill becomes the primary scalable Wood source. Small ambient citizen Wood gathering may continue after the mill exists, but should become economically insignificant compared with mill production in the mid-game and late-game.

Lumber Mill Wood production uses the universal production progression model.

**Implemented**: Wood unlocks at Lumber Mill L1, 30-second cycle, output 1..5. Other functionality remains TBD.

## 10. Mine

**Approved**: reserved footprint **4 x 4 Tiles**; associated profession **Miner**.

Stone and Iron must not initially appear as ordinary visible deposits around the starting settlement. Their availability and environmental representation are introduced through Mine progression. Mine production uses the universal model.

**Implemented**: Stone L1/45 seconds; Iron L4/60 seconds. L3->L4 omits the normally introduced 25 Iron; L4->L5 requires 50 Iron, retaining the virtual curve. Detailed mining visuals remain tunable/future.

## 11. Fisherman's Hut

**Approved**: reserved footprint **3 x 5 Tiles**; associated profession **Fisher**; produces Fish under the universal model.

The complex includes a small pond/lake **within the same reserved 3 x 5 footprint**. The pond occupies no extra territory. Fishing does not require individually persistent fish entities.

**Implemented**: Fish L1/30 seconds, output 1..5. Other effects remain TBD.

## 12. Butcher and livestock

**Approved**: reserved footprint **3 x 6 Tiles**; associated profession **Butcher**.

The working building and livestock enclosure/pen form one complex within the same reserved 3 x 6 footprint. The enclosure does not extend beyond it.

Domestic livestock includes **Cow, Pig, Chicken, and Sheep**. They live in the Butcher's enclosure and do not freely roam the world or Wilderness.

The Butcher produces **both Meat and Wool**, each under the universal production model. Wool is not assigned to a separately invented building or profession.

**Implemented**: Meat L1/30 seconds; Wool L2/45 seconds. L1->L2 omits 25 Wool, while L2->L3 costs 50 Wool, then 75 and 100. Livestock population/breeding, slaughter/shearing representation and polished animations remain TBD; no gore.

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
- At night peaceful wildlife presentation and active ticks suspend. Morning reconciles valid Wilderness before rendering.
- Schematic prototype geometry/colors are replaceable presentation choices, not permanent design rules.

**TBD**: final spawning/population balance, ecology, detailed movement/flee tuning and citizen detection, predator AI, and combat behavior. No hunting or resource yield from decorative wildlife is approved.

## 14. Citizen identity

**Approved design decisions**

Citizens are persistent individuals, not disposable generic units. They include adult men, adult women, and children. Each has a persistent underlying identity and base appearance.

Assigning or changing profession must preserve that same person and underlying appearance. Profession presentation changes clothing, tools, and equipment; it must not replace the citizen with a different person or completely different base character. This remains valid when full 3D models replace prototype visuals.

**Implemented residence access**: citizens walk to valid exterior access, enter logically and hide outdoors while sleeping. Morning exit uses currently valid access. Footprints remain obstacles; rendered interiors remain future work.
## 15. Citizen characteristics

**Planned future model capabilities**: name/identity, sex, age/life stage, family relationships, profession, skills/aptitudes, individual characteristics/statistics, current needs/state, work assignment, and home assignment.

Citizens have persistent identifiers, first names, sex, age, profession state, and base attributes. Approved base attributes are **Strength, Dexterity, Intelligence, Endurance, and Charisma**, represented as integers from **1 through 10**. Builder efficiency uses Strength/Dexterity/Endurance; candidate quality uses all five attributes. Combat, experience and additional suitability/social formulas remain undefined. Future relationships can reference persistent citizen identifiers without replacing those identities.

**TBD**: additional characteristics, skills, needs, happiness, aging rates, additional skill/attribute effects, and profession suitability rules. Do not invent additional statistics or formulas.

## 16. Families and population

**Planned future system; approved direction**

Citizens form a persistent settlement population. Adults may form families and have children. Children are persistent citizens who eventually mature into adults. Population should support growth, generational continuation, and recovery after deaths.

This serves believable settlement gameplay; unnecessary biological simulation is not required.

**TBD**: relationships, partner selection, family formation, child generation, aging duration, family limits and family-specific housing rules. Current residential capacity/recruitment limits are implemented separately.

## 17. Visitors and recruitment

**Implemented**: each completed Tavern independently rolls **10% once per completed game day**. Multiple Taverns increase arrival frequency and may attract separate visitors on the same day. Level affects quality/request tier, not base chance; no Tavern means no roll.

Each candidate has a temporary unique ID, stable adult identity (name, sex, age 18..45) and five independently drawn attributes 1..10. For each attribute draw uniform 1..10; with probability 0.15 * (TavernLevel - 1), use the maximum of that draw and a second independent draw. Higher tiers improve average quality without guaranteeing perfection.

Quality is mean attributes. Request budget = round away from zero of 25 * TavernLevel * clamp(0.75 + quality/20, 0.75, 1.25). Choose 1..3 distinct eligible resources and divide the budget into small bundles. Value weights: Wood/food 1, Wool/Stone 2, Iron 4. Integer rounding may slightly alter total value.

Requests use only actually obtainable resources from completed appropriately leveled producers, and only products whose producer Town Hall unlock and resource unlock are no higher than Tavern level. Wood is always eligible. Requests stay stable.

Candidates are not population/residents/workers before recruitment. Stay is **three full game days**, expiring at arrival day +3. Recruitment requires free housing and the complete bundle, pays atomically once, adds that same person to roster, assigns residence and removes candidate state. No automatic recruitment/replacement or departed/dismissed history.

Named-confirmed permanent dismissal removes the citizen from roster, home, workplace, gathering and visuals. Unfinished Builder jobs retain paid materials/progress and await a qualified replacement. Families/children/reputation and extreme-loss guarantees remain future/TBD.

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
| 2 | Guard Tower | 2 x 2 | Warrior assignment; dispatch/combat future |
| 3 | Warehouse | 4 x 4 | Courier (planned) |
| 3 | Lumber Mill | 5 x 5 | Lumberjack |
| 3 | Mine | 4 x 4 | Miner |
| 3 | Barracks | 3 x 5 | Warrior |
| 4 | Laboratory | 6 x 6 | Scientist |
| 4 | Mage Tower + Place of Power | Shared 5 x 5 site | Mage |

A building type becomes available when the Town Hall reaches its required level. **Unlocks are cumulative**: Level 1 provides the initial three building types; Level 2 retains those and adds its four types; Level 3 retains all previous types and adds its four types; Level 4 makes all currently designed build-menu types available. TH5 unlocks no new type; levels beyond 5 remain undesigned.

The Mage Tower and its Place of Power occupy the same overall **5 x 5 reserved building footprint/site**. The Place of Power belongs to the Mage Tower site and is not separately placeable. Only the Mage Tower itself visually changes as the building progresses/upgrades; the Place of Power remains part of that same site (section 29).

This is the single approved building-type unlock list. Building-level caps are defined separately in section 6 and do not replace unlock requirements. Costs/times are implemented in SettlementPrototype. This slice uses one explicit workplace worker; extra staffing, building limits/prerequisites and unspecified effects remain TBD. Town Hall quests are approved future design. Do not invent additional buildings or prerequisites.

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

**Implemented**: starts Level 1, 5 x 5 at (7,7) inside exact 20 x 20 claims, no starting influence. Management/storage/residential anchor; not normally placeable, protected from prototype demolition. Capacity 4/5/6/7/8 at L1..L5; all four starting adults live there.

| Upgrade | Explicit cost |
| --- | --- |
| L1->L2 | 150 Wood |
| L2->L3 | 300 Wood +100 Wool |
| L3->L4 | 450 Wood +250 Wool +150 Stone |
| L4->L5 | 600 Wood +350 Wool +250 Stone +100 Iron |

Prototype upgrades require resource payment only and complete immediately. No generic formula or Builder timer. TH5 is max and adds no type. Management shows inventory, next cost, missing amounts, availability and max state.

**Approved future**: quest chains/prerequisites followed by construction. Exact quests, future work durations and effects remain TBD; these are not current gates.

## 22. Builder's Hut

**Implemented**: 3 x 3, explicit Builder assignment. Player may directly build any number of L1 Huts for **25 Wood**, even with busy NPC Builders, holding **E** during Day for **300 base work seconds**. Release/deselection/Help pauses without resetting. Player cannot upgrade Huts or build/upgrade ordinary buildings.

| Target | Authoritative Hut cost |
| --- | --- |
| Build L1 | 25 Wood |
| L1->L2 | 50 Wood; no Wool |
| L2->L3 | 75 Wood +50 Wool +25 Stone |
| L3->L4 | 100 Wood +75 Wool +50 Stone; no Iron |
| L4->L5 | 150 Wood +100 Wool +75 Stone +50 Iron |

No special Wood curve. Existing components add 25 below 100, 50 at/above 100: 25->50->75->100->150->200.... Missing Wool/Stone/Iron enter at target L2/L3/L4 with 25. Deferred Wool (Hut/Butcher target L2) and Iron (Hut/Mine target L4) are omitted only at that target, retaining virtual later costs.

NPC Builders construct ordinary buildings and all upgrades, approach access and work during Day. One Builder/job and one job/Builder. Other upgrades require Hut at target level. A Builder may upgrade their own Hut by one level if TH permits. Pay atomically at start; completed costs accumulate as investment. Reserve initial footprints immediately; initial completion grants influence once, upgrades never repeat.

Required work = base seconds * target level (1 initially). Rate = (1 + .20 * max(0, hutLevel - currentBuildingLevel)) * (.90 + (Strength + Dexterity + Endurance)/150).
Initial construction uses current level 1. Attributes 5/5/5 give personal efficiency 1; 1/1/1 give .92; 10/10/10 give 1.1. House L1->L2: Hut L1 neutral Builder takes 600 active seconds; Hut L4 takes 600/1.6=375, plus travel/night pauses.

All recipes/generated costs/times are in [SettlementPrototype.md](SettlementPrototype.md). Immediate TryPlacePrototypeBuilding remains a domain fixture only. Physical player range/tools/carrying, extra staffing and construction cancellation UI remain TBD.

## 23. House

**Implemented**: 3 x 3, initial 25 Wood/300 base work seconds; capacity 2/3/4/5/6 at L1..L5. Explicit residence reassignment does not teleport. Capacity increases only after upgrade completes.

**Future / TBD**: family-specific behavior and additional benefits.

## 24. Warehouse

**Approved**: reserved footprint **4 x 4 Tiles**; associated planned profession **Courier**; associated with settlement storage, logistics, and resource movement/distribution.

**Approved future storage rule**: after the first completed Warehouse, it becomes the primary settlement storage/logistics center. Inventory operations are independent of the storage building identity so this transition does not require rewriting resource transactions. Warehouse transfer/logistics and Courier behavior are not implemented.

Courier resource transportation/distribution must not depend on a constructed road network. Citizens navigate through available traversable space. **TBD**: capacity, Courier behavior, delivery priorities, storage rules, and upgrade effects; road dependency is not an open design option.

## 25. Tavern

**Implemented**: 4 x 5, 100 Wood/480 base work seconds; residential capacity 3/4/5/6/7 at L1..L5 and recruitment in section 17. Housing contributes ordinary population capacity independently of waiting visitors.

**Future / TBD**: Cook functionality, meals, recipes, hunger, ingredients and social systems. No invented Tavern production.

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

**Implemented**

1. TH1, 10 Wood, four Town Hall residents, 400 claims and peaceful wildlife.
2. Unemployed adults return +3 Wood per successful 90% trip.
3. At 25 Wood place L1 Hut, hold E during Day for 300 work seconds, complete influence once, explicitly assign Builder.
4. Save 150 Wood, buy TH2, upgrade Builder's own Hut L2 for 50 Wood without Wool.
5. Build Butcher, assign worker, upgrade L2 without Wool and produce Wool.
6. Buy TH3. Build Mine L1 using L2 Builder, assign Miner and produce Stone **before** paying Stone for Hut L3. Upgrade Hut L3 and Mine through L3.
7. Buy TH4; upgrade Hut L4 without Iron, then Mine L4 without Iron and produce Iron.
8. Buy TH5, upgrade Hut L5 and remaining buildings; no new TH5 type.
9. Build homes/Taverns, recruit with goods/free housing, manage jobs/residences and confirmed demolition/dismissal.

This path avoids producer deadlocks without a special Wood curve. Night pauses work; progress persists, claims never shrink. Menu/profile routing is unchanged; runtime resets after Play Mode. Future Town Hall quests/construction are not current gates.

## 31. Resource and settlement management UI

**Implemented**: compact Wood/time HUD, debug 1x/2x/4x/8x/16x controls, scrollable management. Build list follows cumulative TH unlocks and shows recipes. Town Hall displays ten resource amounts/availability, next cost/missing amounts/max state, population/capacity, employment, homelessness, residences and live roster.

Completed workplaces provide explicit citizen assignment. Buildings show level, upgrade eligibility/cost/work, residents where applicable and confirmed demolition/refund. Tavern shows candidate identity/attributes/request/remaining days and affordable recruitment requiring housing. Citizen inspection offers named permanent-dismissal confirmation; Town Hall offers residence reassignment.

Universal E stays centralized: press opens completed building/citizen management, hold only initial L1 Hut work. Help/placement suppress world E, including closing-frame guards. Scrollable Help describes implemented systems without pausing simulation.

**TBD**: polished UI/art, sorting, icons/tooltips and layout refinements.

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

TH1..5, L1..5 recipes, producer exceptions, concurrent cycles, construction times, Builder gates/efficiency, explicit staffing, housing capacity, day/night, recruitment, dismissal and refunds are settled by this milestone.

**Approved future / still TBD**

- Town Hall quest chains and future construction durations; no invented TH6+ or new TH5 buildings.
- Extra workplace staff, building limits, extra prerequisites and unspecified effects.
- Physical player controller/range/tools/inventory, carrying, hauling, Warehouse capacities and Courier logistics; no gameplay roads.
- Skills, needs, happiness, aging, extra stat effects/suitability and final balance.
- Families/children, family housing, death/morale/graves/inheritance, extreme-loss safeguards. No guaranteed visitors/replacements.
- Cook meals/recipes/hunger/social functions; military equipment/training/combat/dispatch, predators.
- Livestock population/breeding, slaughter/shearing and polished work/level visuals.
- Research, technology tree, magic and Power site functions; no separately placeable Place of Power.
- Inputs, storage blocking, delivery/offline production, final schedules and advanced interruptions. Current partial cycles/daytime attendance are implemented.
- Construction cancellation UI, ruins/combat destruction and extra rebuilding effects. Completed-building refunds/permanent claims are settled; prototype Town Hall is protected.
- Final Tile/world scale, full 3D navigation, streaming, perspectives and polished UI.
- World-edge/diagonal/disconnected-claim influence edge cases beyond current union behavior, final 3D Town Hall placement, and physical player boundary enforcement remain future questions. Current prototype origin (7,7), TH1 start and player-inside-claims rule are settled.

Whole-footprint placement and completion influence are compatible. Influence union adds only genuinely unclaimed Tiles; upgrades repeat none; demolition removes occupancy without revoking claims. Decorative trees/expansion award no resources.


## 34. Day/night and ambience

**Implemented**: Day 420 seconds, Night 180, cycle 600; day starts 1. Speeds 1/2/4/8/16 scale simulation through <=.1-second movement steps split at boundaries, not UI/transforms. Day/phase/progress are exposed.

Night pauses production/Builder work and new gathering; pending Wood returns first. Residents walk to home exterior access, sleep inside logically, then exit valid access in morning. Homeless citizens remain outside near Town Hall; footprints stay obstacles.

Smooth world material/ground/lighting darkening leaves UI unchanged. Actual sleeping residences show warm windows and procedural per-resident moon phases; sparse unsynchronized fireflies use valid Wilderness. Peaceful wildlife sleeps off-screen and reconciles morning claims. Profession equipment/work motion/dust are replaceable presentation only.

## 35. Demolition and permanent dismissal

**Implemented**: confirmed completed ordinary-building demolition refunds floor(invested resource / 2), counting initial construction and completed upgrades. In-progress upgrades must finish first. Occupancy is removed, territory stays claimed; workers lose jobs, residents reassign or become homeless, Tavern candidates leave. Unfinished jobs retain paid materials/progress and await qualified replacement Builders.

Named-confirmed dismissal removes citizens from roster, workplace/residence/gathering and visual state, frees capacity and never automatically replaces or retains history. Starting Town Hall is protected as the prototype storage/management anchor; future anchor destruction/replacement remains TBD.
