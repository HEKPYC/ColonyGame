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

        private void Start()
        {
            State = SettlementState.CreatePrototype();
            hud = new PrototypeHud(transform, State.AvailableBuildings, SelectBuilding, CancelPlacement);
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
            Debug.Log("Building prototype: temporary Town Hall Level 4; B toggles build menu; R rotates; left click places; Escape cancels.", this);
        }
        private void RefreshPresentation()
        {
            claimedTileCount = State.Territory.Count;
            townHallOccupiedTileCount = State.TownHall.OccupiedTiles.Count();
            if (generatedRoot != null) { generatedRoot.SetActive(false); Destroy(generatedRoot); }
            if (groundMesh != null) Destroy(groundMesh);
            generatedRoot = new GameObject("Generated Settlement Presentation");
            generatedRoot.transform.SetParent(transform, false);
            minX = State.Territory.ClaimedTiles.Min(t => t.X) - 6;
            minZ = State.Territory.ClaimedTiles.Min(t => t.Z) - 6;
            maxX = State.Territory.ClaimedTiles.Max(t => t.X) + 7;
            maxZ = State.Territory.ClaimedTiles.Max(t => t.Z) + 7;
            var camera = Camera.main;
            if (camera != null && camera.TryGetComponent<PrototypeCamera>(out var navigation))
            {
                navigation.SetInspectionBounds(minX, minZ, maxX, maxZ);
                navigation.IsPointerOverUI = hud.IsPointerOverUI;
            }
            CreateGround();
            foreach (var building in State.Buildings)
                SchematicBuildingView.Create(building, generatedRoot.transform, palette);
            CreateForest();
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
                    if (pattern != 0) continue;
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
            selectedBuilding = definition;
            orientationDegrees = 0;
            RebuildPreview();
        }
        private void CancelPlacement()
        {
            selectedBuilding = null;
            hasCursorTile = false;
            if (previewRoot != null) previewRoot.SetActive(false);
        }
        private void Update()
        {
            if (State == null || hud == null) return;
            wildlifeView?.Tick(Time.deltaTime);
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.bKey.wasPressedThisFrame) hud.SetBuildListOpen(!hud.BuildListOpen);
                if (keyboard.escapeKey.wasPressedThisFrame) { CancelPlacement(); hud.SetBuildListOpen(false); }
                if (selectedBuilding != null && keyboard.rKey.wasPressedThisFrame)
                {
                    orientationDegrees = (orientationDegrees + 90) % 360;
                    RebuildPreview();
                }
            }
            var mouse = Mouse.current;
            var camera = Camera.main;
            hasCursorTile = false;
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
                    placementValidity = State.ValidateBuildingPlacement(selectedBuilding.Type, cursorTile, orientationDegrees);
                    if (mouse.leftButton.wasPressedThisFrame && placementValidity == BuildingPlacementValidity.Valid &&
                        State.TryPlacePrototypeBuilding(selectedBuilding.Type, cursorTile, orientationDegrees, out _))
                    {
                        RefreshPresentation();
                        placementValidity = State.ValidateBuildingPlacement(selectedBuilding.Type, cursorTile, orientationDegrees);
                    }
                }
            }
            if (previewRoot != null) previewRoot.SetActive(selectedBuilding != null && hasCursorTile);
            if (hasCursorTile)
            {
                previewRoot.transform.localPosition = new Vector3(cursorTile.X, 0, cursorTile.Z);
                var material = placementValidity == BuildingPlacementValidity.Valid ? validPreview : invalidPreview;
                foreach (var cell in previewCells) cell.sharedMaterial = material;
            }
        }
        private void LateUpdate()
        {
            if (State != null && hud != null)
                hud.Update(State, selectedBuilding, orientationDegrees, hasCursorTile ? $"{PlacementStatus(placementValidity)} | {cursorTile}" : "Move cursor onto ground", wildlife?.Animals.Count(a => a.Active) ?? 0);
        }
        private static string PlacementStatus(BuildingPlacementValidity validity)
        {
            switch (validity)
            {
                case BuildingPlacementValidity.Valid: return "VALID";
                case BuildingPlacementValidity.OutsideTerritory: return "INVALID: outside territory";
                case BuildingPlacementValidity.Occupied: return "INVALID: occupied Tiles";
                case BuildingPlacementValidity.Locked: return "INVALID: locked by Town Hall level";
                case BuildingPlacementValidity.NotPlayerPlaceable: return "INVALID: not player-placeable";
                default: return "INVALID: building type or orientation";
            }
        }
        private void OnGUI() { if (State != null) DrawBuildingLabels(); }
        private void DrawBuildingLabels()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            foreach (var building in State.Buildings)
            {
                var center = new Vector3(building.Origin.X + building.Width * 0.5f, 1.2f, building.Origin.Z + building.Depth * 0.5f);
                var screen = camera.WorldToScreenPoint(transform.TransformPoint(center));
                if (screen.z <= 0 || !camera.pixelRect.Contains(new Vector2(screen.x, screen.y))) continue;
                var point = new Vector2(screen.x, Screen.height - screen.y);
                var label = new Rect(point.x - 95, point.y - 12, 190, 24);
                if (hud != null && hud.IsPointerOverUI(new Vector2(screen.x, screen.y))) continue;
                GUI.Box(label, building.Definition.DisplayName, style);
            }
        }
        private void OnDestroy()
        {
            hud?.Dispose();
            wildlifeView?.Dispose();
            palette?.Dispose();
            if (tileMaterials != null) foreach (var material in tileMaterials) Destroy(material);
            if (groundMesh != null) Destroy(groundMesh);
            if (validPreview != null) Destroy(validPreview);
            if (invalidPreview != null) Destroy(invalidPreview);
        }
    }
}
