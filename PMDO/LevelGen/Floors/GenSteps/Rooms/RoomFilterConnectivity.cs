using System;
using RogueElements;

namespace PMDO.LevelGen
{
    [Serializable]
    public class RoomFilterConnectivity : BaseRoomFilter
    {
        public RoomFilterConnectivity()
        { }

        public RoomFilterConnectivity(ConnectivityRoom.Connectivity connectivity)
        {
            this.Connection = connectivity;
        }

        public ConnectivityRoom.Connectivity Connection;

        public override bool PassesFilter(IRoomPlan plan)
        {
            ConnectivityRoom.Connectivity testConnection = ConnectivityRoom.Connectivity.None;
            ConnectivityRoom component;
            if (plan.Components.TryGet<ConnectivityRoom>(out component))
                testConnection = component.Connection;
            
            return (testConnection & Connection) != ConnectivityRoom.Connectivity.None;
        }
    }
}
