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
        public bool CanOccupy(CitizenPosition p)=>Eligible(p,false);
        // Search permission is not movement permission. Actual recovery uses a route whitelist.
        public bool CanTraverseRecovery(CitizenPosition p,CitizenPosition origin)=>Eligible(p,true);
        public bool CanFollowRecovery(CitizenPosition p,ISet<TileCoordinate> permitted)=>permitted!=null&&Eligible(p,true,permitted);
        public bool IsOccupied(TileCoordinate tile)=>occupied(tile);
        public bool IsConnected(CitizenPosition p)=>CanOccupy(p)&&connected.Contains(p.Tile);
        public void RefreshConnectivity()
        {
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
}