using System;
using System.Collections.Generic;
using RogueEssence.Data;
using RogueEssence.Menu;
using RogueElements;
using RogueEssence.Content;
using RogueEssence.LevelGen;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using PMDC.Dev;
using PMDC.Data;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using NLua;
using RogueEssence.Script;
using System.Linq;

namespace PMDC.Dungeon
{
    //Battle events that change PP


    /// <summary>
    /// Event that subtracts PP from all the user's move if the target was dealt damage by a move
    /// </summary>
    [Serializable]
    public class GrudgeEvent : BattleEvent
    {
        public override GameEvent Clone() { return new GrudgeEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe && context.GetContextStateInt<DamageDealt>(0) > 0 && context.ActionType == BattleActionType.Skill
                && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_GRUDGE").ToLocal()));
                yield return CoroutineManager.Instance.StartCoroutine(context.User.DeductCharges(-1, 3));
            }
        }
    }

    /// <summary>
    /// Event that increases the user's move PP usage by the specified amount 
    /// </summary>
    [Serializable]
    public class PressureEvent : BattleEvent
    {

        /// <summary>
        /// The increased PP usage amount
        /// </summary>
        public int Amount;
        public PressureEvent() { }
        public PressureEvent(int amount)
        {
            Amount = amount;
        }
        protected PressureEvent(PressureEvent other)
        {
            Amount = other.Amount;
        }
        public override GameEvent Clone() { return new PressureEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe && context.ActionType == BattleActionType.Skill
                && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                if (context.User.Skills[context.UsageSlot].Element.Charges > 0)
                {
                    int deduction = Amount;
                    if (context.ContextStates.Contains<PressurePlus>())
                    {
                        deduction += 1;
                        context.ContextStates.Remove<PressurePlus>();
                    }

                    if (deduction > 0)
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(context.User.DeductCharges(context.UsageSlot, deduction, true, false, true));
                        if (context.User.Skills[context.UsageSlot].Element.Charges == 0)
                            context.SkillUsedUp.Skill = context.User.Skills[context.UsageSlot].Element.SkillNum;
                    }
                }
            }
        }
    }




    /// <summary>
    /// Event the sets the PP of all the character's move to 1 
    /// </summary>
    [Serializable]
    public class PPTo1Event : BattleEvent
    {
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public PPTo1Event() { }
        public PPTo1Event(bool affectTarget) { AffectTarget = affectTarget; }
        protected PPTo1Event(PPTo1Event other)
        {
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new PPTo1Event(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            for (int ii = 0; ii < target.Skills.Count; ii++)
            {
                if (!String.IsNullOrEmpty(target.Skills[ii].Element.SkillNum))
                    target.SetSkillCharges(ii, 1);
            }

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PP_TO_ONE").ToLocal(), target.GetDisplayName(false)));
        }
    }





    /// <summary>
    /// Event that subtracts PP from the target if the user is hit by a move
    /// </summary>
    [Serializable]
    public class SpiteEvent : BattleEvent
    {
        /// <summary>
        /// The status that contains the last used move slot 
        /// This should usually be "last_used_move_slot"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastSlotStatusID;

        /// <summary>
        /// The amount of PP to subtract
        /// </summary>
        public int PP;

        public SpiteEvent() { LastSlotStatusID = ""; }
        public SpiteEvent(string statusID, int pp) { LastSlotStatusID = statusID; PP = pp; }
        protected SpiteEvent(SpiteEvent other)
        {
            LastSlotStatusID = other.LastSlotStatusID;
            PP = other.PP;
        }
        public override GameEvent Clone() { return new SpiteEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect status = context.Target.GetStatusEffect(LastSlotStatusID);
            if (status != null)
            {
                int slot = status.StatusStates.GetWithDefault<SlotState>().Slot;
                if (slot > -1 && slot < CharData.MAX_SKILL_SLOTS)
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.DeductCharges(slot, PP));
                else
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NO_EFFECT").ToLocal(), context.Target.GetDisplayName(false)));
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NO_EFFECT").ToLocal(), context.Target.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that restores PP on all move slots
    /// </summary>
    [Serializable]
    public class RestorePPEvent : BattleEvent
    {
        /// <summary>
        /// The amount of PP to restore
        /// </summary>
        public int PP;

        public RestorePPEvent() { }
        public RestorePPEvent(int pp) { PP = pp; }
        protected RestorePPEvent(RestorePPEvent other)
        {
            PP = other.PP;
        }
        public override GameEvent Clone() { return new RestorePPEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            yield return CoroutineManager.Instance.StartCoroutine(context.Target.RestoreCharges(PP));
        }
    }

}

