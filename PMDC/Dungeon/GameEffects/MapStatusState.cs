using System;
using RogueEssence;
using RogueEssence.Dungeon;

namespace PMDC.Dungeon
{

    [Serializable]
    public class MapTickState : MapStatusState
    {
        public int Counter;
        public MapTickState() { }
        public MapTickState(int counter) { Counter = counter; }
        protected MapTickState(MapTickState other) { Counter = other.Counter; }
        public override GameplayState Clone() { return new MapTickState(this); }
    }
}
