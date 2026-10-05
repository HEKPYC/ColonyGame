using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public enum PeacefulSpecies { Squirrel, Badger, Deer, Fox }
    public enum WildlifeMovement { Wandering, Evacuating, Fleeing }
    public readonly struct WildlifePosition
    {
        public double X { get; }
        public double Z { get; }
        public WildlifePosition(double x, double z) { X = x; Z = z; }
        public static WildlifePosition Center(TileCoordinate tile) => new WildlifePosition(tile.X + 0.5, tile.Z + 0.5);
        public double DistanceSquared(WildlifePosition other) => (X - other.X) * (X - other.X) + (Z - other.Z) * (Z - other.Z);
    }
    public readonly struct WildlifeBounds
    {
        public int MinX { get; } public int MinZ { get; } public int MaxX { get; } public int MaxZ { get; }
        public WildlifeBounds(int minX, int minZ, int maxX, int maxZ)
        { MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; }
    }
    // A policy for this peaceful population, not a universal restriction on future animals.
    public interface IWildlifeHabitat
    {
        bool CanOccupy(WildlifePosition position, double radius);
        bool CanTraverseEvacuation(WildlifePosition position, double radius);
        IEnumerable<TileCoordinate> SpawnTiles { get; }
        double SafetyDepth(WildlifePosition position);
    }
    public sealed class PeacefulWildernessHabitat : IWildlifeHabitat
    {
        private readonly SettlementTerritory territory;
        public WildlifeBounds Bounds { get; set; }
        public PeacefulWildernessHabitat(SettlementTerritory territory, WildlifeBounds bounds)
        { this.territory = territory; Bounds = bounds; }
        public bool CanOccupy(WildlifePosition p, double radius)
        {
            if (double.IsNaN(p.X) || double.IsNaN(p.Z) || double.IsInfinity(p.X) || double.IsInfinity(p.Z) || double.IsNaN(radius) || double.IsInfinity(radius) || radius < 0) return false;
            if (p.X - radius < Bounds.MinX || p.Z - radius < Bounds.MinZ || p.X + radius >= Bounds.MaxX || p.Z + radius >= Bounds.MaxZ) return false;
            // Conservative body clearance: even rotated schematic silhouettes cannot cross a claim.
            for (int z = (int)Math.Floor(p.Z - radius); z <= (int)Math.Floor(p.Z + radius); z++)
                for (int x = (int)Math.Floor(p.X - radius); x <= (int)Math.Floor(p.X + radius); x++)
                    if (territory.IsClaimed(new TileCoordinate(x, z))) return false;
            return true;
        }
        public bool CanTraverseEvacuation(WildlifePosition p, double radius) =>
            !double.IsNaN(p.X) && !double.IsNaN(p.Z) && !double.IsInfinity(p.X) && !double.IsInfinity(p.Z) &&
            p.X - radius >= Bounds.MinX && p.Z - radius >= Bounds.MinZ && p.X + radius < Bounds.MaxX && p.Z + radius < Bounds.MaxZ;
        public IEnumerable<TileCoordinate> SpawnTiles
        {
            get
            {
                for (int z = Bounds.MinZ; z < Bounds.MaxZ; z++)
                    for (int x = Bounds.MinX; x < Bounds.MaxX; x++)
                    {
                        var tile = new TileCoordinate(x, z);
                        if (!territory.IsClaimed(tile)) yield return tile;
                    }
            }
        }
        public double SafetyDepth(WildlifePosition p)
        {
            // Nearest distance to actual claimed square surfaces, not an original rectangle/radius.
            double nearest = double.PositiveInfinity;
            foreach (var tile in territory.ClaimedTiles)
            {
                double dx = Math.Max(Math.Max(tile.X - p.X, 0), p.X - tile.X - 1);
                double dz = Math.Max(Math.Max(tile.Z - p.Z, 0), p.Z - tile.Z - 1);
                nearest = Math.Min(nearest, dx * dx + dz * dz);
            }
            return nearest;
        }
    }
    public sealed class PeacefulWildlifeState
    {
        public int Id { get; }
        public PeacefulSpecies Species { get; }
        public WildlifePosition Position { get; internal set; }
        public WildlifePosition Destination { get; internal set; }
        public bool Moving { get; internal set; }
        public bool Active { get; internal set; } = true;
        public WildlifeMovement Movement { get; internal set; }
        internal readonly Queue<WildlifePosition> Route = new Queue<WildlifePosition>();
        internal double Pause;
        internal bool CitizenThreat;
        // Every schematic animal fits within this radius in every orientation.
        public const double ClearanceRadius = 0.48;
        internal PeacefulWildlifeState(int id, PeacefulSpecies species, WildlifePosition position)
        { Id = id; Species = species; Position = Destination = position; }
    }
    public sealed class PeacefulWildlifeSystem
    {
        private readonly IWildlifeHabitat habitat;
        private readonly Random random;
        private readonly List<PeacefulWildlifeState> animals = new List<PeacefulWildlifeState>();
        public IReadOnlyList<PeacefulWildlifeState> Animals { get; }
        public double RunSpeedMultiplier { get; }
        public const double WalkSpeed = 0.65;
        public event Action<string> DevelopmentWarning;
        private static readonly TileCoordinate[] steps = { new TileCoordinate(1, 0), new TileCoordinate(-1, 0), new TileCoordinate(0, 1), new TileCoordinate(0, -1) };

        public PeacefulWildlifeSystem(IWildlifeHabitat habitat, int perSpecies = 2, int seed = 731, double runSpeedMultiplier = 1.8)
        {
            if (perSpecies < 0) throw new ArgumentOutOfRangeException(nameof(perSpecies));
            if (double.IsNaN(runSpeedMultiplier) || double.IsInfinity(runSpeedMultiplier) || runSpeedMultiplier <= 1)
                throw new ArgumentOutOfRangeException(nameof(runSpeedMultiplier));
            this.habitat = habitat ?? throw new ArgumentNullException(nameof(habitat));
            RunSpeedMultiplier = runSpeedMultiplier;
            random = new Random(seed);
            Animals = animals.AsReadOnly();
            var spawn = habitat.SpawnTiles.Where(t => Valid(WildlifePosition.Center(t))).ToList();
            foreach (PeacefulSpecies species in Enum.GetValues(typeof(PeacefulSpecies)))
                for (int i = 0; i < perSpecies && spawn.Count > 0; i++)
                {
                    int index = random.Next(spawn.Count);
                    animals.Add(new PeacefulWildlifeState(animals.Count, species, WildlifePosition.Center(spawn[index])) { Pause = random.NextDouble() * 2 });
                    spawn.RemoveAt(index);
                }
        }
        private bool Valid(WildlifePosition p) => habitat.CanOccupy(p, PeacefulWildlifeState.ClearanceRadius);
        private bool Traversable(WildlifePosition p) => habitat.CanTraverseEvacuation(p, PeacefulWildlifeState.ClearanceRadius);
        private static TileCoordinate Tile(WildlifePosition p) => new TileCoordinate((int)Math.Floor(p.X), (int)Math.Floor(p.Z));
        private static IEnumerable<TileCoordinate> Adjacent(TileCoordinate tile)
        {
            foreach (var step in steps) yield return new TileCoordinate(tile.X + step.X, tile.Z + step.Z);
        }
        private IEnumerable<WildlifePosition> Neighbors(PeacefulWildlifeState animal) =>
            Adjacent(Tile(animal.Position)).Select(WildlifePosition.Center).Where(Valid);

        private bool PlanEvacuation(PeacefulWildlifeState animal)
        {
            animal.Route.Clear();
            var start = Tile(animal.Position);
            if (!Traversable(animal.Position) || !Traversable(WildlifePosition.Center(start))) return false;
            // Breadth-first cardinal search: shortest exit through the actual irregular claims.
            // Search stops at the first Wilderness layer; ties prefer more boundary clearance.
            var frontier = new Queue<TileCoordinate>();
            var parent = new Dictionary<TileCoordinate, TileCoordinate> { [start] = start };
            frontier.Enqueue(start);
            TileCoordinate? exit = null;
            while (frontier.Count > 0 && !exit.HasValue)
            {
                int layer = frontier.Count;
                for (int i = 0; i < layer; i++)
                {
                    var tile = frontier.Dequeue();
                    var center = WildlifePosition.Center(tile);
                    if (Valid(center))
                    {
                        if (!exit.HasValue || habitat.SafetyDepth(center) > habitat.SafetyDepth(WildlifePosition.Center(exit.Value))) exit = tile;
                        continue;
                    }
                    foreach (var next in Adjacent(tile))
                    {
                        if (parent.ContainsKey(next) || !Traversable(WildlifePosition.Center(next))) continue;
                        parent.Add(next, tile);
                        frontier.Enqueue(next);
                    }
                }
            }
            if (!exit.HasValue) return false;
            var path = new List<WildlifePosition>();
            for (var tile = exit.Value; !tile.Equals(start); tile = parent[tile]) path.Add(WildlifePosition.Center(tile));
            path.Add(WildlifePosition.Center(start));
            path.Reverse();
            var safe = WildlifePosition.Center(exit.Value);
            // Once outside, one additional safe cardinal step gives a short run into Wilderness.
            var deeper = Adjacent(exit.Value).Select(WildlifePosition.Center)
                .Where(p => Valid(p) && habitat.SafetyDepth(p) > habitat.SafetyDepth(safe))
                .OrderByDescending(habitat.SafetyDepth).ToArray();
            if (deeper.Length > 0) path.Add(deeper[0]);
            foreach (var p in path)
                if (p.DistanceSquared(animal.Position) > 0.0000000001) animal.Route.Enqueue(p);
            animal.Destination = path[path.Count - 1];
            animal.Movement = WildlifeMovement.Evacuating;
            animal.Active = animal.Moving = true;
            animal.Pause = 0;
            return true;
        }
        private void EmergencyRecovery(PeacefulWildlifeState animal)
        {
            var recovery = habitat.SpawnTiles.Select(WildlifePosition.Center).Where(Valid)
                .OrderBy(p => p.DistanceSquared(animal.Position)).ToArray();
            animal.Route.Clear();
            animal.Movement = WildlifeMovement.Wandering;
            animal.Moving = false;
            if (recovery.Length == 0)
            {
                animal.Active = false;
                DevelopmentWarning?.Invoke($"Wildlife {animal.Id}: no evacuation route or Wilderness destination; emergency dormancy.");
                return;
            }
            animal.Position = animal.Destination = recovery[0];
            animal.Active = true;
            animal.Pause = 1;
            DevelopmentWarning?.Invoke($"Wildlife {animal.Id}: no evacuation route; emergency relocation to Wilderness.");
        }
        public void ReconcileTerritory(bool recoverDormant = true)
        {
            foreach (var animal in animals)
            {
                if (!animal.Active && !recoverDormant) continue;
                if (!animal.Active)
                {
                    // Dormant recovery is also exceptional, not the ordinary expansion path.
                    if (!PlanEvacuation(animal)) EmergencyRecovery(animal);
                    continue;
                }
                if (!Valid(animal.Position))
                {
                    if (animal.Movement != WildlifeMovement.Evacuating || recoverDormant || !Valid(animal.Destination))
                        if (!PlanEvacuation(animal)) EmergencyRecovery(animal);
                    continue;
                }
                // Body clearance has crossed the boundary: claimed traversal permission ends now.
                if (animal.Movement == WildlifeMovement.Evacuating) animal.Movement = WildlifeMovement.Fleeing;
                if (!Valid(animal.Destination) || animal.Route.Any(p => !Valid(p)))
                {
                    animal.Route.Clear();
                    animal.Movement = WildlifeMovement.Wandering;
                    animal.Destination = WildlifePosition.Center(Tile(animal.Position));
                    animal.Moving = true;
                }
            }
        }
        public void Tick(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            ReconcileTerritory(false);
            foreach (var animal in animals)
            {
                if (!animal.Active) continue;
                if (!animal.Moving)
                {
                    if(animal.CitizenThreat) continue;
                    animal.Pause -= seconds;
                    if (animal.Pause > 0) continue;
                    var choices = Neighbors(animal).ToArray();
                    if (choices.Length == 0) { animal.Pause = 1; continue; }
                    animal.Destination = choices[random.Next(choices.Length)];
                    animal.Moving = true;
                }
                double remaining = seconds;
                do
                {
                    var target = animal.Route.Count > 0 ? animal.Route.Peek() : animal.Destination;
                    double dx = target.X - animal.Position.X, dz = target.Z - animal.Position.Z;
                    double distance = Math.Sqrt(dx * dx + dz * dz);
                    double speed = WalkSpeed * (animal.Movement == WildlifeMovement.Wandering ? 1 : RunSpeedMultiplier);
                    double amount = Math.Min(distance, remaining * speed);
                    var next = distance == 0 ? target : new WildlifePosition(animal.Position.X + dx / distance * amount, animal.Position.Z + dz / distance * amount);
                    bool allowed = animal.Movement == WildlifeMovement.Evacuating ? Traversable(next) : Valid(next);
                    if (!allowed) { animal.Moving = false; animal.Pause = 1; break; }
                    animal.Position = next;
                    if (animal.Movement == WildlifeMovement.Evacuating && Valid(next)) animal.Movement = WildlifeMovement.Fleeing;
                    remaining -= amount / speed;
                    if (amount < distance) break;
                    if (animal.Route.Count > 0) animal.Route.Dequeue();
                    if (animal.Route.Count == 0)
                    {
                        animal.Moving = false;
                        animal.Movement = animal.CitizenThreat ? WildlifeMovement.Fleeing : WildlifeMovement.Wandering;
                        animal.Pause = 0.8 + random.NextDouble() * 2;
                    }
                } while (animal.Moving && remaining > 0);
            }
        }
        // Actual citizens interrupt ordinary movement. Expansion routes retain priority.
        public void ReactToCitizens(IEnumerable<Citizen> citizens,double detectionRadius)
        {
            if(!BootstrapSettings.Finite(detectionRadius)||detectionRadius<=0) throw new ArgumentOutOfRangeException(nameof(detectionRadius));
            var threats=citizens.Where(c=>c.Active).Select(c=>new WildlifePosition(c.Position.X,c.Position.Z)).ToArray();
            ReconcileTerritory(false);
            foreach(var animal in animals)
            {
                if(!animal.Active||animal.Movement==WildlifeMovement.Evacuating||animal.Route.Count>0) continue;
                var nearest=threats.OrderBy(p=>p.DistanceSquared(animal.Position)).ToArray();
                double distance=nearest.Length==0?double.PositiveInfinity:nearest[0].DistanceSquared(animal.Position);
                double safeRadius=detectionRadius+2;
                if(animal.CitizenThreat&&distance>=safeRadius*safeRadius)
                {
                    animal.CitizenThreat=false; animal.Moving=false; animal.Movement=WildlifeMovement.Wandering; animal.Pause=1;
                    animal.Destination=animal.Position;
                    continue;
                }
                if(nearest.Length==0||(!animal.CitizenThreat&&distance>detectionRadius*detectionRadius)) continue;
                var threat=nearest[0];
                animal.CitizenThreat=true;
                if(animal.Moving&&animal.Movement==WildlifeMovement.Fleeing&&animal.Destination.DistanceSquared(threat)>distance) continue;
                bool SafeSegment(WildlifePosition target)
                {
                    double dx=target.X-animal.Position.X,dz=target.Z-animal.Position.Z;
                    if(dx*(animal.Position.X-threat.X)+dz*(animal.Position.Z-threat.Z)<0) return false;
                    for(int i=1;i<=10;i++)
                        if(!Valid(new WildlifePosition(animal.Position.X+dx*i/10,animal.Position.Z+dz*i/10))) return false;
                    return true;
                }
                var candidates=Neighbors(animal).Where(p=>p.DistanceSquared(threat)>distance&&SafeSegment(p)).ToArray();
                var deeper=candidates.Where(p=>habitat.SafetyDepth(p)>habitat.SafetyDepth(animal.Position)).ToArray();
                if(deeper.Length>0) candidates=deeper;
                var choice=candidates.OrderByDescending(p=>p.DistanceSquared(threat)).ThenByDescending(habitat.SafetyDepth).ToArray();
                animal.Movement=WildlifeMovement.Fleeing; animal.Pause=0;
                if(choice.Length==0) { animal.Moving=false; animal.Pause=1; continue; }
                animal.Route.Clear(); animal.Destination=choice[0]; animal.Moving=true;
            }
        }
        public bool RequestFlee(int id, WildlifePosition threat)
        {
            var animal = animals.First(a => a.Id == id);
            if (!animal.Active || animal.Moving) return false;
            var choices = Neighbors(animal).Where(p => p.DistanceSquared(threat) > animal.Position.DistanceSquared(threat))
                .OrderByDescending(habitat.SafetyDepth).ThenByDescending(p => p.DistanceSquared(threat)).ToArray();
            if (choices.Length == 0) return false;
            animal.Destination = choices[0];
            animal.Movement = WildlifeMovement.Fleeing;
            animal.Moving = true;
            return true;
        }
    }
}