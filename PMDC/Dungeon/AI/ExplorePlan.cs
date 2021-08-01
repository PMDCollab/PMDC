using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class ExplorePlan : AIPlan
    {
        private List<Loc> goalPath;
        private List<Loc> locHistory;
        public ExplorePlan(AIFlags iq) : base(iq)
        {
            goalPath = new List<Loc>();
            locHistory = new List<Loc>();
        }
        protected ExplorePlan(ExplorePlan other) : base(other) { }
        public override BasePlan CreateNew() { return new ExplorePlan(this); }
        public override void Initialize(Character controlledChar)
        {
            //create a pathfinding map?
        }
        public override void SwitchedIn()
        {
            goalPath = new List<Loc>();
            locHistory = new List<Loc>();
            base.SwitchedIn();
        }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            if (controlledChar.CantWalk)
                return null;

            //remove all locs from the locHistory that are no longer on screen
            Loc seen = Character.GetSightDims();
            for (int ii = locHistory.Count - 1; ii >= 0; ii--)
            {
                Loc diff = locHistory[ii] - controlledChar.CharLoc;
                if (Math.Abs(diff.X) > seen.X || Math.Abs(diff.Y) > seen.Y || ii > 15)
                {
                    locHistory.RemoveRange(0, ii);
                    break;
                }
            }
            Loc offset = controlledChar.CharLoc - seen;

            //CHECK FOR ADVANCE
            if (goalPath.Count > 1 && goalPath[goalPath.Count-2] == controlledChar.CharLoc)//check if we advanced since last time
                goalPath.RemoveAt(goalPath.Count-1);//remove our previous trail

            //check to see if the end loc is still valid... or, just check to see if *the next step* is still valid
            if (goalPath.Count > 1)
            {
                if (controlledChar.CharLoc == goalPath[goalPath.Count - 1])//check if on the trail
                {
                    if (!ZoneManager.Instance.CurrentMap.TileBlocked(goalPath[goalPath.Count - 2], controlledChar.Mobility) &&
                        !BlockedByTrap(controlledChar, goalPath[goalPath.Count - 2]) &&
                        !BlockedByHazard(controlledChar, goalPath[goalPath.Count - 2]))//check to make sure the next step didn't suddely become blocked
                    {
                        //update current traversals
                        if (locHistory.Count == 0 || locHistory[locHistory.Count - 1] != controlledChar.CharLoc)
                            locHistory.Add(controlledChar.CharLoc);
                        if (!preThink)
                        {
                            Character destChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(goalPath[goalPath.Count - 2]);
                            if (destChar != null && ZoneManager.Instance.CurrentMap.TerrainBlocked(controlledChar.CharLoc, destChar.Mobility))
                                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
                        }
                        GameAction act = TrySelectWalk(controlledChar, DirExt.GetDir(goalPath[goalPath.Count - 1], goalPath[goalPath.Count - 2]));
                        //attempt to continue the path
                        //however, we can only verify that we continued on the path on the next loop, using the CHECK FOR ADVANCE block
                        return act;
                    }
                }
            }

            goalPath = new List<Loc>();
            //if it isn't find a new end loc
            List<Loc> seenExits = GetAreaExits(controlledChar);

            if (seenExits.Count == 0)
                return null;
            //one element in the acceptable range will be randomly selected to be the one that drives the heuristic

            //later, rate the exits based on how far they are from the tail point of the lochistory
            //add them to a sorted list

            List<Loc> forwardFacingLocs = new List<Loc>();
            if (locHistory.Count > 0)
            {
                Loc pastDir = locHistory[0] - controlledChar.CharLoc;
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

            if (locHistory.Count == 0 || locHistory[locHistory.Count - 1] != controlledChar.CharLoc)
                locHistory.Add(controlledChar.CharLoc);

            //TODO: we seldom ever run into other characters who obstruct our path, but if they do, try to wait courteously for them if they are earlier on the team list than us
            //check to make sure we aren't force-warping anyone from their position
            if (!preThink && goalPath.Count > 1)
            {
                Character destChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(goalPath[goalPath.Count - 2]);
                if (destChar != null && ZoneManager.Instance.CurrentMap.TerrainBlocked(controlledChar.CharLoc, destChar.Mobility))
                    return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            }
            return SelectChoiceFromPath(controlledChar, goalPath);
        }

    }
}
