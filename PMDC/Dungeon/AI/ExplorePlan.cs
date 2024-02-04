using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using Newtonsoft.Json;

namespace PMDC.Dungeon
{
    [Serializable]
    public class ExploreIfSeenPlan : ExplorePlan
    {
        public bool Negate;

        public ExploreIfSeenPlan(bool negate, AIFlags iq) : base(iq)
        {
            Negate = negate;
        }
        protected ExploreIfSeenPlan(ExploreIfSeenPlan other) : base(other)
        {
            Negate = other.Negate;
        }
        public override BasePlan CreateNew() { return new ExploreIfSeenPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            //specifically check for players
            foreach (Character target in ZoneManager.Instance.CurrentMap.ActiveTeam.Players)
            {
                if (controlledChar.CanSeeCharacter(target, Map.SightRange.Clear) == Negate)
                {
                    //if a threat is in the vicinity (doesn't have to be seen), abort this plan
                    return null;
                }
            }

            return base.Think(controlledChar, preThink, rand);
        }
    }

    [Serializable]
    public class ExplorePlan : AIPlan
    {
        [NonSerialized]
        protected List<Loc> goalPath;
        [NonSerialized]
        public List<Loc> LocHistory;

        public ExplorePlan() : base()
        { }

        public ExplorePlan(AIFlags iq) : base(iq)
        {
            goalPath = new List<Loc>();
            LocHistory = new List<Loc>();
        }
        public ExplorePlan(AIFlags iq, int attackRange, int statusRange, int selfStatusRange, TerrainData.Mobility restrictedMobilityTypes, bool restrictMobilityPassable) : base(iq, attackRange, statusRange, selfStatusRange, restrictedMobilityTypes, restrictMobilityPassable)
        {
            goalPath = new List<Loc>();
            LocHistory = new List<Loc>();
        }
        protected ExplorePlan(ExplorePlan other) : base(other) { }
        public override BasePlan CreateNew() { return new ExplorePlan(this); }
        public override void Initialize(Character controlledChar)
        {
            //create a pathfinding map?
        }
        public override void SwitchedIn(BasePlan currentPlan)
        {
            goalPath = new List<Loc>();
            LocHistory = new List<Loc>();
            if (currentPlan is AttackFoesPlan)
                LocHistory.AddRange(((AttackFoesPlan)currentPlan).LocHistory);
            base.SwitchedIn(currentPlan);
        }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.CantWalk)
                return null;

            //remove all locs from the locHistory that are no longer on screen
            Loc seen = Character.GetSightDims();
            for (int ii = LocHistory.Count - 1; ii >= 0; ii--)
            {
                Loc diff = LocHistory[ii] - controlledChar.CharLoc;
                if (Math.Abs(diff.X) > seen.X || Math.Abs(diff.Y) > seen.Y || ii > 15)
                {
                    LocHistory.RemoveRange(0, ii);
                    break;
                }
            }
            Loc offset = controlledChar.CharLoc - seen;

            //CHECK FOR ADVANCE
            if (goalPath.Count > 1 && ZoneManager.Instance.CurrentMap.WrapLoc(goalPath[goalPath.Count-2]) == controlledChar.CharLoc)//check if we advanced since last time
                goalPath.RemoveAt(goalPath.Count-1);//remove our previous trail

            //check to see if the end loc is still valid... or, just check to see if *the next step* is still valid
            if (goalPath.Count > 1)
            {
                if (controlledChar.CharLoc == ZoneManager.Instance.CurrentMap.WrapLoc(goalPath[goalPath.Count - 1]))//check if on the trail
                {
                    if (!IsPathBlocked(controlledChar, goalPath[goalPath.Count - 2]) && !BlockedByObstacleChar(controlledChar, goalPath[goalPath.Count - 2]))//check to make sure the next step didn't suddely become blocked
                    {
                        //update current traversals
                        if (LocHistory.Count == 0 || LocHistory[LocHistory.Count - 1] != controlledChar.CharLoc)
                            LocHistory.Add(controlledChar.CharLoc);
                        if (!preThink)
                        {
                            Character destChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(goalPath[goalPath.Count - 2]);
                            // if there's a character there, and they're ordered before us
                            if (!canPassChar(controlledChar, destChar, false))
                                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
                        }
                        GameAction act = TrySelectWalk(controlledChar, ZoneManager.Instance.CurrentMap.GetClosestDir8(goalPath[goalPath.Count - 1], goalPath[goalPath.Count - 2]));
                        //attempt to continue the path
                        //however, we can only verify that we continued on the path on the next loop, using the CHECK FOR ADVANCE block
                        return act;
                    }
                }
            }

            goalPath = new List<Loc>();
            //if it isn't find a new end loc
            List<Loc> seenExits = GetDestinations(controlledChar);

            if (seenExits.Count == 0)
                return null;
            //one element in the acceptable range will be randomly selected to be the one that drives the heuristic

            //later, rate the exits based on how far they are from the tail point of the lochistory
            //add them to a sorted list

            List<Loc> forwardFacingLocs = new List<Loc>();
            if (LocHistory.Count > 0)
            {
                Loc pastLoc = ZoneManager.Instance.CurrentMap.GetClosestUnwrappedLoc(controlledChar.CharLoc, LocHistory[0]);
                Loc pastDir = pastLoc - controlledChar.CharLoc;
                for (int ii = seenExits.Count - 1; ii >= 0; ii--)
                {
                    if (Loc.Dot(pastDir, (seenExits[ii] - controlledChar.CharLoc)) <= 0)
                    {
                        forwardFacingLocs.Add(seenExits[ii]);
                        seenExits.RemoveAt(ii);
                    }
                }
            }

            //if any of the tiles are reached in the search, they will be automatically chosen

            //Analysis:
            //if there is only one exit, and it's easily reached, the speed is the same - #1 fastest case
            //if there are many exits, and they're easily reached, the speed is the same - #1 fastest case
            //if there's one exit, and it's impossible, the speed is the same - #2 fastest case
            //if there's many exits, and they're all impossible, the speed is faster - #2 fastest case
            //if there's many exits, and only the backtrack is possible, the speed is faster - #2 fastest case

            //first attempt the ones that face forward
            if (forwardFacingLocs.Count > 0)
                goalPath = GetRandomPathPermissive(rand, controlledChar, forwardFacingLocs);

            //then attempt remaining locations
            if (goalPath.Count == 0)
                goalPath = GetRandomPathPermissive(rand, controlledChar, seenExits);

            if (goalPath.Count == 0)
                return null;

            if (LocHistory.Count == 0 || LocHistory[LocHistory.Count - 1] != controlledChar.CharLoc)
                LocHistory.Add(controlledChar.CharLoc);

            //TODO: we seldom ever run into other characters who obstruct our path, but if they do, try to wait courteously for them if they are earlier on the team list than us
            //check to make sure we aren't force-warping anyone from their position
            if (!preThink && goalPath.Count > 1)
            {
                Character destChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(goalPath[goalPath.Count - 2]);
                if (!canPassChar(controlledChar, destChar, false))
                    return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            }
            return SelectChoiceFromPath(controlledChar, goalPath);
        }

        protected virtual List<Loc> GetDestinations(Character controlledChar)
        {
            return GetAreaExits(controlledChar);
        }
    }
}
