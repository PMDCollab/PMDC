using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dev.ViewModels;
using RogueEssence.Dungeon;

namespace PMDC.Dev.ViewModels
{
    public class SkillDataViewModel : ViewModelBase
    {
        private SkillData skillData;
        private BasePowerState powerState;
        public int Index { get; }
        private bool _monsterLearns;
        

        public SkillDataViewModel(string skillKey, int index)
        {
            Index = index;
            skillData = DataManager.Instance.GetSkill(skillKey);
            ElementDisplay = DataManager.Instance.GetElement(skillData.Data.Element).Name.ToLocal();
            powerState = skillData.Data.SkillStates.GetWithDefault<BasePowerState>();
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
            get { return skillData.Name.ToLocal();  }
        }
        
        public string Element
        {
            get { return skillData.Data.Element;  }
        }
        
        public string ElementDisplay
        {
            get;
        }
        
        public BattleData.SkillCategory Category
        {
            get { return skillData.Data.Category; }
        }
        public string CategoryDisplay
        {
            get { return skillData.Data.Category.ToLocal();  }
        }
        
        public int BasePower
        {
            get { return powerState != null ? powerState.Power : -1; }
        }
        
        public int BaseCharges
        {
            get { return skillData.BaseCharges; }
        }

        
        public int Accuracy
        {
            get { return skillData.Data.HitRate; }
        }

        public string RangeDescription
        {
            get { return skillData.HitboxAction.GetDescription(); }
        }
        
        public string Description
        {
            get { return skillData.Desc.ToLocal(); }
        }

        public bool Released
        {
            get { return skillData.Released; }
        }
    }
}