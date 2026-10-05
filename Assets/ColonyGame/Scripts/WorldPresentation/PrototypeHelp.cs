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
                    "Click the Town Hall to view settlement resources, population and the complete citizen roster.",
                    $"First Builder's Hut: {balance.HutWoodCost} Wood -> place blueprint -> select site -> hold E to build -> complete construction -> assign Builder.",
                    "Help is informational: simulation keeps running. B, R and E are disabled while Help is open; close Help to resume those actions."),
                new PrototypeHelpSection("CONTROLS",
                    "WASD / Arrow Keys - move camera (close Help first).",
                    "Middle Mouse Drag - pan camera. Mouse Wheel - zoom. Over Help, drag/pan is blocked and the wheel scrolls this panel.",
                    "Left Click - select a citizen/building, inspect a roster entry, or confirm placement in placement mode.",
                    "B - open / close Build. R - rotate the selected building by 90 degrees; rectangles swap width/depth.",
                    "E (hold) - manually work on the selected first Builder's Hut construction site.",
                    "H / Help [H] - toggle this reference. Escape - close Help first; otherwise cancel placement and close inspection/Build.",
                    "Close - close Help without cancelling placement or clearing selection."),
                new PrototypeHelpSection("GAMEPLAY - PROTOTYPE BALANCE",
                    $"Start: {balance.StartingWood} Wood, stored in Town Hall.",
                    $"Unemployed adults occasionally gather Wood in nearby Wilderness. Each trip rolls once: {balance.SuccessChance:P0} success, {balance.WoodYield} Wood on success, zero on failure.",
                    "Wood is added only after the citizen walks back to the Town Hall access area, never at the forest gathering point.",
                    $"Gathering authorizes travel up to {balance.WildernessDepth} Tiles beyond the actual current irregular settlement boundary.",
                    $"The first Hut costs {balance.HutWoodCost} Wood and is built by the player: {balance.PlayerWorkSeconds:0.#} seconds of active held work. Release E to pause; progress persists.",
                    "After completion, open the Hut, choose Assign Worker, and explicitly select an unemployed adult as Builder. Its territory influence applies only at completion."),
                new PrototypeHelpSection("BUILDING",
                    "Press B, then choose a catalog entry to preview its footprint. R rotates it. Green = valid placement; red = invalid. Read placement feedback for cost or deferred-construction restrictions.",
                    "Every footprint Tile must fit inside already claimed territory and be free of another reserved footprint.",
                    "Currently only the first Builder's Hut supports playable construction. Other entries remain previews; subsequent construction is deferred.",
                    "Initial completion expands territory by the footprint plus 3 Tiles on every side, once per building. The first Hut grants no expansion while incomplete.",
                    "Town Hall is the starting 5 x 5 building, grants no starting expansion, and is not normally placeable.",
                    $"Town Hall Level {townHallLevel} [TEST OVERRIDE]. Unlocks below are cumulative; Town Hall upgrading is not implemented.")
            };
            for(int level=1;level<=4;level++)
                sections.Add(new PrototypeHelpSection($"TOWN HALL LEVEL {level} - NEW UNLOCKS",
                    BuildingCatalog.All.Where(d=>d.IsPlayerPlaceable&&d.RequiredTownHallLevel==level)
                        .Select(d=>$"{d.DisplayName} - {d.BaseWidth} x {d.BaseDepth}").ToArray()));
            sections.Add(new PrototypeHelpSection("CITIZENS",
                "Four adults start unemployed. Identity, name, sex, age and attributes persist through profession changes within this settlement; the prototype resets after Play Mode.",
                "Click a citizen to inspect them, or use the full Town Hall roster. Attributes: Strength, Dexterity, Intelligence, Endurance and Charisma.",
                "Ordinary movement stays inside settlement territory and routes around building footprints. Ambient gathering grants temporary Wilderness authorization to unemployed adults.",
                "If construction covers or traps a citizen, recovery attempts a continuous physical escape. Only impossible/broken recovery uses a warning-bearing emergency fallback.",
                "Builder is the first functional profession, assigned through a completed Builder's Hut. Work clothing and workplace change; identity and attributes do not. Assigned Builders receive no new ambient trips."));
            sections.Add(new PrototypeHelpSection("WILDLIFE",
                "Peaceful species: Squirrel, Badger, Deer and Fox.",
                "They live in Wilderness, never voluntarily enter settlement claims, and do not attack citizens. Citizens do not attack them; they provide no resources.",
                "Nearby citizens trigger fleeing away and preferably deeper into Wilderness. Animals later resume ordinary wandering.",
                "If expansion claims ground underneath an animal, it physically evacuates toward Wilderness; this escape has priority over citizen-triggered fleeing."));
            return sections.AsReadOnly();
        }
    }
}