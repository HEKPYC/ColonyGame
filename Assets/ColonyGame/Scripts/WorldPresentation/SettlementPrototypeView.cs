using System.Collections.Generic;
using System.Linq;
using ColonyGame.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

namespace ColonyGame.Presentation
{
    public sealed class SettlementPrototypeView : MonoBehaviour
    {
        [SerializeField] private Material claimedGround = null;
        [SerializeField] private Material wildernessGround = null;
        [SerializeField] private Material footprintGround = null;
        [SerializeField] private Material treeTrunk = null;
        [SerializeField] private Material treeCanopy = null;
        [SerializeField] private Material hallWalls = null;
        [SerializeField] private int claimedTileCount;
        [SerializeField] private int townHallOccupiedTileCount;
        [SerializeField, Range(0, 12)] private int wildlifePerSpecies = 2;
        [SerializeField, Range(1.1f, 3f)] private float wildlifeRunSpeedMultiplier = 1.8f;
        [SerializeField, Min(.1f)] private float citizenWalkSpeed = .8f;
        [SerializeField, Min(0)] private float citizenMinimumPause = 1f;
        [SerializeField, Min(0)] private float citizenMaximumPause = 3f;
        private readonly BootstrapSettings bootstrapSettings = new BootstrapSettings();
        private SettlementBootstrap bootstrap;
        private SettlementEconomy economy;
        private SettlementAmbience ambience;
        private CitizenSimulation citizens;
        private CitizenView citizenView;
        private PlayerInteraction playerInteraction;
        private Citizen selectedCitizen => playerInteraction?.CitizenTarget;
        private PrototypePalette palette;
        private Material[] tileMaterials;
        private PeacefulWildernessHabitat wildlifeHabitat;
        private PeacefulWildlifeSystem wildlife;
        private PeacefulWildlifeView wildlifeView;
        private Mesh groundMesh;
        private GameObject generatedRoot;
        private GameObject previewRoot;
        private readonly List<Renderer> previewCells = new List<Renderer>();
        private Material validPreview;
        private Material invalidPreview;
        private BuildingDefinition selectedBuilding;
        private int orientationDegrees;
        private PrototypeHud hud;
        private bool hasCursorTile;
        private TileCoordinate cursorTile;
        private BuildingPlacementValidity placementValidity;
        private int minX = -6, minZ = -6, maxX = 26, maxZ = 26;
        public SettlementState State { get; private set; }

        private static SettlementEconomy activeSession;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()=>activeSession=null;
        private void Start()
        {
            State = activeSession?.Bootstrap.Settlement??SettlementState.CreatePrototype();
            hud = new PrototypeHud(transform, State.AvailableBuildings, SelectBuilding, CancelPlacement);
            hud.BuildListOpened += () => { SelectCitizen(null); hud.ShowBuilding(null); };
            palette = new PrototypePalette(hallWalls);
            var shader = Resources.Load<Shader>("PrototypeTileCells");
            if (shader == null) throw new MissingReferenceException("PrototypeTileCells shader must be imported before Play Mode.");
            tileMaterials = new[] { claimedGround, wildernessGround, footprintGround }.Select(source =>
            {
                var material = new Material(shader) { name = "Runtime Tile cell surface" };
                material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                return material;
            }).ToArray();
            CreatePreviewMaterials();
            RefreshPresentation();
            wildlifeHabitat = new PeacefulWildernessHabitat(State.Territory, new WildlifeBounds(minX, minZ, maxX, maxZ));
            wildlife = new PeacefulWildlifeSystem(wildlifeHabitat, wildlifePerSpecies, runSpeedMultiplier: wildlifeRunSpeedMultiplier);
            wildlifeView = new PeacefulWildlifeView(transform, wildlife, palette);
            citizens = CitizenSimulation.CreatePrototype(State, new CitizenMovementSettings(citizenWalkSpeed, citizenMinimumPause, citizenMaximumPause));
            citizenView = new CitizenView(transform, citizens, palette);
            bootstrap = activeSession?.Bootstrap??new SettlementBootstrap(State,citizens,bootstrapSettings);
            if(citizens.Gathering==null) citizens.Gathering = new AmbientWoodGathering(bootstrap);
            economy = activeSession??new SettlementEconomy(bootstrap);activeSession=economy;
            economy.PresentationChanged += RefreshPresentation;
            economy.ProductionEvent += OnProduction;
            ambience = new SettlementAmbience(transform,economy,palette,tileMaterials);
            playerInteraction = new PlayerInteraction(bootstrap);
            hud.BindBootstrap(bootstrap,SelectCitizen);
            Debug.Log("Building prototype: Town Hall Level 1; B toggles build menu; R rotates; left click places; Escape cancels.", this);
        }
        private void RefreshPresentation()
        {
            claimedTileCount = State.Territory.Count;
            townHallOccupiedTileCount = State.TownHall.OccupiedTiles.Count();
            if (generatedRoot != null) { generatedRoot.SetActive(false); Destroy(generatedRoot); }
            if (groundMesh != null) Destroy(groundMesh);
            generatedRoot = new GameObject("Generated Settlement Presentation");
            generatedRoot.transform.SetParent(transform, false);
            minX = State.Territory.ClaimedTiles.Min(t => t.X) - bootstrapSettings.WildernessDepth - 1;
            minZ = State.Territory.ClaimedTiles.Min(t => t.Z) - bootstrapSettings.WildernessDepth - 1;
            maxX = State.Territory.ClaimedTiles.Max(t => t.X) + bootstrapSettings.WildernessDepth + 2;
            maxZ = State.Territory.ClaimedTiles.Max(t => t.Z) + bootstrapSettings.WildernessDepth + 2;
            var camera = Camera.main;
            if (camera != null && camera.TryGetComponent<PrototypeCamera>(out var navigation))
            {
                navigation.SetInspectionBounds(minX, minZ, maxX, maxZ);
                navigation.IsPointerOverUI = hud.IsPointerOverUI;
                navigation.IsKeyboardInputBlocked = () => hud.BlocksGameplayShortcuts;
            }
            CreateGround();
            foreach (var building in State.Buildings)
                SchematicBuildingView.Create(building, generatedRoot.transform, palette);
            CreateForest();
            pathLines.Clear();
            var gold=palette.Get(new Color(.82f,.77f,.58f));
            foreach(var segment in PerimeterNavigation.Segments(State.Buildings))
            {
                var path=new GameObject("Thin golden pedestrian path");path.transform.SetParent(generatedRoot.transform,false);
                var line=path.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=2;
                line.SetPosition(0,new Vector3((float)segment.Start.X,.025f,(float)segment.Start.Z));
                line.SetPosition(1,new Vector3((float)segment.End.X,.025f,(float)segment.End.Z));
                line.sharedMaterial=gold;line.alignment=LineAlignment.View;
                line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
                pathLines.Add(line);
            }
            UpdatePathWidths();
            citizens?.ReconcileBuildings();
            citizenView?.Sync(selectedCitizen);
            if (wildlifeHabitat != null)
            {
                wildlifeHabitat.Bounds = new WildlifeBounds(minX, minZ, maxX, maxZ);
                wildlife.ReconcileTerritory();
                wildlifeView?.Sync();
            }
            Debug.Log($"Settlement prototype: {claimedTileCount} claimed Tiles; {State.Buildings.Count} buildings; initial range X/Z 0..19 (20 x 20). Town Hall: {State.TownHall.Width} x {State.TownHall.Depth}, origin {State.TownHall.Origin}, {townHallOccupiedTileCount} occupied Tiles; no starting influence.", this);
        }
        private void CreateGround()
        {
            var occupied = new HashSet<TileCoordinate>(State.Buildings.SelectMany(b => b.OccupiedTiles));
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int z = minZ; z < maxZ; z++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    int start = vertices.Count;
                    // Each visual cell spans exactly one logical Tile; no grouped regions.
                    vertices.Add(new Vector3(x, 0, z));
                    vertices.Add(new Vector3(x, 0, z + 1));
                    vertices.Add(new Vector3(x + 1, 0, z + 1));
                    vertices.Add(new Vector3(x + 1, 0, z));
                    uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
                    var tile = new TileCoordinate(x, z);
                    int material = occupied.Contains(tile) ? 2 : State.Territory.IsClaimed(tile) ? 0 : 1;
                    triangles[material].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }
            groundMesh = new Mesh { name = "Contiguous Prototype Tiles", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            groundMesh.SetVertices(vertices);
            groundMesh.SetUVs(0, uv);
            groundMesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) groundMesh.SetTriangles(triangles[i], i);
            groundMesh.RecalculateNormals();
            groundMesh.RecalculateBounds();
            var ground = new GameObject("Ground Tiles");
            ground.transform.SetParent(generatedRoot.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = tileMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        private void CreateForest()
        {
            // Deterministic, finite decorative placement, with no resource-node/domain entities.
            for (int z = minZ; z < maxZ; z++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    if (State.Territory.IsClaimed(new TileCoordinate(x, z))) continue;
                    int pattern = ((x + 6) * 17 + (z + 6) * 31) % 11;
                    if (!PrototypePresentationRules.ForestCell(x,z)) continue;
                    var position = new Vector3(x + 0.5f, 0, z + 0.5f);
                    Primitive("Decorative Tree Trunk", PrimitiveType.Cylinder, position + Vector3.up * 0.3f, new Vector3(0.18f, 0.3f, 0.18f), treeTrunk);
                    Primitive("Decorative Tree Canopy", PrimitiveType.Sphere, position + Vector3.up * 0.75f, new Vector3(0.65f, 0.8f, 0.65f), treeCanopy);
                }
            }
        }
        private void Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, int orientation = 0)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(generatedRoot.transform, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            visual.transform.localRotation = Quaternion.Euler(0, orientation, 0);
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            // These visuals are not authoritative occupancy or resource nodes.
            Destroy(visual.GetComponent<Collider>());
        }
        private void CreatePreviewMaterials()
        {
            validPreview = new Material(claimedGround) { name = "Valid Building Preview" };
            invalidPreview = new Material(claimedGround) { name = "Invalid Building Preview" };
            validPreview.SetColor("_BaseColor", new Color(0.2f, 0.85f, 0.35f));
            invalidPreview.SetColor("_BaseColor", new Color(0.9f, 0.22f, 0.2f));
        }
        private void RebuildPreview()
        {
            if (previewRoot != null) { previewRoot.SetActive(false); Destroy(previewRoot); }
            previewCells.Clear();
            previewRoot = new GameObject("Building Placement Preview");
            previewRoot.transform.SetParent(transform, false);
            var footprint = selectedBuilding.GetFootprint(orientationDegrees);
            for (int z = 0; z < footprint.Depth; z++)
            {
                for (int x = 0; x < footprint.Width; x++)
                {
                    var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = "Preview Tile";
                    cell.transform.SetParent(previewRoot.transform, false);
                    cell.transform.localPosition = new Vector3(x + 0.5f, 0.04f, z + 0.5f);
                    cell.transform.localScale = new Vector3(0.92f, 0.025f, 0.92f);
                    var renderer = cell.GetComponent<Renderer>();
                    renderer.sharedMaterial = validPreview;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    previewCells.Add(renderer);
                    Destroy(cell.GetComponent<Collider>());
                }
            }
            previewRoot.SetActive(false);
            hasCursorTile = false;
        }
        private void SelectBuilding(BuildingDefinition definition)
        {
            SelectCitizen(null);
            hud.ShowBuilding(null);
            selectedBuilding = definition;
            orientationDegrees = 0;
            RebuildPreview();
        }
        private void SelectCitizen(Citizen citizen)
        {
            playerInteraction?.SelectCitizen(citizen);
            if (citizen != null) hud.SetBuildListOpen(false);
            hud.ShowCitizen(citizen);
            citizenView?.Sync(citizen);
        }
        private void SelectInteractionBuilding(BuildingState building)
        {
            playerInteraction.SelectBuilding(building);
            hud.ShowCitizen(null);
            hud.ShowBuilding(building);
            citizenView?.Sync(null);
        }
        private void UpdatePlayerInteraction(Keyboard keyboard)
        {
            ambience.ShowPlayerWork(null,false);
            if(keyboard==null) return;
            bool pressed=keyboard.eKey.wasPressedThisFrame;
            bool blocked=hud.BlocksGameplayShortcuts||selectedBuilding!=null||hud.BuildListOpen;
            // A new press may reopen a selected site's panel; closing it stops an existing hold.
            if(!blocked&&pressed&&playerInteraction.AvailableInteraction==PlayerInteractionType.ManualConstruction&&
                hud.ManagedBuilding!=playerInteraction.BuildingTarget)
                hud.ShowBuilding(playerInteraction.BuildingTarget);
            if(playerInteraction.AvailableInteraction==PlayerInteractionType.ManualConstruction&&
                hud.ManagedBuilding!=playerInteraction.BuildingTarget) blocked=true;
            var result=playerInteraction.Act(pressed,keyboard.eKey.isPressed,Time.deltaTime*economy.Clock.Speed,blocked);
            ambience.ShowPlayerWork(playerInteraction.BuildingTarget,!blocked&&keyboard.eKey.isPressed&&result.Type==PlayerInteractionType.ManualConstruction);
            switch(result.Type)
            {
                case PlayerInteractionType.TownHallManagement:
                case PlayerInteractionType.WorkplaceManagement:
                    if(hud.ManagedBuilding!=playerInteraction.BuildingTarget) hud.ShowBuilding(playerInteraction.BuildingTarget);
                    break;
                case PlayerInteractionType.CitizenInformation:
                    hud.ShowCitizen(playerInteraction.CitizenTarget);
                    break;
                case PlayerInteractionType.ManualConstruction:
                    if(result.ConstructionCompleted)
                    {
                        RefreshPresentation();
                        hud.ShowBuilding(playerInteraction.BuildingTarget);
                    }
                    break;
            }
        }
        private void CancelPlacement()
        {
            selectedBuilding = null;
            hasCursorTile = false;
            if (previewRoot != null) previewRoot.SetActive(false);
        }
        private void Update()
        {
            if(generatedRoot!=null&&State!=null)
                foreach(var building in State.Buildings)
                {
                    var visual=generatedRoot.transform.Find(building.Id);
                    if(visual!=null) SchematicBuildingView.UpdateConstruction(building,visual);
                }
            if (State == null || hud == null) return;
            economy.Advance(Time.deltaTime,(step,day)=>
            {
                wildlifeView.SetVisible(day);
                if(day)
                {
                    wildlife.ReconcileTerritory(false);
                    wildlife.ReactToCitizens(citizens.Citizens.Where(c=>c.InsideBuildingId==null),bootstrap.Settings.WildlifeDetectionRadius);
                    wildlifeView.Tick((float)step);
                }
            });
            if(selectedCitizen!=null&&!bootstrap.Roster.Contains(selectedCitizen)) SelectCitizen(null);
            ambience.Update(Time.deltaTime);
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                hud.HandleHelpKeys(keyboard.hKey.wasPressedThisFrame, keyboard.escapeKey.wasPressedThisFrame);
            }
            if (keyboard != null && !hud.BlocksGameplayShortcuts)
            {
                if (keyboard.bKey.wasPressedThisFrame) hud.SetBuildListOpen(!hud.BuildListOpen);
                if (keyboard.escapeKey.wasPressedThisFrame) { CancelPlacement(); SelectCitizen(null); hud.ShowBuilding(null); hud.SetBuildListOpen(false); }
                if (selectedBuilding != null && keyboard.rKey.wasPressedThisFrame)
                {
                    orientationDegrees = (orientationDegrees + 90) % 360;
                    RebuildPreview();
                }
            }
            UpdatePlayerInteraction(keyboard);
            var mouse = Mouse.current;
            var camera = Camera.main;
            hasCursorTile = false;
            bool wasPlacing = selectedBuilding != null;
            if (selectedBuilding != null && mouse != null && camera != null)
            {
                var position = mouse.position.ReadValue();
                var plane = new Plane(transform.up, transform.position);
                var ray = camera.ScreenPointToRay(position);
                if (!hud.IsPointerOverUI(position) && camera.pixelRect.Contains(position) && plane.Raycast(ray, out var distance))
                {
                    var local = transform.InverseTransformPoint(ray.GetPoint(distance));
                    cursorTile = new TileCoordinate(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.z));
                    hasCursorTile = true;
                    placementValidity = ValidateBootstrapPlacement();
                    if (mouse.leftButton.wasPressedThisFrame && placementValidity == BuildingPlacementValidity.Valid &&
                        economy.TryBuild(selectedBuilding.Type,cursorTile, orientationDegrees, out var newSite))
                    {
                        RefreshPresentation();
                        CancelPlacement(); SelectInteractionBuilding(newSite);
                    }
                }
            }
            if (!wasPlacing && mouse != null && camera != null && mouse.leftButton.wasPressedThisFrame)
            {
                var position = mouse.position.ReadValue();
                if (!hud.IsPointerOverUI(position) && camera.pixelRect.Contains(position))
                {
                    var ray=camera.ScreenPointToRay(position);
                    var citizen=citizenView?.Pick(ray);
                    SelectCitizen(citizen);
                    hud.ShowBuilding(null);
                    if(citizen==null)
                    {
                        var plane=new Plane(transform.up,transform.position);
                        if(plane.Raycast(ray,out float distance))
                        {
                            var local=transform.InverseTransformPoint(ray.GetPoint(distance));
                            var tile=new TileCoordinate(Mathf.FloorToInt(local.x),Mathf.FloorToInt(local.z));
                            SelectInteractionBuilding(State.Buildings.FirstOrDefault(b=>b.OccupiedTiles.Contains(tile)));
                        }
                    }
                }
            }
            if (selectedCitizen != null && (!selectedCitizen.Active||!bootstrap.Roster.Contains(selectedCitizen))) SelectCitizen(null);
            citizenView?.Sync(selectedCitizen);
            hud.ShowCitizen(selectedCitizen);
            if (previewRoot != null) previewRoot.SetActive(selectedBuilding != null && hasCursorTile);
            if (hasCursorTile)
            {
                previewRoot.transform.localPosition = new Vector3(cursorTile.X, 0, cursorTile.Z);
                var material = placementValidity == BuildingPlacementValidity.Valid ? validPreview : invalidPreview;
                foreach (var cell in previewCells) cell.sharedMaterial = material;
            }
        }
        private readonly System.Collections.Generic.List<LineRenderer> pathLines=new System.Collections.Generic.List<LineRenderer>();
        private void UpdatePathWidths()
        {
            var camera=Camera.main;if(camera==null||camera.pixelHeight<=0) return;
            foreach(var line in pathLines)
            {
                float distance=Vector3.Distance(camera.transform.position,line.transform.TransformPoint(line.GetPosition(0)));
                float span=camera.orthographic?camera.orthographicSize*2:2*distance*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
                line.startWidth=line.endWidth=span/camera.pixelHeight*1.5f;
            }
        }
        private void LateUpdate()
        {
            UpdatePathWidths();
            if (State != null && hud != null)
                hud.Update(State, selectedBuilding, orientationDegrees, hasCursorTile ? $"{PlacementStatus(placementValidity)} | {cursorTile}" : "Move cursor onto ground", economy.Clock.IsDay ? wildlife?.Animals.Count(a => a.Active) ?? 0 : 0, citizens?.Citizens.Count(a => a.Active) ?? 0);
        }
        private BuildingPlacementValidity ValidateBootstrapPlacement()
        {
            var geometric=State.ValidateAccessiblePlacement(selectedBuilding.Type,cursorTile,orientationDegrees);
            if(geometric!=BuildingPlacementValidity.Valid) return geometric;
            return economy.CanBuild(selectedBuilding.Type)?BuildingPlacementValidity.Valid:BuildingPlacementValidity.ConstructionDeferred;
        }
        private string PlacementStatus(BuildingPlacementValidity validity)
        {
            switch (validity)
            {
                case BuildingPlacementValidity.Valid: return $"VALID: {BuildingEconomyRules.Describe(BuildingEconomyRules.Cost(selectedBuilding.Type))}";
                case BuildingPlacementValidity.InsufficientResources: return $"Need {BuildingEconomyRules.Describe(BuildingEconomyRules.Cost(selectedBuilding.Type))}";
                case BuildingPlacementValidity.ConstructionDeferred: return "A free assigned Builder is required";
                case BuildingPlacementValidity.AccessBlocked: return "INVALID: blocks reachable building access or Town Hall delivery";
                case BuildingPlacementValidity.OutsideTerritory: return "INVALID: outside territory";
                case BuildingPlacementValidity.Occupied: return "INVALID: occupied Tiles";
                case BuildingPlacementValidity.Locked: return "INVALID: locked by Town Hall level";
                case BuildingPlacementValidity.NotPlayerPlaceable: return "INVALID: not player-placeable";
                default: return "INVALID: building type or orientation";
            }
        }
        private sealed class OutputFeedback
        {
            public Vector3 Position;
            public string Text;
            public float Expires;
        }
        private readonly List<OutputFeedback> feedback=new List<OutputFeedback>();
        private void OnProduction(BuildingState building,ResourceType resource,int stored,int blocked)
        {
            if(feedback.Count>=12) feedback.RemoveAt(0);
            feedback.Add(new OutputFeedback {Position=new Vector3(building.Origin.X+building.Width*.5f,1.8f,building.Origin.Z+building.Depth*.5f),
                Text=stored>0?"+"+stored+" "+resource+(blocked>0?" (storage limited)":""):resource+" storage full",Expires=Time.unscaledTime+1.5f});
        }
        private void OnGUI()
        {
            feedback.RemoveAll(f=>f.Expires<=Time.unscaledTime);
            var camera=Camera.main;if(camera==null||hud?.HelpOpen==true) return;
            ambience?.DrawFireflies(camera);
            var previous=GUI.color;
            foreach(var item in feedback)
            {
                var screen=camera.WorldToScreenPoint(transform.TransformPoint(item.Position));
                if(screen.z<=0) continue;
                GUI.color=new Color(1,1,.75f,Mathf.Clamp01(item.Expires-Time.unscaledTime));
                GUI.Label(new Rect(screen.x-70,Screen.height-screen.y-12,160,28),item.Text);
            }
            GUI.color=previous;
        }
        private void OnDestroy()
        {
            if(economy!=null) {economy.PresentationChanged-=RefreshPresentation;economy.ProductionEvent-=OnProduction;}
            hud?.Dispose();
            wildlifeView?.Dispose();
            citizenView?.Dispose();
            ambience?.Dispose();
            palette?.Dispose();
            if (tileMaterials != null) foreach (var material in tileMaterials) Destroy(material);
            if (groundMesh != null) Destroy(groundMesh);
            if (validPreview != null) Destroy(validPreview);
            if (invalidPreview != null) Destroy(invalidPreview);
        }
    }
}
