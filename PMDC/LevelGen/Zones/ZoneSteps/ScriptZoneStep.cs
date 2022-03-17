using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using RogueEssence.LevelGen;
using RogueEssence.Dev;
using RogueEssence.Script;
using NLua;
using System.Linq;

namespace PMDC.LevelGen
{
    [Serializable]
    public class ScriptZoneStep : ZoneStep
    {
        public string Script;
        [Multiline(0)]
        public string ArgTable;
        [NonSerialized]
        private ulong seed;

        public ScriptZoneStep()
        {
            Script = "";
            ArgTable = "{}";
        }
        public ScriptZoneStep(string script)
        {
            Script = script;
            ArgTable = "{}";
        }
        protected ScriptZoneStep(ScriptZoneStep other, ulong seed)
        {
            Script = other.Script;
            ArgTable = other.ArgTable;
            this.seed = seed;
        }

        public override ZoneStep Instantiate(ulong seed) { return new ScriptZoneStep(this, seed); }

        public override void Apply(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue)
        {
            LuaFunction luafun = LuaEngine.Instance.LuaState.GetFunction("ZONE_GEN_SCRIPT." + Script);

            if (luafun != null)
            {
                LuaTable args = LuaEngine.Instance.RunString("return " + ArgTable).First() as LuaTable;
                luafun.Call(new object[] { zoneContext, context, queue, seed, args });
            }
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }
}
