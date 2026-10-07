using System;
using System.Collections.Generic;
using System.Linq;
using ColonyGame.World;

namespace ColonyGame.Presentation
{
    // Presentation-only state. It has no settlement reference and cannot pause the simulation.
    public sealed class PrototypeHelpState
    {
        public bool IsOpen { get; private set; }
        public bool AllowsGameplayShortcuts => !IsOpen;
        public void Toggle() { IsOpen=!IsOpen; }
        public void Close() { IsOpen=false; }
        // A handled Help key consumes this frame, including the frame which closes Help.
        public bool HandleKeys(bool toggle,bool escape)
        {
            if(toggle) { Toggle(); return true; }
            if(escape&&IsOpen) { Close(); return true; }
            return false;
        }
    }

    public sealed class PrototypeHelpSection
    {
        public string Title { get; }
        public IReadOnlyList<string> Entries { get; }
        public PrototypeHelpSection(string title,params string[] entries)
        {
            Title=title;
            Entries=Array.AsReadOnly(entries);
        }
    }

    // Structured reference content is separate from input and rendering. Balance/catalog reads
    // are read-only so hints follow the configured prototype values without duplicating rules.
    public static class PrototypeHelpContent
    {
        public static IReadOnlyList<PrototypeHelpSection> Create(BootstrapSettings balance,int townHallLevel)
        {
            var sections=new List<PrototypeHelpSection>
            {
                new PrototypeHelpSection("START HERE",
                    $"Start at Town Hall Level 1 with four adult residents, capacity 4/4, and {balance.StartingWood} Wood.",
                    $"Unemployed adults gather nearby Wilderness Wood: {balance.SuccessChance:P0} success, +{balance.WoodYield} Wood only after returning to Town Hall.",
                    $"Build any number of L1 Builder's Huts for {balance.HutWoodCost} Wood each. Select the site and hold E during Day for {balance.PlayerWorkSeconds:0} base work seconds.",
                    "After completion, choose Assign Worker and explicitly select an unemployed adult as Builder. No automatic staffing."),
                new PrototypeHelpSection("CONTROLS",
                    "WASD / Arrow Keys: camera pan. Middle mouse drag: pan. Mouse wheel: zoom. UI scrolling does not zoom the world.",
                    "Left Click: select a citizen/building, choose a roster entry, or confirm a valid blueprint.",
                    "B: Build menu. R: rotate footprint by 90 degrees. Escape: close Help first, otherwise cancel placement and inspection.",
                    "E = Interact / Action. Press E for Town Hall/building management or citizen inspection. Hold E only for initial L1 Hut construction.",
                    "H / Help button: toggle this scrollable reference. Help blocks B/R/E world actions; simulation continues.",
                    "Use 1x / 2x / 4x / 8x / 16x HUD buttons to accelerate simulation without accelerating UI input."),
                new PrototypeHelpSection("PROGRESSION",
                    "Town Hall starts L1 and reaches L5. Upgrades require resource payment only in this prototype; quest prerequisites are future design.",
                    "Town Hall: L2 150 Wood; L3 300 Wood +100 Wool; L4 450 Wood +250 Wool +150 Stone; L5 600 Wood +350 Wool +250 Stone +100 Iron.",
                    "Unlocks are cumulative. TH5 adds no new building type. Buildings cannot exceed the current Town Hall level.",
                    "The next upgrade price and missing resources appear in management. Maximum building level is 5."),
                new PrototypeHelpSection("BUILDERS AND CONSTRUCTION",
                    "The player builds only initial L1 Builder's Huts, even while NPC Builders are busy. NPC Builders construct ordinary buildings and perform all upgrades.",
                    "A Builder's Hut must reach the target level for other upgrades. Its Builder may upgrade their own Hut by one level if Town Hall permits.",
                    "One Builder per job and one job per Builder. Different Builders work independently. Builders walk to access before working.",
                    "Base work: Hut/House 5 min; Farm/Guard Tower 6; Fisher 7; Tavern/Butcher 8; Warehouse/Barracks 10; Lumber Mill/Mine 12; Laboratory 15; Mage site 18.",
                    "Upgrade work = base seconds x target level. Hut advantage and Strength/Dexterity/Endurance change work speed, never progression gates.",
                    "Hut L2 costs 50 Wood without Wool. Hut L3 costs 75 Wood +50 Wool +25 Stone. Hut L4 costs 100 Wood +75 Wool +50 Stone without Iron. Hut L5 adds 50 Iron.",
                    "Butcher L2 defers Wool; L3 requires 50 Wool. Mine L4 defers Iron; L5 requires 50 Iron. Deferred components retain the normal later curve."),
                new PrototypeHelpSection("RESOURCES AND PRODUCTION",
                    "One authoritative settlement inventory. Town Hall is initial storage; Warehouse hauling/logistics is future work.",
                    "All known resources appear in Town Hall; unavailable resources show Requirements are not met yet.",
                    "An assigned corresponding worker must reach a completed workplace. Each unlocked product has an independent cycle, including all older Farm crops.",
                    "Quantity per cycle = building level - product unlock level + 1; cycle time stays fixed.",
                    "Farm: Beetroot L1/15s, Wheat L2/30s, Potato L3/45s, Carrot L4/60s.",
                    "Butcher: Meat L1/30s and Wool L2/45s. Fisher: Fish L1/30s. Lumber Mill: Wood L1/30s. Mine: Stone L1/45s and Iron L4/60s.",
                    "No automatic production is invented for Guard Tower, Barracks, Laboratory, Mage site, Tavern, Warehouse or Builder's Hut."),
                new PrototypeHelpSection("HOUSING AND NIGHT",
                    "Town Hall houses 4/5/6/7/8 citizens at L1..L5. House houses 2/3/4/5/6. Tavern houses 3/4/5/6/7.",
                    "Town Hall roster shows population/capacity, employment, homelessness and residences. Residence assignment changes the link without teleporting the citizen.",
                    "At 1x, Day lasts 7 minutes and Night 3 minutes. Production, construction and new ambient trips pause at Night; partial progress remains.",
                    "Pending Wood returns first. Citizens walk home, enter via exterior access, and disappear outdoors while sleeping. Morning restores them at valid access.",
                    "Homeless citizens remain outside near Town Hall. No imaginary housing and no deletion.",
                    "Night darkens gently; actual sleeping residences show warm windows and schematic dreams. Peaceful wildlife rests off-screen; sparse Wilderness fireflies appear."),
                new PrototypeHelpSection("TAVERN VISITORS",
                    "Each completed Tavern independently rolls 10% once per completed game day. More Taverns increase arrival frequency; several may attract separate visitors on the same day.",
                    "Tavern level improves average candidate quality and request tier. Visitors are not population or residents before recruitment.",
                    "Visitors remain for three full game days, then leave. Inspect their identity, five attributes, remaining days and requested goods.",
                    "Requests use a few actually obtainable resources allowed by Tavern tier. Target value is 25 x Tavern level, adjusted within bounded quality limits.",
                    "Recruit consumes the whole bundle once and requires free residential capacity. No automatic recruitment or replacement.",
                    "Permanently dismiss a chosen citizen only after the named confirmation. They leave the roster, housing and workplace permanently."),
                new PrototypeHelpSection("TERRITORY AND DEMOLITION",
                    "A footprint must be wholly inside claimed territory and free of reserved buildings. Town Hall is the starting 5 x 5 anchor, not normally placeable.",
                    "Initial completion claims footprint plus 3 Tiles each side, once. Upgrades never enlarge the footprint or repeat influence.",
                    "Demolish completed ordinary buildings after confirmation. Refund is floor(50%) of each resource actually invested in construction and completed upgrades.",
                    "Claims stay permanent. Workers lose the workplace; residents reassign or become homeless. A demolished Tavern's waiting candidates leave."),
                new PrototypeHelpSection("CITIZENS AND WILDLIFE",
                    "Identity, sex, age and five attributes persist through work changes. Profession clothing/tools change without changing identity.",
                    "Idle wandering stays in settlement territory. Authorized gathering uses the actual irregular boundary, up to 10 Tiles beyond it.",
                    "Footprints remain outdoor obstacles. Continuous recovery handles trapped citizens; impossible recovery alone uses a logged emergency fallback.",
                    "Squirrel, Badger, Deer and Fox live in Wilderness, flee approaching citizens and evacuate new claims. They supply no resources. Predators/families/children/quests are future work.")
            };
            for(int level=1;level<=4;level++)
                sections.Add(new PrototypeHelpSection($"TOWN HALL LEVEL {level} - NEW UNLOCKS",
                    BuildingCatalog.All.Where(d=>d.IsPlayerPlaceable&&d.RequiredTownHallLevel==level)
                        .Select(d=>$"{d.DisplayName} - {d.BaseWidth} x {d.BaseDepth}").ToArray()));
            return sections.AsReadOnly();
        }
    }
}