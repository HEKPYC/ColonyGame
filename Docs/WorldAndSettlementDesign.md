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

**Planned future presentation**: a fully 3D settlement that the player can explore directly, potentially switching between first-person and third-person perspectives. Specific controls and switching behavior are TBD.

## 2. Settlement Territory and Wilderness

**Approved design decisions**

- The world is conceptually divided into claimed **Settlement Territory** and surrounding **Wilderness**.
- The player may physically move only inside claimed Settlement Territory.
- Citizens may enter nearby Wilderness but must never travel farther than **10 Tiles beyond the current settlement boundary**.
- A building may be placed only when its **entire reserved footprint** is already inside existing claimed Settlement Territory.
- Placing a building partly or fully outside the territory to claim new land is prohibited.

Citizen Wilderness distance is conceptually measured relative to the **nearest actual claimed Settlement Territory boundary**, respecting its irregular shape. It is not measured from the original 20 x 20 area, Town Hall, settlement center, or rectangular bounding box.

The exact pathfinding/distance metric remains **TBD**: do not choose Manhattan, Euclidean, Chebyshev, or actual traversable path distance yet. Boundary-handling behavior is also TBD. The actual irregular shape and the 10-Tile limit remain approved.

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

**Approved design decisions**

Early-game Wilderness is primarily forest. Its visual presentation may contain trees, grass, ground vegetation, decorative flowers, and decorative wildlife.

Wilderness trees are initially environmental representation. They are not intended to be individually persistent resource nodes requiring targeting, reservation, chopping, permanent removal, or regeneration. Ambient forest trips do not need to locate or destroy a rendered tree.

Ambient Wood gathering is available to **ordinary adult citizens** and requires no Lumberjack profession. An adult with no profession may take trips. The **initial Builder may also take ambient Wood trips when not occupied by construction work**.

These citizens may occasionally enter nearby Wilderness and return with a **very small amount of Wood**. This provides visible ambient activity and small bootstrap Wood income for early construction such as the Builder's Hut. It remains intentionally inefficient; Lumber Mill + Lumberjack later provides proper scalable Wood production. The 10-Tile limit relative to the actual irregular territory still applies.

When territory expands, vegetation on newly claimed Tiles disappears visually. **Territory expansion awards no resources**, including no Wood from visually removed trees.

**TBD**: trip duration, Wood quantity, citizen selection, scheduling, frequency/cooldown, carrying animation, destination selection, and interruption behavior. Eligibility beyond the explicitly approved ordinary-adult, unassigned-adult, and idle-initial-Builder cases remains TBD. No numeric gathering rate is approved.

## 5. Resources

**Approved resource vocabulary**

| Category | Resources |
| --- | --- |
| Materials | Wood, Stone, Metal, Wool |
| Food / agricultural resources | Beetroot, Wheat, Potato, Carrot, Fish, Meat |

**Planned future system**: resource definitions should be data-driven and extensible. These resources are not an exhaustive permanent list; additional types may be approved later.

The player should eventually have a persistent, understandable UI panel or HUD showing quantities of relevant/unlocked resources. Resource UI presentation is TBD; see section 31. No resource values, starting quantities, storage units beyond stated production units, or economic prices are approved here.

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
- When future citizens approach, peaceful wildlife should flee away from the citizen while remaining entirely in Wilderness. Among reasonable escape alternatives, prefer movement deeper into Wilderness relative to the actual irregular settlement boundary. Citizens and citizen-triggered fleeing are not implemented by this design update.
- The Wilderness-only policy belongs to peaceful ambient wildlife. Future Wolves/Bears may enter Settlement Territory and follow different movement/combat rules; their implementation remains deferred.
- Cow, Pig, Chicken, and Sheep are separate future livestock associated with the Butcher's enclosure and do not roam Wilderness.
- Schematic prototype geometry/colors are replaceable presentation choices, not permanent design rules.

**TBD**: final spawning/population balance, ecology, detailed movement/flee tuning and citizen detection, predator AI, and combat behavior. No hunting or resource yield from decorative wildlife is approved.

## 14. Citizen identity

**Approved design decisions**

Citizens are persistent individuals, not disposable generic units. They include adult men, adult women, and children. Each has a persistent underlying identity and base appearance.

Assigning or changing profession must preserve that same person and underlying appearance. Profession presentation changes clothing, tools, and equipment; it must not replace the citizen with a different person or completely different base character. This remains valid when full 3D models replace prototype visuals.

## 15. Citizen characteristics

**Planned future model capabilities**: name/identity, sex, age/life stage, family relationships, profession, skills/aptitudes, individual characteristics/statistics, current needs/state, work assignment, and home assignment.

These are future concepts the model should be capable of supporting, not a finalized schema or stat list.

**TBD**: exact characteristics, skills, needs, happiness, aging rates, skill effects, and profession suitability rules. Do not invent detailed statistics or formulas.

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

**Approved**: reserved footprint **5 x 5 Tiles**; already present inside the exactly 20 x 20 predefined starting territory; a major progression gate. It triggers no initial completion expansion at world creation. Its exact starting position and level remain TBD.

Town Hall upgrades are not simply instant purchases. They involve an associated quest chain. After the relevant quest progression is completed, the Town Hall itself is upgraded through construction.

Building-type unlocks through Town Hall Level 4 are approved in section 20. The current design is limited to Town Hall Levels 1 through 4; no Level 5+ content is defined.

**TBD**: quests, quest requirements, upgrade costs, construction times, and other unspecified level-specific effects.

## 22. Builder's Hut

**Approved**: reserved footprint **3 x 3 Tiles**; associated profession **Builder**.

Initially, the player uses the Town Hall to assign the first suitable citizen as Builder. That initial Builder may gather ambient Wood when not occupied by construction, without becoming a Lumberjack. This provides bootstrap Wood income for early construction such as the Builder's Hut. The Builder constructs it. Exact costs and other required starting resources remain TBD.

After the hut exists, construction progression depends on it. Its maximum level is limited by current Town Hall level: **Town Hall Level 1 cannot support a Builder's Hut above Level 1**. This relationship must remain compatible with the global Level 1-4 system; it does not specify every other prerequisite.

**TBD**: construction speeds, upgrade costs, worker limits, construction range, and level-specific construction capability.

## 23. House

**Approved**: reserved footprint **3 x 3 Tiles**; supports settlement population and family life.

**TBD**: citizen capacity, family capacity, upgrade benefits, and housing requirements.

## 24. Warehouse

**Approved**: reserved footprint **4 x 4 Tiles**; associated planned profession **Courier**; associated with settlement storage, logistics, and resource movement/distribution.

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

**Approved progression direction for future slices**, not a request to implement the whole sequence at once:

1. The player creates or selects a game profile.
2. The player enters a new settlement.
3. The settlement starts as a simple/flat **exactly 20 x 20 claimed Tile** area.
4. A Town Hall already exists inside that area and triggers no expansion at world creation.
5. Forest Wilderness surrounds the settlement.
6. At least one initial citizen exists.
7. Through the Town Hall, the player assigns an initial suitable citizen as Builder.
8. Ordinary adult citizens, including unassigned adults and the initial Builder when idle from construction, can make ambient forest trips without requiring the Lumberjack profession.
9. These trips slowly supply small amounts of Wood.
10. Enough initial resources are eventually accumulated to construct the Builder's Hut.
11. The Builder constructs the Builder's Hut.
12. Initial completion applies the universal influence rule once. Later upgrades do not reapply influence, and claimed Tiles remain permanent after demolition/destruction.
13. Further construction/progression depends on Builder's Hut capability.
14. Other buildings' maximum levels are capped by Town Hall level (section 6). Building types unlock cumulatively at the Town Hall levels listed in section 20.
15. Later Town Hall upgrades require quest-chain progression followed by construction.

Starting quantities, exact citizen count beyond the minimum, construction costs, and the detailed transition from bootstrap construction to hut-dependent progression remain TBD. Existing profile selection currently leads to a placeholder; this design does not change it.

## 31. Resource UI

**Planned future system; approved direction**

The player should be able to see quantities of relevant/unlocked resources without inspecting each production building. The UI must support future additions beyond Wood, Stone, Metal, Wool, Beetroot, Wheat, Potato, Carrot, Fish, and Meat.

**TBD**: screen position, icons, grouping, sorting, locked-resource visibility, tooltips, animations, and exact visual presentation. Do not implement this UI as part of this documentation task.

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

No specific storage format, pathfinding architecture, class hierarchy, streaming scheme, or implementation roadmap is approved by this document.

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

- Exact construction and upgrade costs, exact construction times, and undefined level-specific benefits.
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
- **Boundary distances**: what metric measures the citizen's 10-Tile Wilderness limit around irregular borders, protrusions, and corners? How is movement handled when territory changes during a trip?
- **Influence geometry edge cases**: how are diagonal corners, world edges, disconnected claimed areas, or holes treated beyond the approved straight-boundary examples?
- **Starting Town Hall details**: its exact position within the predefined territory and its initial level remain TBD. It applies no initial completion expansion.
- **Demolition, destruction, and rebuilding details**: costs, refunds, ruins, rebuilding rules, and detailed destruction behavior remain TBD. Permanent claimed territory and no repeated upgrade influence are settled rules.
- **Placement presentation and special cases**: entrance/traversal representation and detailed visual orientation remain TBD. No extra general placement requirement is approved beyond claimed territory and non-overlapping footprints. Any future special-building requirements need explicit design; roads are not an unresolved prerequisite.
- **Player boundary enforcement**: how is the prohibition on physical movement outside claimed territory enforced without defining an unapproved movement mechanic?
- **Ambient gathering operation**: trip duration, Wood quantity, citizen selection, scheduling, frequency/cooldown, carrying animation, and destination/interruption rules remain TBD. Eligibility beyond the explicitly approved ordinary-adult, unassigned-adult, and idle-initial-Builder cases remains TBD; those approved cases must not be reopened.
- **Bootstrap viability**: what starting resources and citizen composition support the approved early-game sequence? Builder's Hut material costs must be defined consistently with resources available before scalable production; no Wood-only recipe is assumed.
- **Production operation**: concurrent cycles, selected products, player priorities, Farmer-selected work, divided worker time, or another schedule remain undecided. Worker count per building, additional workers from upgrades, efficiency/stat modifiers, work schedules, absence behavior, travel time, inputs, interruptions/partial progress, storage blocking, delivery/output destinations, upgrades during cycles, and offline/background production remain TBD. Appropriate assigned-profession workers are already required.
- **Population deadlock prevention**: how should recovery work after extreme population loss when family growth is impossible? Emergency visitors, replacement citizens, minimum protected population, recruitment guarantees, and game-over conditions are not approved. The exact fail-safe remains TBD.
- **Non-production levels**: what level behavior applies to each building where the global ceiling is known but building-specific functionality is not? Do not infer a resource output rule for Tavern meals without an approved definition.

### Contradiction and ambiguity review

No direct contradiction was identified among the explicit rules. Unspecified operational details above must remain open. In particular:

- Full-footprint placement inside existing territory and expansion on completion are compatible: the footprint is already claimed, while influence can claim surrounding Wilderness.
- Visually removing trees during expansion grants no resources; separate ambient citizen trips are the approved source of small early Wood income.
- Universal production defines output and level availability for an active process, with an appropriate assigned-profession worker required. Scheduling, worker counts/attendance details, and inputs remain undecided.
- The initial territory is exactly 20 x 20 Tiles. The already existing Town Hall causes no expansion at creation; subsequent initially completed buildings do, upgrades do not, and demolition/destruction never revoke claimed territory.
- Unassigned adults and the initial Builder idle from construction can provide ambient bootstrap Wood without a Lumberjack profession. Exact costs and starting conditions still need definition; no Wood-only recipe or numerical yield is assumed.
- Non-overlapping reserved footprints and 90-degree rotation are approved. Influence overlap adds no bonus. Road connectivity is neither a placement nor an operational requirement.
