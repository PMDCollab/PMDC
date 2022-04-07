using System;
using System.IO;
using RogueEssence.Data;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Xml.Serialization;
using RogueEssence.Content;
using Newtonsoft.Json;
using NLua;
using RogueElements;
using Newtonsoft.Json.Linq;
using RogueEssence.Dungeon;
using PMDC.Data;

namespace PMDC.Dev
{
    //TODO: Created v0.5.10, delete on v1.0.0
    public class MonsterFormDataConverter : JsonConverter<MonsterFormData>
    {
        public override void WriteJson(JsonWriter writer, MonsterFormData value, JsonSerializer serializer)
        {
            throw new NotImplementedException("We shouldn't be here.");
        }

        public override MonsterFormData ReadJson(JsonReader reader, Type objectType, MonsterFormData existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            MonsterFormData container = new MonsterFormData();
            serializer.Populate(jObject.CreateReader(), container);

            return container;
        }


        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

    }
}
