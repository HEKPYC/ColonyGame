using System.Collections.Generic;
using System.Linq;
using ColonyGame.World;
using UnityEngine;
using UnityEngine.Rendering;

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
        public SettlementState State { get; private set; }

        private void Start()
        {
            State = SettlementState.CreatePrototype();
            claimedTileCount = State.Territory.Count;
            townHallOccupiedTileCount = State.TownHall.OccupiedTiles.Count();
            CreateGround();
            CreateTownHall();
            CreateForest();
            Debug.Log($"Settlement prototype: {claimedTileCount} claimed Tiles; initial range X/Z 0..19 (20 x 20). Town Hall: 5 x 5, origin (7, 7), X/Z 7..11, {townHallOccupiedTileCount} occupied Tiles; no starting influence.", this);
        }
        private void CreateGround()
        {
            Primitive("Grid Background", PrimitiveType.Cube, new Vector3(10, -0.12f, 10), new Vector3(32, 0.2f, 32), gridBackground);
            var occupied = new HashSet<TileCoordinate>(State.TownHall.OccupiedTiles);
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            for (int z = -6; z < 26; z++)
            {
                for (int x = -6; x < 26; x++)
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
            for (int boundary = -6; boundary <= 26; boundary++)
            {
                float low = Mathf.Max(-6, boundary - halfLineWidth);
                float high = Mathf.Min(26, boundary + halfLineWidth);
                AddGridQuad(vertices, triangles[3], low, -6, high, 26, gridHeight);
                AddGridQuad(vertices, triangles[3], -6, low, 26, high, gridHeight);
            }
            groundMesh = new Mesh { name = "Prototype Tile Grid" };
            groundMesh.SetVertices(vertices);
            groundMesh.subMeshCount = 4;
            for (int i = 0; i < 4; i++) groundMesh.SetTriangles(triangles[i], i);
            groundMesh.RecalculateNormals();
            groundMesh.RecalculateBounds();
            var ground = new GameObject("Tile Grid");
            ground.transform.SetParent(transform, false);
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
        private void CreateForest()
        {
            // Deterministic, finite decorative placement, with no resource-node/domain entities.
            for (int z = -6; z < 26; z++)
            {
                for (int x = -6; x < 26; x++)
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
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            // These visuals are not authoritative occupancy or resource nodes.
            Destroy(visual.GetComponent<Collider>());
        }
        private void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 450, 110), "Settlement Prototype");
            GUI.Label(new Rect(24, 38, 420, 24), $"Claimed Tiles: {claimedTileCount} | Initial territory: 20 x 20");
            GUI.Label(new Rect(24, 62, 420, 24), $"Town Hall: 5 x 5 | Occupied Tiles: {townHallOccupiedTileCount}");
            GUI.Label(new Rect(24, 86, 420, 24), "WASD / Arrows: pan | Middle-drag: pan | Wheel: zoom");
        }
        private void OnDestroy() { if (groundMesh != null) Destroy(groundMesh); }
    }
}
