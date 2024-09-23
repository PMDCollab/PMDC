using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dev.ViewModels;
using RogueEssence.Dungeon;

namespace PMDC.Dev.ViewModels
{
    public class SkillDataViewModel : ViewModelBase
    {
        private SkillDataSummary summary;
        public int Index { get; }
        private bool _monsterLearns;
        

        public SkillDataViewModel(string skillKey, int index)
        {
            Index = index;
            summary = (SkillDataSummary) DataManager.Instance.DataIndices[DataManager.DataType.Skill].Get(skillKey);
        }


        public void SetMonsterLearns(bool learns)
        {
            _monsterLearns = learns;
        }
        
        
        public bool MonsterLearns
        {
            get { return _monsterLearns; }
        }
        
        public string Name
        {
            get { return summary.Name.ToLocal();  }
        }
        
        public string Element
        {
            get { return summary.Element;  }
        }
        
        public string ElementDisplay
        {
            get { return DataManager.Instance.GetElement(Element).Name.ToLocal(); }
        }
        
        public BattleData.SkillCategory Category
        {
            get { return summary.Category; }
        }
        public string CategoryDisplay
        {
            get { return summary.Category.ToLocal();  }
        }
        
        public int BasePower
        {
            get { return summary.BasePower; }
        }
        
        public int BaseCharges
        {
            get { return summary.BaseCharges; }
        }
        
        public int Accuracy
        {
            get { return summary.HitRate; }
        }

        public string RangeDescription
        {
            get { return summary.RangeDescription; }
        }
        
        public string Description
        {
            get { return summary.Description.ToLocal(); }
        }

        public bool Released
        {
            get { return summary.Released; }
        }
    }
}