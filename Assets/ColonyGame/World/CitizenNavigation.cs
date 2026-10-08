using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    // Jobs may supply another policy; Settlement-only is an idle behavior, not citizen identity.
    public interface ICitizenMovementPolicy
    {
        IEnumerable<TileCoordinate> CandidateTiles { get; }
        bool CanOccupy(CitizenPosition position);
        bool CanTraverseRecovery(CitizenPosition position, CitizenPosition recoveryOrigin);
    }

    public sealed class SettlementIdleMovementPolicy : ICitizenMovementPolicy
    {
        private readonly SettlementTerritory territory;
        private readonly Func<TileCoordinate,bool> occupied;
        private readonly SettlementState settlement;
        private HashSet<TileCoordinate> connected=new HashSet<TileCoordinate>();
        public TileCoordinate? NavigationAnchor { get; private set; }
        public int ConnectedTileCount => connected.Count;
        public SettlementIdleMovementPolicy(SettlementState settlement)
            : this(settlement.Territory,settlement.IsOccupied) { this.settlement=settlement; }
        public SettlementIdleMovementPolicy(SettlementTerritory territory,Func<TileCoordinate,bool> occupied)
        {
            this.territory=territory ?? throw new ArgumentNullException(nameof(territory));
            this.occupied=occupied ?? throw new ArgumentNullException(nameof(occupied));
        }
        public IEnumerable<TileCoordinate> CandidateTiles => territory.ClaimedTiles;
        private bool Eligible(CitizenPosition p,bool recovery,ISet<TileCoordinate> permitted=null)
        {
            double r=Citizen.BodyHalfExtent;
            for(int z=(int)Math.Floor(p.Z-r);z<=(int)Math.Floor(p.Z+r);z++)
                for(int x=(int)Math.Floor(p.X-r);x<=(int)Math.Floor(p.X+r);x++)
                {
                    var tile=new TileCoordinate(x,z);
                    if(!territory.IsClaimed(tile)) return false;
                    if(occupied(tile)&&(!recovery||(permitted!=null&&!permitted.Contains(tile)))) return false;
                }
            return true;
        }
        public bool CanOccupy(CitizenPosition p)=>settlement==null?Eligible(p,false):PerimeterNavigation.CanOccupy(p,territory,settlement.Buildings);
        internal SettlementState Settlement=>settlement;
        private HashSet<TileCoordinate> edgeConnected=new HashSet<TileCoordinate>();
        // Search permission is not movement permission. Actual recovery uses a route whitelist.
        public bool CanTraverseRecovery(CitizenPosition p,CitizenPosition origin)=>Eligible(p,true);
        public bool CanFollowRecovery(CitizenPosition p,ISet<TileCoordinate> permitted)=>permitted!=null&&Eligible(p,true,permitted);
        public bool IsOccupied(TileCoordinate tile)=>occupied(tile);
        public bool IsConnected(CitizenPosition p)=>CanOccupy(p)&&(settlement==null?connected.Contains(p.Tile):PerimeterNavigation.Contains(edgeConnected,p));
        public void RefreshConnectivity()
        {
            if(settlement!=null)
            {
                var seed=CandidateTiles.OrderBy(t=>t.Z).ThenBy(t=>t.X).Where(t=>CanOccupy(CitizenPosition.Center(t))).Select(t=>(TileCoordinate?)t).FirstOrDefault();
                // A completely reserved territory still has navigable footprint edges.
                var start=seed.HasValue?CitizenPosition.Center(seed.Value):PerimeterNavigation.Entrance(settlement.TownHall);
                edgeConnected=PerimeterNavigation.Reachable(start,CanOccupy);
                connected=new HashSet<TileCoordinate>(CandidateTiles.Where(t=>CanOccupy(CitizenPosition.Center(t))&&PerimeterNavigation.Contains(edgeConnected,CitizenPosition.Center(t))));
                NavigationAnchor=connected.Count==0?(TileCoordinate?)null:connected.OrderBy(t=>t.Z).ThenBy(t=>t.X).First();
                return;
            }
            var walkable=new HashSet<TileCoordinate>(CandidateTiles.Where(t=>CanOccupy(CitizenPosition.Center(t))));
            var remaining=new HashSet<TileCoordinate>(walkable);
            var best=new HashSet<TileCoordinate>();
            TileCoordinate? hallAccess=settlement?.TownHall.Entrance.ExteriorTile;
            while(remaining.Count>0)
            {
                var seed=remaining.OrderBy(t=>t.Z).ThenBy(t=>t.X).First();
                var component=CitizenNavigation.ReachableTiles(seed,walkable.Contains);
                remaining.ExceptWith(component);
                bool hallTie=component.Count==best.Count&&hallAccess.HasValue&&component.Contains(hallAccess.Value)&&!best.Contains(hallAccess.Value);
                if(component.Count>best.Count||hallTie) best=component;
            }
            // Largest component avoids making a sealed Town Hall courtyard the destination.
            // Equal-size components prefer Town Hall access, then stable Z/X ordering.
            connected=best;
            NavigationAnchor=best.Count==0?(TileCoordinate?)null:
                hallAccess.HasValue&&best.Contains(hallAccess.Value)?hallAccess:
                best.OrderBy(t=>t.Z).ThenBy(t=>t.X).First();
        }
        public int EntrancePenalty(TileCoordinate tile)
        {
            if(settlement==null) return 0;
            foreach(var building in settlement.Buildings)
                if(tile.X>=building.Origin.X&&tile.X<building.Origin.X+building.Width&&
                    tile.Z>=building.Origin.Z&&tile.Z<building.Origin.Z+building.Depth)
                {
                    var door=building.Entrance.FootprintTile;
                    // Soft preference only: never trades additional occupied Tiles for a doorway.
                    return Math.Min(2,Math.Abs(tile.X-door.X)+Math.Abs(tile.Z-door.Z));
                }
            return 0;
        }
    }
    // Small cardinal BFS shared by idle navigation and footprint recovery; no Unity dependencies.
    public static class CitizenNavigation
    {
        private static readonly TileCoordinate[] steps={new TileCoordinate(1,0),new TileCoordinate(-1,0),new TileCoordinate(0,1),new TileCoordinate(0,-1)};
        internal static IEnumerable<TileCoordinate> Neighbors(TileCoordinate t)
        {
            foreach(var step in steps)
            {
                long x=(long)t.X+step.X,z=(long)t.Z+step.Z;
                if(x>=int.MinValue&&x<=int.MaxValue&&z>=int.MinValue&&z<=int.MaxValue)
                    yield return new TileCoordinate((int)x,(int)z);
            }
        }
        private static List<TileCoordinate> Reconstruct(TileCoordinate target,Dictionary<TileCoordinate,TileCoordinate> parents)
        {
            var route=new List<TileCoordinate>{target};
            while(!parents[target].Equals(target)) { target=parents[target]; route.Add(target); }
            route.Reverse(); return route;
        }
        public static IReadOnlyList<TileCoordinate> FindPath(TileCoordinate start,TileCoordinate destination,Func<TileCoordinate,bool> canWalk)
        {
            if(!canWalk(start)||!canWalk(destination)) return Array.Empty<TileCoordinate>();
            return FindNearest(start,canWalk,t=>t.Equals(destination));
        }
        public static IReadOnlyList<TileCoordinate> FindNearest(TileCoordinate start,Func<TileCoordinate,bool> canTraverse,Func<TileCoordinate,bool> canStop)
        {
            if(!canTraverse(start)) return Array.Empty<TileCoordinate>();
            var pending=new Queue<TileCoordinate>();
            var parents=new Dictionary<TileCoordinate,TileCoordinate>{{start,start}};
            pending.Enqueue(start);
            while(pending.Count>0)
            {
                var tile=pending.Dequeue();
                if(canStop(tile)) return Reconstruct(tile,parents);
                foreach(var next in Neighbors(tile))
                    if(!parents.ContainsKey(next)&&canTraverse(next))
                    { parents.Add(next,tile); pending.Enqueue(next); }
            }
            return Array.Empty<TileCoordinate>();
        }
        public static HashSet<TileCoordinate> ReachableTiles(TileCoordinate start,Func<TileCoordinate,bool> canWalk)
        {
            var visited=new HashSet<TileCoordinate>();
            if(!canWalk(start)) return visited;
            var pending=new Queue<TileCoordinate>();pending.Enqueue(start);visited.Add(start);
            while(pending.Count>0)
                foreach(var next in Neighbors(pending.Dequeue()))
                    if(!visited.Contains(next)&&canWalk(next)) { visited.Add(next);pending.Enqueue(next); }
            return visited;
        }
        private readonly struct RecoveryCost : IComparable<RecoveryCost>
        {
            public readonly int Occupied,Steps;
            public readonly long Preference;
            public RecoveryCost(int occupied,long preference,int steps) { Occupied=occupied;Preference=preference;Steps=steps; }
            public int CompareTo(RecoveryCost other)
            {
                int comparison=Occupied.CompareTo(other.Occupied);
                if(comparison==0) comparison=Preference.CompareTo(other.Preference);
                return comparison==0?Steps.CompareTo(other.Steps):comparison;
            }
        }
        private readonly struct RecoveryNode
        {
            public readonly TileCoordinate Tile;
            public readonly RecoveryCost Cost;
            public readonly long Sequence;
            public RecoveryNode(TileCoordinate tile,RecoveryCost cost,long sequence) { Tile=tile;Cost=cost;Sequence=sequence; }
        }
        public static IReadOnlyList<TileCoordinate> FindRecoveryPath(TileCoordinate start,Func<TileCoordinate,bool> canTraverse,
            Func<TileCoordinate,bool> canStop,Func<TileCoordinate,bool> isOccupied,Func<TileCoordinate,int> entrancePenalty)
        {
            if(!canTraverse(start)) return Array.Empty<TileCoordinate>();
            var comparer=Comparer<RecoveryNode>.Create((a,b)=>
            {
                int cost=a.Cost.CompareTo(b.Cost);
                return cost==0?a.Sequence.CompareTo(b.Sequence):cost;
            });
            var pending=new SortedSet<RecoveryNode>(comparer);
            var initial=new RecoveryCost(isOccupied(start)?1:0,0,0);
            var costs=new Dictionary<TileCoordinate,RecoveryCost>{{start,initial}};
            var parents=new Dictionary<TileCoordinate,TileCoordinate>{{start,start}};
            long sequence=0;
            pending.Add(new RecoveryNode(start,initial,sequence++));
            while(pending.Count>0)
            {
                var node=pending.Min;pending.Remove(node);
                if(node.Cost.CompareTo(costs[node.Tile])!=0) continue;
                if(canStop(node.Tile)) return Reconstruct(node.Tile,parents);
                foreach(var next in Neighbors(node.Tile))
                {
                    if(!canTraverse(next)) continue;
                    bool blocked=isOccupied(next);
                    var cost=new RecoveryCost(node.Cost.Occupied+(blocked?1:0),
                        node.Cost.Preference+1+(blocked?Math.Max(0,entrancePenalty(next)):0),node.Cost.Steps+1);
                    if(costs.TryGetValue(next,out var previous)&&cost.CompareTo(previous)>=0) continue;
                    costs[next]=cost;parents[next]=node.Tile;
                    pending.Add(new RecoveryNode(next,cost,sequence++));
                }
            }
            return Array.Empty<TileCoordinate>();
        }        public static IReadOnlyList<IReadOnlyList<TileCoordinate>> NearbyRoutes(TileCoordinate start,Func<TileCoordinate,bool> canWalk,int maxSteps)
        {
            var routes=new List<IReadOnlyList<TileCoordinate>>();
            if(!canWalk(start)) return routes;
            var pending=new Queue<TileCoordinate>();
            var parents=new Dictionary<TileCoordinate,TileCoordinate>{{start,start}};
            var depths=new Dictionary<TileCoordinate,int>{{start,0}};
            pending.Enqueue(start);
            while(pending.Count>0)
            {
                var tile=pending.Dequeue();
                if(depths[tile]>=maxSteps) continue;
                foreach(var next in Neighbors(tile))
                    if(!parents.ContainsKey(next)&&canWalk(next))
                    {
                        parents.Add(next,tile); depths.Add(next,depths[tile]+1);
                        routes.Add(Reconstruct(next,parents)); pending.Enqueue(next);
                    }
            }
            return routes;
        }
    }

    // Half-tile cardinal graph. Footprint edges, not occupied tile centers, are walkable.
    public static class PerimeterNavigation
    {
        public const double StructuralInset = Citizen.BodyHalfExtent + .08;
        public static CitizenPosition Entrance(BuildingState b)=>new CitizenPosition(
            b.Entrance.FootprintTile.X+.5+b.Entrance.Facing.X*.5,
            b.Entrance.FootprintTile.Z+.5+b.Entrance.Facing.Z*.5);
        public static IEnumerable<CitizenPosition> AccessPoints(BuildingState b)
        {
            yield return Entrance(b);
            for(int x=0;x<b.Width;x++)
            { yield return new CitizenPosition(b.Origin.X+x+.5,b.Origin.Z);yield return new CitizenPosition(b.Origin.X+x+.5,b.Origin.Z+b.Depth); }
            for(int z=0;z<b.Depth;z++)
            { yield return new CitizenPosition(b.Origin.X,b.Origin.Z+z+.5);yield return new CitizenPosition(b.Origin.X+b.Width,b.Origin.Z+z+.5); }
        }
        public static bool ClearOfStructures(CitizenPosition p,IEnumerable<BuildingState> buildings)
        {
            double r=Citizen.BodyHalfExtent;
            foreach(var b in buildings)
                if(p.X+r>b.Origin.X+StructuralInset&&p.X-r<b.Origin.X+b.Width-StructuralInset&&
                    p.Z+r>b.Origin.Z+StructuralInset&&p.Z-r<b.Origin.Z+b.Depth-StructuralInset) return false;
            return true;
        }
        public static bool CanOccupy(CitizenPosition p,SettlementTerritory territory,IEnumerable<BuildingState> buildings)
        {
            double r=Citizen.BodyHalfExtent;
            bool boundary=buildings.Any(b=>
                ((Math.Abs(p.X-b.Origin.X)<1e-9||Math.Abs(p.X-b.Origin.X-b.Width)<1e-9)&&p.Z>=b.Origin.Z&&p.Z<=b.Origin.Z+b.Depth)||
                ((Math.Abs(p.Z-b.Origin.Z)<1e-9||Math.Abs(p.Z-b.Origin.Z-b.Depth)<1e-9)&&p.X>=b.Origin.X&&p.X<=b.Origin.X+b.Width));
            for(int z=(int)Math.Floor(p.Z-r);z<=(int)Math.Floor(p.Z+r);z++)
                for(int x=(int)Math.Floor(p.X-r);x<=(int)Math.Floor(p.X+r);x++)
                    if(!territory.IsClaimed(new TileCoordinate(x,z))&&!boundary) return false;
            return ClearOfStructures(p,buildings);
        }
        public static bool SegmentClear(CitizenPosition a,CitizenPosition b,Func<CitizenPosition,bool> walk)
        {
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(a.DistanceSquared(b))/.05));
            for(int i=0;i<=steps;i++)
                if(!walk(new CitizenPosition(a.X+(b.X-a.X)*i/steps,a.Z+(b.Z-a.Z)*i/steps))) return false;
            return true;
        }
        private static CitizenPosition Position(TileCoordinate node)=>new CitizenPosition(node.X*.5,node.Z*.5);
        private static TileCoordinate? Attach(CitizenPosition start,Func<CitizenPosition,bool> walk)
        {
            if(!walk(start)) return null;
            var nearest=new TileCoordinate((int)Math.Round(start.X*2),(int)Math.Round(start.Z*2));
            return walk(Position(nearest))&&SegmentClear(start,Position(nearest),walk)?nearest:(TileCoordinate?)null;
        }
        public static IReadOnlyList<CitizenPosition> FindPath(CitizenPosition start,CitizenPosition end,Func<CitizenPosition,bool> walk)
        {
            var a=Attach(start,walk);var b=Attach(end,walk);
            if(!a.HasValue||!b.HasValue) return Array.Empty<CitizenPosition>();
            var route=CitizenNavigation.FindPath(a.Value,b.Value,n=>walk(Position(n)));
            if(route.Count==0) return Array.Empty<CitizenPosition>();
            var result=route.Select(Position).ToList();
            if(result[result.Count-1].DistanceSquared(end)>1e-12) result.Add(end);
            return result;
        }
        public static HashSet<TileCoordinate> Reachable(CitizenPosition start,Func<CitizenPosition,bool> walk)
        {
            var node=Attach(start,walk);
            return node.HasValue?CitizenNavigation.ReachableTiles(node.Value,n=>walk(Position(n))):new HashSet<TileCoordinate>();
        }
        public static bool Contains(HashSet<TileCoordinate> nodes,CitizenPosition p)=>nodes.Contains(new TileCoordinate((int)Math.Round(p.X*2),(int)Math.Round(p.Z*2)));
        public static IReadOnlyList<IReadOnlyList<CitizenPosition>> Nearby(CitizenPosition start,Func<CitizenPosition,bool> walk,int steps)
        {
            var node=Attach(start,walk);
            return node.HasValue?CitizenNavigation.NearbyRoutes(node.Value,n=>walk(Position(n)),steps*2)
                .Select(r=>(IReadOnlyList<CitizenPosition>)r.Select(Position).ToArray()).ToArray():Array.Empty<IReadOnlyList<CitizenPosition>>();
        }
        // Unit boundary segments are canonical and deduplicated, including shared edges.
        public static IEnumerable<(CitizenPosition Start,CitizenPosition End)> Segments(IEnumerable<BuildingState> buildings)
        {
            var keys=new HashSet<(int X,int Z,bool Vertical)>();
            foreach(var b in buildings)
            {
                for(int x=b.Origin.X;x<b.Origin.X+b.Width;x++)
                {keys.Add((x,b.Origin.Z,false));keys.Add((x,b.Origin.Z+b.Depth,false));}
                for(int z=b.Origin.Z;z<b.Origin.Z+b.Depth;z++)
                {keys.Add((b.Origin.X,z,true));keys.Add((b.Origin.X+b.Width,z,true));}
            }
            foreach(var k in keys.OrderBy(k=>k.Z).ThenBy(k=>k.X).ThenBy(k=>k.Vertical))
                yield return (new CitizenPosition(k.X,k.Z),new CitizenPosition(k.X+(k.Vertical?0:1),k.Z+(k.Vertical?1:0)));
        }
    }
}
