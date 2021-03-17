using System;
using RogueEssence.Dungeon;

namespace PMDC.Dungeon
{

    [Serializable]
    public class AdditionalEffectState : SkillState
    {
        public int EffectChance;
        public AdditionalEffectState() { }
        public AdditionalEffectState(int effectChance) { EffectChance = effectChance; }
        protected AdditionalEffectState(AdditionalEffectState other) { EffectChance = other.EffectChance; }
        public override GameplayState Clone() { return new AdditionalEffectState(this); }
    }
    [Serializable]
    public class ContactState : SkillState
    {
        public ContactState() { }
        public override GameplayState Clone() { return new ContactState(); }
    }
    [Serializable]
    public class SoundState : SkillState
    {
        public SoundState() { }
        public override GameplayState Clone() { return new SoundState(); }
    }
    [Serializable]
    public class FistState : SkillState
    {
        public FistState() { }
        public override GameplayState Clone() { return new FistState(); }
    }
    [Serializable]
    public class PulseState : SkillState
    {
        public PulseState() { }
        public override GameplayState Clone() { return new PulseState(); }
    }
    [Serializable]
    public class JawState : SkillState
    {
        public JawState() { }
        public override GameplayState Clone() { return new JawState(); }
    }
}
