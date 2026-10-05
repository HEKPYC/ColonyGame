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
        [SerializeField] private Material gridBackground = null;
        [SerializeField] private Material treeTrunk = null;
        [SerializeField] private Material treeCanopy = null;
        [SerializeField] private Material hallWalls = null;
        [SerializeField] private Material hallRoof = null;
        [SerializeField] private int claimedTileCount;
        [SerializeField] private int townHallOccupiedTileCount;
        private Mesh groundMesh;
        private GameObject generatedRoot;
        private GameObject previewRoot;
        private readonly List<Renderer> previewCells = new List<Renderer>();
        private Material validPreview;
        private Material invalidPreview;
        private bool placingHouse;
        private bool hasCursorTile;
        private TileCoordinate cursorTile;
        private HousePlacementValidity placementValidity;
        private int minX = -6, minZ = -6, maxX = 26, maxZ = 26;
        private readonly Rect debugPanel = new Rect(12, 12, 510, 165);
        public SettlementState State { get; private set; }

        private void Start()
        {
            State = SettlementState.CreatePrototype();
            CreatePreview();
            RefreshPresentation();
            Debug.Log("House prototype: H toggles placement; left click places; Escape cancels.", this);
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
                navigation.SetInspectionBounds(minX, minZ, maxX, maxZ);
            CreateGround();
            CreateTownHall();
            foreach (var building in State.Buildings)
                if (building.Type == BuildingType.House) CreateHouse(building);
            CreateForest();
            Debug.Log($"Settlement prototype: {claimedTileCount} claimed Tiles; initial range X/Z 0..19 (20 x 20). Town Hall: 5 x 5, origin (7, 7), X/Z 7..11, {townHallOccupiedTileCount} occupied Tiles; no starting influence.", this);
        }
        private void CreateGround()
        {
            Primitive("Grid Background", PrimitiveType.Cube, new Vector3((minX + maxX) * 0.5f, -0.12f, (minZ + maxZ) * 0.5f), new Vector3(maxX - minX, 0.2f, maxZ - minZ), gridBackground);
            var occupied = new HashSet<TileCoordinate>(State.Buildings.SelectMany(b => b.OccupiedTiles));
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
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
                    var tile = new TileCoordinate(x, z);
                    int material = occupied.Contains(tile) ? 2 : State.Territory.IsClaimed(tile) ? 0 : 1;
                    triangles[material].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }
            // Equal-width grid strips replace inset gaps. Every integer boundary is identical.
            // The small elevation prevents coplanar depth artifacts against the filled ground.
            const float halfLineWidth = 0.02f;
            const float gridHeight = 0.01f;
            for (int boundary = minX; boundary <= maxX; boundary++)
                AddGridQuad(vertices, triangles[3], Mathf.Max(minX, boundary - halfLineWidth), minZ,
                    Mathf.Min(maxX, boundary + halfLineWidth), maxZ, gridHeight);
            for (int boundary = minZ; boundary <= maxZ; boundary++)
                AddGridQuad(vertices, triangles[3], minX, Mathf.Max(minZ, boundary - halfLineWidth),
                    maxX, Mathf.Min(maxZ, boundary + halfLineWidth), gridHeight);
            groundMesh = new Mesh { name = "Prototype Tile Grid", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            groundMesh.SetVertices(vertices);
            groundMesh.subMeshCount = 4;
            for (int i = 0; i < 4; i++) groundMesh.SetTriangles(triangles[i], i);
            groundMesh.RecalculateNormals();
            groundMesh.RecalculateBounds();
            var ground = new GameObject("Tile Grid");
            ground.transform.SetParent(generatedRoot.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { claimedGround, wildernessGround, footprintGround, gridBackground };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        private static void AddGridQuad(List<Vector3> vertices, List<int> triangles,
            float minX, float minZ, float maxX, float maxZ, float height)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(minX, height, minZ));
            vertices.Add(new Vector3(minX, height, maxZ));
            vertices.Add(new Vector3(maxX, height, maxZ));
            vertices.Add(new Vector3(maxX, height, minZ));
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        private void CreateTownHall()
        {
            var hall = State.TownHall;
            var center = new Vector3(hall.Origin.X + hall.Width * 0.5f, 0, hall.Origin.Z + hall.Depth * 0.5f);
            // A compact presentation marker leaves the 25 reserved footprint cells readable.
            Primitive("Town Hall Walls", PrimitiveType.Cube, center + Vector3.up * 0.3f, new Vector3(0.6f, 0.6f, 0.6f), hallWalls);
            Primitive("Town Hall Roof", PrimitiveType.Cube, center + Vector3.up * 0.65f, new Vector3(0.8f, 0.1f, 0.8f), hallRoof);
            Primitive("Town Hall Tower", PrimitiveType.Cube, center + Vector3.up * 0.82f, new Vector3(0.2f, 0.24f, 0.2f), hallWalls);
        }
        private void CreateHouse(BuildingState house)
        {
            var center = new Vector3(house.Origin.X + house.Width * 0.5f, 0, house.Origin.Z + house.Depth * 0.5f);
            Primitive("House Walls " + house.Id, PrimitiveType.Cube, center + Vector3.up * 0.25f,
                new Vector3(0.6f, 0.5f, 0.6f), hallWalls);
            Primitive("House Roof " + house.Id, PrimitiveType.Cube, center + Vector3.up * 0.55f,
                new Vector3(0.8f, 0.1f, 0.8f), hallRoof);
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
        private void Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(generatedRoot.transform, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            // These visuals are not authoritative occupancy or resource nodes.
            Destroy(visual.GetComponent<Collider>());
        }
        private void CreatePreview()
        {
            validPreview = new Material(claimedGround) { name = "Valid House Preview" };
            invalidPreview = new Material(claimedGround) { name = "Invalid House Preview" };
            validPreview.SetColor("_BaseColor", new Color(0.2f, 0.85f, 0.35f));
            invalidPreview.SetColor("_BaseColor", new Color(0.9f, 0.22f, 0.2f));
            previewRoot = new GameObject("House Placement Preview");
            previewRoot.transform.SetParent(transform, false);
            for (int z = 0; z < 3; z++)
            {
                for (int x = 0; x < 3; x++)
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
        }
        private void Update()
        {
            if (State == null || previewRoot == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.hKey.wasPressedThisFrame) placingHouse = !placingHouse;
                if (keyboard.escapeKey.wasPressedThisFrame) placingHouse = false;
            }
            var mouse = Mouse.current;
            var camera = Camera.main;
            hasCursorTile = false;
            if (placingHouse && mouse != null && camera != null)
            {
                var position = mouse.position.ReadValue();
                var guiPosition = new Vector2(position.x, Screen.height - position.y);
                var plane = new Plane(transform.up, transform.position);
                var ray = camera.ScreenPointToRay(position);
                if (!debugPanel.Contains(guiPosition) && camera.pixelRect.Contains(position) && plane.Raycast(ray, out var distance))
                {
                    var local = transform.InverseTransformPoint(ray.GetPoint(distance));
                    cursorTile = new TileCoordinate(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.z));
                    hasCursorTile = true;
                    placementValidity = State.ValidateHousePlacement(cursorTile);
                    if (mouse.leftButton.wasPressedThisFrame && placementValidity == HousePlacementValidity.Valid &&
                        State.TryPlacePrototypeHouse(cursorTile, out _))
                    {
                        RefreshPresentation();
                        placementValidity = State.ValidateHousePlacement(cursorTile);
                    }
                }
            }
            previewRoot.SetActive(placingHouse && hasCursorTile);
            if (hasCursorTile)
            {
                previewRoot.transform.localPosition = new Vector3(cursorTile.X, 0, cursorTile.Z);
                var material = placementValidity == HousePlacementValidity.Valid ? validPreview : invalidPreview;
                foreach (var cell in previewCells) cell.sharedMaterial = material;
            }
        }
        private void OnGUI()
        {
            GUI.Box(debugPanel, "Settlement Prototype");
            GUI.Label(new Rect(24, 38, 475, 24), $"Claimed Tiles: {claimedTileCount} | Buildings: {State?.Buildings.Count ?? 0}");
            GUI.Label(new Rect(24, 62, 475, 24), $"Initial territory: 20 x 20 | Town Hall: 5 x 5 ({townHallOccupiedTileCount} Tiles)");
            GUI.Label(new Rect(24, 86, 475, 24), "WASD / Arrows: pan | Middle-drag: pan | Wheel: zoom");
            GUI.Label(new Rect(24, 110, 475, 24), "H: toggle House placement | Left click: place | Escape: cancel");
            string status = !placingHouse ? "House Placement: Off" : !hasCursorTile ? "House Placement: move cursor onto ground" :
                $"House Placement {cursorTile}: " + (placementValidity == HousePlacementValidity.Valid ? "Valid" :
                    placementValidity == HousePlacementValidity.Occupied ? "Invalid: occupied Tiles" : "Invalid: footprint outside territory");
            GUI.Label(new Rect(24, 134, 475, 24), status);
        }
        private void OnDestroy()
        {
            if (groundMesh != null) Destroy(groundMesh);
            if (validPreview != null) Destroy(validPreview);
            if (invalidPreview != null) Destroy(invalidPreview);
        }
    }
}
