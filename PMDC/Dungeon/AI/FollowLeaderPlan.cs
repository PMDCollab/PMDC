using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class FollowLeaderPlan : AIPlan
    {
        public FollowLeaderPlan() { }
        public FollowLeaderPlan(AIFlags iq) : base(iq) { }
        public FollowLeaderPlan(AIFlags iq, int attackRange, int statusRange, int selfStatusRange, TerrainData.Mobility restrictedMobilityTypes, bool restrictMobilityPassable) : base(iq, attackRange, statusRange, selfStatusRange, restrictedMobilityTypes, restrictMobilityPassable) { }
        protected FollowLeaderPlan(FollowLeaderPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new FollowLeaderPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.CantWalk)
                return null;

            List<Character> seenCharacters = new List<Character>();
            bool seeLeader = false;

            foreach (Character testChar in controlledChar.MemberTeam.IterateByRank())
            {
                if (testChar == controlledChar)
                {
                    if (preThink)//follow only leaders in pre-thinking
                        break;
                    else//settle for other partners in action
                    {
                        if (seeLeader)
                            continue;
                        else
                            return null;
                    }
                }
                else if (controlledChar.IsInSightBounds(testChar.CharLoc))
                {
                    seenCharacters.Add(testChar);
                    seeLeader = true;
                }
            }

            //gravitate to the CLOSEST ally.
            //iterate in increasing character indices
            GameAction result = null;
            foreach(Character targetChar in seenCharacters)
            {
                //get the direction to that character
                //use A* to get this first direction?  check only walls?
                List<Loc>[] paths = GetPaths(controlledChar, new Loc[1] { targetChar.CharLoc }, true, false);
                List<Loc> path = paths[0];
                Dir8 dirToChar = ZoneManager.Instance.CurrentMap.GetClosestDir8(controlledChar.CharLoc, targetChar.CharLoc);
                if (path.Count > 1)
                    dirToChar = ZoneManager.Instance.CurrentMap.GetClosestDir8(path[path.Count - 1], path[path.Count - 2]);                    

                //is it possible to move in that direction?
                //if so, use it
                result = tryDir(controlledChar, targetChar, dirToChar, !preThink);
                if (result != null)
                    return result;
                if (dirToChar.IsDiagonal())
                {
                    Loc diff = controlledChar.CharLoc - targetChar.CharLoc;
                    DirH horiz;
                    DirV vert;
                    dirToChar.Separate(out horiz, out vert);
                    //start with the one that covers the most distance
                    if (Math.Abs(diff.X) < Math.Abs(diff.Y))
                    {
                        result = tryDir(controlledChar, targetChar, vert.ToDir8(), !preThink);
                        if (result != null)
                            return result;
                        result = tryDir(controlledChar, targetChar, horiz.ToDir8(), !preThink);
                        if (result != null)
                            return result;
                    }
                    else
                    {
                        result = tryDir(controlledChar, targetChar, horiz.ToDir8(), !preThink);
                        if (result != null)
                            return result;
                        result = tryDir(controlledChar, targetChar, vert.ToDir8(), !preThink);
                        if (result != null)
                            return result;
                    }
                }
                else
                {
                    result = tryDir(controlledChar, targetChar, DirExt.AddAngles(dirToChar, Dir8.DownLeft), !preThink);
                    if (result != null)
                        return result;
                    result = tryDir(controlledChar, targetChar, DirExt.AddAngles(dirToChar, Dir8.DownRight), !preThink);
                    if (result != null)
                        return result;
                }
            }

            //if a path can't be found to anyone, return false
            return null;
        }

        private GameAction tryDir(Character controlledChar, Character targetChar, Dir8 testDir, bool respectPeers)
        {
            Loc endLoc = controlledChar.CharLoc + testDir.GetLoc();

            //check to see if it's possible to move in this direction
            bool blocked = Grid.IsDirBlocked(controlledChar.CharLoc, testDir,
                (Loc testLoc) =>
                {
                    if (IsPathBlocked(controlledChar, testLoc))
                        return true;

                    if (ZoneManager.Instance.CurrentMap.WrapLoc(testLoc) != targetChar.CharLoc && respectPeers)
                    {
                        Character destChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc);
                        if (!canPassChar(controlledChar, destChar, true))
                            return true;
                    }

                    return false;
                },
                (Loc testLoc) =>
                {
                    return (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, controlledChar.Mobility, true));
                },
                1);

            //if that direction is good, send the command to move in that direction
            if (blocked)
                return null;

            //check to see if moving in this direction will get to the target char
            if (ZoneManager.Instance.CurrentMap.WrapLoc(endLoc) == targetChar.CharLoc)
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);


            //get the A* path to the target; if the direction goes farther from the character, return false

            return TrySelectWalk(controlledChar, testDir);
        }
    }
}
