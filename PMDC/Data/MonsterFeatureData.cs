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
        public int Family;
        public EvoFlag Stage;

        public int Element1;
        public int Element2;

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
        public Dictionary<int, Dictionary<int, FormFeatureSummary>> FeatureData;

        public MonsterFeatureData()
        {
            FeatureData = new Dictionary<int, Dictionary<int, FormFeatureSummary>>();
        }

        public override void ContentChanged(int idx)
        {
            string dataPath = DataManager.DATA_PATH + DataManager.DataType.Monster.ToString() + "/";
            string dir = PathMod.ModPath(dataPath + idx + ".bin");
            MonsterData data = (MonsterData)DataManager.LoadData(dir);
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
                int num = Convert.ToInt32(file);
                MonsterData data = (MonsterData)DataManager.LoadData(dir);
                Dictionary<int, FormFeatureSummary> formSummaries = computeSummary(dataPath, num, data);
                FeatureData[num] = formSummaries;
            }
        }

        private Dictionary<int, FormFeatureSummary> computeSummary(string dataPath, int num, MonsterData data)
        {
            Dictionary<int, FormFeatureSummary> formFeatureData = new Dictionary<int, FormFeatureSummary>();
            int family = num;
            MonsterData preEvo = data;
            while (preEvo.PromoteFrom > -1)
            {
                family = preEvo.PromoteFrom;
                string preDir = PathMod.ModPath(dataPath + family + ".bin");
                preEvo = (MonsterData)DataManager.LoadData(preDir);
            }
            EvoFlag stage = EvoFlag.NoEvo;
            bool evolvedFrom = (data.PromoteFrom > -1);
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