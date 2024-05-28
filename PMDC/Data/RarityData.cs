using System;
using System.Collections.Generic;
using RogueElements;
using System.Drawing;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dungeon;
using System.IO;
using PMDC.Dungeon;

namespace PMDC.Data
{
    [Serializable]
    public class RarityData : BaseData
    {
        public override string FileName => "Rarity";
        public override DataManager.DataType TriggerType => DataManager.DataType.Item;

        /// <summary>
        /// Maps monster, rarity to list of applicable items 
        /// </summary>
        public Dictionary<string, Dictionary<int, List<string>>> RarityMap;

        public RarityData()
        {
            RarityMap = new Dictionary<string, Dictionary<int, List<string>>>();
        }

        public override void ContentChanged(string idx)
        {
            //remove the index from its previous locations
            foreach (Dictionary<int, List<string>> rarityTable in RarityMap.Values)
            {
                foreach (List<string> items in rarityTable.Values)
                {
                    if (items.Remove(idx))
                        break;
                }
            }

            //Add it to its new locations
            string dataPath = DataManager.DATA_PATH + DataManager.DataType.Item.ToString() + "/";
            //check against deletion
            ItemData data = DataManager.LoadData<ItemData>(dataPath, idx, DataManager.DATA_EXT);
            if (data != null)
            {
                computeSummary(idx, data);
            }
        }

        public override void ReIndex()
        {
            RarityMap.Clear();

            string dataPath = DataManager.DATA_PATH + DataManager.DataType.Item.ToString() + "/";
            foreach (string dir in PathMod.GetModFiles(dataPath, "*" + DataManager.DATA_EXT))
            {
                string file = Path.GetFileNameWithoutExtension(dir);
                ItemData data = DataManager.LoadData<ItemData>(dataPath, file, DataManager.DATA_EXT);
                if (data.Released)
                    computeSummary(file, data);
            }
        }

        private void computeSummary(string num, ItemData data)
        {
            FamilyState family;
            if (data.ItemStates.TryGet<FamilyState>(out family))
            {
                foreach (string monster in family.Members)
                {
                    if (!RarityMap.ContainsKey(monster))
                        RarityMap[monster] = new Dictionary<int, List<string>>();

                    if (!RarityMap[monster].ContainsKey(data.Rarity))
                        RarityMap[monster][data.Rarity] = new List<string>();

                    RarityMap[monster][data.Rarity].Add(num);
                }
            }
        }
    }

}