using System;
using System.Collections.Generic;
using System.Linq;
using ColonyGame.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColonyGame.Presentation
{
    // Runtime-only prototype interface. Transparent layout elements never consume world input.
    public sealed class PrototypeHud
    {
        private readonly GameObject host;
        private readonly PanelSettings settings;
        private readonly VisualElement root;
        private readonly PrototypeHelpState helpState = new PrototypeHelpState();
        private readonly VisualElement helpPanel;
        private readonly ScrollView helpScroll;
        private readonly Button helpButton;
        private int helpInputFrame = -1;
        public bool HelpOpen => helpState.IsOpen;
        public bool BlocksGameplayShortcuts => !helpState.AllowsGameplayShortcuts || helpInputFrame == Time.frameCount;
        private SettlementBootstrap bootstrap;
        private Action<Citizen> inspectCitizen;
        private VisualElement management;
        private Label managementInfo;
        private ScrollView roster;
        private Button assignButton;
        private Button upgradeButton,demolishButton,dismissButton,citizenUnassign,citizenMove;
        private CitizenManagement citizenManagement;
        private Label economyInfo;
        private readonly Dictionary<BuildingType,Button> buildEntries=new Dictionary<BuildingType,Button>();
        private readonly List<System.Tuple<RecruitmentCandidate,Button>> recruitButtons=new List<System.Tuple<RecruitmentCandidate,Button>>();
        private string rosterSignature;
        private Citizen inspectedCitizen;
        private bool confirmDemolition,confirmDismissal;
        private BuildingState managedBuilding;
        private bool choosingWorker, choosingJobBuilder, allowJobReassignment;

        private readonly VisualElement buildList;
        private readonly VisualElement placement;
        private readonly VisualElement citizenPanel;
        private readonly Label citizenInfo;
        public event Action BuildListOpened;
        private readonly Label counts;
        private readonly Label selection;
        private readonly Label validity;
        private readonly Button buildButton;
        private readonly HashSet<VisualElement> blockingPanels = new HashSet<VisualElement>();
        private int pointerInputFrame = -1;
        public bool BuildListOpen { get; private set; }
        public BuildingState ManagedBuilding => managedBuilding;

        public PrototypeHud(Transform parent, IEnumerable<BuildingDefinition> definitions, Action<BuildingDefinition> select, Action cancel)
        {
            host = new GameObject("Prototype UI");
            host.transform.SetParent(parent, false);
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "Runtime Prototype Panel";
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("PrototypeTheme");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.RegisterCallback<PointerDownEvent>(_ => pointerInputFrame = Time.frameCount, TrickleDown.TrickleDown);

            var column = new VisualElement { name = "left-interface", pickingMode = PickingMode.Ignore };
            column.style.position = Position.Absolute;
            column.style.left = column.style.top = 12;
            column.style.width = 280;
            column.style.maxWidth = Length.Percent(90);
            column.style.maxHeight = Length.Percent(96);
            root.Add(column);

            var hud = Panel("compact-hud", column);
            counts = Text(hud, "");
            Text(hud, "WASD: pan | Drag MMB | Wheel: zoom").style.fontSize = 11;
            buildButton = new Button(() => SetBuildListOpen(!BuildListOpen)) { text = "Build (B)" };
            hud.Add(buildButton);
            helpButton = new Button(ToggleHelp) { text = "Help [H]" };
            hud.Add(helpButton);

            buildList = Panel("build-list", column);
            buildList.style.flexShrink = 1;
            buildList.style.minHeight = 80;
            buildList.style.maxHeight = 400;
            Text(buildList, "Choose a building");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            buildList.Add(scroll);
            foreach (var definition in BuildingCatalog.All.Where(d=>d.IsPlayerPlaceable))
            {
                var entry = new Button(() =>
                {
                    pointerInputFrame = Time.frameCount;
                    select(definition);
                    SetBuildListOpen(false);
                });
                entry.style.backgroundColor = new Color(0.16f, 0.21f, 0.26f);
                entry.style.minHeight = 44;
                entry.style.marginBottom = 3;
                entry.Add(TextElement(definition.DisplayName));
                var details = TextElement($"{definition.BaseWidth} x {definition.BaseDepth} | Requires TH {definition.RequiredTownHallLevel} | {BuildingEconomyRules.Describe(BuildingEconomyRules.Cost(definition.Type))}");
                details.style.fontSize = 11;
                entry.Add(details);
                scroll.Add(entry);
                buildEntries.Add(definition.Type,entry);
            }
            placement = Panel("placement-status", column);
            placement.style.flexShrink = 0;
            selection = Text(placement, "");
            validity = Text(placement, "");
            Text(placement, "R: Rotate | Esc: Cancel").style.fontSize = 11;
            placement.Add(new Button(() => { pointerInputFrame = Time.frameCount; cancel(); }) { text = "Cancel" });
            placement.style.display = DisplayStyle.None;
            citizenPanel = Panel("citizen-info", column);
            citizenInfo = Text(citizenPanel, "");
            citizenPanel.style.flexShrink = 1;
            citizenPanel.style.display = DisplayStyle.None;
            SetBuildListOpen(false);
            helpPanel = Panel("help-hints", root);
            helpPanel.style.position = Position.Absolute;
            helpPanel.style.right = helpPanel.style.top = 12;
            helpPanel.style.width = 420;
            helpPanel.style.maxWidth = Length.Percent(90);
            helpPanel.style.maxHeight = Length.Percent(90);
            var title = Text(helpPanel, "Help & Hints");
            title.style.fontSize = 19;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            helpScroll = new ScrollView(ScrollViewMode.Vertical);
            helpScroll.style.flexShrink = 1;
            helpScroll.style.minHeight = 0;
            helpPanel.Add(helpScroll);
            Text(helpPanel, "Prototype - mechanics and balance may change.").style.fontSize = 11;
            helpPanel.Add(new Button(CloseHelp) { text = "Close (H / Escape)" });
            helpPanel.style.display = DisplayStyle.None;
        }

        private VisualElement Panel(string name, VisualElement parent)
        {
            var panel = new VisualElement { name = name, pickingMode = PickingMode.Position };
            panel.style.backgroundColor = new Color(0.08f, 0.11f, 0.14f, 0.96f);
            panel.style.paddingLeft = panel.style.paddingRight = 8;
            panel.style.paddingTop = panel.style.paddingBottom = 6;
            panel.style.marginBottom = 6;
            parent.Add(panel);
            blockingPanels.Add(panel);
            return panel;
        }
        private static Label TextElement(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false };
            label.style.fontSize = 13;
            label.style.color = Color.white;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
        private static Label Text(VisualElement parent, string text)
        {
            var label = TextElement(text);
            parent.Add(label);
            return label;
        }
        public void SetBuildListOpen(bool open)
        {
            if (open && !BuildListOpen) BuildListOpened?.Invoke();
            BuildListOpen = open;
            buildList.style.display = open && !HelpOpen ? DisplayStyle.Flex : DisplayStyle.None;
            buildButton.text = open ? "Close list (B)" : "Build (B)";
        }
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (pointerInputFrame == Time.frameCount) return true;
            if (root.panel == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            for (var picked = root.panel.Pick(point); picked != null; picked = picked.parent)
                if (blockingPanels.Contains(picked)) return true;
            return false;
        }
        public void Update(SettlementState state, BuildingDefinition definition, int orientation, string status, int wildlifeCount = 0, int citizenCount = 0)
        {
            RefreshManagement();
            if(bootstrap?.Economy!=null)
            {
                foreach(var entry in buildEntries) entry.Value.style.display=BuildingCatalog.Get(entry.Key).RequiredTownHallLevel<=state.TownHallLevel?DisplayStyle.Flex:DisplayStyle.None;
                economyInfo.text=$"Day {bootstrap.Economy.Clock.Day} | {(bootstrap.Economy.Clock.IsDay?"Day":"Night")} {bootstrap.Economy.Clock.PhaseProgress:P0} | {bootstrap.Economy.Clock.Speed}x";
            }
            counts.text = $"Claimed Tiles: {state.Territory.Count} | Buildings: {state.Buildings.Count}\nTown Hall Level: {state.TownHallLevel} | Wildlife: {wildlifeCount}\nCitizens: {citizenCount}\nWood: {bootstrap?.Resources.Amount(ResourceType.Wood) ?? 0}";
            placement.style.display = definition == null || HelpOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if (definition == null) return;
            var footprint = definition.GetFootprint(orientation);
            selection.text = $"{definition.DisplayName}\nFootprint: {footprint.Width} x {footprint.Depth}";
            validity.text = status;
            validity.style.color = status.StartsWith("VALID") ? new Color(0.4f, 1f, 0.5f) : Color.white;
        }
        public void ShowCitizen(Citizen citizen)
        {
            if(inspectedCitizen!=citizen) {inspectedCitizen=citizen;confirmDismissal=false;}
            if(dismissButton!=null) dismissButton.text=confirmDismissal?$"Confirm permanent dismissal of {citizen?.FirstName}":"Dismiss citizen permanently";
            citizenPanel.style.display = citizen == null || HelpOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if(citizen == null) return;
            dismissButton?.SetEnabled(bootstrap.Economy.CanDismiss(citizen));
            if(citizenUnassign!=null) citizenUnassign.style.display=citizen.WorkplaceBuildingId!=null?DisplayStyle.Flex:DisplayStyle.None;
            citizenMove?.SetEnabled(citizenManagement.CanMoveToHall(citizen));
            var a=citizen.Attributes;
            citizenInfo.text=$"{citizen.FirstName}\nSex: {citizen.Sex} | Age: {citizen.Age}\nProfession: {citizen.Profession}\nWorkplace: {bootstrap?.WorkplaceName(citizen) ?? "None"}\nResidence: {bootstrap?.Settlement.Buildings.FirstOrDefault(b=>b.Id==citizen.ResidenceBuildingId)?.Definition.DisplayName ?? "Homeless"}\nTask: {citizen.Task}\nHealth: {citizen.Health:F0} | Satiety: {citizen.Satiety:P0} | Hungry days: {citizen.HungryDays}\nFamily: {citizen.PartnerId??"None"} | Parents: {citizen.ParentOneId??"None"}, {citizen.ParentTwoId??"None"}\nStrength: {a.Strength}\nDexterity: {a.Dexterity}\nIntelligence: {a.Intelligence}\nEndurance: {a.Endurance}\nCharisma: {a.Charisma}";
        }
        public void BindBootstrap(SettlementBootstrap state,Action<Citizen> inspect)
        {
            bootstrap=state; inspectCitizen=inspect;
            citizenManagement=new CitizenManagement(state,inspect);
            economyInfo=Text(buildButton.parent,"");
            var speedRow=new VisualElement();speedRow.style.flexDirection=FlexDirection.Row;buildButton.parent.Add(speedRow);
            foreach(int speed in new[]{1,2,4,8,16})
            {
                int value=speed;
                speedRow.Add(new Button(()=>bootstrap.Economy.Clock.SetSpeed(value)){text=value+"x"});
            }
            dismissButton=new Button(()=>
            {
                pointerInputFrame=Time.frameCount;
                if(inspectedCitizen==null) return;
                if(!confirmDismissal) {confirmDismissal=true;return;}
                if(bootstrap.Economy.Dismiss(inspectedCitizen)) {inspectCitizen(null);ShowCitizen(null);rosterSignature=null;}
            });
            citizenUnassign=new Button(()=>
            {
                pointerInputFrame=Time.frameCount;
                if(citizenManagement.Unassign(inspectedCitizen)) {rosterSignature=null;ShowCitizen(inspectedCitizen);RefreshManagement();}
            }){name="citizen-unassign-worker",text="Unassign Worker"};
            citizenMove=new Button(()=>
            {
                pointerInputFrame=Time.frameCount;
                if(citizenManagement.MoveToHall(inspectedCitizen)) {rosterSignature=null;ShowCitizen(inspectedCitizen);RefreshManagement();}
            }){name="citizen-move-to-hall",text="Move to Town Hall"};
            citizenPanel.Add(citizenUnassign);citizenPanel.Add(citizenMove);
            citizenPanel.Add(dismissButton);
            PopulateHelp(PrototypeHelpContent.Create(state.Settings,state.Settlement.TownHallLevel));
            management=Panel("building-management",root);
            management.style.position=Position.Absolute;
            management.style.right=12; management.style.top=12;
            management.style.width=320; management.style.maxWidth=Length.Percent(45);
            management.style.maxHeight=Length.Percent(90);
                        var body=new ScrollView(ScrollViewMode.Vertical);
            body.style.flexShrink=1;body.style.minHeight=0;
            management.Add(body);
            managementInfo=Text(body,"");
            assignButton=new Button(()=> { choosingWorker=true; choosingJobBuilder=false; rosterSignature=null; RefreshManagement(); }) { text="Assign Worker" };
            body.Add(assignButton);
            var unassign=new Button(()=>{bootstrap.UnassignWorker(managedBuilding);rosterSignature=null;RefreshManagement();}){name="unassign-worker",text="Unassign Worker"};body.Add(unassign);
            var inspectWorker=new Button(()=>inspectCitizen(bootstrap.WorkerAt(managedBuilding))){name="inspect-worker",text="Inspect Assigned Worker"};body.Add(inspectWorker);
            var pause=new Button(()=>{var job=bootstrap.Economy.JobAt(managedBuilding);if(job?.Builder!=null)bootstrap.Economy.ReleaseBuilder(job.Builder);rosterSignature=null;RefreshManagement();}){name="pause-job",text="Pause / Release Builder"};body.Add(pause);
            upgradeButton=new Button(()=>
            {
                pointerInputFrame=Time.frameCount;
                if(managedBuilding==bootstrap.Settlement.TownHall) bootstrap.Economy.TryUpgradeTownHall();
                else bootstrap.Economy.TryUpgrade(managedBuilding);
                rosterSignature=null;RefreshManagement();
            }){text="Upgrade"};
            body.Add(upgradeButton);
            var fundButton=new Button(()=>{bootstrap.Economy.Fund(managedBuilding);RefreshManagement();}) {text="Start manual L1 Hut construction (pay once)"};
            fundButton.name="fund-construction";body.Add(fundButton);
            var jobBuilderButton=new Button(()=>{choosingWorker=true;choosingJobBuilder=true;allowJobReassignment=false;rosterSignature=null;RefreshManagement();}) {text="Choose available Builder / Start"};
            jobBuilderButton.name="job-builder";body.Add(jobBuilderButton);
            body.Add(new Button(()=>{choosingWorker=true;choosingJobBuilder=true;allowJobReassignment=true;rosterSignature=null;RefreshManagement();}){name="reassign-job",text="Reassign a busy Builder (pause their previous job)"});
            demolishButton=new Button(()=>
            {
                pointerInputFrame=Time.frameCount;
                if(!confirmDemolition) {confirmDemolition=true;return;}
                if(!managedBuilding.IsCompleted) {if(bootstrap.Economy.CancelConstruction(managedBuilding)) ShowBuilding(null);}
                else if(bootstrap.Economy.Demolish(managedBuilding)) {confirmDemolition=false;RefreshManagement();}
            });
            body.Add(demolishButton);
            roster=new ScrollView(ScrollViewMode.Vertical);
            roster.style.flexShrink=1; roster.style.maxHeight=440;
            body.Add(roster);
            body.Add(new Button(()=>ShowBuilding(null)) { text="Close" });
            management.style.display=DisplayStyle.None;
        }
        public void ShowBuilding(BuildingState building)
        {
            managedBuilding=building; choosingWorker=false; rosterSignature=null;confirmDemolition=false;
            if(management==null) return;
            management.style.display=building==null||HelpOpen?DisplayStyle.None:DisplayStyle.Flex;
            if(building!=null) SetBuildListOpen(false);
            RefreshManagement();
        }
        private string CitizenDescription(Citizen c)
        {
            var a=c.Attributes;
            return $"{c.FirstName} | {c.Sex} | Age {c.Age}\n{c.Profession} | Health {c.Health:F0} | Satiety {c.Satiety:P0} | Hungry days {c.HungryDays}\nWorkplace: {bootstrap.WorkplaceName(c)} | Residence: {bootstrap.Settlement.Buildings.FirstOrDefault(home=>home.Id==c.ResidenceBuildingId)?.Definition.DisplayName??"Homeless"}\nStrength {a.Strength} | Dexterity {a.Dexterity}\nIntelligence {a.Intelligence} | Endurance {a.Endurance} | Charisma {a.Charisma}";
        }
        private void RefreshManagement()
        {
            if(managedBuilding==null||bootstrap?.Economy==null) return;
            if(!bootstrap.Settlement.Buildings.Contains(managedBuilding)) {ShowBuilding(null);return;}
            var b=managedBuilding;var economy=bootstrap.Economy;
            bool hall=b.Type==BuildingType.TownHall;
            int capacity=BuildingEconomyRules.ResidenceCapacity(b);
            string text=$"{b.Definition.DisplayName} | Level {b.Level}\nStatus: {economy.JobStatus(b)}\n";
            if(b.ConstructionStage==ConstructionStage.UnderConstruction)
                text+=$"{(b.IsUpgrading?"Upgrade":"Construction")} {b.ConstructionProgress:P0}\nRemaining work: {b.RequiredConstructionWork-b.ConstructionWork:F1}s\n"+
                    (b.Type==BuildingType.BuildersHut&&!b.IsUpgrading&&economy.JobAt(b)?.Builder==null?"Hold E during Day or Night to build\n":"Assigned Builder works during Day\n");
                        text+=$"Worker: {bootstrap.WorkerAt(b)?.FirstName??"None"}\n";
            var activeJob=economy.JobAt(b);
            if(activeJob!=null)
            {
                text+=$"Job: {activeJob.Kind}\nBuilder: {activeJob.Builder?.FirstName??"None"}\n";
                if(activeJob.Kind==ConstructionJobKind.Construction) text+="Cancellation refund: "+(activeJob.IsFunded?BuildingEconomyRules.Describe(economy.CancellationRefund(b)):"None (unfunded)")+"\n";
                if(activeJob.Kind==ConstructionJobKind.Demolition) text+=$"Demolition: {activeJob.Progress/activeJob.RequiredWork:P0}\n";
                else text+=$"Materials ({(activeJob.IsFunded?"Funded":"Unfunded")}): "+BuildingEconomyRules.Describe(activeJob.Materials)+"\n"+
                    string.Join("\n",activeJob.Materials.Select(x=>$"{x.Key}: {bootstrap.Resources.Amount(x.Key)} available / {x.Value} required"))+"\n";
            }
            management.Q<Button>("fund-construction").style.display=activeJob!=null&&!activeJob.IsFunded&&activeJob.Kind==ConstructionJobKind.Construction&&b.Type==BuildingType.BuildersHut?DisplayStyle.Flex:DisplayStyle.None;
            management.Q<Button>("fund-construction").SetEnabled(activeJob!=null&&bootstrap.Resources.CanPay(activeJob.Materials));
            management.Q<Button>("reassign-job").style.display=activeJob!=null&&activeJob.Builder==null?DisplayStyle.Flex:DisplayStyle.None;
            management.Q<Button>("pause-job").style.display=activeJob?.Builder!=null?DisplayStyle.Flex:DisplayStyle.None;
            management.Q<Button>("unassign-worker").style.display=bootstrap.WorkerAt(b)!=null?DisplayStyle.Flex:DisplayStyle.None;
            management.Q<Button>("inspect-worker").style.display=bootstrap.WorkerAt(b)!=null?DisplayStyle.Flex:DisplayStyle.None;
            management.Q<Button>("job-builder").style.display=activeJob!=null&&activeJob.Builder==null?DisplayStyle.Flex:DisplayStyle.None;
            if(capacity>0)
                text+=$"Residents: {economy.Residences.Residents(b).Count()} / {capacity}\n"+
                    string.Join(", ",economy.Residences.Residents(b).Select(c=>c.FirstName))+"\n";
            var worker=bootstrap.WorkerAt(b);
            if(worker!=null&&b.Type==BuildingType.BuildersHut)
            {
                var current=economy.Jobs.FirstOrDefault(j=>j.Builder==worker);
                text+="Builder job: "+(current==null?"Available":current.Building.Definition.DisplayName+" - "+economy.JobStatus(current.Building))+"\n";
            }
            foreach(var recipe in ResourceCatalog.Production.Where(p=>p.Building==b.Type&&p.UnlockLevel<=b.Level))
                text+=$"{recipe.Resource}: {recipe.Quantity(b.Level)} / {LivingRules.CycleSeconds(recipe,b.Level):F1}s | "+(worker==null?"No worker - no production":bootstrap.Resources.Amount(recipe.Resource)>=bootstrap.Resources.Capacity?"Storage full":economy.Clock.IsDay?"Daytime work / breaks":"Paused At Night")+"\n";
            if(b.Type==BuildingType.Warehouse) text+=$"Storage contribution: {(b.IsCompleted?b.Level*250:0)} per resource\n";
            if(hall)
            {
                text+=$"Adults: {bootstrap.Roster.Count(c=>c.IsAdult)} | Children: {bootstrap.Roster.Count(c=>!c.IsAdult)}\nFood nutrition: {economy.Living.FoodSupply:F1} | Days remaining: {economy.Living.FoodDays:F1}\nAt risk: {bootstrap.Roster.Count(c=>c.HungryDays>0)} | Starvation damage: {bootstrap.Roster.Count(c=>c.HungryDays>LivingRules.StarvationGraceDays)}\n{economy.Living.LatestEvent}\n";
                text+=$"Population: {bootstrap.Roster.Count} / {economy.Residences.Capacity}\nHoused: {bootstrap.Roster.Count-economy.Residences.Homeless} | Employed: {bootstrap.EmployedCount} | Unemployed: {bootstrap.UnemployedCount} | Homeless: {economy.Residences.Homeless}\nStorage: one central inventory\n";
                foreach(ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
                    text+=$"{resource}: {bootstrap.Resources.Amount(resource)} / {bootstrap.Resources.Capacity}"+(ResourceCatalog.Obtainable(bootstrap.Settlement,resource)?"":" | Requirements are not met yet")+"\n";
                text+="Residences:\n"+string.Join("\n",economy.Residences.Buildings.Select(home=>$"{home.Definition.DisplayName} L{home.Level}: {economy.Residences.Residents(home).Count()}/{BuildingEconomyRules.ResidenceCapacity(home)}"))+"\n";
            }
            var cost=b.IsCompleted&&b.Level<5?BuildingEconomyRules.Cost(b.Type,b.Level+1):null;
            bool canUpgrade=activeJob==null&&b.ConstructionStage==ConstructionStage.Completed&&b.Level<5&&(hall||b.Level<bootstrap.Settlement.TownHallLevel);
            if(cost!=null)
            {
                text+="Next upgrade: "+BuildingEconomyRules.Describe(cost)+"\n";
                var missing=cost.Where(x=>bootstrap.Resources.Amount(x.Key)<x.Value).ToDictionary(x=>x.Key,x=>x.Value-bootstrap.Resources.Amount(x.Key));
                text+="Missing: "+(missing.Count==0?"None":BuildingEconomyRules.Describe(missing))+"\n";
                text+=canUpgrade?"":"Requires a higher Town Hall level.\n";
                upgradeButton.SetEnabled(canUpgrade);
            }
            else if(b.Level>=5) text+="Maximum Level 5\n";
            upgradeButton.style.display=cost!=null&&b.ConstructionStage==ConstructionStage.Completed?DisplayStyle.Flex:DisplayStyle.None;
            demolishButton.style.display=!hall&&(activeJob==null||!b.IsCompleted)?DisplayStyle.Flex:DisplayStyle.None;
            demolishButton.SetEnabled(!b.IsCompleted||activeJob==null);
            demolishButton.text=!b.IsCompleted?(confirmDemolition?"Confirm cancellation (full materials refund)":"Cancel unfinished site / full refund: "+BuildingEconomyRules.Describe(economy.CancellationRefund(b))):(confirmDemolition?"Confirm demolition: ":"Demolish / Refund: ")+BuildingEconomyRules.Describe(economy.Refund(b));
            managementInfo.text=text;
            assignButton.text="Assign unemployed adult";
            assignButton.SetEnabled(bootstrap.EligibleWorkers(b).Any());
            assignButton.style.display=BuildingEconomyRules.Profession(b.Type)!=CitizenProfession.Unemployed&&b.IsCompleted&&bootstrap.WorkerAt(b)==null?DisplayStyle.Flex:DisplayStyle.None;
            if(b.IsCompleted&&bootstrap.WorkerAt(b)==null&&BuildingEconomyRules.Profession(b.Type)!=CitizenProfession.Unemployed&&!bootstrap.EligibleWorkers(b).Any())managementInfo.text+="\nNo unemployed adults available. Unassign an existing worker first; permanent dismissal is a separate action.";
            foreach(var item in recruitButtons) item.Item2.SetEnabled(economy.Recruitment.CanRecruit(item.Item1));
            var tavernCandidates=economy.Recruitment.Candidates.Where(c=>c.TavernId==b.Id).ToArray();
            string signature=b.Id+":"+b.Level+":"+string.Join(",",economy.Jobs.Select(j=>j.Building.Id+j.Builder?.Id+j.Kind))+":"+choosingWorker+choosingJobBuilder+allowJobReassignment+":"+economy.Clock.Day+":"+string.Join(",",bootstrap.Roster.Select(c=>c.Id+c.Profession+c.ResidenceBuildingId+c.IsAdult+c.HungryDays+c.Health+c.PartnerId))+":"+string.Join(",",tavernCandidates.Select(c=>c.Citizen.Id));
            if(signature==rosterSignature) return;
            rosterSignature=signature;roster.Clear();recruitButtons.Clear();
            if(hall&&!choosingWorker)
                foreach(var citizen in citizenManagement.LivingRoster)
                {
                    var selected=citizen;
                    var row=new Button(()=>citizenManagement.Select(selected)){text=citizenManagement.RosterLabel(selected)};
                    row.style.minHeight=28;roster.Add(row);
                }
            if(choosingWorker)
            {
                foreach(var citizen in choosingJobBuilder?economy.EligibleJobBuilders(b,allowJobReassignment):bootstrap.EligibleWorkers(b))
                {
                    var selected=citizen;
                    var row=new Button(()=>
                    {
                        pointerInputFrame=Time.frameCount;
                        if(choosingJobBuilder?economy.StartConstruction(b,selected.Id,allowJobReassignment):bootstrap.TryAssignWorker(b,selected.Id)) {choosingWorker=false;rosterSignature=null;RefreshManagement();}
                    }){text=(choosingJobBuilder?(allowJobReassignment?"Reassign Builder: ":"Assign Builder: ")+selected.FirstName+" | Hut L"+bootstrap.Settlement.Buildings.Single(h=>h.Id==selected.WorkplaceBuildingId).Level+" | "+(economy.Jobs.Any(j=>j.Builder==selected)?"Busy":"Available"):CitizenDescription(selected))};
                    row.style.whiteSpace=WhiteSpace.Normal;row.style.minHeight=100;roster.Add(row);
                }
                if(roster.childCount==0) Text(roster,choosingJobBuilder?"No eligible available Builders. Assign an adult to a completed qualifying Hut, or explicitly choose Reassign a busy Builder.":"No unemployed adults available. Unassign employment first; children cannot work.");
            }
            if(choosingWorker) roster.Add(new Button(()=>{choosingWorker=false;rosterSignature=null;RefreshManagement();}){text="Back to management"});
            if(b.Type==BuildingType.Tavern&&!choosingWorker)
            {
                if(tavernCandidates.Length==0) Text(roster,"No visitors today. Each completed Tavern rolls 10% independently each game day.");
                foreach(var candidate in tavernCandidates)
                {
                    var selected=candidate;
                    Text(roster,CitizenDescription(candidate.Citizen)+$"\nRemaining days: {candidate.RemainingDays(economy.Clock.Day)}\nRequest: {BuildingEconomyRules.Describe(candidate.Request)}");
                    var recruit=new Button(()=>{pointerInputFrame=Time.frameCount;if(economy.Recruitment.Recruit(selected)) {rosterSignature=null;RefreshManagement();}}){text="Recruit (payment, housing and 3 days of food required)"};
                    recruit.SetEnabled(economy.Recruitment.CanRecruit(selected));roster.Add(recruit);
                    recruitButtons.Add(System.Tuple.Create(selected,recruit));
                }
            }
        }
        private void PopulateHelp(IEnumerable<PrototypeHelpSection> sections)
        {
            helpScroll.Clear();
            foreach(var section in sections)
            {
                var heading=Text(helpScroll,section.Title);
                heading.style.unityFontStyleAndWeight=FontStyle.Bold;
                heading.style.color=new Color(.5f,.85f,1f);
                heading.style.marginTop=10;
                heading.style.marginBottom=4;
                foreach(var entry in section.Entries)
                {
                    var label=Text(helpScroll,entry);
                    label.style.marginBottom=7;
                }
            }
        }
        public void ToggleHelp()
        {
            helpState.Toggle();
            RefreshHelpVisibility();
        }
        public void CloseHelp()
        {
            helpState.Close();
            RefreshHelpVisibility();
        }
        public bool HandleHelpKeys(bool toggle,bool escape)
        {
            if(!helpState.HandleKeys(toggle,escape)) return false;
            RefreshHelpVisibility();
            return true;
        }
        private void RefreshHelpVisibility()
        {
            // Same-frame guards also prevent a closing click/key leaking into gameplay.
            helpInputFrame=pointerInputFrame=Time.frameCount;
            helpPanel.style.display=HelpOpen?DisplayStyle.Flex:DisplayStyle.None;
            helpButton.text=HelpOpen?"Close Help [H]":"Help [H]";
            buildButton.SetEnabled(!HelpOpen);
            buildList.style.display=BuildListOpen&&!HelpOpen?DisplayStyle.Flex:DisplayStyle.None;
            if(HelpOpen)
            {
                placement.style.display=citizenPanel.style.display=DisplayStyle.None;
                if(management!=null) management.style.display=DisplayStyle.None;
                helpPanel.BringToFront();
            }
            else if(management!=null)
                management.style.display=managedBuilding==null?DisplayStyle.None:DisplayStyle.Flex;
            // Placement/citizen presentation is restored by the normal per-frame refresh.
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(settings);
        }
    }
}
