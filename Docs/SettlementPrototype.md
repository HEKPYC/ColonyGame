# Settlement Prototype

## Implemented milestone and scope

Unity 6.6 / **6000.6.4f1**. Open the existing SettlementPrototype scene after script import; no regeneration is required. Menus/profiles, packages, ProjectSettings and serialized scene assets are unchanged. Runtime state resets after Play Mode.

Starts with **TH1**, zero Wood, four existing adult identities, exactly 400 claims (0..19), Hall origin (7,7)/5 x 5, existing peaceful wildlife. All normal buildings and Town Hall reach L5. Cumulative unlock counts TH1..5 are **3/7/11/13/13**; TH5 introduces no new type.

The player builds any number of initial L1 Huts for **25 Wood** and 300 base work seconds using held E during Day or Night after explicit funding. NPC Builders perform ordinary construction and all upgrades. Automatic and manual workplace assignment use existing live Citizen objects. Physical player inventory, quests, combat, roads, research, magic and hauling remain future work.

## Domain and presentation architecture

| File | Responsibility |
| --- | --- |
| World/BuildingEconomyRules.cs | Central cost scaling/exceptions, explicit TH costs, times, Builder rate, residence capacities/professions, production catalog and clock. |
| World/SettlementEconomy.cs | Existing-state orchestration, paid jobs, production progress, schedules, bounded simulation steps, demolition and dismissal. |
| World/SettlementLiving.cs | Daily nutrition/fractional credit, hunger/health/death, families/births/child growth and storage/production balance. |
| World/SettlementPopulation.cs | Explicit residence links/reassignment and deterministic injectable Tavern arrivals/candidates/payments/expiry. |
| World/SettlementBootstrap.cs | Balance, authoritative atomic inventory, player L1 Hut bootstrap and explicit profession assignment. |
| World/SettlementState.cs | Building levels/investment, reserved occupancy, construction/upgrade state and permanent influence union. |
| World/Citizen.cs and CitizenSimulation.cs | Immutable identity/attributes, live additions/removals, residence/interior/task links and shared continuous movement/recovery. |
| World/AmbientWoodGathering.cs | Existing irregular-boundary trips, pending arrival-only Wood and night dispatch suppression. |
| World/PlayerInteraction.cs | Central press/hold context; stale/dismissed/demolished targets produce no action. |
| Scripts/WorldPresentation/SettlementPrototypeView.cs | Composition, input and presentation refresh; no economy state duplicated in GameObjects. |
| Scripts/WorldPresentation/PrototypeHud.cs and PrototypeHelp.cs | Scrollable management, costs/affordability, staffing/housing/recruitment, confirmed destructive actions, debug speeds and Help. |
| Scripts/WorldPresentation/CitizenView.cs | Dynamic roster figures, preserved base identity, profession equipment and work motion; hide actual sleepers. |
| Scripts/WorldPresentation/SettlementAmbience.cs | Smooth world tint/lighting, sleeping windows/moons, stationary forest/Wilderness glimmers and pooled work puffs. |
| Scripts/WorldPresentation/SchematicBuildingView.cs | Distinct oriented structures/entrances, staged construction and level trim; no external decorative frames. |
| Scripts/WorldPresentation/PeacefulWildlifeView.cs | Night presentation suspension; existing wildlife domain retains Wilderness/recovery rules. |

The World assembly remains free of Unity references. One resource inventory serves the settlement; Warehouse is future logistics specialization, not a second balance. Bundle payment validates every resource/overflow before changing any amount. Matching transaction retry is idempotent; conflicting retry fails.

The existing immediate TryPlacePrototypeBuilding API remains a domain fixture only. Runtime placement only reserves; manual Start chooses a Builder, pays once and begins work. Higher TH initialization is explicit in old geometric fixtures; runtime starts TH1.

## Town Hall progression

| Upgrade | Explicit payment |
| --- | --- |
| TH1->2 | 150 Wood |
| TH2->3 | 300 Wood +100 Wool |
| TH3->4 | 450 Wood +250 Wool +150 Stone |
| TH4->5 | 600 Wood +350 Wool +250 Stone +100 Iron |

Explicit Town Hall upgrade requests prepare an unfunded, unassigned job. Choose a Builder from a Hut at the Hall's current level and Start to pay once. Hall base work is 600 seconds (approved correction); upgrades require base * target level and existing Builder efficiency. The Builder must travel and work during Day. Payment happens once and the level changes at 100%, never on resource accumulation or funding alone. Finish the Hall upgrade before upgrading the Hut. TH5 is max; quest prerequisites remain future work.

## Initial recipes and base construction times

| Building | Initial cost | Base seconds | Base minutes |
| --- | --- | --- | --- |
| Town Hall (upgrade base; not player placeable) | See explicit upgrade table | 600 | 10 |
| Builder's Hut | 25 Wood | 300 | 5 |
| House | 25 Wood | 300 | 5 |
| Farm | 50 Wood | 360 | 6 |
| Tavern | 100 Wood | 480 | 8 |
| Butcher | 150 Wood | 480 | 8 |
| Fisherman's Hut | 100 Wood | 420 | 7 |
| Guard Tower | 75 Wood | 360 | 6 |
| Warehouse | 150 Wood, 25 Wool | 600 | 10 |
| Lumber Mill | 200 Wood, 100 Wool | 720 | 12 |
| Mine | 250 Wood, 50 Wool | 720 | 12 |
| Barracks | 150 Wood, 100 Wool | 600 | 10 |
| Laboratory | 300 Wood, 150 Wool, 100 Stone, 50 Iron | 900 | 15 |
| Mage Tower + Place of Power | 250 Wood, 100 Wool, 50 Stone, 100 Iron | 1080 | 18 |

## Generated upgrade-cost table

This table was generated by executing BuildingEconomyRules.Cost against the compiled domain assembly. Each cell is the payment for that target stage, **not cumulative investment**.

| Building | Build L1 | To L2 | To L3 | To L4 | To L5 |
| --- | --- | --- | --- | --- | --- |
| Builder's Hut | 25 Wood | 50 Wood | 75 Wood, 50 Wool, 25 Stone | 100 Wood, 75 Wool, 50 Stone | 150 Wood, 100 Wool, 75 Stone, 50 Iron |
| House | 25 Wood | 50 Wood, 25 Wool | 75 Wood, 50 Wool, 25 Stone | 100 Wood, 75 Wool, 50 Stone, 25 Iron | 150 Wood, 100 Wool, 75 Stone, 50 Iron |
| Farm | 50 Wood | 75 Wood, 25 Wool | 100 Wood, 50 Wool, 25 Stone | 150 Wood, 75 Wool, 50 Stone, 25 Iron | 200 Wood, 100 Wool, 75 Stone, 50 Iron |
| Tavern | 100 Wood | 150 Wood, 25 Wool | 200 Wood, 50 Wool, 25 Stone | 250 Wood, 75 Wool, 50 Stone, 25 Iron | 300 Wood, 100 Wool, 75 Stone, 50 Iron |
| Butcher | 150 Wood | 200 Wood | 250 Wood, 50 Wool, 25 Stone | 300 Wood, 75 Wool, 50 Stone, 25 Iron | 350 Wood, 100 Wool, 75 Stone, 50 Iron |
| Fisherman's Hut | 100 Wood | 150 Wood, 25 Wool | 200 Wood, 50 Wool, 25 Stone | 250 Wood, 75 Wool, 50 Stone, 25 Iron | 300 Wood, 100 Wool, 75 Stone, 50 Iron |
| Guard Tower | 75 Wood | 100 Wood, 25 Wool | 150 Wood, 50 Wool, 25 Stone | 200 Wood, 75 Wool, 50 Stone, 25 Iron | 250 Wood, 100 Wool, 75 Stone, 50 Iron |
| Warehouse | 150 Wood, 25 Wool | 200 Wood, 50 Wool | 250 Wood, 75 Wool, 25 Stone | 300 Wood, 100 Wool, 50 Stone, 25 Iron | 350 Wood, 150 Wool, 75 Stone, 50 Iron |
| Lumber Mill | 200 Wood, 100 Wool | 250 Wood, 150 Wool | 300 Wood, 200 Wool, 25 Stone | 350 Wood, 250 Wool, 50 Stone, 25 Iron | 400 Wood, 300 Wool, 75 Stone, 50 Iron |
| Mine | 250 Wood, 50 Wool | 300 Wood, 75 Wool | 350 Wood, 100 Wool, 25 Stone | 400 Wood, 150 Wool, 50 Stone | 450 Wood, 200 Wool, 75 Stone, 50 Iron |
| Barracks | 150 Wood, 100 Wool | 200 Wood, 150 Wool | 250 Wood, 200 Wool, 25 Stone | 300 Wood, 250 Wool, 50 Stone, 25 Iron | 350 Wood, 300 Wool, 75 Stone, 50 Iron |
| Laboratory | 300 Wood, 150 Wool, 100 Stone, 50 Iron | 350 Wood, 200 Wool, 150 Stone, 75 Iron | 400 Wood, 250 Wool, 200 Stone, 100 Iron | 450 Wood, 300 Wool, 250 Stone, 150 Iron | 500 Wood, 350 Wool, 300 Stone, 200 Iron |
| Mage Tower + Place of Power | 250 Wood, 100 Wool, 50 Stone, 100 Iron | 300 Wood, 150 Wool, 75 Stone, 150 Iron | 350 Wood, 200 Wool, 100 Stone, 200 Iron | 400 Wood, 250 Wool, 150 Stone, 250 Iron | 450 Wood, 300 Wool, 200 Stone, 300 Iron |

Existing resource components increase +25 while below 100, then +50 at/above 100. Missing Wool/Stone/Iron normally enter at target L2/L3/L4 at 25 and continue that virtual curve. Builder's Hut Wood now uses the same 25/50/75/100/150 progression as House; the old special curve is removed.

**Deferred exceptions:** Hut and Butcher omit Wool only at target L2 (later 50/75/100). Hut and Mine omit Iron only at target L4 (target L5 is 50). No curve resets after deferral.

**No-deadlock path:** zero resources -> ambient Wood -> Hut1 -> staffed Farm1/food -> TH2 -> own Hut2 without Wool -> Butcher1/2 without Wool -> Wool -> TH3 -> Mine1 using Hut2 -> Stone -> Hut3/Mine3 -> TH4 -> own Hut4 without Iron -> Mine4 without Iron -> Iron -> TH5 -> Hut5. Mine1 must precede Hut3 because the latter requires Stone.

## Production and resource unlocks

An appropriately assigned worker must physically reach valid completed-workplace access. Independent cycles persist across night/travel/absence. Every unlocked Farm crop continues concurrently; all producers use quantity = level - unlock level + 1. Non-Farm cycle times stay fixed; Farm intervals normalize nutrition to approximately five adults per level per day. Old levels/capacities remain active during an upgrade until completion.

| Resource | Producer / profession | Unlock building level | Cycle seconds | L1 | L2 | L3 | L4 | L5 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Beetroot | Farm / Farmer | 1 | balanced below | 1 | 2 | 3 | 4 | 5 |
| Wheat | Farm / Farmer | 2 | balanced below | - | 1 | 2 | 3 | 4 |
| Potato | Farm / Farmer | 3 | balanced below | - | - | 1 | 2 | 3 |
| Carrot | Farm / Farmer | 4 | balanced below | - | - | - | 1 | 2 |
| Meat | Butcher / Butcher | 1 | 30 | 1 | 2 | 3 | 4 | 5 |
| Wool | Butcher / Butcher | 2 | 10 | - | 1 | 2 | 3 | 4 |
| Fish | Fisherman's Hut / Fisher | 1 | 30 | 1 | 2 | 3 | 4 | 5 |
| Wood | Lumber Mill / Lumberjack | 1 | 10 | 1 | 2 | 3 | 4 | 5 |
| Stone | Mine / Miner | 1 | 20 | 1 | 2 | 3 | 4 | 5 |
| Iron | Mine / Miner | 4 | 40 | - | - | - | 1 | 2 |

Wood is always obtainable from ambient gathering. Other UI/request availability requires an actually completed producer at the product's unlock level. Remaining starting balances are zero. No resource production is assigned to Tavern, Warehouse, Hut, military, Laboratory or Mage site. Warrior/Scientist/Mage workplace links/equipment exist; their functional systems remain future.

### Balanced Farm intervals

| Level | Beetroot quantity / seconds | Wheat | Potato | Carrot | Target nutrition / day |
| --- | --- | --- | --- | --- | --- |
| 1 | 1 / 54.00 | - | - | - | 5 |
| 2 | 2 / 67.50 | 1 / 135.00 | - | - | 10 |
| 3 | 3 / 81.00 | 2 / 162.00 | 1 / 243.00 | - | 15 |
| 4 | 4 / 92.81 | 3 / 185.62 | 2 / 278.44 | 1 / 371.25 | 20 |
| 5 | 5 / 99.90 | 4 / 199.80 | 3 / 299.70 | 2 / 399.60 | 25 |

Intervals are in active work seconds. All unlocked crops remain concurrent and preserve quantities. The normalization uses **270 productive seconds per 600-second day** after travel/breaks. A staffed Farm targets 5/10/15/20/25 adult meals per day; actual discrete harvests fluctuate around that average. No worker means zero production.

## Builder work and construction

Required work = base seconds x target level: initial x1, L1->2 x2, L2->3 x3, L3->4 x4, L4->5 x5.

Work rate = level efficiency x personal efficiency:

- Level efficiency = 1 + .20 x max(0, BuilderHutLevel - currentBuildingLevel).
- Personal efficiency = .90 + (Strength + Dexterity + Endurance)/150.
- Initial construction uses current level 1.
- 5/5/5 =1.00; 1/1/1 =.92; 10/10/10 =1.10.
- House L1->2 requires 600 work; neutral Hut L1 rate1 gives 600 active seconds, Hut L4 rate1.6 gives 375; strong Hut L4 rate1.76 gives about 340.91.

One Builder/job and one job/Builder, independent jobs in parallel. Own Hut may be upgraded one level above itself if Town Hall permits; other upgrades need Hut at target level. Attributes never bypass costs, unlocks or caps. Builders walk to access before contributing; no passive completion. Night pauses stored work. Player cannot upgrade any building or manually construct ordinary types.

Initial footprint reserves immediately, remains an outdoor obstacle, and grants footprint-plus-three influence only at completion. At Hut origin (17,8), initial completion still changes claims 400->427 once. Upgrade changes level after work only and grants no influence. Trees disappearing after claims grant no resources.

Placement reserves an unfinished L1 site even without stock or a Builder. Initial prices always use type/L1; Farm always costs 50 Wood with no Wool regardless of Hall level. Placement never deducts or assigns. Any number of pending sites may coexist. Choose Builder / Start to validate qualification, availability and stock, pay once and begin. Reassign explicitly pauses that Builder's previous job without losing materials/progress. Pause releases the Builder; resume never pays twice. Initial L1 Huts have a separate explicit manual Start/funding action. Income and idle Builders never auto-start work. Night assignment is allowed; NPC work waits until dawn. Full materials/stock, assigned actor and job status are visible. Cancel unfinished site requires confirmation, refunds all committed materials once and frees footprint/job without granting influence, housing or production. Completed buildings require timed NPC demolition instead.

Paid unfinished jobs whose Builder is dismissed or loses their Hut retain progress/materials, await a qualified replacement and never retain that citizen reference.

## Housing, population and dismissal

| Residence | L1 | L2 | L3 | L4 | L5 |
| --- | --- | --- | --- | --- | --- |
| Town Hall | 4 | 5 | 6 | 7 | 8 |
| House | 2 | 3 | 4 | 5 | 6 |
| Tavern | 3 | 4 | 5 | 6 | 7 |

Four distinct randomized adults (two men/two women) initially live in Town Hall. Configurable name pools provide unique names; ages18..45 and all attributes1..10 use the same injectable seed. No explicit seed uses fresh entropy only at new-settlement creation. Existing settlement objects reuse their initial live simulation; runtime scene reload reuses the active in-memory economy. Residence IDs are explicit; capacity is summed from completed residences. For citizens without an explicit override, automatic reconciliation fills Houses before Taverns before Town Hall, relocates residents only to a strictly better available residence and accommodates homeless citizens. Assignments stay stable between nights. Four initial citizens plus House L1 means House 2/2, Hall 2/4, total 4/6. Capacity growth and demolition completion reconcile assignments; no individual residence may exceed capacity. Settlement save/load remains future work. Manual reassignment changes the link, not outdoor position. Demolition reassigns displaced residents; overflow becomes Homeless without deleting/teleporting citizens. Homeless citizens remain outside near Town Hall at night.

Permanent dismissal uses a named two-step confirmation in citizen inspection. It removes live roster membership, workplace, residence, gathering records, active job reference and visual figure; frees capacity; does not add a historical database or automatically replace anyone.

## Nutrition, hunger and natural population

Nutrition per unit: Beetroot/Wheat **1**, Potato/Carrot **1.5**, Fish **2**, Meat **2.5**. Adults require **1 nutrition per completed game day**, children **0.5**. There is no physical Food resource. At each dawn (after a full 600-second cycle), allocation consumes the smallest nutrition units first, then enum order, only as many as necessary. Indivisible surplus persists as fractional nutrition credit. Shortage is shared in the same proportional ratio across every resident; individual satiety is that ratio. Non-food materials are never consumed. Repeating a processed day is idempotent. Newborns first receive allocation the next dawn.

Any insufficient full day increments the individual's consecutive hungry-day counter. Five such days cause no damage; from day six, daily damage is **20 x unfulfilled fraction**. With no food, death occurs after ten full hungry days. Full feeding resets the counter, stops damage and heals **10 health/day**, up to 100. Health at or below 25 prevents employment activity and gathering. Death removes the live person, partner link, housing, workplace, gathering, routines and active Builder reference; funded jobs keep their progress/materials. Death bypasses voluntary dismissal protections.

Eligible active adults aged **18..45**, with health above 25, residence and no partnership form automatic male/female pairs once per dawn, preferring a shared home then stable ID order. Ancestry prevents parent/child and shared-ancestor pairs. A couple can produce a child only with both parents fully fed, healthy, housed together (automatic family relocation is allowed when it respects explicit residence preferences), one free slot, and **three days of remaining nutrition for the prospective population**. Each eligible couple independently rolls **20% per day** after a **10-day cooldown**, initially measured from partnership formation. Tavern arrivals use a separate random stream.

Children have unique IDs/names/sex, persistent parent IDs/ancestry, and each attribute is rounded parent mean plus an independently drawn -1/0/+1, clamped 1..10. Age advances one year per completed game day until 18: newborn childhood lasts **18 game days**. Adult aging remains future work. Children wander/play within claims, sleep at home, never gather Wilderness resources or take a profession/job. Families are kept together when a preferred residence has enough space; otherwise they retain their existing valid housing. Birth/adulthood/death messages appear in Hall management. No historical database or emergency population replacement is added.

Voluntary dismissal requires an adult, named confirmation, and **more than four living citizens**. Children count toward that floor but cannot be dismissed individually. At five, exactly one adult can leave before the floor applies. Recruitment remains explicit and requires its complete bundle, free housing and three days of food after payment for the enlarged population.

## Shared per-resource storage and approved refund exception

One authoritative inventory remains. Per-resource Town Hall capacity at L1..5 is **200/350/500/750/1000**; each completed Warehouse adds **250 x its level**, additive across Warehouses. Pending sites add no capacity. Upgrades retain the old level's capacity until completion. Physical stacks, Courier hauling and production inputs remain future work.

Production and ambient gathering deposit only the available room for their particular resource. Amount at or above capacity blocks new deposits of that resource; others continue normally. Spending remains allowed. Once below capacity, normal deposits resume and clip at the limit. Capacity loss never discards existing stock. Hall management displays amount/capacity; Warehouse displays its contribution; successful feedback shows the actual stored resource/quantity, while rejected output reports storage full.

**Approved exception:** cancelling unfinished construction returns **100% of committed materials**; completed demolition returns **floor(50% of cumulative investment) per resource**. Both bypass storage limits and may create/increase overflow. Refunds are immediate, atomic and idempotent, never delayed or clipped, and never count as production/gathering. Unfunded cancellation returns nothing. Duplicate cancellation/demolition cannot refund twice. Integer-range failure rejects the entire transaction without partial changes; there is no capacity-based refund failure.

Every Hall upgrade bundle fits the previous Hall's base capacity, including TH4->5's 600 Wood within 750. Prices and deferred Wool/Iron exceptions are unchanged.

## Clock, schedules and ambience

Day =420 simulation seconds; Night =180; cycle =600. Starts Day1. HUD exposes game day, phase and normalized phase progress. Speeds 1/2/4/8/16 scale simulation only; movement is subdivided into <=.1-second steps and split at phase boundaries. UI/input does not use Unity Time.timeScale.

Night stops production, NPC Builder work and new ambient dispatch. Player held E continues initial L1 Hut construction at Night. A pending Wood trip returns first. Citizens walk home to real boundary access, become logically inside and disappear outdoors while sleeping. Morning uses a valid exterior location before resuming work; blocked access defers exit. Footprints remain obstacles; rendered interiors are future work.

World palette/ground tints and directional intensity transition smoothly to readable night. UI is unchanged. Actual sleeping residences have warm subtle point/window lighting; per-resident procedural moon phases vary with attributes/presentation time. Profession layers preserve hair/face/tunic identity. Work motion and small pooled dust puffs are schematic; no heavy particles or gore. Player-held construction has a matching puff.

Wildlife view/ticks suspend at night; morning reconciles Wilderness eligibility before rendering. Fireflies use six pooled presentation-only swarm slots, nine possible points per swarm and three illuminated points per group. Visible unclaimed procedural forest cells inside the rendered Wilderness bounds are required; off-camera, claimed or invalid anchors are cleared. Spawns are staggered about 1.3 seconds apart and centers stay at least 2.5 tiles apart. Each local cluster lasts 8-15 real presentation seconds and fades during its first/last second; each stationary point group smoothly fades in/out around a 1.8-second cycle, then a new nearby subset lights. Local offsets stay within 1.1 tiles; points never crawl or fly. Rendering projects warm golden (1, 0.85, 0.3) unlit one-screen-pixel points, independent of zoom, without per-frame GameObject creation. Dawn clears all slots/alpha; the next night starts new swarms. Tuning is centralized in FireflySwarmSettings in PrototypeHelp.cs. Fewer than three swarms are allowed during warm-up or when valid visible locations are scarce. No predators/hunting/loot are added.

Workers travel home -> workplace, perform short local movement loops, and take brief breaks. Builders prepare at their own Hut for 3 seconds each morning, travel to the manually chosen site and make a supply trip after 180 active seconds. Idle Builders visit/enter their own Hut. Miner/Butcher/idle-Builder loops include short interior stays and valid exit. Employed adults may take ambient Wood trips only during the last 120 daylight seconds when not assigned construction; assigned jobs always take priority. Access destinations reserve distinct nearby positions, preserving per-citizen profession equipment ownership. Permanent world labels are removed; E management and bounded temporary production/storage feedback remain.

## Tavern arrivals and requests

**Each completed Tavern independently rolls 10% once per completed game day**, using injected randomness. Two Taverns can both attract separate visitors. With N Taverns, chance of at least one arrival is 1 - .9^N; expected arrivals per day is .1 x N. Level affects candidate quality/request tier rather than the 10% base chance.

Candidates have stable temporary IDs and independent adult identity/attributes. For each attribute, draw uniform 1..10; with probability .15 x (TavernLevel-1), use max(first, second independent draw). This raises average high-tier quality while allowing both lucky low-tier and imperfect high-tier people.

Quality =mean five attributes. Target budget =25 x TavernLevel; multiply by clamp(.75 +quality/20,.75,1.25), round away from zero. Select 1..3 distinct allowed resources, split budget, divide by values (Wood/food1, Wool/Stone2, Iron4), floor quantities with minimum1. Integer rounding permits small value deviations. Requests stay fixed.

Eligibility requires actual completed producer/product unlock and Tavern tier >= both producer TH unlock and resource building unlock. Wood always qualifies. A low Tavern never asks for future Wool/Stone/Iron/crops. Availability is domain-derived, not a UI resource list.

Candidates are not roster/population/housing until recruited. Stay is three full game days (arrival day +3 expiry); no departed history. Tavern UI shows identity, attributes, request, remaining days and payment/housing availability. Recruit checks full payment, free residence and three days of food remaining after payment, consumes once, preserves the candidate's identity as permanent citizen and removes candidate state. Full housing blocks payment; never auto-replace.

## Demolition, storage and controls

Completed ordinary buildings can be demolished after confirmation only by an exclusive qualified NPC Builder job (50% of L1 base work, existing efficiency, travel and night pauses). Housing, staffing and footprint remain until completion; only then refund floor(50%) **per accumulated resource actually invested** in initial construction and completed upgrades. Example House L3 investment Wood150/Wool75/Stone25 returns75/37/12. Upgrades in progress must finish first. Claims remain permanent; only occupancy is removed. Workers become Unemployed, homes reassign/homeless and demolished Tavern candidates leave. The prototype Town Hall remains protected as its storage/management anchor; future anchor replacement/destruction is not implemented.

Storage and refund behavior follow the shared-capacity section above; physical hauling/logistics remains future work.

E remains centralized universal Interact/Action: press for selected completed building/citizen; hold only initial L1 Hut work. Closing construction panel stops held work until a fresh press/selection. B Build, R rotation, H Help, contextual Escape and camera/pointer guards remain. Help describes the new milestone, is scrollable, hides other large panels without clearing state and blocks world shortcuts while simulation continues.

## Approved future and tunable/TBD

Future: physical player/controller/transport/tools, dialogue/quests, Town Hall quest prerequisites, additional needs/happiness, skill progression, Courier hauling, Cook meals, combat/predators, research/magic, rendered interiors, saving/streaming/full 3D camera perspectives.

Tunable prototype decisions: arrival quality/request weights and bounded budgets, cadence, base times, simple ambience/clothing/level trim, one worker/workplace. Undefined additional staffing/limits/prerequisites, production inputs/offline work and non-production benefits remain TBD. No new buildings are invented.

## Validation

Domain, Unity presentation/Editor and separate NUnit test assembly are checked against installed Unity 6000.6.4f1 assemblies. The standalone Mono runner executes the available NUnit test methods; it is not Unity EditMode or Play Mode verification. Final result is recorded after the completed run. New regressions cover manual site transitions, employment, zero-resource bootstrap, food/families/starvation, storage/refund overflow/recovery and full zero-resource TH1->5 progression. Existing economy, navigation, identity, Tavern and pure presentation regressions remain.

Manual Unity import and Play Mode acceptance are required. Let Unity generate metadata for the two new C# assets; do not regenerate existing metadata or scenes.

## Manual Play Mode acceptance checklist

1. Exit Play Mode and allow Unity to import all new scripts/tests and generate their .meta files; verify Console and run World EditMode tests.
2. Open existing SettlementPrototype, enter Play Mode: TH1, Wood0, four citizens, 400 claims and original Hall.
3. Confirm all four starting residents belong to Town Hall and population/capacity is4/4; compare identity/attributes with roster.
4. Observe successful ambient trips giving +3 Wood only on return, failure +0, existing staggering/depth/cooldowns.
5. At Wood25 place an L1 Hut, verify no debit; choose manual Start, verify one25 debit and reserved3x3 without early influence; hold E during Day or Night for300 base work seconds.
6. Release/close/deselect/Help interrupts work; progress persists; completion at(17,8) claims427 once.
7. Build another L1 Hut manually; confirm no single-Hut restriction and no dependence on idle NPC Builders.
8. Confirm the first eligible adult was automatically employed at the completed Hut. Inspect identity/profession/equipment and pending Wood delivery. Unassign deliberately, confirm the manual vacancy remains empty, then explicitly hire a chosen unemployed adult.
9. Accumulate150 Wood, prepare TH2; choose Builder / Start; verify one payment, Builder travel, 1200 base work and level1 until completion, then level2 and cumulative7 unlocked types.
10. Upgrade own Hut L2 for50 Wood, no Wool; player E cannot upgrade; NPC walks/works.
11. Build ordinary House/Farm/Butcher using Builder; player cannot directly work them, no instant completion.
12. Verify one Builder/job; add another Hut/Builder and run separate jobs concurrently.
13. Build Butcher L1, confirm automatic Butcher staffing when an unemployed adult is available (otherwise deliberately unassign/hire): Meat only; upgrade L2 without Wool and confirm 1 Wool per 10 active simulation seconds.
14. Confirm Butcher next Wool costs50, then75 and100; no curve reset.
15. Build House/Tavern, verify automatic preferred occupancy (House before Tavern before Hall), total/housed/homeless counts and each citizen’s residence; assignments remain stable across nights without teleporting.
16. Accumulate Wood300/Wool100, request TH3 with Hut2 Builder, finish1800 base work; cumulative11 types.
17. Build Mine L1 with Hut2 Builder and produce Stone before trying Hut3 cost.
18. Upgrade Hut3, Mine2/3; ensure TH cap and Hut target-level restrictions cannot be bypassed.
19. Request TH4 for450Wood/250Wool/150Stone with Hut3 Builder; finish2400 base work, then cumulative13 types.
20. Upgrade Hut4 with no Iron, Mine3->4 with no Iron; assign Miner and confirm Iron only begins atL4.
21. Confirm Mine4->5 and Hut4->5 each include50Iron.
22. Request TH5 for600Wood/350Wool/250Stone/100Iron with Hut4 Builder; finish3000 base work, then max state; no new type.
23. Upgrade Hut5 and normal buildings toL5; no upgrade beyond5 and no footprint/influence change.
24. Inspect every initial/upgrade recipe against generated table and missing-resource UI; failed payment changes no partial totals.
25. Confirm base-time table and upgrade target multipliers; strong/weak Builders affect duration without bypassing gates.
26. Farm L1..5: verify unlocked crops/cycle times/quantities, including simultaneous old crops and L5 5/4/3/2.
27. Verify Meat/Wool, Fish, Wood and Stone/Iron cycles; no production without an assigned appropriate worker.
28. Run two identical producers and verify independent progress/output.
29. Confirm all ten resources in Hall, obtainable amounts versus Requirements are not met yet; Warehouse does not double totals.
30. Observe Day420/Night180 at1x; phase progress/day counter at correct boundaries.
31. At night production and NPC construction/demolition stop while player E on initial L1 Huts continues; partial progress remains; no new ambient trip.
32. Verify pending gathering deliveries finish before returning home and homes do not consume imaginary capacity.
33. Housed citizens walk to exterior, sleep logically inside and disappear; morning exit returns through currently valid access.
34. Block an entrance and check safe fallback/recovery; footprints never become ordinary walkable ground.
35. Create housing loss and confirm homeless citizens remain outside near Hall, not deleted/teleported.
36. Observe smooth readable darkening, unchanged UI, occupied sleeping window lights and unoccupied/daytime lights off.
37. Verify per-resident moon/dream phases and gentle presentation; only actual sleepers.
38. Wildlife rests off-screen at night; morning resumes valid Wilderness, including expansion/recovery cases.
39. Observe temporary independent localized Wilderness firefly swarms, smooth group twinkling/expiry, no claimed/building spawn or bright glare.
40. Test1x/2x/4x/8x/16x: clock/jobs/production/trips/expiry accelerate; UI/camera input remains usable without movement jumps.
41. Build Tavern and advance days: independent10% roll per completed Tavern once/day, never per frame or one settlement-wide roll.
42. Use multiple Taverns; confirm separate candidates may arrive on same day; level affects quality/requests.
43. Inspect candidate identity/attributes/request/remaining days; candidate not in population/residence before recruitment.
44. Confirm three full day stay and expiry removal; no retained departed history.
45. Low-tier Tavern must not request future resources; exact bundle remains stable during stay.
46. Gather complete bundle; recruit with free housing; payment once, same person added to roster, assigned residence and visible outdoors.
47. Repeat Recruit and check no double charge/addition; insufficient bundle changes no partial resource.
48. Fill residential capacity; recruitment must be blocked without payment; expand housing or dismiss first.
49. Select citizen, click dismissal and inspect named confirmation; only confirm intentionally.
50. Confirm permanent disappearance, freed housing, cleared workplace/gather/job references and no automatic return/replacement.
51. Dismiss a busy Builder, assign replacement; paid unfinished job resumes retained progress without duplicate payment.
52. Inspect upgraded building cumulative refund and confirmed timed NPC demolition; no early refund/functionality loss, odd invested amounts floor per resource only at completion.
53. Confirm demolished footprint is freed but territory stays claimed; rebuilding grants only genuinely unclaimed influence.
54. Demolish workplace/home/Tavern: workerUnemployed, residence reassignment/homeless, waiting candidatesleave, permanent citizensremain.
55. Retest B/R/E/H/Escape, panel scrolling/close/click-through, camera, Tile outlines, wildlife flee/evacuation and trapped-citizen recovery.
56. Repeat at smaller viewport sizes: every scrollable management action/confirmation/Close remains reachable.
57. Check profession equipment and active worker/player construction feedback without identity changes or heavy particles.
58. Restart Play Mode for initial reset; smoke-test unchanged MainMenu/profile/settings routes and Console errors.

Nothing is staged, committed or pushed as part of this milestone.

## Living settlement acceptance checks

- Start with all resources zero. Establish a manually funded Hut and staffed Farm before prolonged starvation. Verify ordinary placement never pays or selects a Builder, even when stock is available.
- Place several sites. Explicitly Start, Pause, Reassign and Resume. Each job has one Builder, each Builder one job, permanent workplace stays the Hut, and materials/progress survive interruption. Verify busy-Builder replacement is explicit and failed Start spends nothing.
- Hire/unassign workers at night; assignments succeed but NPC work waits. Hall has no worker position. Unstaffed producers remain silent.
- Verify Wood10s, Stone20s, Wool10s and Iron40s, plus the balanced Farm table over several normal days. Compare actual stored quantity in temporary feedback. Full storage must report blockage, never successful phantom output.
- Inspect daily nutrition, fractional credit, adult/child needs, shortage satiety, five-day grace, gradual damage/recovery and eventual death cleanup. Death may reduce population below four; voluntary adult dismissal may not.
- Observe automatic pairs, free-housing/three-day-food/cooldown birth gates, persistent child identity/parents/attributes, 18-day adulthood and child-only wandering/sleeping. Children cannot work/gather/dismiss. Verify Tavern food/housing/payment gates and independent 10% daily rolls.
- Fill storage and refund cancelled funded sites at/above capacity: the full material bundle appears once. Demolish a Warehouse: old stock and half investment refund survive capacity loss. Other resources continue accumulating. Spend below capacity and confirm capped production/gathering resumes.
- Watch Builder morning preparation/site loops/supply trips, worker breaks/interior exits and evening home travel. No persistent entrance stacking, profession flicker, teleports or permanent world labels. Fisher should work beside the pond. Fireflies remain stationary in forest/Wilderness with tiny warm fade/plateau/fade patches.
- Repeat input/Help/selection, viewport scrolling, camera, rotation, navigation recovery, unchanged MainMenu/profile routes and Console checks. No visual/Play Mode verification is claimed by source compilation or standalone tests.

## Files changed for this milestone

New: `World/SettlementLiving.cs`, `Tests/Editor/SettlementLivingTests.cs` under Assets/ColonyGame, with metadata generated by Unity on import.

Modified for living settlement: World/AmbientWoodGathering.cs, BuildingEconomyRules.cs, Citizen.cs, SettlementBootstrap.cs, SettlementEconomy.cs, SettlementPopulation.cs; presentation CitizenView.cs, PrototypeHelp.cs, PrototypeHud.cs, SettlementAmbience.cs, SettlementPrototypeView.cs; tests PlayerInteractionTests.cs, PrototypeHelpStateTests.cs, SettlementBootstrapTests.cs, SettlementEconomyTests.cs; both authoritative documents.

Previously pending corrective changes in Editor/SettlementPrototypeSetup.cs, PrototypeCamera.cs, SchematicBuildingView.cs and World/PlayerInteraction.cs are preserved. Scenes, packages, ProjectSettings, existing GUIDs and Git configuration are unchanged. No staging, commit or push.

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
