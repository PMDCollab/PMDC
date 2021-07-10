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
        public int StatusIndex;
        //continue to the last place the enemy was found (if no other enemies can be found) before losing aggro
        private Loc? targetLoc;

        public AttackFoesPlan() { }
        public AttackFoesPlan(AIFlags iq, AttackChoice attackPattern, int status) : base(iq)
        {
            StatusIndex = status;
            AttackPattern = attackPattern;
        }
        protected AttackFoesPlan(AttackFoesPlan other) : base(other) { StatusIndex = other.StatusIndex; }
        public override BasePlan CreateNew() { return new AttackFoesPlan(this); }
        public override void SwitchedIn() { targetLoc = null; base.SwitchedIn(); }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            Faction foeFaction = (IQ & AIFlags.NeutralFoeConflict) != AIFlags.None ? Faction.Foe : Faction.None;
            List<Character> seenCharacters = controlledChar.GetSeenCharacters(GetAcceptableTargets(), foeFaction);

            StatusEffect lastHit = controlledChar.GetStatusEffect(StatusIndex);
            if (lastHit != null && lastHit.TargetChar != null)
            {
                if (!seenCharacters.Contains(lastHit.TargetChar))
                    seenCharacters.Add(lastHit.TargetChar);
            }

            bool teamPartner = (IQ & AIFlags.TeamPartner) != AIFlags.None;
            if (teamPartner)
            {
                //check for statuses that may make them ineligible targets
                for (int ii = seenCharacters.Count - 1; ii >= 0; ii--)
                {
                    //NOTE: specialized AI code!
                    if (seenCharacters[ii].GetStatusEffect(1) != null || seenCharacters[ii].GetStatusEffect(3) != null)//if they're asleep or frozen, do not attack
                        seenCharacters.RemoveAt(ii);
                    else if (seenCharacters[ii].GetStatusEffect(25) == null)//last targeted by someone; NOTE: specialized AI code!
                    {
                        //don't attack certain kinds of foes that won't attack first
                        if (seenCharacters[ii].Tactic.ID == 10)//weird tree; NOTE: specialized AI code!
                            seenCharacters.RemoveAt(ii);
                        else if (seenCharacters[ii].Tactic.ID == 8)//wait attack; NOTE: specialized AI code!
                            seenCharacters.RemoveAt(ii);
                        else if (seenCharacters[ii].Tactic.ID == 18)//tit for tat; NOTE: specialized AI code!
                            seenCharacters.RemoveAt(ii);
                    }
                }
            }

            if (controlledChar.CantWalk)
            {
                Character target = seenCharacters[0];
                for (int ii = 1; ii < seenCharacters.Count; ii++)
                {
                    if ((seenCharacters[ii].CharLoc - controlledChar.CharLoc).DistSquared() < (target.CharLoc - controlledChar.CharLoc).DistSquared())
                        target = seenCharacters[ii];
                }
                Dir8 closestDir = (target.CharLoc - controlledChar.CharLoc).ApproximateDir8();
                GameAction attack = TryAttackChoice(rand, controlledChar, closestDir, AttackPattern);
                if (attack.Type != GameAction.ActionType.Wait)
                    return attack;

                return null;
            }

            //path to the closest enemy
            List<Loc> path = null;
            Character targetChar = null;
            Dictionary<Loc, RangeTarget> endHash = new Dictionary<Loc, RangeTarget>();
            Loc[] ends = null;
            bool aimForDistance = false; // detrmines if we are pathing directly to the target or to a tile we can hit the target from

            // If the attackchoice is SmartAttack, take attack ranges into consideration
            // the end points should be all locations where one can attack the target
            // for projectiles, it should be the farthest point where they can attack:
            if (AttackPattern == AttackChoice.SmartAttack)
            {
                //get all move ranges and use all their ranges to denote destination tiles.
                FillRangeTargets(controlledChar, seenCharacters, endHash);
                List<Loc> endList = new List<Loc>();
                foreach (Loc endLoc in endHash.Keys)
                {
                    if (aimForDistance)
                    {
                        if (endHash[endLoc].Weight > 0)
                            endList.Add(endLoc);
                    }
                    else
                    {
                        if (endHash[endLoc].Weight > 0)
                        {
                            aimForDistance = true;
                            endList.Clear();
                        }
                        endList.Add(endLoc);
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

            if (ends.Length > 0)
            {
                List<Loc>[] closestPaths = GetPaths(controlledChar, ends, false, !preThink);
                int closestIdx = -1;
                for (int ii = 0; ii < ends.Length; ii++)
                {
                    if (closestPaths[ii] != null)
                    {
                        if (closestIdx == -1)
                            closestIdx = ii;
                        else
                        {
                            if (endHash[ends[ii]].Weight > endHash[ends[closestIdx]].Weight)
                                closestIdx = ii;
                        }
                    }
                }

                path = closestPaths[closestIdx];
                targetChar = endHash[ends[closestIdx]].Origin;
            }

            //update last-seen target location if we have a target, otherwise leave it alone
            if (targetChar != null)
                targetLoc = targetChar.CharLoc;


            if (path != null)
            {
                Dir8 closestDir = (targetChar.CharLoc - controlledChar.CharLoc).ApproximateDir8();
                GameAction attack = TryAttackChoice(rand, controlledChar, closestDir, AttackPattern);
                if (attack.Type != GameAction.ActionType.Wait)
                    return attack;

                //pursue the enemy if one is located
                if (path[0] == targetChar.CharLoc)
                    path.RemoveAt(0);
                return SelectChoiceFromPath(controlledChar, path);
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
    }
}
