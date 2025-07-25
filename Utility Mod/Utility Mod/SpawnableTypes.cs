namespace Utility_Mod
{
    public enum BlockType
    {
        // Terrain
        Dirt = 0,
        CaveRock = 1,
        SandyEarth = 2,
        DesertSand = 50,
        // Environment-specific terrain
        CaveEarth = 100,
        HellRock = 103,
        DesertCaveRock = 104,
        DesertCaveEarth = 105,
        DesertEarth = 110,
        CityEarth = 111,
        Skulls = 101,
        Bones = 102,
        // Alien terrain
        AlienEarth = 43,
        AlienFlesh = 44,
        AlienExplodingFlesh = 45,
        AlienDirt = 47,
        // Large terrain blocks
        BigBlock = 11,
        SacredBigBlock = 13,
        
        // Bridges/Platforms/Ladders
        Bridge = 7,
        Bridge2 = 60,
        MetalBridge = 61,
        SacredBridge = 62,
        AlienBridge = 63,
        MetalBridge2 = 64,
        Ladder = 51,
        AlienLadder = 52,
        MetalLadder = 53,
        CityLadder = 54,
        DesertLadder = 55,
        
        // Building Materials & Structures
        Brick = 3,
        BrickTop = 4,
        Metal = 5,
        Steel = 6,
        // Military structures
        Bunker = 80,  // Urban Brick Sewer
        WatchTower = 82,  // Watch Tower Floor
        // Roofs
        Roof = 81,
        ThatchRoof = 84,
        FactoryRoof = 88,
        DesertRoof = 89,
        DesertRoofRed = 90,
        TentRoof = 91,
        // Sacred/Temple structures
        SacredTemple = 112,
        SacredTempleGold = 117,
        // City structures
        CityBrick = 113,
        CityRoad = 116,
        // Desert structures
        DesertBrick = 114,
        // Miscellaneous structures
        Pipe = 83,
        OilPipe = 124,
        Statue = 85,
        TyreBlock = 87,
        CityAssMouth = 115,
        
        // Special Blocks
        FallingBlock = 8,
        Quicksand = 123,
        // Large blocks
        Boulder = 10,
        BoulderBig = 12,
        // Other special blocks
        Sandbag = 9,
        Vault = 120,
        SmallCageBlock = 121,
        StandardCage = 122,
        
        // Destructibles (barrels/explosives)
        ExplosiveBarrel = 20,
        RedExplosiveBarrel = 21,
        PropaneTank = 22,
        DesertOilBarrel = 23,
        OilTank = 24,
        
        // Organic Destructibles
        BeeHive = 40,
        AlienEgg = 41,
        AlienEggExplosive = 42,
        
        // Crates
        Crate = 30,
        AmmoCrate = 31,
        TimeAmmoCrate = 32,
        RCCarAmmoCrate = 33,
        AirStrikeAmmoCrate = 34,
        MechDropAmmoCrate = 35,
        AlienPheromonesAmmoCrate = 36,
        SteroidsAmmoCrate = 37,
        PigAmmoCrate = 38,
        FlexAmmoCrate = 39,
        MoneyCrate = 70,
        PillsCrate = 71,
        
        // Hazards (note: these are BlockTypes that appear in the Hazards menu)
        AlienBarbShooter = 46,
        BuriedRocket = 86
    }

    public enum SpawnableDoodadType
    {
        // Checkpoints & Cages
        RescueCage = 0,
        CheckPoint = 1,
        
        // Hazards
        Spikes = 2,
        Mines = 3,
        SawBlade = 4,
        HiddenExplosives = 5,
        
        // Doors
        Door = 8,
        MookDoor = 9,
        
        // Environment
        ZiplinePoint = 10,
        Scaffolding = 12,
        Fence = 13,
        Tree = 16,
        Bush = 17,
        
        // Hanging Objects (Interactive)
        HangingVines = 18,
        HangingBrazier = 19
    }
    
    public static class SpawnableTypesExtensions
    {
        public static string GetDisplayName(this BlockType blockType)
        {
            switch (blockType)
            {
                // Terrain
                case BlockType.Dirt: return "Dirt";
                case BlockType.CaveRock: return "Cave Rock";
                case BlockType.SandyEarth: return "Sandy Earth";
                case BlockType.DesertSand: return "Desert Sand";
                case BlockType.AlienEarth: return "Alien Earth";
                case BlockType.AlienFlesh: return "Alien Flesh";
                case BlockType.AlienExplodingFlesh: return "Alien Exploding Flesh";
                case BlockType.AlienDirt: return "Alien Dirt";
                
                // Bridges/Platforms/Ladders
                case BlockType.Bridge: return "Bridge";
                case BlockType.Bridge2: return "Bridge 2";
                case BlockType.MetalBridge: return "Metal Bridge";
                case BlockType.SacredBridge: return "Sacred Bridge";
                case BlockType.AlienBridge: return "Alien Bridge";
                case BlockType.MetalBridge2: return "Metal Bridge 2";
                case BlockType.Ladder: return "Ladder";
                case BlockType.AlienLadder: return "Alien Ladder";
                case BlockType.MetalLadder: return "Metal Ladder";
                case BlockType.CityLadder: return "City Ladder";
                case BlockType.DesertLadder: return "Desert Ladder";
                
                // Building Materials & Structures
                case BlockType.Brick: return "Brick";
                case BlockType.BrickTop: return "Brick Top";
                case BlockType.Metal: return "Metal";
                case BlockType.Steel: return "Steel";
                case BlockType.Bunker: return "Urban Brick";
                case BlockType.Roof: return "Roof";
                case BlockType.WatchTower: return "Watch Tower Floor";
                case BlockType.Pipe: return "Pipe";
                case BlockType.ThatchRoof: return "Thatch Roof";
                case BlockType.Statue: return "Statue";
                case BlockType.BuriedRocket: return "Buried Rocket";
                case BlockType.TyreBlock: return "Tyre Block";
                case BlockType.FactoryRoof: return "Factory Roof";
                case BlockType.DesertRoof: return "Desert Roof";
                case BlockType.DesertRoofRed: return "Desert Roof Red";
                case BlockType.TentRoof: return "Tent Roof";
                
                // Special Blocks
                case BlockType.FallingBlock: return "Falling Block";
                case BlockType.Sandbag: return "Sandbag";
                case BlockType.Boulder: return "Boulder";
                case BlockType.BigBlock: return "Big Block";
                case BlockType.BoulderBig: return "Boulder Big";
                case BlockType.SacredBigBlock: return "Sacred Big Block";
                case BlockType.Vault: return "Vault";
                case BlockType.SmallCageBlock: return "Small Cage Block";
                case BlockType.StandardCage: return "Standard Cage";
                case BlockType.Quicksand: return "Quicksand";
                case BlockType.OilPipe: return "Oil Pipe";
                case BlockType.AlienBarbShooter: return "Alien Barb Shooter";
                
                // Destructibles
                case BlockType.ExplosiveBarrel: return "Explosive Barrel";
                case BlockType.RedExplosiveBarrel: return "Red Explosive Barrel";
                case BlockType.PropaneTank: return "Propane Tank";
                case BlockType.DesertOilBarrel: return "Desert Oil Barrel";
                case BlockType.OilTank: return "Oil Tank";
                
                // Organic Destructibles
                case BlockType.BeeHive: return "Beehive";
                case BlockType.AlienEgg: return "Alien Egg";
                case BlockType.AlienEggExplosive: return "Alien Egg Explosive";
                
                // Crates
                case BlockType.Crate: return "Crate";
                case BlockType.AmmoCrate: return "Ammo Crate";
                case BlockType.TimeAmmoCrate: return "Time Ammo Crate";
                case BlockType.RCCarAmmoCrate: return "RC Car Ammo Crate";
                case BlockType.AirStrikeAmmoCrate: return "Air Strike Ammo Crate";
                case BlockType.MechDropAmmoCrate: return "Mech Drop Ammo Crate";
                case BlockType.AlienPheromonesAmmoCrate: return "Alien Pheromones Ammo Crate";
                case BlockType.SteroidsAmmoCrate: return "Steroids Ammo Crate";
                case BlockType.PigAmmoCrate: return "Pig Ammo Crate";
                case BlockType.FlexAmmoCrate: return "Flex Ammo Crate";
                case BlockType.MoneyCrate: return "Money Crate";
                case BlockType.PillsCrate: return "Pills Crate";
                
                // Cave/Hell
                case BlockType.CaveEarth: return "Cave Earth";
                case BlockType.Skulls: return "Skulls";
                case BlockType.Bones: return "Bones";
                case BlockType.HellRock: return "Hell Rock";
                case BlockType.DesertCaveRock: return "Desert Cave Rock";
                case BlockType.DesertCaveEarth: return "Desert Cave Earth";
                
                // City/Desert/Sacred
                case BlockType.DesertEarth: return "Desert Earth";
                case BlockType.CityEarth: return "City Earth";
                case BlockType.SacredTemple: return "Sacred Temple";
                case BlockType.CityBrick: return "City Brick";
                case BlockType.DesertBrick: return "Desert Brick";
                case BlockType.CityAssMouth: return "City Ass Mouth";
                case BlockType.CityRoad: return "City Road";
                case BlockType.SacredTempleGold: return "Sacred Temple Gold";
                
                default: return blockType.ToString();
            }
        }
        
        public static string GetDisplayName(this SpawnableDoodadType doodadType)
        {
            switch (doodadType)
            {
                // Checkpoints & Cages
                case SpawnableDoodadType.RescueCage: return "Rescue Cage";
                case SpawnableDoodadType.CheckPoint: return "Checkpoint";
                
                // Hazards
                case SpawnableDoodadType.Spikes: return "Spikes";
                case SpawnableDoodadType.Mines: return "Mine Field";
                case SpawnableDoodadType.SawBlade: return "Saw Blade";
                case SpawnableDoodadType.HiddenExplosives: return "Hidden Explosives";
                
                // Doors
                case SpawnableDoodadType.Door: return "Door";
                case SpawnableDoodadType.MookDoor: return "Mook Door";
                
                // Environment
                case SpawnableDoodadType.ZiplinePoint: return "Zipline Anchor Point";
                case SpawnableDoodadType.Scaffolding: return "Scaffolding";
                case SpawnableDoodadType.Fence: return "Fence";
                case SpawnableDoodadType.Tree: return "Tree";
                case SpawnableDoodadType.Bush: return "Bush";
                
                // Hanging Objects
                case SpawnableDoodadType.HangingVines: return "Hanging Vines";
                case SpawnableDoodadType.HangingBrazier: return "Hanging Brazier";
                
                default: return doodadType.ToString();
            }
        }
    }
}