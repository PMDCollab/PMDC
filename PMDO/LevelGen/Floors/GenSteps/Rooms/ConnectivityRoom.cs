using RogueElements;
using System;

namespace PMDO.LevelGen
{
    [Serializable]
    public class ConnectivityRoom : RoomComponent
    {
        [Flags]
        public enum Connectivity
        {
            None = 0,
            Main = 1,
            Disconnected = 2,
            SwitchVault = 4,
            KeyVault = 8,
            BossLocked = 16
        }

        public ConnectivityRoom()
        { }

        public ConnectivityRoom(Connectivity connectivity)
        {
            Connection = connectivity;
        }

        public ConnectivityRoom(ConnectivityRoom other)
        {
            Connection = other.Connection;
        }

        public Connectivity Connection { get; set; }

        public override RoomComponent Clone() { return new ConnectivityRoom(this); }

        public override string ToString()
        {
            return "ConnectType: " + Connection;
        }
    }
}
