using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public static class LivingRules
    {
        public const int ExpectedProductionSecondsPerDay=270;
        public const int ChildhoodDays=18, BirthCooldownDays=10, StarvationGraceDays=5;
        public const double BirthChance=.2, BirthReserveDays=3, StarvationDamagePerDay=20, RecoveryPerDay=10;
        public static double Nutrition(ResourceType resource)
        {
            switch(resource)
            {
                case ResourceType.Beetroot: case ResourceType.Wheat:return 1;
                case ResourceType.Potato: case ResourceType.Carrot:return 1.5;
                case ResourceType.Fish:return 2;
                case ResourceType.Meat:return 2.5;
                default:return 0;
            }
        }
        public static double DailyNeed(Citizen citizen)=>citizen.IsAdult?1:.5;
        public static int StorageCapacity(SettlementState state)=>checked(new[]{200,350,500,750,1000}[state.TownHallLevel-1]+
            state.Buildings.Where(b=>b.Type==BuildingType.Warehouse&&b.IsCompleted).Sum(b=>250*b.Level));
        public static double CycleSeconds(ProductionDefinition recipe,int level)
        {
            if(recipe.Building!=BuildingType.Farm) return recipe.Seconds;
            // Keep all crop identities/unlocks/quantities; normalize total mature-farm nutrition to 5 per level/day.
            double baseline=ResourceCatalog.Production.Where(p=>p.Building==BuildingType.Farm&&p.UnlockLevel<=level)
                .Sum(p=>p.Quantity(level)*Nutrition(p.Resource)*ExpectedProductionSecondsPerDay/p.Seconds);
            return recipe.Seconds*baseline/(5*level);
        }
    }
    public sealed class SettlementLiving
    {
        private readonly SettlementEconomy economy;
        private readonly IGatheringRandom random;
        private int processedDay=1,sequence;
        public double NutritionCredit {get;private set;}
        public string LatestEvent {get;private set;}="";
        public int Births {get;private set;}
        public int Deaths {get;private set;}
        public double DailyDemand=>economy.Bootstrap.Roster.Sum(LivingRules.DailyNeed);
        public double FoodSupply=>NutritionCredit+Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>()
            .Sum(r=>economy.Bootstrap.Resources.Amount(r)*LivingRules.Nutrition(r));
        public double FoodDays=>DailyDemand>0?FoodSupply/DailyDemand:0;
        public SettlementLiving(SettlementEconomy economy,IGatheringRandom random=null)
        {this.economy=economy;this.random=random??new SeededGatheringRandom(4091);}
        public bool HasReserveFor(Citizen newcomer,IReadOnlyDictionary<ResourceType,int> payment=null)
        {
            double after=FoodSupply-(payment?.Sum(p=>p.Value*LivingRules.Nutrition(p.Key))??0);
            return after+1e-9>=LivingRules.BirthReserveDays*(DailyDemand+LivingRules.DailyNeed(newcomer));
        }
        public void ProcessDay(int day)
        {
            if(day<processedDay) throw new ArgumentOutOfRangeException(nameof(day));
            while(processedDay<day)
            {
                processedDay++;
                AllocateFood(processedDay);
                foreach(var child in economy.Bootstrap.Roster.Where(c=>!c.IsAdult).ToArray())
                {
                    child.ChildhoodElapsedDays++;
                    child.Age=Math.Min(18,child.InitialAge+child.ChildhoodElapsedDays);
                    if(child.IsAdult) {LatestEvent=child.FirstName+" became an adult.";economy.Bootstrap.FillAutomaticVacancies();}
                }
                FormFamilies(processedDay);
                foreach(var parent in economy.Bootstrap.Roster.Where(c=>c.Sex==CitizenSex.Male&&c.PartnerId!=null).ToArray())
                    TryBirth(parent,processedDay);
            }
        }
        private void AllocateFood(int day)
        {
            var roster=economy.Bootstrap.Roster.ToArray();double need=roster.Sum(LivingRules.DailyNeed);
            var inventory=economy.Bootstrap.Resources;
            // Consume the smallest units first. Unused nutrition survives as fractional credit, never a physical Food resource.
            foreach(var resource in Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().Where(r=>LivingRules.Nutrition(r)>0)
                .OrderBy(LivingRules.Nutrition).ThenBy(r=>r))
            {
                if(NutritionCredit+1e-9>=need) break;
                int count=Math.Min(inventory.Amount(resource),(int)Math.Ceiling((need-NutritionCredit)/LivingRules.Nutrition(resource)-1e-9));
                if(count>0&&inventory.TryTransaction("meal-"+day+"-"+resource,resource,-count)) NutritionCredit+=count*LivingRules.Nutrition(resource);
            }
            double ratio=need<=0?1:Math.Min(1,NutritionCredit/need);
            NutritionCredit=Math.Max(0,NutritionCredit-need*ratio);
            foreach(var citizen in roster)
            {
                citizen.Satiety=ratio;
                if(ratio>=1-1e-9) {citizen.HungryDays=0;citizen.Health=Math.Min(100,citizen.Health+LivingRules.RecoveryPerDay);}
                else
                {
                    citizen.HungryDays++;
                    if(citizen.HungryDays>LivingRules.StarvationGraceDays)
                        citizen.Health=Math.Max(0,citizen.Health-LivingRules.StarvationDamagePerDay*(1-ratio));
                }
                if(citizen.Health<=0&&economy.RemoveCitizen(citizen))
                {Deaths++;LatestEvent=citizen.FirstName+" died of starvation.";}
            }
        }
        private static bool Related(Citizen a,Citizen b)=>a.Id==b.Id||a.Ancestors.Contains(b.Id)||b.Ancestors.Contains(a.Id)||a.Ancestors.Overlaps(b.Ancestors);
        private void FormFamilies(int day)
        {
            bool Eligible(Citizen c)=>c.Active&&c.IsAdult&&c.Age<=45&&c.PartnerId==null&&c.Health>25&&c.ResidenceBuildingId!=null;
            foreach(var male in economy.Bootstrap.Roster.Where(c=>Eligible(c)&&c.Sex==CitizenSex.Male))
            {
                var partner=economy.Bootstrap.Roster.Where(c=>Eligible(c)&&c.Sex==CitizenSex.Female&&!Related(male,c))
                    .OrderBy(c=>c.ResidenceBuildingId==male.ResidenceBuildingId?0:1).ThenBy(c=>c.Id,StringComparer.Ordinal).FirstOrDefault();
                if(partner==null) continue;
                male.PartnerId=partner.Id;partner.PartnerId=male.Id;
                male.LastBirthDay=partner.LastBirthDay=day;
                LatestEvent=male.FirstName+" and "+partner.FirstName+" formed a family.";
            }
        }
        private void TryBirth(Citizen parent,int day)
        {
            var partner=economy.Bootstrap.Roster.FirstOrDefault(c=>c.Id==parent.PartnerId);
            if(partner==null||!parent.IsAdult||!partner.IsAdult||parent.Age>45||partner.Age>45||
                parent.HungryDays>0||partner.HungryDays>0||parent.Health<=25||partner.Health<=25||
                day-parent.LastBirthDay<LivingRules.BirthCooldownDays||economy.Residences.FreeResidence==null||
                FoodSupply+1e-9<LivingRules.BirthReserveDays*(DailyDemand+.5)) return;
            var home=economy.Residences.Buildings.OrderBy(b=>b.Type==BuildingType.House?0:b.Type==BuildingType.Tavern?1:2)
                .FirstOrDefault(b=>(parent.PreferredResidenceId==null||parent.PreferredResidenceId==b.Id)&&
                    (partner.PreferredResidenceId==null||partner.PreferredResidenceId==b.Id)&&
                    BuildingEconomyRules.ResidenceCapacity(b)-economy.Residences.Residents(b).Count(c=>c!=parent&&c!=partner)>=3);
            if(home==null||random.NextDouble()>=LivingRules.BirthChance) return;
            if(!economy.Residences.AssignAutomatic(parent,home)||!economy.Residences.AssignAutomatic(partner,home)) return;
            int Attribute(int a,int b)=>Math.Max(1,Math.Min(10,(int)Math.Round((a+b)/2.0,MidpointRounding.AwayFromZero)+random.Next(3)-1));
            var a=parent.Attributes;var b=partner.Attributes;
            var child=new Citizen("child-"+(++sequence),new[]{"Robin","Morgan","Alex","Jamie","Rowan","Casey"}[random.Next(6)],random.Next(2)==0?CitizenSex.Male:CitizenSex.Female,0,
                new CitizenAttributes(Attribute(a.Strength,b.Strength),Attribute(a.Dexterity,b.Dexterity),Attribute(a.Intelligence,b.Intelligence),Attribute(a.Endurance,b.Endurance),Attribute(a.Charisma,b.Charisma)),parent.Position);
            child.ParentOneId=parent.Id;child.ParentTwoId=partner.Id;child.ResidenceBuildingId=home.Id;
            child.Ancestors.UnionWith(parent.Ancestors);child.Ancestors.UnionWith(partner.Ancestors);child.Ancestors.Add(parent.Id);child.Ancestors.Add(partner.Id);
            economy.Bootstrap.Simulation.AddCitizen(child);parent.LastBirthDay=partner.LastBirthDay=day;
            Births++;LatestEvent=child.FirstName+" was born to "+parent.FirstName+" and "+partner.FirstName+".";
            economy.Residences.Reconcile();
        }
    }
}
