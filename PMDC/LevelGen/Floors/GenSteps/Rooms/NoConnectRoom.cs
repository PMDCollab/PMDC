using RogueElements;
using System;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Rooms that are disconnected from the main path.
    /// </summary>
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
