using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class StayInRangePlan : AIPlan
    {
        const int MAX_RANGE = 4;

        [NonSerialized]
        private Loc targetLoc;

        public StayInRangePlan() : base()
        { }
        public StayInRangePlan(AIFlags iq) : base(iq)
        {
            targetLoc = new Loc(-1);
        }
        protected StayInRangePlan(StayInRangePlan other) : base(other) { }
        public override BasePlan CreateNew() { return new StayInRangePlan(this); }

        public override void Initialize(Character controlledChar)
        {
            targetLoc = controlledChar.CharLoc;
            base.Initialize(controlledChar);
        }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.CantWalk)
                return null;

            //check to see if the end loc is still valid
            if (ZoneManager.Instance.CurrentMap.InRange(controlledChar.CharLoc, targetLoc, MAX_RANGE))
            {
                List<Dir8> dirs = new List<Dir8>();
                dirs.Add(Dir8.None);
                for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
                    dirs.Add((Dir8)ii);
                //walk to random locations
                while (dirs.Count > 0)
                {
                    int randIndex = rand.Next(dirs.Count);
                    if (dirs[randIndex] == Dir8.None)
                        return new GameAction(GameAction.ActionType.Wait, Dir8.None);
                    else
                    {
                        Loc endLoc = controlledChar.CharLoc + ((Dir8)dirs[randIndex]).GetLoc();
                        if (ZoneManager.Instance.CurrentMap.InRange(endLoc, targetLoc, MAX_RANGE))
                        {

                            bool blocked = Grid.IsDirBlocked(controlledChar.CharLoc, (Dir8)dirs[randIndex],
                                (Loc testLoc) =>
                                {
                                    if (IsPathBlocked(controlledChar, testLoc))
                                        return true;

                                    if (!preThink && BlockedByChar(controlledChar, testLoc, Alignment.Friend | Alignment.Foe))
                                        return true;

                                    return false;
                                },
                                (Loc testLoc) =>
                                {
                                    return (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, controlledChar.Mobility, true));
                                },
                                1);

                            if (!blocked)
                                return TrySelectWalk(controlledChar, (Dir8)dirs[randIndex]);
                        }
                        dirs.RemoveAt(randIndex);
                    }
                }
            }
            return null;
        }
    }
}
