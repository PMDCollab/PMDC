using System;
using RogueEssence.Data;
using RogueEssence.Dev;
using RogueEssence.Dungeon;

namespace PMDC.Dungeon
{
    [Serializable]
    public class StickyHoldState : CharState
    {
        public StickyHoldState() { }
        public override GameplayState Clone() { return new StickyHoldState(); }
    }

    [Serializable]
    public class AnchorState : CharState
    {
        public AnchorState() { }
        public override GameplayState Clone() { return new AnchorState(); }
    }
    [Serializable]
    public class HitAndRunState : CharState
    {
        [DataType(0, DataManager.DataType.Item, false)]
        public string OriginItem;
        public HitAndRunState() { OriginItem = ""; }
        public HitAndRunState(string origin) { OriginItem = origin; }
        public HitAndRunState(HitAndRunState other) { OriginItem = other.OriginItem; }
        public override GameplayState Clone() { return new HitAndRunState(this); }
    }

    [Serializable]
    public class SleepWalkerState : CharState
    {
        public SleepWalkerState() { }
        public override GameplayState Clone() { return new SleepWalkerState(); }
    }

    [Serializable]
    public class ChargeWalkerState : CharState
    {
        public ChargeWalkerState() { }
        public override GameplayState Clone() { return new ChargeWalkerState(); }
    }

    [Serializable]
    public class DrainDamageState : CharState
    {
        public int Mult;
        public DrainDamageState() { }
        public DrainDamageState(int mult) { Mult = mult; }
        public DrainDamageState(DrainDamageState other) { Mult = other.Mult; }
        public override GameplayState Clone() { return new DrainDamageState(this); }
    }

    [Serializable]
    public class NoRecoilState : CharState
    {
        public NoRecoilState() { }
        public override GameplayState Clone() { return new NoRecoilState(); }
    }

    [Serializable]
    public class HeatproofState : CharState
    {
        public HeatproofState() { }
        public override GameplayState Clone() { return new HeatproofState(); }
    }

    [Serializable]
    public class LavaState : CharState
    {
        public LavaState() { }
        public override GameplayState Clone() { return new LavaState(); }
    }

    [Serializable]
    public class PoisonState : CharState
    {
        public PoisonState() { }
        public override GameplayState Clone() { return new PoisonState(); }
    }

    [Serializable]
    public class MagicGuardState : CharState
    {
        public MagicGuardState() { }
        public override GameplayState Clone() { return new MagicGuardState(); }
    }

    [Serializable]
    public class SandState : CharState
    {
        public SandState() { }
        public override GameplayState Clone() { return new SandState(); }
    }

    [Serializable]
    public class HailState : CharState
    {
        public HailState() { }
        public override GameplayState Clone() { return new HailState(); }
    }

    [Serializable]
    public class SnipeState : CharState
    {
        public SnipeState() { }
        public override GameplayState Clone() { return new SnipeState(); }
    }

    [Serializable]
    public class PoisonHealState : CharState
    {
        public PoisonHealState() { }
        public override GameplayState Clone() { return new PoisonHealState(); }
    }

    [Serializable]
    public class HeavyWeightState : CharState
    {
        public HeavyWeightState() { }
        public override GameplayState Clone() { return new HeavyWeightState(); }
    }

    [Serializable]
    public class LightWeightState : CharState
    {
        public LightWeightState() { }
        public override GameplayState Clone() { return new LightWeightState(); }
    }

    [Serializable]
    public class TrapState : CharState
    {
        public TrapState() { }
        public override GameplayState Clone() { return new TrapState(); }
    }

    [Serializable]
    public class GripState : CharState
    {
        public GripState() { }
        public override GameplayState Clone() { return new GripState(); }
    }

    [Serializable]
    public class ExtendWeatherState : CharState
    {
        public ExtendWeatherState() { }
        public override GameplayState Clone() { return new ExtendWeatherState(); }
    }

    [Serializable]
    public class BindState : CharState
    {
        public BindState() { }
        public override GameplayState Clone() { return new BindState(); }
    }

    [Serializable]
    public class GemBoostState : CharState
    {
        public GemBoostState() { }
        public override GameplayState Clone() { return new GemBoostState(); }
    }


    [Serializable]
    public class CoinModGenState : ModGenState
    {
        public CoinModGenState() { }
        public CoinModGenState(int mod) : base(mod) { }
        protected CoinModGenState(CoinModGenState other) : base(other) { }
        public override GameplayState Clone() { return new CoinModGenState(this); }
    }

    [Serializable]
    public class StairsModGenState : ModGenState
    {
        public StairsModGenState() { }
        public StairsModGenState(int mod) : base(mod) { }
        protected StairsModGenState(StairsModGenState other) : base(other) { }
        public override GameplayState Clone() { return new StairsModGenState(this); }
    }

    [Serializable]
    public class ChestModGenState : ModGenState
    {
        public ChestModGenState() { }
        public ChestModGenState(int mod) : base(mod) { }
        protected ChestModGenState(ChestModGenState other) : base(other) { }
        public override GameplayState Clone() { return new ChestModGenState(this); }
    }

    [Serializable]
    public class ShopModGenState : ModGenState
    {
        public ShopModGenState() { }
        public ShopModGenState(int mod) : base(mod) { }
        protected ShopModGenState(ShopModGenState other) : base(other) { }
        public override GameplayState Clone() { return new ShopModGenState(this); }
    }
}
