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
            buildList.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
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
            counts.text = $"Claimed Tiles: {state.Territory.Count} | Buildings: {state.Buildings.Count}\nTown Hall Level: {state.TownHallLevel} (test override) | Wildlife: {wildlifeCount}\nCitizens: {citizenCount}";
            placement.style.display = definition == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (definition == null) return;
            var footprint = definition.GetFootprint(orientation);
            selection.text = $"{definition.DisplayName}\nFootprint: {footprint.Width} x {footprint.Depth}";
            validity.text = status;
            validity.style.color = status.StartsWith("VALID") ? new Color(0.4f, 1f, 0.5f) : Color.white;
        }
        public void ShowCitizen(Citizen citizen)
        {
            citizenPanel.style.display = citizen == null ? DisplayStyle.None : DisplayStyle.Flex;
            if(citizen == null) return;
            var a=citizen.Attributes;
            citizenInfo.text=$"{citizen.FirstName}\nSex: {citizen.Sex} | Age: {citizen.Age}\nProfession: {citizen.Profession}\nStrength: {a.Strength}\nDexterity: {a.Dexterity}\nIntelligence: {a.Intelligence}\nEndurance: {a.Endurance}\nCharisma: {a.Charisma}";
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(settings);
        }
    }
}
