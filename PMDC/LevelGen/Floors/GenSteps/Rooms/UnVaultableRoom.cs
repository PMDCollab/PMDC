using RogueElements;
using System;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Rooms that should not be considered for attaching vault entrances.
    /// </summary>
    [Serializable]
    public class UnVaultableRoom : RoomComponent
    {
        public override RoomComponent Clone() { return new UnVaultableRoom(); }
        public override string ToString()
        {
            return "UnVaultable";
        }
    }
}
