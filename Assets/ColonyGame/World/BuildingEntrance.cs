using System;

namespace ColonyGame.World
{
    // Doorway transition data only. Footprint/access occupancy is unchanged.
    public readonly struct BuildingEntrance
    {
        public TileCoordinate FootprintTile { get; }
        public TileCoordinate Facing { get; }
        public TileCoordinate ExteriorTile => new TileCoordinate(checked(FootprintTile.X+Facing.X),checked(FootprintTile.Z+Facing.Z));
        public bool IsExteriorAccessible(SettlementState settlement) => settlement!=null&&settlement.Territory.IsClaimed(ExteriorTile)&&!settlement.IsOccupied(ExteriorTile);
        private BuildingEntrance(TileCoordinate footprintTile,TileCoordinate facing)
        { FootprintTile=footprintTile; Facing=facing; }

        public static BuildingEntrance For(BuildingDefinition definition,TileCoordinate origin,int orientationDegrees)
        {
            if(definition==null) throw new ArgumentNullException(nameof(definition));
            if(!BuildingDefinition.IsValidOrientation(orientationDegrees)) throw new ArgumentOutOfRangeException(nameof(orientationDegrees));
            // Canonical south edge, lower central Tile for even widths; matches Unity's Y rotation.
            int u=(definition.BaseWidth-1)/2,x,z;
            TileCoordinate facing;
            switch(orientationDegrees)
            {
                case 0: x=u;z=0;facing=new TileCoordinate(0,-1);break;
                case 90: x=0;z=definition.BaseWidth-1-u;facing=new TileCoordinate(-1,0);break;
                case 180: x=definition.BaseWidth-1-u;z=definition.BaseDepth-1;facing=new TileCoordinate(0,1);break;
                default: x=definition.BaseDepth-1;z=u;facing=new TileCoordinate(1,0);break;
            }
            return new BuildingEntrance(new TileCoordinate(checked(origin.X+x),checked(origin.Z+z)),facing);
        }
    }
}
