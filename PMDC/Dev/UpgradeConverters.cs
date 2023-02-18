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
using PMDC.Dungeon;
using RogueEssence.LevelGen;
using System.Collections;

namespace PMDC.Dev
{
    public class ItemFakeTableConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Dictionary<ItemFake, MobSpawn> dict = (Dictionary<ItemFake, MobSpawn>)value;
            writer.WriteStartArray();
            foreach (ItemFake item in dict.Keys)
            {
                serializer.Serialize(writer, (item, dict[item]));
            }
            writer.WriteEndArray();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Dictionary<ItemFake, MobSpawn> dict = new Dictionary<ItemFake, MobSpawn>();

            JArray jArray = JArray.Load(reader);
            List<(ItemFake, MobSpawn)> container = new List<(ItemFake, MobSpawn)>();
            serializer.Populate(jArray.CreateReader(), container);

            foreach ((ItemFake, MobSpawn) item in container)
                dict[item.Item1] = item.Item2;

            return dict;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Dictionary<ItemFake, MobSpawn>);
        }
    }
}
