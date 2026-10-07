using System;
using System.Collections.Generic;

namespace ColonyGame.World
{
    public enum CitizenSex { Male, Female }
    public enum CitizenProfession { Unemployed, Builder, Farmer, Butcher, Fisher, Lumberjack, Miner, Warrior, Scientist, Mage }
    public enum CitizenTask { Idle, Wandering, GatheringWoodOutbound, GatheringWood, ReturningWood, Recovery, MovingToWork, Working, ReturningHome, Sleeping }
    public enum CitizenMovement { Idle, Walking, Recovering }

    public readonly struct CitizenPosition
    {
        public double X { get; }
        public double Z { get; }
        public CitizenPosition(double x, double z)
        {
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z))
                throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Z = z;
        }
        public TileCoordinate Tile => new TileCoordinate((int)Math.Floor(X), (int)Math.Floor(Z));
        public static CitizenPosition Center(TileCoordinate tile) => new CitizenPosition(tile.X + .5, tile.Z + .5);
        public double DistanceSquared(CitizenPosition other) => (X-other.X)*(X-other.X)+(Z-other.Z)*(Z-other.Z);
    }

    public sealed class CitizenAttributes
    {
        public int Strength { get; }
        public int Dexterity { get; }
        public int Intelligence { get; }
        public int Endurance { get; }
        public int Charisma { get; }
        public CitizenAttributes(int strength, int dexterity, int intelligence, int endurance, int charisma)
        {
            foreach (int value in new[] { strength, dexterity, intelligence, endurance, charisma })
                if (value < 1 || value > 10) throw new ArgumentOutOfRangeException(nameof(strength));
            Strength=strength; Dexterity=dexterity; Intelligence=intelligence; Endurance=endurance; Charisma=charisma;
        }
    }

    // Identity/data survives replacement of prototype visuals and future profession changes.
    public sealed class Citizen
    {
        public string Id { get; }
        public string FirstName { get; }
        public CitizenSex Sex { get; }
        public int Age { get; }
        public CitizenProfession Profession { get; internal set; } = CitizenProfession.Unemployed;
        public string WorkplaceBuildingId { get; internal set; }
        public string ResidenceBuildingId { get; internal set; }
        internal CitizenTask ScheduledTask = CitizenTask.Idle;
        internal CitizenTask GatheringTask = CitizenTask.Idle;
        public CitizenTask Task => Movement == CitizenMovement.Recovering ? CitizenTask.Recovery :
            GatheringTask != CitizenTask.Idle ? GatheringTask : ScheduledTask != CitizenTask.Idle ? ScheduledTask : Movement == CitizenMovement.Walking ? CitizenTask.Wandering : CitizenTask.Idle;
        public CitizenAttributes Attributes { get; }
        public CitizenPosition Position { get; internal set; }
        public CitizenPosition Destination { get; internal set; }
        public CitizenMovement Movement { get; internal set; }
        public bool Active { get; internal set; } = true;
        public const double BodyHalfExtent = .28;
        internal readonly Queue<CitizenPosition> Route = new Queue<CitizenPosition>();
        public IReadOnlyList<CitizenPosition> PlannedRoute => Route.ToArray();
        // Explicit logical occupancy after walking to a valid exterior entrance.
        public string InsideBuildingId { get; internal set; }
        internal readonly HashSet<TileCoordinate> RecoveryTiles=new HashSet<TileCoordinate>();
        internal CitizenPosition RecoveryOrigin;
        internal double Pause;
        public Citizen(string id, string firstName, CitizenSex sex, int age, CitizenAttributes attributes, CitizenPosition position)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Citizen identity is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First name is required.", nameof(firstName));
            if (!Enum.IsDefined(typeof(CitizenSex),sex)) throw new ArgumentOutOfRangeException(nameof(sex));
            if (age < 0) throw new ArgumentOutOfRangeException(nameof(age));
            Id=id; FirstName=firstName; Sex=sex; Age=age;
            Attributes=attributes ?? throw new ArgumentNullException(nameof(attributes));
            Position=Destination=position;
        }
    }
}