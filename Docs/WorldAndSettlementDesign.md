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

**Approved primary player control**: **E = Interact / Action**, one universal/context-sensitive player intent rather than separate keys for every activity. Press triggers discrete interactions; hold provides continuous interaction where appropriate. The current prototype operates on the selected valid target: Town Hall management, completed building/workplace management, citizen information, or held manual construction work on an initial L1 Hut site. Existing mouse inspection remains available. Workplace interaction opens management without hiring by itself; completed construction/recruitment/adulthood may automatically fill an unlocked vacancy. Manual hiring remains explicit. Help and placement input priority suppress world interaction. Future physical-player targeting, dialogue/citizen quests, storage, items, fishing and quest objects will extend this action; those systems are not implemented now. Physical range requirements remain deferred until a Player Controller exists.

**Planned future presentation**: a fully 3D settlement that the player can explore directly, potentially switching between first-person and third-person perspectives. Specific controls and switching behavior are TBD.

## 2. Settlement Territory and Wilderness

**Approved design decisions**

- The world is conceptually divided into claimed **Settlement Territory** and surrounding **Wilderness**.
- The player may physically move only inside claimed Settlement Territory.
- Idle citizens wander only inside claimed Settlement Territory. Specific future tasks/professions may authorize Wilderness travel; ordinary idle wandering does not grant that permission.
- Citizens on authorized Wilderness trips must never travel farther than **10 Tiles beyond the current settlement boundary**.
- Building interiors and all occupied tile centers are obstacles for normal outdoor movement. The only exception is a narrow boundary lane represented by half-tile nodes, including shared edges between touching footprints and their corners. Citizens approach oriented entrance boundary points and transition inside logically; no occupied tile becomes generally walkable. Reserved footprints and the 1x1 tile grid are unchanged. Logical residence entry/exit is implemented at night; rendered interiors remain future work.
- A building may be placed only when its **entire reserved footprint** is already inside existing claimed Settlement Territory.
- Placing a building partly or fully outside the territory to claim new land is prohibited.

Citizen Wilderness distance is conceptually measured relative to the **nearest actual claimed Settlement Territory boundary**, respecting its irregular shape. It is not measured from the original 20 x 20 area, Town Hall, settlement center, or rectangular bounding box.

Implemented prototype metric: multi-source cardinal breadth-first depth from all actual claimed boundary cells. The first exterior Wilderness cell is depth 1; only cells through depth 10 are authorized. This respects protrusions, holes, and corners without claiming a bounding rectangle. Route search also requires structural body clearance, including the narrow perimeter lanes and a reachable return access area. The cached area refreshes after claims change; blocked task routes yield to recovery or replan a return while preserving pending Wood. This is the current prototype metric, not a final 3D pathfinding commitment.

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

**Approved design / implemented now**: eligible adult citizens occasionally make authorized Wilderness trips for small supplementary Wood income; no Lumberjack is required. Employed adults may gather only during the last 120 daylight seconds without active construction; unemployed adults retain ordinary cadence. Profession and current task are separate: Idle/Wandering, GatheringWoodOutbound, GatheringWood, ReturningWood, and Recovery.

The visible loop is outbound travel, a gathering pause, one success roll, and a return to the Town Hall outdoor access area. Successful Wood is pending during return and deposited only on arrival. A failed trip returns normally with zero Wood. Ordinary wandering remains settlement-only. Recovery/safety takes priority, followed by returning pending Wood, mandatory assigned work, ambient gathering, and idle behavior. Assigned Builder jobs are implemented and take priority over ambient/idle work; night return-home schedules stop new work.

**Prototype balance**: start with zero Wood; successful trip yields 3 Wood with 90% success; maximum Wilderness depth is 10 Tiles from the current actual irregular boundary. The implemented cardinal boundary distance and path eligibility are described in section 2. Destinations are reachable Wilderness cells, without requiring a rendered tree. Defaults: 3-second gathering pause, 18-32-second cooldown after return, staggered initial delays of 4/10/16/22 seconds, and at most two simultaneous trips. Current destination sampling prefers nearby reachable cells within depth 1-4, among the nearest 24 candidates. These configurable cadence choices are prototype tuning, not final balance.

Early Wilderness is primarily forest. Decorative trees are not persistent harvest nodes; trips do not chop, destroy, reserve, or regenerate them. Newly claimed vegetation disappears visually, but territory expansion awards no resources. Lumber Mill + assigned Lumberjack is the implemented scalable Wood source; ambient income should eventually be economically insignificant.

**TBD**: final gathering balance, physical carrying/animations, more advanced scheduling/interruption behavior. Children, sleeping and incapacitated citizens never gather; actual jobs take priority.

## 5. Resources

**Approved resource vocabulary**

| Category | Resources |
| --- | --- |
| Materials | Wood, Stone, Iron, Wool |
| Food / agricultural resources | Beetroot, Wheat, Potato, Carrot, Fish, Meat |

**Planned future system**: resource definitions should be data-driven and extensible. These resources are not an exhaustive permanent list; additional types may be approved later.

**Implemented**: one authoritative inventory supports all ten listed resources; Metal is replaced by Iron. Start with zero of every resource. Town Hall always shows stored amounts and capacities; missing producer/unlock requirements are additionally marked "Requirements are not met yet". Warehouse does not duplicate totals; physical hauling/logistics remains future work. Recipes, cycles and generated costs are in [SettlementPrototype.md](SettlementPrototype.md).

## 6. Global building levels and reserved footprints

**Approved / implemented**

- Town Hall and all normal building types support Levels 1 through 5.
- A normal building cannot exceed current Town Hall level. Unlock level and maximum building level are separate.
- TH5 adds no new building type; it permits L5 upgrades, production/Builder progression and Town Hall capacity 8.
- Reserve the maximum footprint initially; upgrades never enlarge it or repeat influence.
- Undefined effects remain TBD. Do not invent TH6+ content.

## 7. Universal production progression

**Implemented**: a completed producer requires one assigned living adult of the corresponding profession at valid access: Farm/Farmer, Lumber Mill/Lumberjack, Mine/Miner, Fisherman's Hut/Fisher, Butcher/Butcher. Employment follows the automatic/manual rules below.

Quantity per completed cycle = building level - resource unlock level + 1. Non-Farm cycle durations stay fixed; Farm intervals are normalized as in section 8. All unlocked products run independently/concurrently. Work occurs during Day after travel. Partial cycles persist overnight and during absence; output goes directly to the single inventory. Upgrades retain old capacity/output until completion. No inputs or physical delivery costs are invented.

| Product | Producer | Unlock level | Cycle seconds | L1 / L2 / L3 / L4 / L5 quantity |
| --- | --- | --- | --- | --- |
| Beetroot | Farm | 1 | section 8 | 1 / 2 / 3 / 4 / 5 |
| Wheat | Farm | 2 | section 8 | - / 1 / 2 / 3 / 4 |
| Potato | Farm | 3 | section 8 | - / - / 1 / 2 / 3 |
| Carrot | Farm | 4 | section 8 | - / - / - / 1 / 2 |
| Meat | Butcher | 1 | 30 | 1 / 2 / 3 / 4 / 5 |
| Wool | Butcher | 2 | 10 | - / 1 / 2 / 3 / 4 |
| Fish | Fisherman's Hut | 1 | 30 | 1 / 2 / 3 / 4 / 5 |
| Wood | Lumber Mill | 1 | 10 | 1 / 2 / 3 / 4 / 5 |
| Stone | Mine | 1 | 20 | 1 / 2 / 3 / 4 / 5 |
| Iron | Mine | 4 | 40 | - / - / - / 1 / 2 |

**Future / TBD**: inputs, hauling, extra workers, non-Builder stat efficiency, priorities and offline production.

## 8. Farm

**Approved**: reserved footprint **3 x 4 Tiles**; associated profession **Farmer**; products Beetroot, Wheat, Potato, and Carrot.

| Level | Beetroot quantity / seconds | Wheat | Potato | Carrot | Target nutrition / day |
| --- | --- | --- | --- | --- | --- |
| 1 | 1 / 54.00 | - | - | - | 5 |
| 2 | 2 / 67.50 | 1 / 135.00 | - | - | 10 |
| 3 | 3 / 81.00 | 2 / 162.00 | 1 / 243.00 | - | 15 |
| 4 | 4 / 92.81 | 3 / 185.62 | 2 / 278.44 | 1 / 371.25 | 20 |
| 5 | 5 / 99.90 | 4 / 199.80 | 3 / 299.70 | 2 / 399.60 | 25 |

All unlocked crops remain concurrent. Intervals use 270 productive seconds/day and normalize total nutrition to 5/10/15/20/25 adult meals at Farm L1..5. Quantity and crop unlocks are unchanged; normal travel/breaks produce small daily fluctuations. No Farmer means no output.

Farm starts at 50 Wood and 6 minutes base work. Upgrade costs follow section 22/SettlementPrototype. Additional benefits and polished field visuals remain TBD.

## 9. Wood and Lumber Mill

**Approved**: reserved footprint **5 x 5 Tiles**; associated profession **Lumberjack**.

The Lumber Mill becomes the primary scalable Wood source. Small ambient citizen Wood gathering may continue after the mill exists, but should become economically insignificant compared with mill production in the mid-game and late-game.

Lumber Mill Wood production uses the universal production progression model.

**Implemented**: Wood unlocks at Lumber Mill L1, 10-second cycle, output 1..5. Other functionality remains TBD.

## 10. Mine

**Approved**: reserved footprint **4 x 4 Tiles**; associated profession **Miner**.

Stone and Iron must not initially appear as ordinary visible deposits around the starting settlement. Their availability and environmental representation are introduced through Mine progression. Mine production uses the universal model.

**Implemented**: Stone L1/20 seconds; Iron L4/40 seconds. L3->L4 omits the normally introduced 25 Iron; L4->L5 requires 50 Iron, retaining the virtual curve. Detailed mining visuals remain tunable/future.

## 11. Fisherman's Hut

**Approved**: reserved footprint **3 x 5 Tiles**; associated profession **Fisher**; produces Fish under the universal model.

The complex includes a small pond/lake **within the same reserved 3 x 5 footprint**. The pond occupies no extra territory. Fishing does not require individually persistent fish entities.

**Implemented**: Fish L1/30 seconds, output 1..5. Other effects remain TBD.

## 12. Butcher and livestock

**Approved**: reserved footprint **3 x 6 Tiles**; associated profession **Butcher**.

The working building and livestock enclosure/pen form one complex within the same reserved 3 x 6 footprint. The enclosure does not extend beyond it.

Domestic livestock includes **Cow, Pig, Chicken, and Sheep**. They live in the Butcher's enclosure and do not freely roam the world or Wilderness.

The Butcher produces **both Meat and Wool**, each under the universal production model. Wool is not assigned to a separately invented building or profession.

**Implemented**: Meat L1/30 seconds; Wool L2/10 seconds. L1->L2 omits 25 Wool, while L2->L3 costs 50 Wool, then 75 and 100. Livestock population/breeding, slaughter/shearing representation and polished animations remain TBD; no gore.

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

**Implemented residence assignments**: each citizen retains a specific completed residence ID. Initial citizens live in Town Hall. For citizens without an explicit override, reconciliation automatically moves residents only into strictly preferred available accommodation: Houses, then Taverns, then Town Hall. Houses fill from Town Hall/Taverns on completion or capacity increase; homeless citizens take available slots. With four citizens, TH1 and one House L1 have House 2/2, Hall 2/4, total 4/6. Assignments remain stable between nights and never exceed individual capacity. Residence loss reassigns displaced residents; excess remains homeless. Saving/loading settlements is not implemented yet.

**Implemented residence access**: citizens follow the outdoor graph to entrance boundary access, enter logically and hide outdoors while sleeping. Morning exits preserve a valid boundary position. Structural interiors remain obstacles; rendered interiors remain future work.
## 15. Citizen characteristics

**Planned future model capabilities**: name/identity, sex, age/life stage, family relationships, profession, skills/aptitudes, individual characteristics/statistics, current needs/state, work assignment, and home assignment.

Citizens have persistent identifiers, first names, sex, age, profession state, and base attributes. Approved base attributes are **Strength, Dexterity, Intelligence, Endurance, and Charisma**, represented as integers from **1 through 10**. Builder efficiency uses Strength/Dexterity/Endurance; candidate quality uses all five attributes. Combat, experience and additional suitability/social formulas remain undefined. Future relationships can reference persistent citizen identifiers without replacing those identities.

**Implemented**: individual satiety, hungry days, health, parent/partner links and child growth under section 16. **TBD**: additional characteristics, skills, needs, happiness, adult aging rates, additional skill/attribute effects, and profession suitability rules. Do not invent additional statistics or formulas.

## 16. Families and population

Nutrition per unit: Beetroot/Wheat **1**, Potato/Carrot **1.5**, Fish **2**, Meat **2.5**. Adults require **1 nutrition per completed game day**, children **0.5**. There is no physical Food resource. At each dawn (after a full 600-second cycle), allocation consumes the smallest nutrition units first, then enum order, only as many as necessary. Indivisible surplus persists as fractional nutrition credit. Shortage is shared in the same proportional ratio across every resident; individual satiety is that ratio. Non-food materials are never consumed. Repeating a processed day is idempotent. Newborns first receive allocation the next dawn.

Any insufficient full day increments the individual's consecutive hungry-day counter. Five such days cause no damage; from day six, daily damage is **20 x unfulfilled fraction**. With no food, death occurs after ten full hungry days. Full feeding resets the counter, stops damage and heals **10 health/day**, up to 100. Health at or below 25 prevents employment activity and gathering. Death removes the live person, partner link, housing, workplace, gathering, routines and active Builder reference; funded jobs keep their progress/materials. Death bypasses voluntary dismissal protections.

Eligible active adults aged **18..45**, with health above 25, residence and no partnership form automatic male/female pairs once per dawn, preferring a shared home then stable ID order. Ancestry prevents parent/child and shared-ancestor pairs. A couple can produce a child only with both parents fully fed, healthy, housed together (automatic family relocation is allowed when it respects explicit residence preferences), one free slot, and **three days of remaining nutrition for the prospective population**. Each eligible couple independently rolls **20% per day** after a **10-day cooldown**, initially measured from partnership formation. Tavern arrivals use a separate random stream.

Children have unique IDs/names/sex, persistent parent IDs/ancestry, and each attribute is rounded parent mean plus an independently drawn -1/0/+1, clamped 1..10. Age advances one year per completed game day until 18: newborn childhood lasts **18 game days**. Adult aging remains future work. Children wander/play within claims, sleep at home, never gather Wilderness resources or take a profession/job. Families are kept together when a preferred residence has enough space; otherwise they retain their existing valid housing. Birth/adulthood/death messages appear in Hall management. No historical database or emergency population replacement is added.

Voluntary dismissal requires an adult, named confirmation, and **more than four living citizens**. Children count toward that floor but cannot be dismissed individually. At five, exactly one adult can leave before the floor applies. Recruitment remains explicit and requires its complete bundle, free housing and three days of food after payment for the enlarged population.

### Shared per-resource storage and approved refund exception

One authoritative inventory remains. Per-resource Town Hall capacity at L1..5 is **200/350/500/750/1000**; each completed Warehouse adds **250 x its level**, additive across Warehouses. Pending sites add no capacity. Upgrades retain the old level's capacity until completion. Physical stacks, Courier hauling and production inputs remain future work.

Production and ambient gathering deposit only the available room for their particular resource. Amount at or above capacity blocks new deposits of that resource; others continue normally. Spending remains allowed. Once below capacity, normal deposits resume and clip at the limit. Capacity loss never discards existing stock. Hall management displays amount/capacity; Warehouse displays its contribution; successful feedback shows the actual stored resource/quantity, while rejected output reports storage full.

**Approved exception:** cancelling unfinished construction returns **100% of committed materials**; completed demolition returns **floor(50% of cumulative investment) per resource**. Both bypass storage limits and may create/increase overflow. Refunds are immediate, atomic and idempotent, never delayed or clipped, and never count as production/gathering. Unfunded cancellation returns nothing. Duplicate cancellation/demolition cannot refund twice. Integer-range failure rejects the entire transaction without partial changes; there is no capacity-based refund failure.

Every Hall upgrade bundle fits the previous Hall's base capacity, including TH4->5's 600 Wood within 750. Prices and deferred Wool/Iron exceptions are unchanged.

## 17. Visitors and recruitment

**Implemented**: each completed Tavern independently rolls **10% once per completed game day**. Multiple Taverns increase arrival frequency and may attract separate visitors on the same day. Level affects quality/request tier, not base chance; no Tavern means no roll.

Each candidate has a temporary unique ID, stable adult identity (name, sex, age 18..45) and five independently drawn attributes 1..10. For each attribute draw uniform 1..10; with probability 0.15 * (TavernLevel - 1), use the maximum of that draw and a second independent draw. Higher tiers improve average quality without guaranteeing perfection.

Quality is mean attributes. Request budget = round away from zero of 25 * TavernLevel * clamp(0.75 + quality/20, 0.75, 1.25). Choose 1..3 distinct eligible resources and divide the budget into small bundles. Value weights: Wood/food 1, Wool/Stone 2, Iron 4. Integer rounding may slightly alter total value.

Requests use only actually obtainable resources from completed appropriately leveled producers, and only products whose producer Town Hall unlock and resource unlock are no higher than Tavern level. Wood is always eligible. Requests stay stable.

Candidates are not population/residents/workers before recruitment. Stay is **three full game days**, expiring at arrival day +3. Recruitment requires free housing, the complete bundle and three days of remaining food after payment for the enlarged population, pays atomically once, adds that same person to roster, assigns residence and removes candidate state. No automatic recruitment/replacement or departed/dismissed history.

Named-confirmed permanent dismissal removes the citizen from roster, home, workplace, gathering and visuals. Unfinished Builder jobs retain paid materials/progress and await a qualified replacement. Voluntary dismissal is adult-only and cannot reduce living population below four. Reputation and extreme-loss guarantees remain future/TBD.

## 18. Citizen death and population continuity

**Implemented starvation; approved future danger constraints**

Citizens may eventually die from Wolves, Bears, or future hostile dangers. Because individuals persist, death should matter. It must not automatically force an unrecoverable population decline.

Recovery can eventually come through families having children, children becoming adults, and recruitment of outside visitors. These are intended recovery mechanisms, not defined automatic replacement behavior or guarantees with invented timing.

**Population deadlock prevention remains explicitly TBD.** Visitor/recruitment systems may eventually provide recovery when family-based growth is impossible, but no extreme-loss fail-safe is approved. Do not define guaranteed emergency visitors, automatic replacement citizens, death-protected population, recruitment guarantees, or game-over conditions. The voluntary dismissal floor of four is implemented; deaths remain unrestricted.

**TBD**: additional death consequences, morale effects, graves/memorials, material inheritance, and exact recovery behavior when no suitable adults or citizens remain.

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

This is the single approved building-type unlock list. Building-level caps are defined separately in section 6 and do not replace unlock requirements. Costs/times are implemented in SettlementPrototype. This slice uses one automatically or manually assigned workplace worker; extra staffing, building limits/prerequisites and unspecified effects remain TBD. Town Hall quests are approved future design. Do not invent additional buildings or prerequisites.

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

The approved prototype uses automatic thin golden pedestrian boundary paths backed by a half-tile navigation graph. These are not constructed Road Tiles or a separate economy system: touching building footprints automatically share the same edge path, without tile reservations, costs or movement bonuses.

## 21. Town Hall

**Implemented**: starts Level 1, 5 x 5 at (7,7) inside exact 20 x 20 claims, no starting influence. Management/storage/residential anchor; not normally placeable, protected from prototype demolition. Capacity 4/5/6/7/8 at L1..L5; all four starting adults live there.

| Upgrade | Explicit cost |
| --- | --- |
| L1->L2 | 150 Wood |
| L2->L3 | 300 Wood +100 Wool |
| L3->L4 | 450 Wood +250 Wool +150 Stone |
| L4->L5 | 600 Wood +350 Wool +250 Stone +100 Iron |

Town Hall upgrades require an explicit player request, one atomic payment, and an assigned qualified NPC Builder who travels to access and works during Day. The approved base duration is 600 seconds; required work is 600 * target level, adjusted by the existing Builder efficiency formula. A Hut at the current Town Hall level qualifies (L1 Hut upgrades TH1->2); the Hall level changes only at 100% completion, enabling the next Hut upgrade without a circular dependency. Resource accumulation never starts an upgrade. Duplicate requests neither create jobs nor charge again. TH5 is max and adds no type. Management shows materials, Builder, progress/status, missing amounts and max state.

**Approved future**: quest chains/prerequisites followed by construction. Exact quests and unspecified additional effects remain TBD; quest prerequisites are not current gates.

## 22. Builder's Hut

**Implemented**: 3 x 3, automatic or manual Builder employment. Builder job assignment remains explicit. Player may directly build any number of L1 Huts for **25 Wood**, even with busy NPC Builders, holding **E** during Day or Night for **300 base work seconds**. Release/deselection/Help pauses without resetting. Player cannot upgrade Huts or build/upgrade ordinary buildings.

| Target | Authoritative Hut cost |
| --- | --- |
| Build L1 | 25 Wood |
| L1->L2 | 50 Wood; no Wool |
| L2->L3 | 75 Wood +50 Wool +25 Stone |
| L3->L4 | 100 Wood +75 Wool +50 Stone; no Iron |
| L4->L5 | 150 Wood +100 Wool +75 Stone +50 Iron |

No special Wood curve. Existing components add 25 below 100, 50 at/above 100: 25->50->75->100->150->200.... Missing Wool/Stone/Iron enter at target L2/L3/L4 with 25. Deferred Wool (Hut/Butcher target L2) and Iron (Hut/Mine target L4) are omitted only at that target, retaining virtual later costs.

NPC Builders construct ordinary buildings and all upgrades, approach access and work during Day. One Builder/job and one job/Builder. Other upgrades require Hut at target level. A Builder may upgrade their own Hut by one level if TH permits. Placement creates an L1 site priced only by building type, never Town Hall level. Placement never funds or assigns. Explicit Choose Builder / Start validates stock and a qualified worker, pays once and begins. Pause/Reassign/Resume preserve progress/materials; idle Builders and subsequent income never auto-start sites. Initial L1 Huts use explicit manual Start/funding. Management shows required/available materials and Awaiting Resources, Awaiting Builder, Builder Travelling, Under Construction, Paused At Night or Completed. Paid materials are committed once; completed costs accumulate as investment. Reserve initial footprints immediately; initial completion grants influence once, upgrades never repeat.

Required work = base seconds * target level (1 initially). Rate = (1 + .20 * max(0, hutLevel - currentBuildingLevel)) * (.90 + (Strength + Dexterity + Endurance)/150).
Initial construction uses current level 1. Attributes 5/5/5 give personal efficiency 1; 1/1/1 give .92; 10/10/10 give 1.1. House L1->L2: Hut L1 neutral Builder takes 600 active seconds; Hut L4 takes 600/1.6=375, plus travel/night pauses.

All recipes/generated costs/times are in [SettlementPrototype.md](SettlementPrototype.md). Immediate TryPlacePrototypeBuilding remains a domain fixture only. Physical player range/tools/carrying, extra staffing remain TBD. Unfinished sites can be cancelled through confirmed management without a Builder: committed materials refund in full once, the footprint and job are released, and no influence or completion benefit is granted. Claimed territory remains permanent.

## 23. House

**Implemented**: 3 x 3, initial 25 Wood/300 base work seconds; capacity 2/3/4/5/6 at L1..L5. Explicit residence reassignment does not teleport. Capacity increases only after upgrade completes.

**Implemented**: family residence grouping and birth capacity gates as in section 16. Additional benefits remain TBD.

## 24. Warehouse

**Approved**: reserved footprint **4 x 4 Tiles**; associated planned profession **Courier**; associated with settlement storage, logistics, and resource movement/distribution.

**Approved future storage rule**: after the first completed Warehouse, it becomes the primary settlement storage/logistics center. Inventory operations are independent of the storage building identity so this transition does not require rewriting resource transactions. Warehouse transfer/logistics and Courier behavior are not implemented.

Courier resource transportation/distribution must not depend on a constructed road network. Citizens navigate through available traversable space. **Implemented**: each completed Warehouse adds 250 capacity per level per resource under section 16. **TBD**: Courier behavior, delivery priorities, physical storage logistics and unspecified upgrade effects; road dependency is not an open design option.

## 25. Tavern

**Implemented**: 4 x 5, 100 Wood/480 base work seconds; residential capacity 3/4/5/6/7 at L1..L5 and recruitment in section 17. Housing contributes ordinary population capacity independently of waiting visitors.

**Future / TBD**: Cook functionality, meals, recipes, hunger, ingredients and social systems. No invented Tavern production.

## 26. Barracks

**Approved**: reserved footprint **3 x 5 Tiles**; associated profession category **Warrior**, with possible **Swordsman** and **Archer** specializations.

**TBD**: training, equipment, capacity, combat, and progression.

## 27. Guard Tower

**Approved**: reserved footprint **2 x 2 Tiles**.

**Planned functionality**: detect or warn about hostile threats, inform the settlement/player of danger, and allow Warriors to be dispatched toward threats.

**TBD**: detection radius, warnings, dispatch behavior, combat interaction, and tower progression. Combat remains future work; employment follows the common vacancy rules.

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

1. TH1, zero resources, four Town Hall residents, 400 claims and peaceful wildlife.
2. Unemployed adults return +3 Wood per successful 90% trip.
3. At 25 Wood place L1 Hut without paying, explicitly Start manual construction, hold E during Day or Night for 300 work seconds, complete influence once, confirm automatic Builder employment (or manually fill a deliberately vacated slot).
4. Establish a staffed Farm for daily food, then save 150 Wood, prepare TH2 and choose Builder / Start, then finish with the L1 Builder, upgrade Builder's own Hut L2 for 50 Wood without Wool.
5. Build Butcher, assign worker, upgrade L2 without Wool and produce Wool.
6. Request and finish TH3 with the L2 Builder. Build Mine L1 using L2 Builder, assign Miner and produce Stone **before** paying Stone for Hut L3. Upgrade Hut L3 and Mine through L3.
7. Request and finish TH4 with the L3 Builder; upgrade Hut L4 without Iron, then Mine L4 without Iron and produce Iron.
8. Request and finish TH5 with the L4 Builder, upgrade Hut L5 and remaining buildings; no new TH5 type.
9. Build homes/Taverns, recruit with goods/free housing/three days of food, manage jobs/residences and confirmed demolition/dismissal.

This path avoids producer deadlocks without a special Wood curve. Night pauses NPC work; player initial Hut work continues; progress persists, claims never shrink. Menu/profile routing is unchanged; runtime resets after Play Mode. Future Town Hall quests are not current gates; timed NPC construction is required now.

## 31. Resource and settlement management UI

**Implemented**: compact Wood/time HUD, debug 1x/2x/4x/8x/16x controls, scrollable management. Build list follows cumulative TH unlocks and shows recipes. Town Hall displays ten resource amounts/availability, next cost/missing amounts/max state, population/capacity, employment, homelessness, residences and live roster.

Completed workplaces provide predictable automatic staffing and explicit manual citizen assignment. Buildings show level, upgrade eligibility/cost/work, residents where applicable and confirmed demolition/refund. Tavern shows candidate identity/attributes/request/remaining days and affordable recruitment requiring housing. Citizen inspection offers employment unassignment, capacity-gated persistent relocation to Hall and named permanent-dismissal confirmation; Town Hall names open the same panel.

Universal E stays centralized: press opens building/site/citizen management, hold only initial L1 Hut work. Help/placement suppress world E, including closing-frame guards. Scrollable Help describes implemented systems without pausing simulation.

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

TH1..5, L1..5 recipes, producer exceptions, concurrent cycles, construction times, Builder gates/efficiency, automatic/manual staffing, housing capacity, day/night, recruitment, dismissal and refunds are settled by this milestone.

**Approved future / still TBD**

- Town Hall quest chains and unspecified upgrade effects; no invented TH6+ or new TH5 buildings.
- Extra workplace staff, building limits, extra prerequisites and unspecified effects.
- Physical player controller/range/tools/inventory, carrying, hauling and Courier logistics; no gameplay roads.
- Skills, additional needs/happiness, adult aging, extra stat effects/suitability and final balance.
- Further family behavior, morale/graves/material inheritance, extreme-loss safeguards. No guaranteed visitors/replacements.
- Cook meals/recipes/extra social functions; military equipment/training/combat/dispatch, predators.
- Livestock population/breeding, slaughter/shearing and polished work/level visuals.
- Research, technology tree, magic and Power site functions; no separately placeable Place of Power.
- Inputs, delivery/offline production, final schedules and advanced interruptions. Current partial cycles/daytime attendance are implemented.
- Ruins/combat destruction and extra rebuilding effects. Completed-building refunds/permanent claims are settled; prototype Town Hall is protected.
- Final Tile/world scale, full 3D navigation, streaming, perspectives and polished UI.
- World-edge/diagonal/disconnected-claim influence edge cases beyond current union behavior, final 3D Town Hall placement, and physical player boundary enforcement remain future questions. Current prototype origin (7,7), TH1 start and player-inside-claims rule are settled.

Whole-footprint placement and completion influence are compatible. Influence union adds only genuinely unclaimed Tiles; upgrades repeat none; demolition removes occupancy without revoking claims. Decorative trees/expansion award no resources.


## 34. Day/night and ambience

**Implemented**: Day 420 seconds, Night 180, cycle 600; day starts 1. Speeds 1/2/4/8/16 scale simulation through <=.1-second movement steps split at boundaries, not UI/transforms. Day/phase/progress are exposed.

Night pauses production/Builder work and new gathering; pending Wood returns first. Residents walk to home boundary access, sleep inside logically, then exit valid access in morning. Homeless citizens remain outside near Town Hall; footprints stay obstacles.

Smooth world material/ground/lighting darkening leaves UI unchanged. Actual sleeping residences show warm windows and procedural per-resident moon phases; temporary localized swarms use valid visible forest/Wilderness and smoothly cycle stationary one-screen-pixel golden points. Peaceful wildlife sleeps off-screen and reconciles morning claims. Profession clothing wraps the tunic with visible colours, hats and tools, refreshed from actual profession state while preserving identity. Procedural construction shows foundation/scaffolding, then walls and roof based on actual work; upgrades retain the existing building with scaffolding and progressive trim. The elevated orthographic camera uses 55 degrees instead of 70 to reveal upright wall faces and roof thickness. Warm illuminated windows match the rotated residential geometry. These are replaceable presentation choices; footprints, X/Z grid and collision rules are unchanged.

## 35. Demolition and permanent dismissal

**Implemented**: confirmed completed ordinary-building demolition creates an exclusive NPC Builder job requiring half the original L1 base construction work, using existing qualification, efficiency and access travel rules. NPC work pauses at Night. The player never contributes demolition work. Footprints, workplaces and housing remain functional until completion. Only then refund floor(invested resource / 2) per resource (initial construction plus completed upgrades), remove occupancy, release workers, reassign residents and remove waiting Tavern candidates. Claims remain permanent. Active jobs block duplicate demolition and upgrade requests; refunds happen once. In-progress upgrades must finish first. Unfinished construction cancellation is a separate immediate confirmed administrative action with a full committed-material refund, no Builder requirement and no completion benefits. Interrupted funded jobs preserve paid materials/progress for qualified replacement Builders.

Named-confirmed dismissal removes citizens from roster, workplace/residence/gathering and visual state, frees capacity and never automatically replaces or retains history. Starting Town Hall is protected as the prototype storage/management anchor; future anchor destruction/replacement remains TBD.

## Living settlement management and verification

Placement only reserves an unfunded/unassigned site. Explicit Choose Builder / Start pays once; Pause/Reassign/Resume preserve progress/materials. Income, completion and idle Builders do not auto-start jobs. One Builder per job, one job per Builder; Hall never has a permanent employee. Night assignment is allowed while NPC construction/production pauses. Workplace controls explicitly hire unemployed adults, unassign employment and inspect assigned citizens. Families/children/hunger/storage rules are authoritative in section 16.

Hall shows adult/child/employment/housing counts, food supply/days, hunger risk, events and stored amount/capacity. Workplace panels show rates, storage blockage and workers; sites show committed/required stock, progress, Builder and Start/Pause/Reassign/Cancel. Permanent world labels are removed; bounded short-lived production indicators show actual stored quantities or rejected-storage status. Builders prepare at their own Hut each morning and make supply trips; workers use nearby movement/break/interior routines and separate access positions. Equipment remains owned by its individual citizen. Help and E/camera/rotation/input behavior are preserved.

Regression coverage includes zero-resource bootstrap and full TH1->5 progression, manual jobs/payments/reassignment/night behavior, nutrition/fractional allocation/grace/recovery/death, families/birth gates/inheritance/growth, dismissal floor, per-resource capacity, full refunds at and above capacity, Warehouse demolition overflow and production recovery. See SettlementPrototype.md for final compilation/test results and the manual Unity checklist. Source tests do not establish Play Mode or visual acceptance. Scenes/settings/packages and existing metadata are preserved; no staging, commit or push.

Validation result: **213 standalone NUnit tests passed, 0 failures in the preceding milestone**; domain, Unity presentation/Editor and separate test assemblies compiled against installed 6000.6.4f1 references with no compiler errors/warnings; `git diff --check` passed. Unity batch EditMode exited 1 before import after `Connection to channel LicenseClient-Admin refused`; no Unity test result XML was produced. Editor import (including new .meta generation), Unity EditMode tests and manual Play Mode/visual verification remain required.

## Play Mode corrective milestone: current rules and acceptance

Initial citizen generation randomizes unique names from configurable male/female pools, ages18..45 and attributes1..10, preserving two men/two women, zero resources, Hall residences and no employment. Explicit seeds reproduce the whole initial group. Generation is cached on the settlement and never replaces an existing live roster. The presentation adapter preserves the active in-memory economy across scene reloads; starting a fresh Play session resets it. Settlement saving/loading remains future work; no save migration or implicit regeneration is added.

Town Hall has no permanent employee. Job selection shows only adults already employed at completed qualifying Huts. The default list excludes busy Builders; a separate explicit Reassign control may pause a selected Builder's previous job. Each row shows name, Hut level and availability. Hall's ordinary citizen roster is hidden while choosing a Builder. The same filtering applies to construction, upgrade and demolition. Default workplace hiring uses unemployed living adults only; changing profession requires explicitly unassigning/fire employment first, then hiring. Permanent dismissal remains a separate named-confirmed action with the four-citizen floor.

Authoritative production: Wood10s with L1..5 quantities1/2/3/4/5; Stone20s with quantities1/2/3/4/5; Iron40s, locked at L1..3, quantity1 at L4 and2 at L5. Wool now uses 10s with L1..5 quantities0/1/2/3/4; Meat30s and all nutrition/Farm/food values are unchanged. Production still needs the correct worker, preserves partial cycles across night/absence, uses independent resource cycles, and respects storage.

Unfinished cancellation previews and returns the entire committed construction bundle at any progress, including99%; unfunded sites return zero. Cancellation immediately releases footprint/job/Builder without demolition work. Completed demolition refunds floor(50% cumulative investment) only when its qualified NPC job completes. Both preserve approved overflow and idempotent accounting.

Runtime placement validates a connected half-tile outdoor access graph serving every existing/proposed building and a reachable actual Wilderness boundary. Narrow footprint boundary lanes are the only occupied-footprint exception: tile centers and structural interiors remain blocked. Shared boundaries and corners connect even when every adjacent exterior tile is reserved by another building. Oriented entrances terminate on these boundary lanes; ordinary movement, worker/home access and ambient Wood return routes use the same graph. Body clearance respects inset structures and actual claims; a body can straddle a claimed outer edge only while its center follows that building boundary. This adds no logical tiles, roads, speed bonuses or stock costs. Truly disconnected access is rejected before reservation/payment. Geometric immediate placement remains a fixture API; Wilderness gathering retains depth10 and pending deliveries.

The missing fireflies were tiny Lit spheres at canopy height, using night-darkened materials and shrinking below a pixel during fades. Their rendering path now projects pooled localized swarm points into one-screen-pixel unlit golden alpha-blended screen patches, with finite lifetimes and stationary twinkle groups. This avoids canopy occlusion and shared material darkening; no scene regeneration or shader asset is needed. Day/claimed/non-forest/off-camera points are suppressed. Residential window emission overrides shared night tint, warm local light is stronger and each pane uses visibly warm golden-orange emission and smooth asynchronous flicker within42-114% nominal brightness. Only nighttime residences with assigned living occupants illuminate; daytime and empty residences remain dark. Small local lights illuminate the pane and immediate surroundings, rather than the whole building.

Manual acceptance:

1. Start several fresh Play sessions: different unique adult groups, two males/two females, valid ranges, zero inventory. Reload the scene in one session: names, attributes, jobs and resources must persist rather than reroll.
2. Prepare a Hall upgrade and open Builder selection: only qualified available Builders, with Hut level/status. With none, show the explanation. Busy Builders appear only through explicit reassignment. Repeat for ordinary sites/upgrades/demolition.
3. Hire every available adult. An empty workplace must disable hiring and explain why. Unassign a worker, then hire elsewhere; population is unchanged. Permanent dismissal retains its separate confirmation/floor.
4. At night pan/zoom around forest margins: sparse stationary warm2x2 glows smoothly fade/hold/fade, remain observable at every normal zoom and vanish during Day or after claims reach them. Verify Help/UI overlay readability. Window candlelight remains visibly on with asynchronous flicker; empty houses and daytime windows stay dark.
5. Check staffed Wood10s, Stone20s and Iron40s through L1..5, including Iron unlock atL4, night pause/retained partial work, independent Mine cycles, actual feedback and capacity clipping. Check the updated Wool10s and unchanged Farm/food balance.
6. Cancel funded sites at0/50/99%: displayed full refund, returned once even above capacity, no work job, released footprint/Builder. Unfunded refund zero. Completed demolition keeps old half cumulative refund/timing and overflow behavior.
7. Place adjacent/rotated buildings beside Hall and workplace doors. Citizens must use valid alternative perimeter access, deliver Wood and continue jobs without wall walking or persistent corner traps. A dense enclosure of touching footprints must remain reachable through shared boundary lanes; truly disconnected access must fail with no stock/footprint change. Recheck irregular-boundary Wilderness trips and zero-resource bootstrap/progression.

Unity EditMode and visual Play Mode acceptance must be reported separately from source compilation and standalone tests. Scenes, packages, ProjectSettings and existing metadata are preserved; nothing is staged, committed or pushed.

Corrective validation: **221 standalone NUnit tests passed, 0 failures**. Domain, Unity presentation/Editor and separate test assemblies compiled against installed6000.6.4f1 references without compiler errors or warnings. `git diff --check` passed. Unity batch EditMode was attempted again and exited1 before import after `Connection to channel LicenseClient-Admin refused` and platform assertions; no Unity test XML was produced. Actual Play Mode rendering has not been verified here. The existing two new Living source/test .meta files, generated during the user's Editor import, are present and preserved.

Corrective files modified (under Assets/ColonyGame): World/BuildingEconomyRules.cs, CitizenSimulation.cs, SettlementState.cs, SettlementBootstrap.cs, SettlementEconomy.cs; Scripts/WorldPresentation/PrototypeHelp.cs, PrototypeHud.cs, SettlementAmbience.cs, SettlementPrototypeView.cs; Tests/Editor/CitizenTests.cs, PlayerInteractionTests.cs, PrototypeHelpStateTests.cs, SettlementBootstrapTests.cs, SettlementEconomyTests.cs, SettlementLivingTests.cs; both design documents. Other previously pending changes are preserved. This correction adds no project assets and does not change scenes, packages, ProjectSettings, metadata, Git configuration or repository security.

## Final boundary paths and inhabited window corrections

External site-edge strips and gate-post frames are removed on every side. Actual walls, roofs, doors, construction scaffolding, windows and entrance thresholds remain. Structures retain their footprint dimensions and 2.5D procedural presentation; connected groups receive no replacement frame.

The renderer draws deduplicated unit boundary segments from the domain perimeter network. Gold lines meet at corners and merge on shared edges. View-facing ribbons target 1.5 screen pixels across normal camera zoom, remain subtle and occupy no extra logical tiles. Their physical navigation clearance accommodates citizen bodies within the existing inset around structures; the visible line is not a wide paved corridor.

All outdoor citizen travel uses cardinal half-tile routes, with continuous interpolation and body checks. Shared boundaries work for touching/rotated buildings, entrance access and dense Hall Wood deliveries. All occupied tile centers and inset structural interiors remain blocked. Ordinary idle movement cannot enter Wilderness; only approved gathering trips use the bounded Wilderness area. Assigning a Builder during an active Wood trip preserves its route and pending delivery before starting construction.

Residential windows have panes on all four structural faces, ensuring the existing camera can see candlelight after 90-degree rotations. Each pane has a distinct deterministic phase and smooth visible flicker. Warm golden-orange emission survives night tint; short-range local lights avoid uniform building illumination. Only completed, occupied residences illuminate at Night; Day and empty residences remain unlit. Firefly swarm behavior follows the current lifecycle below.

Manual Unity Play Mode checklist:

- Inspect Hall, House, Hut and large sites, including rotated and touching clusters: no bulky external strips or group frames on any side; walls/roofs/doors/scaffolding remain.
- At nearest/farthest normal zoom, verify thin 1-2-pixel golden boundaries, continuous corners/T-junctions/shared edges and no doubled thick lines or broad pavement. Check depth/ground occlusion and contrast at Day/Night.
- Form a dense touching enclosure around Hall and observe ambient Wood trips reach the boundary entrance and deposit once. Verify Builder/worker/home routes through shared edges without clipping structures, teleporting, corner traps or sustained doorway stacking.
- Rotate rectangular buildings through 0/90/180/270; inspect footprints, all entrance access and shared boundary routes. Test cancellation/demolition releases navigation without changing claims.
- At Night compare occupied Hall/House/Tavern with empty residences: warm bright windows, asynchronous noticeable smooth flicker and light limited to immediate surroundings. Rotate residences and check camera visibility; during Day all panes are dark. Pan/zoom and check UI readability, rendering cost and Console errors.

Standalone tests and source compilation do not establish visual or Unity Play Mode acceptance. No scenes, packages, ProjectSettings, metadata or Git operations are changed by this correction.

Verification for these corrections: **231 standalone NUnit tests passed, 0 failures**, including the full zero-resource TH1-to-TH5 playthrough. Domain, presentation/Editor and separate test assemblies compiled against installed6000.6.4f1 references without compiler errors or warnings. `git diff --check` passed. Unity batch EditMode exited1 before tests with `LicenseClient-Admin` connection refusal and platform assertions; no test XML was produced. Manual Unity Play Mode/visual acceptance remains outstanding.

Files modified for this correction (under Assets/ColonyGame): World/CitizenNavigation.cs, CitizenSimulation.cs, AmbientWoodGathering.cs, SettlementState.cs, SettlementEconomy.cs, SettlementPopulation.cs; Scripts/WorldPresentation/SchematicBuildingView.cs, SettlementPrototypeView.cs, SettlementAmbience.cs, PrototypeHelp.cs; Tests/Editor/BuildingAccessRecoveryTests.cs, CitizenTests.cs, SettlementBootstrapTests.cs, SettlementLivingTests.cs, PrototypeHelpStateTests.cs; both design documents. Existing pending changes and metadata are preserved. No new project assets, scene regeneration, settings/package changes, staging, commit or push.

## Current automatic employment and citizen management rules

Completed workplace construction and initial employment setup fill unlocked vacancies with the first eligible living unemployed adult in authoritative roster order. Recruitment and adulthood also fill available unlocked vacancies. Children, inactive citizens and employed adults are skipped; existing employment is never transferred or reshuffled. No eligible adult means an unstaffed building and zero production. Manual Unassign Worker releases incompatible Builder jobs without dismissing the citizen or losing committed job materials/progress, and sets a persistent per-building manual vacancy lock. Automatic staffing skips that lock; explicit Assign Worker clears it. Other unlocked workplaces may still hire. Town Hall, Houses, Taverns and non-role buildings have no employees. A Hut may auto-employ a Builder, but construction, upgrade and demolition job selection/funding remain strictly manual.

Explicit residence relocation stores a minimal PreferredResidenceId. The citizen panel offers Move to Town Hall only for a living citizen currently elsewhere when a completed Hall has a free slot; it releases the old slot without teleportation. House preference and family/birth relocation respect that choice. Citizens without an override retain ordinary House-before-Tavern-before-Hall reconciliation. Invalid/demolished residence overrides clear so displaced citizens can rehouse normally. Death/dismissal clears the preference with the other links. Capacity limits and family/birth rules remain unchanged.

Town Hall lists every living adult and child by name only in authoritative roster order. Clicking a name invokes the same selection callback and left citizen information panel as clicking the world figure. Details continue refreshing from the selected live citizen. The panel retains identity, attributes, age, hunger, family, profession, workplace, residence and named permanent-dismissal confirmation/four-person floor, and adds context-sensitive Unassign Worker and Move to Town Hall actions. Dead/dismissed citizens disappear from both roster and selection.

Wool requires an assigned living adult Butcher and uses an independent 10-active-simulation-second cycle: L1 locked/0, L2=1, L3=2, L4=3, L5=4. Night pauses retain partial work. Storage clipping/overflow suppression and feedback use actual deposits; blocked output is never reported as success. Meat remains 30 seconds; Wood, Stone, Iron, Fish and Farm values are unchanged.

Fireflies use six pooled presentation-only swarm slots, nine possible points per swarm and three illuminated points per group. Visible unclaimed procedural forest cells inside the rendered Wilderness bounds are required; off-camera, claimed or invalid anchors are cleared. Spawns are staggered about 1.3 seconds apart and centers stay at least 2.5 tiles apart. Each local cluster lasts 8-15 real presentation seconds and fades during its first/last second; each stationary point group smoothly fades in/out around a 1.8-second cycle, then a new nearby subset lights. Local offsets stay within 1.1 tiles; points never crawl or fly. Rendering projects warm golden (1, 0.85, 0.3) unlit one-screen-pixel points, independent of zoom, without per-frame GameObject creation. Dawn clears all slots/alpha; the next night starts new swarms. Tuning is centralized in FireflySwarmSettings in PrototypeHelp.cs. Fewer than three swarms are allowed during warm-up or when valid visible locations are scarce.

## Firefly swarms and citizen management verification

1. At Night pan across visible forest: expect several independent localized swarms, subject to valid locations. Watch for at least 30 seconds: groups of about three stationary golden pixels fade and change nearby positions, whole swarms expire after 8-15 real seconds, and replacements appear elsewhere. Zoom in/out: points stay about one screen pixel. No crawling, map-wide swarm, claimed-ground pixels or permanent frozen pixels. Check dawn cleanup and fresh spawning the following night.
2. Open Town Hall: verify name-only roster rows include adults/children in stable order. Compare clicking a name with selecting the same citizen in the world: both open the same left panel and update after hunger, employment, age or housing changes. Death/dismissal removes the row and stale inspection.
3. In that panel unassign an employed worker: citizen stays in settlement, workplace becomes manually vacant, active Builder job releases but retains payment/progress, and automatic staffing does not refill that vacancy. Manually hire later; an employed adult cannot be silently moved to another workplace. Repeat from world and Hall selection.
4. With a free Hall housing slot, move a House resident to Hall. Verify old slot released and Hall count increased without teleporting or exceeding capacity. Complete/upgrade another House and pass several dawns: the explicit choice must persist through House preference and family/birth updates. Full/unfinished housing rejects relocation.
5. Finish a new Hut/producer: first eligible unemployed adult in roster order hires automatically, skipping children and employed/dead citizens. With no eligible adult it stays vacant and produces nothing. Recruit a new adult or observe adulthood: unlocked vacancies fill. Hall/Houses/Taverns never hire. Hut employment must never automatically select, fund or start any Builder job.
6. Butcher L1 produces no Wool; L2/L3/L4/L5 produce 1/2/3/4 Wool per 10 active simulation seconds. Travel/breaks are not productive time. Compare management rates and temporary feedback with actual stored quantities. Meat remains its separate 30-second cycle. Observe partial Wool cycle retention overnight; full/overfull Wool storage gives blocked feedback, other resources continue, and spending allows capped deposits again.
7. Recheck manual funding/jobs, multiple Huts at Night, cancellation full refunds, timed demolition/50% refunds including overflow, permanent housing/families/hunger, independent 10% daily Tavern rolls, and zero-stock TH1->TH5 progression. Connected thin historical-golden paths, sandy footprints, rotations, entrances, Wood deliveries, windows and unrelated visuals remain unchanged. Check Console and rendering cost.

Visual appearance, real UI interaction and Unity runtime performance require manual Play Mode verification; standalone tests do not verify pixels or UI rendering.

Validation for this corrective milestone: **249 standalone NUnit tests passed, 0 failures**, including zero-stock playable TH1->TH5, construction/refund/storage, navigation, citizen management, automatic employment and pooled swarm lifecycle regressions. Domain, presentation/Editor and separate test assemblies compiled against installed Unity 6000.6.4f1 references with no compiler errors or warnings. Unity batch EditMode exited 1 before tests after `Connection to channel LicenseClient-Admin refused` and licensing/WMI assertions; no result XML was produced. Actual Editor import, Unity EditMode execution, UI interaction and visual/performance acceptance remain manual checks. `git diff --check` passed. No staging, commit or push.
