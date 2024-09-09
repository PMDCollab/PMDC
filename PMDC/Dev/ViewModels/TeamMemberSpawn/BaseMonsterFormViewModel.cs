using System;
using PMDC.Data;
using RogueEssence.Data;
using RogueEssence.Dev.ViewModels;

namespace PMDC.Dev.ViewModels
{
    public class BaseMonsterFormViewModel : ViewModelBase
    {
        private MonsterFormData monsterForm;

        public int Index { get; }

        public BaseMonsterFormViewModel(string key, int formId, int index)
        {
            MonsterData data = DataManager.Instance.GetMonster(key);
            monsterForm = (MonsterFormData) data.Forms[formId];
            
            this.key = key;
            this.formId = formId;
            Index = index;
        }
        
        public string Name
        {
            get { return monsterForm.FormName.ToLocal();  }
        }

        private string key;
        public string Key
        {
            get { return key; }
        }
        
        private int formId;
        public int FormId
        {
            get { return formId; }
        }
        
        
        public string Element1Display
        {
            get { return DataManager.Instance.GetElement(monsterForm.Element1).Name.ToLocal(); }
        }
        
        public string Element2Display
        {
            get { return DataManager.Instance.GetElement(monsterForm.Element2).Name.ToLocal(); }
        }
        
        public string Element1
        {
            get { return monsterForm.Element1; }
        }
        
        public string Element2
        {
            get { return monsterForm.Element2;  }
        }
        
        public string Intrinsic1
        {
            get { return DataManager.Instance.GetIntrinsic(monsterForm.Intrinsic1).Name.ToLocal(); }
        }
        
        public string Intrinsic2
        {
            get
            {
                return DataManager.Instance.GetIntrinsic(monsterForm.Intrinsic2).Name.ToLocal();
            }
        }
        public string Intrinsic3
        {
            get { return DataManager.Instance.GetIntrinsic(monsterForm.Intrinsic3).Name.ToLocal(); }
        }
        
        public bool Temporary
        {
            get { return monsterForm.Temporary; }
        }
        
        public bool Released
        {
            get { return monsterForm.Released; }
        }
        
        public int BaseHP
        {
            get { return monsterForm.BaseHP; }
        }
        
        public int BaseAtk
        {
            get { return monsterForm.BaseAtk; }
        }
        
        public int BaseDef
        {
            get { return monsterForm.BaseDef; }
        }
        
        public int BaseMAtk
        {
            get { return monsterForm.BaseMAtk; }
        }
        
        public int BaseMDef
        {
            get { return monsterForm.BaseMDef; }
        }
        
        public int BaseSpeed
        {
            get { return monsterForm.BaseSpeed; }
        }
        
        public int BaseTotal
        {
            get
            { 
                return monsterForm.BaseHP + monsterForm.BaseAtk + monsterForm.BaseDef +
                     monsterForm.BaseMAtk +  monsterForm.BaseMDef +  monsterForm.BaseSpeed;
            }
        }
    }
}