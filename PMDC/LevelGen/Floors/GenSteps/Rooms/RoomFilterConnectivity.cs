﻿using System;
using RogueElements;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Filters for rooms based on their connectivity.
    /// </summary>
    [Serializable]
    public class RoomFilterConnectivity : BaseRoomFilter
    {
        public RoomFilterConnectivity()
        { }

        public RoomFilterConnectivity(ConnectivityRoom.Connectivity connectivity)
        {
            this.Connection = connectivity;
        }

        /// <summary>
        /// The connectivity types to filter for.
        /// </summary>
        public ConnectivityRoom.Connectivity Connection;

        public override bool PassesFilter(IRoomPlan plan)
        {
            ConnectivityRoom.Connectivity testConnection = ConnectivityRoom.Connectivity.None;
            ConnectivityRoom component;
            if (plan.Components.TryGet<ConnectivityRoom>(out component))
                testConnection = component.Connection;
            
            return (testConnection & Connection) != ConnectivityRoom.Connectivity.None;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", this.GetType().GetFormattedTypeName(), this.Connection.ToString());
        }
    }
}
