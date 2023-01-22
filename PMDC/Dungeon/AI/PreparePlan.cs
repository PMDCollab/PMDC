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
        public PreparePlan(AIFlags iq, AttackChoice attackPattern) : base(iq)
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
    public class PrepareWithLeaderPlan : AIPlan
    {
        public AttackChoice AttackPattern;

        public PrepareWithLeaderPlan() { }
        public PrepareWithLeaderPlan(AIFlags iq, AttackChoice attackPattern) : base(iq)
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
            // If not transitively touching the leader, do not use this plan
            if (!transitivelyTouchesLeader(controlledChar))
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
                GameAction attackCommand = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
                attackCommand = TryAttackChoice(rand, controlledChar, AttackChoice.StandardAttack);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
            }

            return null;
        }

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

        // higher performance transitive touch check
        private bool transitivelyTouchesLeaderFast(Character controlledChar)
        {
            Team team = controlledChar.MemberTeam;

            bool[] traversedPlayers = new bool[team.Players.Count];
            bool[] traversedGuests = new bool[team.Guests.Count];

            List<CharIndex> openSet = new List<CharIndex>();
            openSet.Add(new CharIndex(Faction.None, -1, false, team.LeaderIndex));

            while (openSet.Count > 0)
            {
                //pop from top; depth first behavior
                CharIndex popped = openSet[openSet.Count-1];
                openSet.RemoveAt(openSet.Count - 1);
                Character poppedChar = team.CharAtIndex(popped.Guest, popped.Char);
                if (poppedChar == controlledChar)
                    return true;

                if (popped.Guest)
                    traversedGuests[popped.Char] = true;
                else
                    traversedPlayers[popped.Char] = true;

                //get members adjacent
                for (int ii = 0; ii < team.Players.Count; ii++)
                {
                    CharIndex charIndex = new CharIndex(Faction.None, -1, false, ii);
                    if (isAdjacent(team, traversedPlayers, charIndex, poppedChar.CharLoc))
                        openSet.Add(charIndex);
                }
                for (int ii = 0; ii < team.Guests.Count; ii++)
                {
                    CharIndex charIndex = new CharIndex(Faction.None, -1, true, ii);
                    if (isAdjacent(team, traversedPlayers, charIndex, poppedChar.CharLoc))
                        openSet.Add(charIndex);
                }

            }
            return true;
        }

        private bool isAdjacent(Team team, bool[] traversed, CharIndex charIndex, Loc checkLoc)
        {
            if (!traversed[charIndex.Char])
            {
                Character checkChar = team.CharAtIndex(false, charIndex.Char);
                if (ZoneManager.Instance.CurrentMap.InRange(checkChar.CharLoc, checkLoc, 1))
                {
                    //in order for the checkChar to be included in the set, it must be able to reach the checkLoc location
                    //check for diagonal interruption
                    Dir8 dir = DirExt.GetDir(checkChar.CharLoc, checkLoc);
                    return !ZoneManager.Instance.CurrentMap.DirBlocked(dir, checkChar.CharLoc, checkChar.Mobility, 1, false, true);
                }
            }
            return false;
        }
    }


    [Serializable]
    public class PreBuffPlan : AIPlan
    {
        [JsonConverter(typeof(StatusConverter))]
        public string FirstMoveStatus;

        public PreBuffPlan() { }
        public PreBuffPlan(AIFlags iq, string firstMoveStatus) : base(iq)
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
        public LeadSkillPlan(AIFlags iq, string firstMoveStatus) : base(iq)
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
