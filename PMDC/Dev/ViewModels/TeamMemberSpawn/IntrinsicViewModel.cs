using RogueEssence.Data;
using RogueEssence.Dev.ViewModels;

namespace PMDC.Dev.ViewModels
{
    public class IntrinsicViewModel : ViewModelBase
    {
        private IntrinsicData intrinsicData;
        private bool _monsterLearns;

        public int Index { get; }

        public IntrinsicViewModel(string key, int index)
        {
            intrinsicData = DataManager.Instance.GetIntrinsic(key);
            Index = index;
        }
        
        public bool MonsterLearns
        {
            get => _monsterLearns;
        }
        
        public void SetMonsterLearns(bool learns)
        {
            _monsterLearns = learns;
        }

        public string Name
        {
            get { return intrinsicData.Name.ToLocal(); }
        }
        public bool Released
        {
            get { return intrinsicData.Released; }
        }
      
        public string Description
        {
            get { return intrinsicData.Desc.ToLocal(); }
        }
    }
}