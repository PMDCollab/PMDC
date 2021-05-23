using System;
using RogueEssence.Data;
using RogueEssence.Dungeon;
using System.Collections.Generic;
using RogueEssence.Dev;

namespace PMDC.Dungeon
{

    public enum ExclusiveItemType
    {
        None,

        Claw,
        Fang,
        Tooth,
        Card,
        Hair,
        Tail,
        Wing,
        Dew,
        Drool,
        Sweat,
        Gasp,
        Foam,
        Song,
        Beam,
        Thorn,
        Shoot,
        Branch,
        Twig,
        Root,
        Seed,
        Mud,
        Leaf,
        Horn,

        Tag,
        Jaw,
        Dust,
        Jewel,
        Crest,
        Seal,
        Charm,
        Rock,
        Pebble,
        Ore,
        Shard,
        Coin,
        Key,
        Heart,
        Aroma,
        Medal,
        Ring,
        Earring,
        Brooch,
        Guard,

        Blade,
        Band,
        Belt,
        Choker,
        Bow,
        Scarf,
        Torc,
        Sash,
        Hat,
        Ruff,
        Crown,
        Tiara,
        Collar,
        Bangle,
        Armlet,
        Tie,
        Cape,
        Mantle,
        Cap,
        Mask,
        Helmet,
        Armor,
        Shield,
        Drill,
        Apron,
        Poncho,
        Veil,
        Robe,
        Specs,
        Glasses,
        Scope,
        Float,
        Dress,
        Coat
    }

    [Serializable]
    public class ExclusiveState : ItemState
    {

        public ExclusiveItemType ItemType;
        public ExclusiveState() { }
        public ExclusiveState(ExclusiveItemType itemType) { ItemType = itemType; }
        protected ExclusiveState(ExclusiveState other) { ItemType = other.ItemType; }
        public override GameplayState Clone() { return new ExclusiveState(this); }
    }

    [Serializable]
    public class FamilyState : ItemState
    {
        [DataType(1, DataManager.DataType.Monster, false)]
        public List<int> Members;
        public FamilyState() { Members = new List<int>(); }
        public FamilyState(int[] dexNums) : this()
        {
            Members.AddRange(dexNums);
        }
        protected FamilyState(FamilyState other) : this()
        {
            Members.AddRange(other.Members);
        }
        public override GameplayState Clone() { return new FamilyState(this); }
    }


    [Serializable]
    public class EdibleState : ItemState
    {
        public override GameplayState Clone() { return new EdibleState(); }
    }

    [Serializable]
    public class FoodState : ItemState
    {
        public override GameplayState Clone() { return new FoodState(); }
    }

    [Serializable]
    public class BerryState : ItemState
    {
        public override GameplayState Clone() { return new BerryState(); }
    }

    [Serializable]
    public class SeedState : ItemState
    {
        public override GameplayState Clone() { return new SeedState(); }
    }

    [Serializable]
    public class HerbState : ItemState
    {
        public override GameplayState Clone() { return new HerbState(); }
    }

    [Serializable]
    public class GummiState : ItemState
    {
        public override GameplayState Clone() { return new GummiState(); }
    }

    [Serializable]
    public class DrinkState : ItemState
    {
        public override GameplayState Clone() { return new DrinkState(); }
    }



    [Serializable]
    public class WandState : ItemState
    {
        public override GameplayState Clone() { return new WandState(); }
    }

    [Serializable]
    public class OrbState : ItemState
    {
        public override GameplayState Clone() { return new OrbState(); }
    }

    [Serializable]
    public class AmmoState : ItemState
    {
        public override GameplayState Clone() { return new AmmoState(); }
    }

    [Serializable]
    public class UtilityState : ItemState
    {
        public override GameplayState Clone() { return new UtilityState(); }
    }

    [Serializable]
    public class HeldState : ItemState
    {
        public override GameplayState Clone() { return new HeldState(); }
    }

    [Serializable]
    public class MachineState : ItemState
    {
        public override GameplayState Clone() { return new MachineState(); }
    }

    [Serializable]
    public class RecruitState : ItemState
    {
        public override GameplayState Clone() { return new RecruitState(); }
    }
}
