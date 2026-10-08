using System;
using System.Collections.Generic;
using System.Linq;
using ColonyGame.World;

namespace ColonyGame.Presentation
{
    // Both world selection and Hall roster selection use the same inspection callback.
    public sealed class CitizenManagement
    {
        private readonly SettlementBootstrap bootstrap;
        private readonly Action<Citizen> inspect;
        public CitizenManagement(SettlementBootstrap bootstrap,Action<Citizen> inspect)
        {this.bootstrap=bootstrap;this.inspect=inspect;}
        public IEnumerable<Citizen> LivingRoster=>bootstrap.Roster.Where(c=>c.Active);
        public string RosterLabel(Citizen citizen)=>citizen.FirstName;
        public void Select(Citizen citizen) {if(LivingRoster.Contains(citizen)) inspect(citizen);}
        public bool Unassign(Citizen citizen)=>LivingRoster.Contains(citizen)&&
            bootstrap.UnassignWorker(bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Id==citizen.WorkplaceBuildingId));
        public bool CanMoveToHall(Citizen citizen)=>citizen!=null&&citizen.ResidenceBuildingId!=bootstrap.Settlement.TownHall.Id&&
            bootstrap.Economy.Residences.CanAssign(citizen,bootstrap.Settlement.TownHall);
        public bool MoveToHall(Citizen citizen)=>CanMoveToHall(citizen)&&bootstrap.Economy.Residences.Assign(citizen,bootstrap.Settlement.TownHall);
    }

    public sealed class FireflySwarmSettings
    {
        // Configure before creating the pool; these are presentation seconds, not game-clock work.
        public int MaximumSwarms {get;set;}=6;
        public int PointsPerSwarm {get;set;}=9;
        public int GlowingPoints {get;set;}=3;
        public double GroupSeconds {get;set;}=1.8;
        public double PointFadeSeconds {get;set;}=.25;
        public double MinimumLifetime {get;set;}=8;
        public double MaximumLifetime {get;set;}=15;
        public double SwarmFadeSeconds {get;set;}=1;
        public double SpawnInterval {get;set;}=1.3;
        public double Radius {get;set;}=1.1;
        public double MinimumSeparation {get;set;}=2.5;
        public int PlacementAttempts {get;set;}=80;
        public float PixelSize {get;set;}=1;
        internal void Validate()
        {
            if(MaximumSwarms<1||PointsPerSwarm<1||GlowingPoints<1||GlowingPoints>PointsPerSwarm||PlacementAttempts<1||
                MaximumLifetime<MinimumLifetime||PointFadeSeconds*2>GroupSeconds||
                new[]{GroupSeconds,PointFadeSeconds,MinimumLifetime,MaximumLifetime,SwarmFadeSeconds,SpawnInterval,Radius,MinimumSeparation,PixelSize}
                    .Any(v=>double.IsNaN(v)||double.IsInfinity(v)||v<=0)) throw new ArgumentOutOfRangeException(nameof(FireflySwarmSettings));
        }
    }

    // Pure presentation lifecycle, with a fixed pool and no GameObject allocations.
    public sealed class FireflySwarm
    {
        public bool Active {get;internal set;}
        public int Generation {get;internal set;}
        public CitizenPosition Center {get;internal set;}
        public double Age {get;internal set;}
        public double Lifetime {get;internal set;}
        public CitizenPosition[] Points {get;}
        public double[] Alpha {get;}
        internal int Group=-1;
        internal FireflySwarm(int points) {Points=new CitizenPosition[points];Alpha=new double[points];}
    }

    public sealed class FireflySwarmSystem
    {
        public FireflySwarmSettings Settings {get;}
        public IReadOnlyList<FireflySwarm> Swarms {get;}
        public int Spawned {get;private set;}
        public int Expired {get;private set;}
        private readonly Random random;
        private double untilSpawn;
        private CitizenPosition? lastSpawn;
        public FireflySwarmSystem(int seed=1039,FireflySwarmSettings settings=null)
        {
            Settings=settings??new FireflySwarmSettings();Settings.Validate();random=new Random(seed);
            Swarms=Array.AsReadOnly(Enumerable.Range(0,Settings.MaximumSwarms).Select(_=>new FireflySwarm(Settings.PointsPerSwarm)).ToArray());
        }
        private static double Smooth(double value)
        {value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value);}
        private void Clear(FireflySwarm swarm)
        {if(swarm.Active) Expired++;swarm.Active=false;Array.Clear(swarm.Alpha,0,swarm.Alpha.Length);}
        public void Tick(double seconds,bool day,Func<CitizenPosition?> candidate,Func<CitizenPosition,bool> valid)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if(day) {foreach(var swarm in Swarms) Clear(swarm);untilSpawn=0;return;}
            foreach(var swarm in Swarms)
            {
                if(!swarm.Active) continue;
                swarm.Age+=seconds;
                if(swarm.Age>=swarm.Lifetime||!valid(swarm.Center)) {Clear(swarm);continue;}
                int group=(int)(swarm.Age/Settings.GroupSeconds);
                if(group!=swarm.Group)
                {
                    swarm.Group=group;
                    // New local offsets only between cycles; points never travel during a glow.
                    for(int i=0;i<swarm.Points.Length;i++)
                    {
                        var point=swarm.Center;
                        for(int attempt=0;attempt<Settings.PlacementAttempts;attempt++)
                        {
                            double angle=random.NextDouble()*Math.PI*2,radius=Math.Sqrt(random.NextDouble())*Settings.Radius;
                            var offset=new CitizenPosition(swarm.Center.X+Math.Cos(angle)*radius,swarm.Center.Z+Math.Sin(angle)*radius);
                            if(valid(offset)) {point=offset;break;}
                        }
                        swarm.Points[i]=point;
                    }
                }
                double phase=swarm.Age%Settings.GroupSeconds;
                double glow=Smooth(phase/Settings.PointFadeSeconds)*Smooth((Settings.GroupSeconds-phase)/Settings.PointFadeSeconds)*
                    Smooth(swarm.Age/Settings.SwarmFadeSeconds)*Smooth((swarm.Lifetime-swarm.Age)/Settings.SwarmFadeSeconds);
                int first=(group*Settings.GlowingPoints)%swarm.Points.Length;
                for(int i=0;i<swarm.Points.Length;i++) swarm.Alpha[i]=(i-first+swarm.Points.Length)%swarm.Points.Length<Settings.GlowingPoints&&valid(swarm.Points[i])?glow:0;
            }
            untilSpawn-=seconds;
            if(untilSpawn>0) return;
            untilSpawn=Settings.SpawnInterval*(.8+random.NextDouble()*.4);
            var available=Swarms.FirstOrDefault(s=>!s.Active);
            if(available==null) return;
            for(int attempt=0;attempt<Settings.PlacementAttempts;attempt++)
            {
                var location=candidate();
                if(!location.HasValue||!valid(location.Value)||
                    lastSpawn.HasValue&&lastSpawn.Value.DistanceSquared(location.Value)<.25||
                    Swarms.Any(s=>s.Active&&s.Center.DistanceSquared(location.Value)<Settings.MinimumSeparation*Settings.MinimumSeparation)) continue;
                available.Center=location.Value;available.Age=0;available.Group=-1;
                available.Lifetime=Settings.MinimumLifetime+random.NextDouble()*(Settings.MaximumLifetime-Settings.MinimumLifetime);
                available.Generation=++Spawned;available.Active=true;lastSpawn=location;break;
            }
        }
    }

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
                    $"Start at Town Hall Level 1 with four randomized adult residents, capacity 4/4, and {balance.StartingWood} Wood.",
                    $"Unemployed adults gather nearby Wilderness Wood: {balance.SuccessChance:P0} success, +{balance.WoodYield} Wood only after returning to Town Hall.",
                    $"Build any number of L1 Builder's Huts for {balance.HutWoodCost} Wood each. Select the site, choose Start manual construction to pay once, then hold E during Day or Night for {balance.PlayerWorkSeconds:0} base work seconds.",
                    "Completed workplaces automatically take the first eligible unemployed adult in roster order. Unassign Worker keeps that vacancy manual until you explicitly hire again. Builder jobs always require explicit assignment."),
                new PrototypeHelpSection("CONTROLS",
                    "WASD / Arrow Keys: camera pan. Middle mouse drag: pan. Mouse wheel: zoom. UI scrolling does not zoom the world.",
                    "Left Click: select a citizen/building, choose a roster entry, or confirm a valid blueprint.",
                    "B: Build menu. R: rotate footprint by 90 degrees. Escape: close Help first, otherwise cancel placement and inspection.",
                    "E = Interact / Action. Press E for Town Hall/building management or citizen inspection. Hold E only for initial L1 Hut construction.",
                    "H / Help button: toggle this scrollable reference. Help blocks B/R/E world actions; simulation continues.",
                    "Use 1x / 2x / 4x / 8x / 16x HUD buttons to accelerate simulation without accelerating UI input."),
                new PrototypeHelpSection("PROGRESSION",
                    "Town Hall starts L1 and reaches L5. Upgrades require explicit payment and timed NPC Builder work; a Hut at the current Hall level qualifies. Quest prerequisites are future design.",
                    "Town Hall: L2 150 Wood; L3 300 Wood +100 Wool; L4 450 Wood +250 Wool +150 Stone; L5 600 Wood +350 Wool +250 Stone +100 Iron.",
                    "Unlocks are cumulative. TH5 adds no new building type. Buildings cannot exceed the current Town Hall level.",
                    "The next upgrade price and missing resources appear in management. Maximum building level is 5."),
                new PrototypeHelpSection("BUILDERS AND CONSTRUCTION",
                    "The player builds only initial L1 Builder's Huts, even while NPC Builders are busy. NPC Builders construct ordinary buildings and perform all upgrades.",
                    "A Builder's Hut must reach the target level for other upgrades. Its Builder may upgrade their own Hut by one level if Town Hall permits.",
                    "Placement reserves an unfunded, unassigned site. Choose Builder / Start to pay once. Pause and explicit reassignment retain progress and materials. One Builder per job and one job per Builder. Different Builders work independently. Builders walk to access before working.",
                    "Base work: Town Hall 10 min; Hut/House 5 min; Farm/Guard Tower 6; Fisher 7; Tavern/Butcher 8; Warehouse/Barracks 10; Lumber Mill/Mine 12; Laboratory 15; Mage site 18.",
                    "Upgrade work = base seconds x target level. Hut advantage and Strength/Dexterity/Endurance change work speed, never progression gates.",
                    "Hut L2 costs 50 Wood without Wool. Hut L3 costs 75 Wood +50 Wool +25 Stone. Hut L4 costs 100 Wood +75 Wool +50 Stone without Iron. Hut L5 adds 50 Iron.",
                    "Butcher L2 defers Wool; L3 requires 50 Wool. Mine L4 defers Iron; L5 requires 50 Iron. Deferred components retain the normal later curve."),
                new PrototypeHelpSection("RESOURCES AND PRODUCTION",
                    "One inventory: per-resource Hall capacity 200/350/500/750/1000; completed Warehouses add 250 per level each. Production/gathering clips to free capacity. Refunds preserve overflow; spending remains possible.",
                    "All resource amounts/capacities appear in Town Hall; unavailable producers additionally show Requirements are not met yet.",
                    "An assigned corresponding worker must reach a completed workplace. Each unlocked product has an independent cycle, including all older Farm crops.",
                    "Quantity per cycle = building level - product unlock level + 1. Farm intervals scale to approximately 5 adults of nutrition per level/day.",
                    "Farm crops unlock at L1 Beetroot, L2 Wheat, L3 Potato, L4 Carrot; exact balanced intervals appear in workplace management.",
                    "Butcher: Meat L1/30s; Wool every 10s, locked at L1, quantities 1/2/3/4 at L2/L3/L4/L5. Fisher: Fish L1/30s. Lumber Mill: Wood L1/10s. Mine: Stone L1/20s and Iron L4/40s.",
                    "No automatic production is invented for Guard Tower, Barracks, Laboratory, Mage site, Tavern, Warehouse or Builder's Hut."),
                new PrototypeHelpSection("HOUSING AND NIGHT",
                    "Town Hall houses 4/5/6/7/8 citizens at L1..L5. House houses 2/3/4/5/6. Tavern houses 3/4/5/6/7.",
                    "Town Hall lists living citizen names; selecting a name opens the same left panel as world selection. The panel offers Unassign Worker and Move to Town Hall when a slot is free. Explicit moves persist; others prefer Houses, then Taverns, then Hall.",
                    "At 1x, Day lasts 7 minutes and Night 3 minutes. Production, NPC Builder work and new ambient trips pause at Night; player L1 Hut construction continues; partial progress remains.",
                    "Pending Wood returns first. Citizens walk home, enter via exterior access, and disappear outdoors while sleeping. Morning restores them at valid access.",
                    "Homeless citizens remain outside near Town Hall. No imaginary housing and no deletion.",
                    "Night darkens gently; occupied residences show warm flickering windows and schematic dreams. Peaceful wildlife rests off-screen; short-lived localized forest firefly swarms twinkle in groups of three."),
                new PrototypeHelpSection("TAVERN VISITORS",
                    "Each completed Tavern independently rolls 10% once per completed game day. More Taverns increase arrival frequency; several may attract separate visitors on the same day.",
                    "Tavern level improves average candidate quality and request tier. Visitors are not population or residents before recruitment.",
                    "Visitors remain for three full game days, then leave. Inspect their identity, five attributes, remaining days and requested goods.",
                    "Requests use a few actually obtainable resources allowed by Tavern tier. Target value is 25 x Tavern level, adjusted within bounded quality limits.",
                    "Recruit consumes the whole bundle once and requires free housing and three days of food for the enlarged population. No automatic recruitment or replacement.",
                    "Permanently dismiss an adult only after named confirmation and only while more than four citizens live here. They leave the roster, housing and workplace permanently."),
                new PrototypeHelpSection("TERRITORY AND DEMOLITION",
                    "A footprint must be wholly inside claimed territory and free of reserved buildings. Town Hall is the starting 5 x 5 anchor, not normally placeable.",
                    "Initial completion claims footprint plus 3 Tiles each side, once. Upgrades never enlarge the footprint or repeat influence.",
                    "Cancel unfinished sites immediately with a full funded-material refund. Demolish completed ordinary buildings after confirmation through NPC Builder work (half the L1 base duration). Refund is floor(50%) of each resource actually invested in construction and completed upgrades.",
                    "Claims stay permanent. Workers lose the workplace; residents reassign or become homeless. A demolished Tavern's waiting candidates leave."),
                new PrototypeHelpSection("CITIZENS AND WILDLIFE",
                    "Identity, sex, age and five attributes persist through work changes. Profession clothing/tools change without changing identity.",
                    "Adults need 1 nutrition/day, children 0.5. Beetroot/Wheat=1, Potato/Carrot=1.5, Fish=2, Meat=2.5. Five insufficient days are safe; later damage is gradual. Fed citizens recover. Idle wandering stays in settlement territory. Authorized gathering uses the actual irregular boundary, up to 10 Tiles beyond it.",
                    "Footprints remain outdoor obstacles. Continuous recovery handles trapped citizens; impossible recovery alone uses a logged emergency fallback.",
                    "Squirrel, Badger, Deer and Fox live in Wilderness, flee approaching citizens and evacuate new claims. They supply no resources. Children consume half an adult meal and become adults after 18 game days. Predators/quests are future work.")
            };
            for(int level=1;level<=4;level++)
                sections.Add(new PrototypeHelpSection($"TOWN HALL LEVEL {level} - NEW UNLOCKS",
                    BuildingCatalog.All.Where(d=>d.IsPlayerPlaceable&&d.RequiredTownHallLevel==level)
                        .Select(d=>$"{d.DisplayName} - {d.BaseWidth} x {d.BaseDepth}").ToArray()));
            return sections.AsReadOnly();
        }
    }
    // Pure presentation decisions shared by the runtime adapters and regression tests.
    public static class PrototypePresentationRules
    {
        public static bool HasHat(CitizenProfession profession)=>profession==CitizenProfession.Builder||profession==CitizenProfession.Miner||
            profession==CitizenProfession.Farmer||profession==CitizenProfession.Fisher||profession==CitizenProfession.Butcher;
        public static bool HasTool(CitizenProfession profession)=>profession==CitizenProfession.Builder||profession==CitizenProfession.Lumberjack||
            profession==CitizenProfession.Warrior||profession==CitizenProfession.Mage||profession==CitizenProfession.Fisher||
            profession==CitizenProfession.Miner||profession==CitizenProfession.Farmer||profession==CitizenProfession.Butcher;
        public static bool ShowConstructionPart(double progress,bool roof,bool walls)=>progress>=(roof?.85:walls?.35:.15);
        public static bool ForestCell(int x,int z)=>((x+6)*17+(z+6)*31)%11==0;
        public static bool FireflyVisible(bool day,bool claimed,bool forest,double glow)=>!day&&!claimed&&forest&&glow>.001;
        public static bool WindowLit(bool day,int sleepingResidents)=>!day&&sleepingResidents>0;
        public static double WindowFlicker(double time,double phase)=>.78+.24*Math.Sin(time*2.3+phase)+.12*Math.Sin(time*5.1+phase*1.73);
        public static double FireflyGlow(double phase)
        {
            double Smooth(double t)=>t*t*(3-2*t);
            return phase<.25?Smooth(phase/.25):phase<.45?1:phase<.8?1-Smooth((phase-.45)/.35):0;
        }
    }
}
