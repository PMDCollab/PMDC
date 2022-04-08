using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

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

    [Serializable]
    public class ShopPriceState : MapStatusState
    {
        public int Amount;
        public int Cart;
        public ShopPriceState() { }
        public ShopPriceState(int amt) { Amount = amt; }
        protected ShopPriceState(ShopPriceState other) { Amount = other.Amount; Cart = other.Cart; }
        public override GameplayState Clone() { return new ShopPriceState(this); }
    }

    [Serializable]
    public class ShopSecurityState : MapStatusState
    {
        public SpawnList<MobSpawn> Security;
        public ShopSecurityState() { Security = new SpawnList<MobSpawn>(); }
        protected ShopSecurityState(ShopSecurityState other) : this()
        {
            for (int ii = 0; ii < other.Security.Count; ii++)
                Security.Add(other.Security.GetSpawn(ii).Copy(), other.Security.GetSpawnRate(ii));
        }
        public override GameplayState Clone() { return new ShopSecurityState(this); }
    }
}
