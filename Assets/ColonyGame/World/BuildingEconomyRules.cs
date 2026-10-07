using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public static class BuildingEconomyRules
    {
        public static IReadOnlyDictionary<ResourceType,int> Cost(BuildingType type,int targetLevel=1)
        {
            if(targetLevel<1||targetLevel>5) throw new ArgumentOutOfRangeException(nameof(targetLevel));
            if(type==BuildingType.TownHall) return TownHallCost(targetLevel);
            var cost=new Dictionary<ResourceType,int>();
            int wood=25,wool=0,stone=0,iron=0;
            switch(type)
            {
                case BuildingType.House: case BuildingType.BuildersHut: break;
                case BuildingType.Farm: wood=50; break;
                case BuildingType.Tavern: case BuildingType.FishermansHut: wood=100; break;
                case BuildingType.GuardTower: wood=75; break;
                case BuildingType.Butcher: wood=150; break;
                case BuildingType.Warehouse: wood=150;wool=25;break;
                case BuildingType.LumberMill: wood=200;wool=100;break;
                case BuildingType.Mine: wood=250;wool=50;break;
                case BuildingType.Barracks: wood=150;wool=100;break;
                case BuildingType.Laboratory: wood=300;stone=100;wool=150;iron=50;break;
                case BuildingType.MageTower: wood=250;stone=50;wool=100;iron=100;break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
            int[] values={wood,wool,stone,iron}; var resources=new[]{ResourceType.Wood,ResourceType.Wool,ResourceType.Stone,ResourceType.Iron};
            int[] introduction={1,2,3,4};
            for(int i=0;i<values.Length;i++)
            {
                int amount=values[i];
                for(int level=2;level<=targetLevel;level++) amount=amount>0?NextAmount(amount):level==introduction[i]?25:0;
                bool deferred=(type==BuildingType.Butcher||type==BuildingType.BuildersHut)&&resources[i]==ResourceType.Wool&&targetLevel==2 ||
                    (type==BuildingType.Mine||type==BuildingType.BuildersHut)&&resources[i]==ResourceType.Iron&&targetLevel==4;
                if(amount>0&&!deferred) cost.Add(resources[i],amount);
            }
            return new System.Collections.ObjectModel.ReadOnlyDictionary<ResourceType,int>(cost);
        }
        public static int NextAmount(int amount)=>checked(amount+(amount<100?25:50));
        public static IReadOnlyDictionary<ResourceType,int> TownHallCost(int target)
        {
            if(target<1||target>5) throw new ArgumentOutOfRangeException(nameof(target));
            var c=new Dictionary<ResourceType,int>();
            if(target==2) c[ResourceType.Wood]=150;
            else if(target==3) {c[ResourceType.Wood]=300;c[ResourceType.Wool]=100;}
            else if(target==4) {c[ResourceType.Wood]=450;c[ResourceType.Stone]=150;c[ResourceType.Wool]=250;}
            else if(target==5) {c[ResourceType.Wood]=600;c[ResourceType.Stone]=250;c[ResourceType.Wool]=350;c[ResourceType.Iron]=100;}
            return new System.Collections.ObjectModel.ReadOnlyDictionary<ResourceType,int>(c);
        }
        public static double BaseSeconds(BuildingType type)
        {
            switch(type)
            {
                case BuildingType.House: case BuildingType.BuildersHut: return 300;
                case BuildingType.Farm: case BuildingType.GuardTower: return 360;
                case BuildingType.FishermansHut: return 420;
                case BuildingType.Tavern: case BuildingType.Butcher:return 480;
                case BuildingType.Warehouse: case BuildingType.Barracks:return 600;
                case BuildingType.LumberMill: case BuildingType.Mine:return 720;
                case BuildingType.Laboratory:return 900;
                case BuildingType.MageTower:return 1080;
                default:return 0;
            }
        }
        public static double BuilderEfficiency(Citizen citizen,int hutLevel,int currentLevel)=>
            (1+.2*Math.Max(0,hutLevel-currentLevel))*(.9+(citizen.Attributes.Strength+citizen.Attributes.Dexterity+citizen.Attributes.Endurance)/150.0);
        public static int ResidenceCapacity(BuildingState b)=>!b.IsCompleted?0:
            b.Type==BuildingType.TownHall?b.Level+3:b.Type==BuildingType.House?b.Level+1:b.Type==BuildingType.Tavern?b.Level+2:0;
        public static CitizenProfession Profession(BuildingType type)
        {
            switch(type)
            {
                case BuildingType.BuildersHut:return CitizenProfession.Builder;
                case BuildingType.Farm:return CitizenProfession.Farmer;
                case BuildingType.Butcher:return CitizenProfession.Butcher;
                case BuildingType.FishermansHut:return CitizenProfession.Fisher;
                case BuildingType.LumberMill:return CitizenProfession.Lumberjack;
                case BuildingType.Mine:return CitizenProfession.Miner;
                case BuildingType.GuardTower:case BuildingType.Barracks:return CitizenProfession.Warrior;
                case BuildingType.Laboratory:return CitizenProfession.Scientist;
                case BuildingType.MageTower:return CitizenProfession.Mage;
                default:return CitizenProfession.Unemployed;
            }
        }
        public static string Describe(IReadOnlyDictionary<ResourceType,int> cost)=>string.Join(", ",cost.Select(x=>$"{x.Value} {x.Key}"));
    }
    public sealed class ProductionDefinition
    {
        public BuildingType Building {get;} public ResourceType Resource {get;} public int UnlockLevel {get;} public double Seconds {get;}
        public ProductionDefinition(BuildingType b,ResourceType r,int level,double seconds) {Building=b;Resource=r;UnlockLevel=level;Seconds=seconds;}
        public int Quantity(int level)=>Math.Max(0,level-UnlockLevel+1);
    }
    public static class ResourceCatalog
    {
        public static IReadOnlyList<ProductionDefinition> Production {get;}=Array.AsReadOnly(new[]{
            new ProductionDefinition(BuildingType.Farm,ResourceType.Beetroot,1,15),new ProductionDefinition(BuildingType.Farm,ResourceType.Wheat,2,30),
            new ProductionDefinition(BuildingType.Farm,ResourceType.Potato,3,45),new ProductionDefinition(BuildingType.Farm,ResourceType.Carrot,4,60),
            new ProductionDefinition(BuildingType.Butcher,ResourceType.Meat,1,30),new ProductionDefinition(BuildingType.Butcher,ResourceType.Wool,2,45),
            new ProductionDefinition(BuildingType.FishermansHut,ResourceType.Fish,1,30),new ProductionDefinition(BuildingType.LumberMill,ResourceType.Wood,1,30),
            new ProductionDefinition(BuildingType.Mine,ResourceType.Stone,1,45),new ProductionDefinition(BuildingType.Mine,ResourceType.Iron,4,60)});
        public static bool Obtainable(SettlementState state,ResourceType resource)=>resource==ResourceType.Wood||Production.Any(p=>p.Resource==resource&&
            state.Buildings.Any(b=>b.IsCompleted&&b.Type==p.Building&&b.Level>=p.UnlockLevel));
        public static bool TavernEligible(SettlementState state,ResourceType resource,int tavernLevel)=>Obtainable(state,resource)&&
            (resource==ResourceType.Wood||Production.Any(p=>p.Resource==resource&&p.UnlockLevel<=tavernLevel&&BuildingCatalog.Get(p.Building).RequiredTownHallLevel<=tavernLevel));
    }
    public sealed class SettlementClock
    {
        public const double DaySeconds=420,NightSeconds=180,CycleSeconds=600;
        public double Elapsed {get;private set;}
        public int Day=>(int)(Elapsed/CycleSeconds)+1;
        public bool IsDay=>Elapsed%CycleSeconds<DaySeconds;
        public double DayProgress=>(Elapsed%CycleSeconds)/CycleSeconds;
        public double PhaseProgress=>IsDay?(Elapsed%CycleSeconds)/DaySeconds:((Elapsed%CycleSeconds)-DaySeconds)/NightSeconds;
        public int Speed {get;private set;}=1;
        public void SetSpeed(int speed) {if(!new[]{1,2,4,8,16}.Contains(speed))throw new ArgumentOutOfRangeException(nameof(speed));Speed=speed;}
        public void Advance(double seconds) {if(!BootstrapSettings.Finite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));Elapsed+=seconds;}
    }
}