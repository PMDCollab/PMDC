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
        public int Factor;
        public bool Omniscient;

        public FleeStairsPlan(AIFlags iq, HashSet<string> destLocations, bool omniscient = false, int factor = 1) : base(iq)
        {
            StairIds = destLocations;
            Omniscient = omniscient;
            Factor = factor;
        }

        protected FleeStairsPlan(FleeStairsPlan other) : base(other)
        {
            StairIds = other.StairIds;
            Omniscient = other.Omniscient;
            Factor = other.Factor;
        }
        public override BasePlan CreateNew() { return new FleeStairsPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.HP * Factor >= controlledChar.MaxHP)
                return null;

            if (controlledChar.CantWalk)
                return null;

            Map map = ZoneManager.Instance.CurrentMap;

            Loc seen = Character.GetSightDims();

            Rect sightBounds = new Rect(Loc.Zero, controlledChar.MemberTeam.ContainingMap.Size);
            if (!Omniscient)
            {
                Rect.FromPoints(controlledChar.CharLoc - seen, controlledChar.CharLoc + seen + Loc.One);
                sightBounds = controlledChar.MemberTeam.ContainingMap.GetClampedSight(sightBounds);
            }
            
            // Get all the visible stairs within vision
            List<Loc> stairLocs = new List<Loc>();  
            for (int xx = sightBounds.X; xx < sightBounds.End.X; xx++)
            {
                for (int yy = sightBounds.Y; yy < sightBounds.End.Y; yy++) { 
                    
                    Loc loc = new Loc(xx, yy);
                    
                    Tile tile = map.GetTile(loc);
                    if (tile != null && tile.Effect.Revealed && StairIds.Contains(tile.Effect.ID) && 
                        (Omniscient || controlledChar.CanSeeLoc(loc, controlledChar.GetCharSight())))
                    {
                        //do nothing if positioned at the stairs
                        if (loc == controlledChar.CharLoc)
                        {
                            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
                        }
                        stairLocs.Add(loc);   
                    };
                }
            }

            List<Loc> path = GetEscapePath(controlledChar, stairLocs.ToArray());
            if (path.Count > 1)
                return SelectChoiceFromPath(controlledChar, path);

            return null;
        }

        protected List<Loc> GetEscapePath(Character controlledChar, Loc[] ends)
        {
            Loc[] wrappedEnds = getWrappedEnds(controlledChar.CharLoc, ends);

            //requires a valid target tile
            Grid.LocTest checkDiagBlock = (Loc loc) => {
                return (ZoneManager.Instance.CurrentMap.TileBlocked(loc, controlledChar.Mobility, true));
                //enemy/ally blockings don't matter for diagonals
            };

            Grid.LocTest checkBlock = (Loc testLoc) => {

                if (IsPathBlocked(controlledChar, testLoc))
                    return true;

                if (BlockedByChar(controlledChar, testLoc, Alignment.Foe))
                    return true;

                return false;
            };

            Rect sightBounds = new Rect(Loc.Zero, controlledChar.MemberTeam.ContainingMap.Size);
            if (!Omniscient)
                sightBounds = new Rect(controlledChar.CharLoc - Character.GetSightDims(), Character.GetSightDims() * 2 + new Loc(1));
            List<Loc>[] paths = Grid.FindNPaths(sightBounds.Start, sightBounds.Size, controlledChar.CharLoc, wrappedEnds, checkBlock, checkDiagBlock, 1, false);
            return paths[0];
        }
    }
}
