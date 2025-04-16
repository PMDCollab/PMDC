using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{

    [Serializable]
    public class AttackFoesPlan : AIPlan
    {
        public AttackChoice AttackPattern;
        public PositionChoice PositionPattern;
        //continue to the last place the enemy was found (if no other enemies can be found) before losing aggro
        [NonSerialized]
        private Loc? targetLoc;
        [NonSerialized]
        public List<Loc> LocHistory;
        [NonSerialized]
        private Character lastSeenChar;

        public AttackFoesPlan() { }
        public AttackFoesPlan(AIFlags iq, int attackRange, int statusRange, int selfStatusRange, AttackChoice attackPattern, PositionChoice positionPattern, TerrainData.Mobility restrictedMobilityTypes, bool restrictMobilityPassable) : base(iq, attackRange, statusRange, selfStatusRange, restrictedMobilityTypes, restrictMobilityPassable)
        {
            AttackPattern = attackPattern;
            PositionPattern = positionPattern;
        }
        protected AttackFoesPlan(AttackFoesPlan other) : base(other)
        {
            AttackPattern = other.AttackPattern;
            PositionPattern = other.PositionPattern;
        }
        public override BasePlan CreateNew() { return new AttackFoesPlan(this); }
        public override void SwitchedIn(BasePlan currentPlan)
        {
            lastSeenChar = null;
            LocHistory = new List<Loc>();
            targetLoc = null;
            base.SwitchedIn(currentPlan);
        }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.CantWalk)
            {
                GameAction attack = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attack.Type != GameAction.ActionType.Wait)
                    return attack;
                return null;
            }


            //past this point, using moves won't work, so try to find a path
            List<Character> seenCharacters = new List<Character>();
            foreach (Character seenChar in GetAcceptableTargets(controlledChar))
                seenCharacters.Add(seenChar);

            Character closestChar = null;
            Loc closestDiff = Loc.Zero;
            for (int ii = 0; ii < seenCharacters.Count; ii++)
            {
                if (closestChar == null)
                {
                    closestChar = seenCharacters[ii];
                    closestDiff = controlledChar.CharLoc - closestChar.CharLoc;
                }
                else
                {
                    Loc newDiff = controlledChar.CharLoc - seenCharacters[ii].CharLoc;
                    if (newDiff.DistSquared() < closestDiff.DistSquared())
                        closestChar = seenCharacters[ii];
                }
            }
            if (closestChar != null)
            {
                lastSeenChar = closestChar;
                targetLoc = closestChar.CharLoc;
            }

            //if we have another move we can make, take this turn to reposition
            int extraTurns = ZoneManager.Instance.CurrentMap.CurrentTurnMap.GetRemainingTurns(controlledChar);

            if (extraTurns <= 1)
            {
                //attempt to use a move
                GameAction attack = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attack.Type != GameAction.ActionType.Wait)
                    return attack;
            }


            //path to the closest enemy
            List<Loc> path = null;
            Character targetChar = null;
            Dictionary<Loc, RangeTarget> endHash = new Dictionary<Loc, RangeTarget>();
            Loc[] ends = null;
            bool hasSelfEnd = false;//the controlledChar's destination is included among ends
            bool aimForDistance = false; // determines if we are pathing directly to the target or to a tile we can hit the target from

            PositionChoice positioning = PositionPattern;
            if (extraTurns > 0)
                positioning = PositionChoice.Avoid;

            if (controlledChar.CantInteract)//TODO: CantInteract doesn't always indicate forced attack, but this'll do for now.
                positioning = PositionChoice.Approach;

            bool playerSense = (IQ & AIFlags.PlayerSense) != AIFlags.None;
            if (!playerSense)
            {
                //for dumb NPCs, if they have a status where they can't attack, treat it as a regular attack pattern so that they walk up to the player
                //only cringe does this right now...
                StatusEffect flinchStatus = controlledChar.GetStatusEffect("flinch"); //NOTE: specialized AI code!
                if (flinchStatus != null)
                    positioning = PositionChoice.Approach;
            }

            // If the Positionchoice is Avoid, take attack ranges into consideration
            // the end points should be all locations where one can attack the target
            // for projectiles, it should be the farthest point where they can attack:
            if (positioning != PositionChoice.Approach)
            {
                //get all move ranges and use all their ranges to denote destination tiles.
                FillRangeTargets(controlledChar, seenCharacters, endHash, positioning != PositionChoice.Avoid);
                List<Loc> endList = new List<Loc>();
                foreach (Loc endLoc in endHash.Keys)
                {
                    bool addLoc = false;
                    if (aimForDistance)
                    {
                        if (endHash[endLoc].Weight > 0)
                            addLoc = true;
                    }
                    else
                    {
                        if (endHash[endLoc].Weight > 0)
                        {
                            aimForDistance = true;
                            endList.Clear();
                        }
                        addLoc = true;
                    }
                    if (addLoc)
                    {
                        if (endLoc != controlledChar.CharLoc)//destination cannot be the current location (unless we have turns to spare)
                            endList.Add(endLoc);
                        else
                        {
                            if (extraTurns > 0)
                            {
                                endList.Add(endLoc);
                                hasSelfEnd = true;
                            }
                        }
                    }
                }
                ends = endList.ToArray();
            }
            else
            {
                ends = new Loc[seenCharacters.Count];
                for (int ii = 0; ii < seenCharacters.Count; ii++)
                {
                    endHash[seenCharacters[ii].CharLoc] = new RangeTarget(seenCharacters[ii], 0);
                    ends[ii] = seenCharacters[ii].CharLoc;
                }
            }

            //now actually decide the path to get there
            if (ends.Length > 0)
            {
                List<Loc>[] closestPaths = GetPaths(controlledChar, ends, !aimForDistance, !preThink, hasSelfEnd ? 2 : 1);
                int closestIdx = -1;
                for (int ii = 0; ii < ends.Length; ii++)
                {
                    if (closestPaths[ii] == null)//no path was found
                        continue;
                    if (closestPaths[ii][0] != ends[ii])//an incomplete path was found
                    {
                        if (endHash[ends[ii]].Origin.CharLoc != ends[ii]) // but only for pathing that goes to a tile to hit the target from
                            continue;
                    }

                    if (closestIdx == -1)
                        closestIdx = ii;
                    else
                    {
                        int cmp = comparePathValues(positioning, endHash[ends[ii]], endHash[ends[closestIdx]]);
                        if (cmp > 0)
                            closestIdx = ii;
                        else if (cmp == 0)
                        {
                            // among ties, the tile closest to the target wins
                            int curDiff = (ends[closestIdx] - endHash[ends[closestIdx]].Origin.CharLoc).DistSquared();
                            int newDiff = (ends[ii] - endHash[ends[ii]].Origin.CharLoc).DistSquared();
                            if (newDiff < curDiff)
                                closestIdx = ii;
                        }
                    }
                }

                if (closestIdx > -1)
                {
                    path = closestPaths[closestIdx];
                    targetChar = endHash[ends[closestIdx]].Origin;
                }
            }

            //update last-seen target location if we have a target, otherwise leave it alone
            if (targetChar != null)
            {
                targetLoc = targetChar.CharLoc;
                lastSeenChar = targetChar;
            }
            else if (targetLoc != null) // follow up on a previous targeted loc
            {
                if (preThink)
                {
                    // no currently seen target, check if the target loc is in sight to determine if we should keep last seen char
                    if (!controlledChar.CanSeeLoc(targetLoc.Value, controlledChar.GetCharSight()))
                        lastSeenChar = null;
                    if (lastSeenChar != null)
                        targetLoc = lastSeenChar.CharLoc;
                }
            }

            //update lochistory for potential movement in exploration
            if (LocHistory.Count == 0 || LocHistory[LocHistory.Count - 1] != controlledChar.CharLoc)
                LocHistory.Add(controlledChar.CharLoc);
            if (LocHistory.Count > 10)
                LocHistory.RemoveAt(0);

            if (path != null)
            {
                //pursue the enemy if one is located
                if (path[0] == targetChar.CharLoc)
                    path.RemoveAt(0);

                GameAction attack = null;
                if (path.Count <= 1 || path.Count > 3)//if it takes more than 2 steps to get into position (list includes the loc for start position, for a total of 3), try a local attack
                {
                    if (ZoneManager.Instance.CurrentMap.InRange(targetChar.CharLoc, controlledChar.CharLoc, 1))
                    {
                        attack = TryAttackChoice(rand, controlledChar, AttackPattern, true);
                        if (attack.Type != GameAction.ActionType.Wait)
                            return attack;
                    }
                    attack = TryAttackChoice(rand, controlledChar, AttackChoice.StandardAttack, true);
                    if (attack.Type != GameAction.ActionType.Wait)
                        return attack;
                }
                //move if the destination can be reached
                if (path.Count > 1)
                    return SelectChoiceFromPath(controlledChar, path);
                //lastly, try normal attack
                if (attack == null)
                    attack = TryAttackChoice(rand, controlledChar, AttackChoice.StandardAttack, true);
                if (attack.Type != GameAction.ActionType.Wait)
                    return attack;
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            }
            else if (!playerSense && targetLoc.HasValue && targetLoc.Value != controlledChar.CharLoc)
            {
                //if no enemy is located, path to the location of the last seen enemy
                List<Loc>[] paths = GetPaths(controlledChar, new Loc[1] { targetLoc.Value }, false, !preThink);
                path = paths[0];
                if (path.Count > 1)
                    return SelectChoiceFromPath(controlledChar, path);
                else
                    targetLoc = null;
            }

            return null;
        }

        /// <summary>
        /// 1 = better, -1 worse, 0 = equal 
        /// </summary>
        /// <param name="newVal"></param>
        /// <param name="curBest"></param>
        /// <returns></returns>
        private int comparePathValues(PositionChoice positioning, RangeTarget newVal, RangeTarget curBest)
        {
            if (newVal.Weight == curBest.Weight)
                return 0;

            switch (positioning)
            {
                case PositionChoice.Avoid:
                    if (newVal.Weight > curBest.Weight)
                        return 1;
                    break;
                case PositionChoice.Close:
                    if (newVal.Weight < curBest.Weight)
                        return 1;
                    break;
            }
            return -1;
        }
    }
}
