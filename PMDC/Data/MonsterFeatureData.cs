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
    [Flags]
    public enum EvoFlag
    {
        None = 0,
        NoEvo = 1,//^0
        FirstEvo = 2,//^1
        FinalEvo = 4,//^2
        MidEvo = 8,//^3
        All = 15
    }

    [Serializable]
    public class FormFeatureSummary
    {
        public string Family;
        public EvoFlag Stage;

        public string Element1;
        public string Element2;

        public Stat BestStat;
        public Stat WorstStat;
    }


    [Serializable]
    public class MonsterFeatureData : BaseData
    {
        public override string FileName => "MonsterFeature";
        public override DataManager.DataType TriggerType => DataManager.DataType.Monster;

        /// <summary>
        /// Maps monster, form to summary
        /// </summary>
        public Dictionary<string, Dictionary<int, FormFeatureSummary>> FeatureData;

        public MonsterFeatureData()
        {
            FeatureData = new Dictionary<string, Dictionary<int, FormFeatureSummary>>();
        }

        public override void ContentChanged(string idx)
        {
            string dataPath = DataManager.DATA_PATH + DataManager.DataType.Monster.ToString() + "/";
            MonsterData data = DataManager.LoadEntryData<MonsterData>(dataPath, idx, DataManager.DATA_EXT);
            Dictionary<int, FormFeatureSummary> formSummaries = computeSummary(dataPath, idx, data);
            FeatureData[idx] = formSummaries;
        }

        public override void ReIndex()
        {
            FeatureData.Clear();

            string dataPath = DataManager.DATA_PATH + DataManager.DataType.Monster.ToString() + "/";
            foreach (string dir in PathMod.GetModFiles(dataPath, "*" + DataManager.DATA_EXT))
            {
                string file = Path.GetFileNameWithoutExtension(dir);
                MonsterData data = DataManager.LoadData<MonsterData>(dataPath, file, DataManager.DATA_EXT);
                Dictionary<int, FormFeatureSummary> formSummaries = computeSummary(dataPath, file, data);
                FeatureData[file] = formSummaries;
            }
        }

        private Dictionary<int, FormFeatureSummary> computeSummary(string dataPath, string num, MonsterData data)
        {
            Dictionary<int, FormFeatureSummary> formFeatureData = new Dictionary<int, FormFeatureSummary>();
            string family = num;
            MonsterData preEvo = data;
            while (!String.IsNullOrEmpty(preEvo.PromoteFrom))
            {
                family = preEvo.PromoteFrom.ToString();
                preEvo = DataManager.LoadData<MonsterData>(dataPath, family, DataManager.DATA_EXT);
            }
            EvoFlag stage = EvoFlag.NoEvo;
            bool evolvedFrom = !String.IsNullOrEmpty(data.PromoteFrom);
            bool evolves = (data.Promotions.Count > 0);
            if (evolvedFrom && evolves)
                stage = EvoFlag.MidEvo;
            else if (evolvedFrom)
                stage = EvoFlag.FinalEvo;
            else if (evolves)
                stage = EvoFlag.FirstEvo;

            for (int ii = 0; ii < data.Forms.Count; ii++)
            {
                FormFeatureSummary summary = new FormFeatureSummary();
                summary.Family = family;
                summary.Stage = stage;

                MonsterFormData formData = data.Forms[ii] as MonsterFormData;
                summary.Element1 = formData.Element1;
                summary.Element2 = formData.Element2;

                Stat bestStat = Stat.HP;
                Stat worstStat = Stat.HP;

                for (int nn = 0; nn < (int)Stat.HitRate; nn++)
                {
                    if (bestStat != Stat.None)
                    {
                        if (formData.GetBaseStat((Stat)nn) > formData.GetBaseStat(bestStat))
                            bestStat = (Stat)nn;
                        else if (formData.GetBaseStat((Stat)nn) == formData.GetBaseStat(bestStat))
                            bestStat = Stat.None;
                    }
                    if (worstStat != Stat.None)
                    {
                        if (formData.GetBaseStat((Stat)nn) < formData.GetBaseStat(worstStat))
                            worstStat = (Stat)nn;
                        else if (formData.GetBaseStat((Stat)nn) == formData.GetBaseStat(worstStat))
                            worstStat = Stat.None;
                    }
                }
                summary.BestStat = bestStat;
                summary.WorstStat = worstStat;

                formFeatureData[ii] = summary;
            }
            return formFeatureData;
        }
    }

}