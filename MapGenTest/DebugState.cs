using System;
using System.Collections.Generic;
using System.Text;
using RogueElements;
using System.Diagnostics;
using RogueEssence.LevelGen;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace MapGenTest
{
    public class DebugState
    {
        public string MapString;

        public DebugState()
        {
            MapString = "";
        }
        public DebugState(string str)
        {
            MapString = str;
        }

    }
}
