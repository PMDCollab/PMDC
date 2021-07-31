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
        public Dictionary<int, Dictionary<int, List<int>>> RarityMap;

        public RarityData()
        {
            RarityMap = new Dictionary<int, Dictionary<int, List<int>>>();
        }

        public override void ContentChanged(int idx)
        {
            //remove the index from its previous locations
            foreach (Dictionary<int, List<int>> rarityTable in RarityMap.Values)
            {
                foreach (List<int> items in rarityTable.Values)
                {
                    if (items.Remove(idx))
                        break;
                }
            }

            //Add it to its new locations
            string dataPath = DataManager.DATA_PATH + DataManager.DataType.Item.ToString() + "/";
            string dir = PathMod.ModPath(dataPath + idx + DataManager.DATA_EXT);
            ItemData data = (ItemData)DataManager.LoadData(dir);
            computeSummary(idx, data);
        }

        public override void ReIndex()
        {
            RarityMap.Clear();

            string dataPath = DataManager.DATA_PATH + DataManager.DataType.Item.ToString() + "/";
            foreach (string dir in PathMod.GetModFiles(dataPath, "*" + DataManager.DATA_EXT))
            {
                string file = Path.GetFileNameWithoutExtension(dir);
                int num = Convert.ToInt32(file);
                ItemData data = (ItemData)DataManager.LoadData(dir);
                if (data.Released)
                    computeSummary(num, data);
            }
        }

        private void computeSummary(int num, ItemData data)
        {
            FamilyState family;
            if (data.ItemStates.TryGet<FamilyState>(out family))
            {
                foreach (int monster in family.Members)
                {
                    if (!RarityMap.ContainsKey(monster))
                        RarityMap[monster] = new Dictionary<int, List<int>>();

                    if (!RarityMap[monster].ContainsKey(data.Rarity))
                        RarityMap[monster][data.Rarity] = new List<int>();

                    RarityMap[monster][data.Rarity].Add(num);
                }
            }
        }
    }

}