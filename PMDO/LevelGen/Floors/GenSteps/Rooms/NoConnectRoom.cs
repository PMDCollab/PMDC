using RogueElements;
using System;

namespace PMDC.LevelGen
{
    [Serializable]
    public class NoConnectRoom : RoomComponent
    {
        public override RoomComponent Clone() { return new NoConnectRoom(); }
        public override string ToString()
        {
            return "NoConnect";
        }
    }
}
