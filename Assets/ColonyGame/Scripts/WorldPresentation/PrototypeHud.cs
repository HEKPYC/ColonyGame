using System;
using System.Collections.Generic;
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
        private BuildingState managedBuilding;
        private bool choosingWorker;
        private int rosterEmployment=-1;
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
            foreach (var definition in definitions)
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
                var details = TextElement($"{definition.BaseWidth} x {definition.BaseDepth} | Requires TH {definition.RequiredTownHallLevel}");
                details.style.fontSize = 11;
                entry.Add(details);
                scroll.Add(entry);
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
            counts.text = $"Claimed Tiles: {state.Territory.Count} | Buildings: {state.Buildings.Count}\nTown Hall Level: {state.TownHallLevel} (test override) | Wildlife: {wildlifeCount}\nCitizens: {citizenCount}\nWood: {bootstrap?.Resources.Amount(ResourceType.Wood) ?? 0}";
            placement.style.display = definition == null || HelpOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if (definition == null) return;
            var footprint = definition.GetFootprint(orientation);
            selection.text = $"{definition.DisplayName}\nFootprint: {footprint.Width} x {footprint.Depth}";
            validity.text = status;
            validity.style.color = status.StartsWith("VALID") ? new Color(0.4f, 1f, 0.5f) : Color.white;
        }
        public void ShowCitizen(Citizen citizen)
        {
            citizenPanel.style.display = citizen == null || HelpOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if(citizen == null) return;
            var a=citizen.Attributes;
            citizenInfo.text=$"{citizen.FirstName}\nSex: {citizen.Sex} | Age: {citizen.Age}\nProfession: {citizen.Profession}\nWorkplace: {bootstrap?.WorkplaceName(citizen) ?? "None"}\nTask: {citizen.Task}\nStrength: {a.Strength}\nDexterity: {a.Dexterity}\nIntelligence: {a.Intelligence}\nEndurance: {a.Endurance}\nCharisma: {a.Charisma}";
        }
        public void BindBootstrap(SettlementBootstrap state,Action<Citizen> inspect)
        {
            bootstrap=state; inspectCitizen=inspect;
            PopulateHelp(PrototypeHelpContent.Create(state.Settings,state.Settlement.TownHallLevel));
            management=Panel("building-management",root);
            management.style.position=Position.Absolute;
            management.style.right=12; management.style.top=12;
            management.style.width=320; management.style.maxWidth=Length.Percent(45);
            management.style.maxHeight=Length.Percent(90);
            managementInfo=Text(management,"");
            assignButton=new Button(()=> { choosingWorker=true; rosterEmployment=-1; RefreshManagement(); }) { text="Assign Worker" };
            management.Add(assignButton);
            roster=new ScrollView(ScrollViewMode.Vertical);
            roster.style.flexShrink=1; roster.style.maxHeight=440;
            management.Add(roster);
            management.Add(new Button(()=>ShowBuilding(null)) { text="Close" });
            management.style.display=DisplayStyle.None;
        }
        public void ShowBuilding(BuildingState building)
        {
            managedBuilding=building; choosingWorker=false; rosterEmployment=-1;
            if(management==null) return;
            management.style.display=building==null||HelpOpen?DisplayStyle.None:DisplayStyle.Flex;
            if(building!=null) SetBuildListOpen(false);
            RefreshManagement();
        }
        private string CitizenDescription(Citizen c)
        {
            var a=c.Attributes;
            return $"{c.FirstName} | {c.Sex} | Age {c.Age}\n{c.Profession} | Workplace: {bootstrap.WorkplaceName(c)}\nStrength {a.Strength} | Dexterity {a.Dexterity}\nIntelligence {a.Intelligence} | Endurance {a.Endurance} | Charisma {a.Charisma}";
        }
        private void RefreshManagement()
        {
            if(managedBuilding==null||bootstrap==null) return;
            var b=managedBuilding;
            bool hall=b.Type==BuildingType.TownHall;
            if(hall)
                managementInfo.text=$"Town Hall\nLevel: {bootstrap.Settlement.TownHallLevel} (TEST OVERRIDE)\nClaimed Tiles: {bootstrap.Settlement.Territory.Count}\nPopulation: {bootstrap.Roster.Count} | Employed: {bootstrap.EmployedCount} | Unemployed: {bootstrap.UnemployedCount}\nWood: {bootstrap.Resources.Amount(ResourceType.Wood)}\nStorage: Town Hall\nCitizen roster";
            else if(!b.IsCompleted)
                managementInfo.text=$"{b.Definition.DisplayName}\nUnder Construction\nProgress: {b.ConstructionProgress:P0}\nWork remaining: {b.RequiredConstructionWork-b.ConstructionWork:F1} seconds\nHold E to Build (active work only)";
            else managementInfo.text=$"{b.Definition.DisplayName}\nCompleted\nWorker: {bootstrap.WorkerAt(b)?.FirstName ?? "None"}";
            assignButton.style.display=b.Type==BuildingType.BuildersHut&&b.IsCompleted&&bootstrap.WorkerAt(b)==null?DisplayStyle.Flex:DisplayStyle.None;
            if(rosterEmployment==bootstrap.EmployedCount) return;
            rosterEmployment=bootstrap.EmployedCount; roster.Clear();
            if(hall)
                foreach(var c in bootstrap.Roster)
                {
                    var citizen=c;
                    var row=new Button(()=>inspectCitizen(citizen)) { text=CitizenDescription(citizen) };
                    row.style.whiteSpace=WhiteSpace.Normal; row.style.minHeight=100;
                    roster.Add(row);
                }
            else if(choosingWorker&&b.IsCompleted)
            {
                foreach(var c in bootstrap.EligibleWorkers(b))
                {
                    var citizen=c;
                    var row=new Button(()=>
                    {
                        pointerInputFrame=Time.frameCount;
                        if(bootstrap.TryAssignBuilder(b,citizen.Id))
                        {
                            choosingWorker=false; rosterEmployment=-1;
                            inspectCitizen(citizen); RefreshManagement();
                        }
                    }) { text=CitizenDescription(citizen) };
                    row.style.whiteSpace=WhiteSpace.Normal; row.style.minHeight=100;
                    roster.Add(row);
                }
                if(roster.childCount==0) Text(roster,"No eligible unemployed adults.");
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
