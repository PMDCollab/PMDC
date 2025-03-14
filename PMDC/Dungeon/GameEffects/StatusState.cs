using System;
using RogueEssence.Data;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using Newtonsoft.Json;

namespace PMDC.Dungeon
{
    [Serializable]
    public class HPState : StatusState
    {
        public int HP;
        public HPState() { }
        public HPState(int hp) { HP = hp; }
        protected HPState(HPState other) { HP = other.HP; }
        public override GameplayState Clone() { return new HPState(this); }
    }
    [Serializable]
    public class RecentState : StatusState
    {
        public RecentState() { }
        public override GameplayState Clone() { return new RecentState(); }
    }
    [Serializable]
    public class SlotState : StatusState
    {
        public int Slot;
        public SlotState() { }
        protected SlotState(SlotState other) { Slot = other.Slot; }
        public override GameplayState Clone() { return new SlotState(this); }
    }
    [Serializable]
    public class IndexState : StatusState
    {
        public int Index;
        public IndexState() { }
        public IndexState(int index) { Index = index; }
        protected IndexState(IndexState other) { Index = other.Index; }
        public override GameplayState Clone() { return new IndexState(this); }
    }
    [Serializable]
    public class IDState : StatusState
    {
        public string ID;
        public IDState() { ID = ""; }
        public IDState(string index) { ID = index; }
        protected IDState(IDState other) { ID = other.ID; }
        public override GameplayState Clone() { return new IDState(this); }
    }
    [Serializable]
    public class StatChangeState : StatusState
    {
        public Stat ChangeStat;
        public StatChangeState() { }
        public StatChangeState(Stat stat) { ChangeStat = stat; }
        protected StatChangeState(StatChangeState other) { ChangeStat = other.ChangeStat; }
        public override GameplayState Clone() { return new StatChangeState(this); }
    }
    [Serializable]
    public class BadStatusState : StatusState
    {
        public BadStatusState() { }
        public override GameplayState Clone() { return new BadStatusState(); }
    }
    [Serializable]
    public class GoodStatusState : StatusState
    {
        public GoodStatusState() { }
        public override GameplayState Clone() { return new GoodStatusState(); }
    }
    [Serializable]
    public class TransferStatusState : StatusState
    {
        public TransferStatusState() { }
        public override GameplayState Clone() { return new TransferStatusState(); }
    }
    [Serializable]
    public class MajorStatusState : StatusState
    {
        public MajorStatusState() { }
        public override GameplayState Clone() { return new MajorStatusState(); }
    }
    [Serializable]
    public class ParalyzeState : StatusState
    {
        public bool Recent;
        public ParalyzeState() { }
        public ParalyzeState(bool recent) { Recent = recent; }
        protected ParalyzeState(ParalyzeState other) { Recent = other.Recent; }
        public override GameplayState Clone() { return new ParalyzeState(this); }
    }
    [Serializable]
    public class AttackedThisTurnState : StatusState
    {
        public bool Attacked;
        public AttackedThisTurnState() { }
        public AttackedThisTurnState(bool attacked) { Attacked = attacked; }
        protected AttackedThisTurnState(AttackedThisTurnState other) { Attacked = other.Attacked; }
        public override GameplayState Clone() { return new AttackedThisTurnState(this); }
    }
    [Serializable]
    public class WalkedThisTurnState : StatusState
    {
        public bool Walked;
        public WalkedThisTurnState() { }
        public WalkedThisTurnState(bool walked) { Walked = walked; }
        protected WalkedThisTurnState(WalkedThisTurnState other) { Walked = other.Walked; }
        public override GameplayState Clone() { return new WalkedThisTurnState(this); }
    }
    [Serializable]
    public class CategoryState : StatusState
    {
        public BattleData.SkillCategory Category;
        public CategoryState() { }
        public CategoryState(BattleData.SkillCategory category) { Category = category; }
        protected CategoryState(CategoryState other) { Category = other.Category; }
        public override GameplayState Clone() { return new CategoryState(this); }
    }
    [Serializable]
    public class ElementState : StatusState
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;
        public ElementState() { }
        public ElementState(string element) { Element = element; }
        protected ElementState(ElementState other) { Element = other.Element; }
        public override GameplayState Clone() { return new ElementState(this); }
    }
    [Serializable]
    public class MonsterIDState : StatusState
    {
        public MonsterID MonID;
        public MonsterIDState() { }
        public MonsterIDState(MonsterID id) { MonID = id; }
        protected MonsterIDState(MonsterIDState other) { MonID = other.MonID; }
        public override GameplayState Clone() { return new MonsterIDState(this); }
    }
}
