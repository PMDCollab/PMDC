using System;
using RogueEssence.LevelGen;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dev;
using RogueEssence.Dungeon;
using Newtonsoft.Json;

namespace PMDC.Dungeon
{

    [Serializable]
    public class WaterTerrainState : TerrainState
    {
        public WaterTerrainState() { }
        public override GameplayState Clone() { return new WaterTerrainState(); }
    }

    [Serializable]
    public class LavaTerrainState : TerrainState
    {
        public LavaTerrainState() { }
        public override GameplayState Clone() { return new LavaTerrainState(); }
    }

    [Serializable]
    public class AbyssTerrainState : TerrainState
    {
        public AbyssTerrainState() { }
        public override GameplayState Clone() { return new AbyssTerrainState(); }
    }

    [Serializable]
    public class WallState : TerrainState
    {
        public WallState() { }
        public override GameplayState Clone() { return new WallState(); }
    }

    [Serializable]
    public class FoliageTerrainState : TerrainState
    {
        public FoliageTerrainState() { }
        public override GameplayState Clone() { return new FoliageTerrainState(); }
    }
}
