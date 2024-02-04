using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using RogueEssence.Dev;

namespace PMDC.Dungeon
{
    [Serializable]
    public class FleeStairsPlan : AIPlan
    {
        [DataType(0, DataManager.DataType.Tile, false)]
        public HashSet<string> StairIds;
        public FleeStairsPlan(AIFlags iq, HashSet<string> destLocations) : base(iq)
        {
            StairIds = destLocations;
        }

        protected FleeStairsPlan(FleeStairsPlan other) : base(other)
        {
            StairIds = other.StairIds;
        }
        public override BasePlan CreateNew() { return new FleeStairsPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            Map map = ZoneManager.Instance.CurrentMap;

            Loc seen = Character.GetSightDims();
            
            Rect sightBounds = Rect.FromPoints(controlledChar.CharLoc - seen, controlledChar.CharLoc + seen + Loc.One);
            sightBounds = controlledChar.MemberTeam.ContainingMap.GetClampedSight(sightBounds);
            
            // Get all the visible stairs within vision
            List<Loc> stairLocs = new List<Loc>();  
            for (int xx = sightBounds.X; xx < sightBounds.End.X; xx++)
            {
                for (int yy = sightBounds.Y; yy < sightBounds.End.Y; yy++) { 
                    
                    Loc loc = new Loc(xx, yy);
                    
                    Tile tile = map.GetTile(loc);
                    if (tile != null && tile.Effect.Revealed && StairIds.Contains(tile.Effect.ID) && controlledChar.CanSeeLoc(loc, controlledChar.GetCharSight()))
                    {
                        //do nothing if positioned at the stairs
                        if (loc == controlledChar.CharLoc)
                        {
                            return new GameAction(GameAction.ActionType.Wait, Dir8.None);;
                        }
                        stairLocs.Add(loc);   
                    };
                }
            }
            GameAction result = null;

            foreach(Loc stairLoc in stairLocs)
            {
                if (controlledChar.CanSeeLoc(stairLoc, controlledChar.GetCharSight()))
                {
                    List<Loc>[] paths = GetPaths(controlledChar, new Loc[1] { stairLoc }, true, false);
                    List<Loc> path = paths[0];
                    
                    Dir8 dirToChar = ZoneManager.Instance.CurrentMap.GetClosestDir8(controlledChar.CharLoc, stairLoc);
                    if (path.Count > 1)
                        dirToChar = ZoneManager.Instance.CurrentMap.GetClosestDir8(path[path.Count - 1],
                            path[path.Count - 2]);
                    
                    result = tryDir(controlledChar, stairLoc, dirToChar, !preThink);
                    if (result != null)
                        return result;
                    if (dirToChar.IsDiagonal())
                    {
                        Loc diff = controlledChar.CharLoc - stairLoc;
                        DirH horiz;
                        DirV vert;
                        dirToChar.Separate(out horiz, out vert);
                        //start with the one that covers the most distance
                        if (Math.Abs(diff.X) < Math.Abs(diff.Y))
                        {
                            result = tryDir(controlledChar, stairLoc, vert.ToDir8(), !preThink);
                            if (result != null)
                                return result;
                            result = tryDir(controlledChar, stairLoc, horiz.ToDir8(), !preThink);
                            if (result != null)
                                return result;
                        }
                        else
                        {
                            result = tryDir(controlledChar, stairLoc, horiz.ToDir8(), !preThink);
                            if (result != null)
                                return result;
                            result = tryDir(controlledChar, stairLoc, vert.ToDir8(), !preThink);
                            if (result != null)
                                return result;
                        }
                    }
                    else
                    {
                        result = tryDir(controlledChar, stairLoc, DirExt.AddAngles(dirToChar, Dir8.DownLeft),
                            !preThink);
                        if (result != null)
                            return result;
                        result = tryDir(controlledChar, stairLoc, DirExt.AddAngles(dirToChar, Dir8.DownRight),
                            !preThink);
                        if (result != null)
                            return result;
                    }
                }
            }
            
            return null;
        }

        private GameAction tryDir(Character controlledChar, Loc stairLoc, Dir8 testDir, bool respectPeers)
        {
            //check to see if it's possible to move in this direction
            bool blocked = Grid.IsDirBlocked(controlledChar.CharLoc, testDir,
                (Loc testLoc) =>
                {
                    if (IsPathBlocked(controlledChar, testLoc))
                        return true;

                    if (ZoneManager.Instance.CurrentMap.WrapLoc(testLoc) != stairLoc && respectPeers)
                    {
                        Character destChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc);
                        if (!canPassChar(controlledChar, destChar, false))
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
            
            //get the A* path to the target; if the direction goes farther from the character, return false
            return TrySelectWalk(controlledChar, testDir);
        }
    }
}
