using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using Newtonsoft.Json;
using RogueEssence.Dev;

namespace PMDC.Dungeon
{
    [Serializable]
    public class WaitUntilAttackedPlan : AIPlan
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusIndex;
        public WaitUntilAttackedPlan(AIFlags iq, string status) : base(iq)
        {
            StatusIndex = status;
        }
        protected WaitUntilAttackedPlan(WaitUntilAttackedPlan other) : base(other) { StatusIndex = other.StatusIndex; }
        public override BasePlan CreateNew() { return new WaitUntilAttackedPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.GetStatusEffect(StatusIndex) == null)
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            return null;
        }
    }

    [Serializable]
    public class WaitUntilMapStatusPlan : AIPlan
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string StatusIndex;
        public WaitUntilMapStatusPlan(AIFlags iq, string status) : base(iq)
        {
            StatusIndex = status;
        }
        protected WaitUntilMapStatusPlan(WaitUntilMapStatusPlan other) : base(other) { StatusIndex = other.StatusIndex; }
        public override BasePlan CreateNew() { return new WaitUntilMapStatusPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (!ZoneManager.Instance.CurrentMap.Status.ContainsKey(StatusIndex))
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            return null;
        }
    }
}
