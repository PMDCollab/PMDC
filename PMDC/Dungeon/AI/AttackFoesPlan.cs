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
        private Loc? targetLoc;

        public AttackFoesPlan() { }
        public AttackFoesPlan(AIFlags iq, AttackChoice attackPattern, PositionChoice positionPattern) : base(iq)
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
        public override void SwitchedIn() { targetLoc = null; base.SwitchedIn(); }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            if (controlledChar.CantWalk)
            {
                GameAction attack = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attack.Type != GameAction.ActionType.Wait)
                    return attack;
                return null;
            }

            //if we have another move we can make, take this turn to reposition
            int extraTurns = controlledChar.MovementSpeed - controlledChar.TiersUsed;

            if (extraTurns <= 0)
            {
                //attempt to use a move
                GameAction attack = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attack.Type != GameAction.ActionType.Wait)
                    return attack;
            }

            //past this point, using moves won't work, so try to find a path

            List<Character> seenCharacters = controlledChar.GetSeenCharacters(GetAcceptableTargets());

            bool teamPartner = (IQ & AIFlags.TeamPartner) != AIFlags.None;
            if (teamPartner)
            {
                //check for statuses that may make them ineligible targets
                for (int ii = seenCharacters.Count - 1; ii >= 0; ii--)
                {
                    if (!teamPartnerCanAttack(seenCharacters[ii]))
                        seenCharacters.RemoveAt(ii);
                }
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

            if (controlledChar.AttackOnly)
                positioning = PositionChoice.Approach;

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
                targetLoc = targetChar.CharLoc;

            if (path != null)
            {
                //pursue the enemy if one is located
                if (path[0] == targetChar.CharLoc)
                    path.RemoveAt(0);

                GameAction attack = null;
                if (path.Count > 3)//if it takes more than 2 steps to get into position (list includes the loc for start position, for a total of 3), try a normal attack locally
                {
                    attack = TryAttackChoice(rand, controlledChar, AttackChoice.StandardAttack);
                    if (attack.Type != GameAction.ActionType.Wait)
                        return attack;
                }
                //move if the destination can be reached
                if (path.Count > 1)
                    return SelectChoiceFromPath(controlledChar, path);
                //lastly, try normal attack
                if (attack == null)
                    attack = TryAttackChoice(rand, controlledChar, AttackChoice.StandardAttack);
                if (attack.Type != GameAction.ActionType.Wait)
                    return attack;
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            }
            else if (!teamPartner && targetLoc.HasValue && targetLoc.Value != controlledChar.CharLoc)
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
