using System;
using RogueElements;
using System.Collections.Generic;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using Newtonsoft.Json;
using RogueEssence.Dev;

namespace PMDC.Dungeon
{
    [Serializable]
    public class PreparePlan : AIPlan
    {
        public AttackChoice AttackPattern;

        public PreparePlan() { }
        public PreparePlan(AIFlags iq, int attackRange, int statusRange, int selfStatusRange, AttackChoice attackPattern, TerrainData.Mobility restrictedMobilityTypes, bool restrictMobilityPassable) : base(iq, attackRange, statusRange, selfStatusRange, restrictedMobilityTypes, restrictMobilityPassable)
        {
            AttackPattern = attackPattern;
        }
        public PreparePlan(PreparePlan other) : base(other)
        {
            AttackPattern = other.AttackPattern;
        }
        public override BasePlan CreateNew() { return new PreparePlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            bool playerSense = (IQ & AIFlags.PlayerSense) != AIFlags.None;
            Character target = null;
            foreach (Character seenChar in GetAcceptableTargets(controlledChar))
            {
                target = seenChar;
                break;
            }

            //need attack action check
            if (target != null)
            {
                GameAction attackCommand = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
                attackCommand = TryAttackChoice(rand, controlledChar, AttackChoice.StandardAttack);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
            }

            return null;
        }
    }

    [Serializable]
    public class PrepareWithLeaderPlan : FollowLeaderPlan
    {
        public AttackChoice AttackPattern;

        public PrepareWithLeaderPlan() { }
        public PrepareWithLeaderPlan(AIFlags iq, int attackRange, int statusRange, int selfStatusRange, AttackChoice attackPattern, TerrainData.Mobility restrictedMobilityTypes, bool restrictMobilityPassable) : base(iq, attackRange, statusRange, selfStatusRange, restrictedMobilityTypes, restrictMobilityPassable)
        {
            AttackPattern = attackPattern;
        }
        public PrepareWithLeaderPlan(PrepareWithLeaderPlan other) : base(other)
        {
            AttackPattern = other.AttackPattern;
        }
        public override BasePlan CreateNew() { return new PrepareWithLeaderPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            GameAction baseAction = base.Think(controlledChar, preThink, rand);

            //behave like followLeader normally
            if (baseAction != null && baseAction.Type != GameAction.ActionType.Wait)
                return baseAction;

            //but if we have no place to walk, try to attack a foe from where we are

            bool playerSense = (IQ & AIFlags.PlayerSense) != AIFlags.None;
            Character target = null;
            foreach (Character seenChar in GetAcceptableTargets(controlledChar))
            {
                target = seenChar;
                break;
            }

            //need attack action check
            if (target != null)
            {
                GameAction attackCommand = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
                attackCommand = TryAttackChoice(rand, controlledChar, AttackChoice.StandardAttack);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
            }

            return baseAction;
        }

        /// <summary>
        /// Checks if the controlled character is close to the highest ranking member in sight.
        /// </summary>
        /// <returns></returns>
        private bool closestToHighestLeader(Character controlledChar)
        {
            foreach (Character testChar in controlledChar.MemberTeam.IterateByRank())
            {
                //no leader found?  don't be preparing.
                if (testChar == controlledChar)
                    return false;
                else if (controlledChar.IsInSightBounds(testChar.CharLoc))
                {
                    //only check the first leader that is within sight
                    //leader found; check if nearby
                    if (ZoneManager.Instance.CurrentMap.InRange(testChar.CharLoc, controlledChar.CharLoc, 1))
                    {
                        //check if able to walk there specifically
                        Dir8 dir = DirExt.GetDir(controlledChar.CharLoc, testChar.CharLoc);
                        if (!ZoneManager.Instance.CurrentMap.DirBlocked(dir, controlledChar.CharLoc, controlledChar.Mobility, 1, false, true))
                            return true;
                    }
                    //if any checks fail, return null
                    return false;
                }
            }
            //couldn't find the leader by some way
            return false;
        }

        /// <summary>
        /// Checks if the controlled character is transitively close to THE leader. Unsure if should use this method.
        /// </summary>
        /// <param name="controlledChar"></param>
        /// <returns></returns>
        private bool transitivelyTouchesLeader(Character controlledChar)
        {
            Team team = controlledChar.MemberTeam;

            //requires a valid target tile
            Grid.LocTest checkDiagBlock = (Loc testLoc) => {
                Character nextChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc);
                if (nextChar == null)
                    return true;
                if (nextChar.MemberTeam != team)
                    return true;

                //check to make sure you can actually walk this way
                return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, controlledChar.Mobility, true);
            };

            Grid.LocTest checkBlock = (Loc testLoc) => {

                Character nextChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc);
                if (nextChar == null)
                    return true;
                if (nextChar.MemberTeam != team)
                    return true;
                return false;
            };

            Loc mapStart = controlledChar.CharLoc - Character.GetSightDims();
            Loc mapSize = Character.GetSightDims() * 2 + new Loc(1);
            List<Loc> path = Grid.FindPath(mapStart, mapSize, controlledChar.CharLoc, team.Leader.CharLoc, checkBlock, checkDiagBlock);

            return (path[0] == team.Leader.CharLoc);
        }
    }


    [Serializable]
    public class PreBuffPlan : AIPlan
    {
        [JsonConverter(typeof(StatusConverter))]
        public string FirstMoveStatus;

        public PreBuffPlan() { }
        public PreBuffPlan(AIFlags iq, int attackRange, int statusRange, int selfStatusRange, string firstMoveStatus, TerrainData.Mobility restrictedMobilityTypes) : base(iq, attackRange, statusRange, selfStatusRange, restrictedMobilityTypes)
        {
            FirstMoveStatus = firstMoveStatus;
        }
        public PreBuffPlan(PreBuffPlan other) : base(other)
        {
            FirstMoveStatus = other.FirstMoveStatus;
        }
        public override BasePlan CreateNew() { return new PreBuffPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.GetStatusEffect(FirstMoveStatus) != null)
                return null;

            bool playerSense = (IQ & AIFlags.PlayerSense) != AIFlags.None;
            Character target = null;
            foreach (Character seenChar in GetAcceptableTargets(controlledChar))
            {
                target = seenChar;
                break;
            }

            //need attack action check
            if (target != null)
            {
                GameAction attackCommand = TryAttackChoice(rand, controlledChar, AttackChoice.StatusAttack);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
            }

            return null;
        }
    }




    [Serializable]
    public class LeadSkillPlan : AIPlan
    {
        [JsonConverter(typeof(StatusConverter))]
        public string FirstMoveStatus;

        public LeadSkillPlan() { FirstMoveStatus = ""; }
        public LeadSkillPlan(AIFlags iq, int attackRange, int statusRange, int selfStatusRange, string firstMoveStatus, TerrainData.Mobility restrictedMobilityTypes) : base(iq, attackRange, statusRange, selfStatusRange, restrictedMobilityTypes)
        {
            FirstMoveStatus = firstMoveStatus;
        }
        public LeadSkillPlan(LeadSkillPlan other) : base(other)
        {
            FirstMoveStatus = other.FirstMoveStatus;
        }
        public override BasePlan CreateNew() { return new LeadSkillPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.GetStatusEffect(FirstMoveStatus) != null)
                return null;

            if (controlledChar.CantInteract)//TODO: CantInteract doesn't always indicate forced attack, but this'll do for now.
                return null;

            //use the first attack
            if (IsSkillUsable(controlledChar, 0))
                return new GameAction(GameAction.ActionType.UseSkill, controlledChar.CharDir, 0);

            return null;
        }
    }
}
