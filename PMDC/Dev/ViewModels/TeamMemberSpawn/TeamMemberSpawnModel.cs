using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using PMDC.Data;
using PMDC.LevelGen;
using ReactiveUI;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dev;
using RogueEssence.Dev.ViewModels;
using RogueEssence.Dev.Views;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.Dev.ViewModels
{ 
    public class TeamMemberSpawnModel : ViewModelBase
    {
        public TeamMemberSpawn TeamSpawn;
        public CollectionBoxViewModel SpawnConditions { get; set; }
        public CollectionBoxViewModel SpawnFeatures { get; set; }

        private bool _applySearch = true;
        private bool _checkSpawnFeatureDiff = true;
        
        private int _selectedMonsterIndex;
        public int SelectedMonsterIndex
        {
            get { return _selectedMonsterIndex; }
            set
            {
               
                if (this.SetIfChanged(ref _selectedMonsterIndex, value))
                {
                    UpdateMonster(_selectedMonsterIndex);
                }
            }
        }
        
        
        private bool _disableUnusedSlots;

        public bool DisableUnusedSlots
        {
            get { return _disableUnusedSlots; }
            set
            {
                if (this.SetIfChanged(ref _disableUnusedSlots, value)  && _checkSpawnFeatureDiff)
                {
                    ToggleUnusedSlots(_disableUnusedSlots);
                }
            }
        }
        
        
        private bool _isWeakMonster;

        public bool IsWeakMob
        {
            get { return _isWeakMonster; }
            set
            {
                if (this.SetIfChanged(ref _isWeakMonster, value)  && _checkSpawnFeatureDiff)
                {
                    ToggleWeakMonster(_isWeakMonster);
                }
            }
        }
        
        private bool _unrecruitable;
        public bool Unrecruitable
        {
            get { return _unrecruitable; }
            set
            {
                if (this.SetIfChanged(ref _unrecruitable, value)  && _checkSpawnFeatureDiff)
                {
                    ToggleUnrecruitable(_unrecruitable);
                }
            }
        }
        

        private void ToggleUnusedSlots(bool disable)
        {
            List<MobSpawnExtra> features = SpawnFeatures.GetList<List<MobSpawnExtra>>();
            if (disable)
            {
                int skillCount = getSkillList().Count;
                SpawnFeatures.InsertItem(SpawnFeatures.Collection.Count, new MobSpawnMovesOff(skillCount));
            }
            else
            {
                for (int ii = features.Count - 1; ii >= 0; ii--)
                {
                    if (features[ii] is MobSpawnMovesOff)
                    {
                        SpawnFeatures.DeleteItem(ii);
                    }
                }
            }
        }
        
        private void ToggleWeakMonster(bool weak)
        {
            List<MobSpawnExtra> features = SpawnFeatures.GetList<List<MobSpawnExtra>>();
            if (weak)
            {
                SpawnFeatures.InsertItem(SpawnFeatures.Collection.Count, new MobSpawnWeak());
            }
            else
            {
                for (int ii = features.Count - 1; ii >= 0; ii--)
                {
                    if (features[ii] is MobSpawnWeak)
                    {
                        SpawnFeatures.DeleteItem(ii);
                    }
                }
            }
        }
        
        private void ToggleUnrecruitable(bool unrecruitable)
        {

            List<MobSpawnExtra> features = SpawnFeatures.GetList<List<MobSpawnExtra>>();
            for (int ii = features.Count - 1; ii >= 0; ii--)
            {
                if (features[ii] is MobSpawnUnrecruitable)
                {
                    SpawnFeatures.DeleteItem(ii);
                }
            }
            
            if (unrecruitable)
            {
                SpawnFeatures.InsertItem(SpawnFeatures.Collection.Count, new MobSpawnUnrecruitable());
            }
        }
        
        private int _selectedIntrinsicIndex;

        public int SelectedIntrinsicIndex
        {
            get { return _selectedIntrinsicIndex; }
            set
            {
                if (this.SetIfChanged(ref _selectedIntrinsicIndex, value))
                {
                    UpdateIntrinsic(_selectedIntrinsicIndex);
                }
            }
        }

        private void UpdateIntrinsic(int index)
        {
            string intrinsic = "";
            if (_selectedIntrinsicIndex != -1)
            {
                intrinsic = intrinsicKeys[index];
                SelectedIntrinsic = intrinsicData[index];
            } 

            TeamSpawn.Spawn.Intrinsic = intrinsic;
        }
        
        private bool _includeUnreleasedIntrinsics;
        public bool IncludeUnreleasedIntrinsics
        {
            get { return _includeUnreleasedIntrinsics; }
            set
            {
                
                if (this.SetIfChanged(ref _includeUnreleasedIntrinsics, value))
                {
                    if (_applySearch) 
                        updateIntrinsicData(SearchIntrinsicFilter);
                    
                }
            }
        }

        private bool _includeUnreleasedSkills;
        public bool IncludeUnreleasedSkills
        {
            get { return _includeUnreleasedSkills; }
            set
            {
                
                if (this.SetIfChanged(ref _includeUnreleasedSkills, value))
                {
                    if (_applySearch) 
                        UpdateSkillData(CurrentSkillSearchFilter);
                }
            }
        }
        
        private bool _includeUnreleasedForms;
        public bool IncludeTemporaryForms
        {
            get => _includeTemporaryForms;
            set
            {
                if (this.SetIfChanged(ref _includeTemporaryForms, value))
                { 
                    if (_applySearch) 
                        updateMonsterForms(SearchMonsterFilter);
                }
            }
        }
        
        private bool _includeTemporaryForms;
        public bool IncludeUnreleasedForms
        {
            get => _includeUnreleasedForms;
            set
            {
                if (this.SetIfChanged(ref _includeUnreleasedForms, value))
                {
                    if (_applySearch) 
                        updateMonsterForms(SearchMonsterFilter);
                }
            }
        }


        private string _searchMonsterFilter;
        public string SearchMonsterFilter
        {
            get => _searchMonsterFilter;
            set
            {
                if (this.SetIfChanged(ref _searchMonsterFilter, value))
                {
                    if (_applySearch) 
                        updateMonsterForms(_searchMonsterFilter);
                }
            }
        }
        private string _searchIntrinsicFilter = string.Empty;
        public string SearchIntrinsicFilter
        {
            get => _searchIntrinsicFilter;
            set
            {
                if (this.SetIfChanged(ref _searchIntrinsicFilter, value))
                {
                    if (_applySearch) 
                        updateIntrinsicData(_searchIntrinsicFilter);
                }
            }
        }
        public int Min
        {
            get { return TeamSpawn.Spawn.Level.Min; }
            set { this.RaiseAndSetIfChanged(ref TeamSpawn.Spawn.Level.Min, value); }
        }
        
        public int Max
        {
            get { return TeamSpawn.Spawn.Level.Max; }
            set { this.RaiseAndSetIfChanged(ref TeamSpawn.Spawn.Level.Max, value); }
        }
            
        private DataGridType _gridViewType = DataGridType.Monster; 
        public DataGridType CurrentDataGridView
        {
            get { return _gridViewType; }
            set { this.RaiseAndSetIfChanged(ref _gridViewType, value); }
        }
        
        public string CurrentSkillSearchFilter;
            
        private string _searchSkill0Filter = string.Empty;
        public string SearchSkill0Filter
        {
            get { return _searchSkill0Filter; }
            set
            { 
                if (this.SetIfChanged(ref _searchSkill0Filter, value))
                {
                    UpdateSkillData(_searchSkill0Filter);
                }
            }
        }
        
         
        private string _searchSkill1Filter = string.Empty;
        public string SearchSkill1Filter
        {
            get { return _searchSkill1Filter; }
            set
            { 
                if (this.SetIfChanged(ref _searchSkill1Filter, value))
                {
                    UpdateSkillData(_searchSkill1Filter);
                }
            }
        }
        
        private string _searchSkill2Filter = string.Empty;
        public string SearchSkill2Filter
        {
            get { return _searchSkill2Filter; }
            set
            { 
                if (this.SetIfChanged(ref _searchSkill2Filter, value))
                {
                    UpdateSkillData(_searchSkill2Filter);
                }
            }
        }
        private string _searchSkill3Filter = string.Empty;
        public string SearchSkill3Filter
        {
            get { return _searchSkill3Filter; }
            set
            { 
                if (this.SetIfChanged(ref _searchSkill3Filter, value))
                {
                    UpdateSkillData(_searchSkill3Filter);
                }
            }
        }

        private void updateMonsterForms(string filter)
        {
            FilteredMonsterForms.Clear();
            IEnumerable<BaseMonsterFormViewModel> result = monsterForms.Select(item => new
                {
                    item,
                    startsWith = startsWith(item.Name, filter),
                    prefixStartsWith = prefixStartsWith(item.Name, filter),
                    includeUnreleased = IncludeUnreleasedForms || item.Released,
                    temporary = !item.Temporary || IncludeTemporaryForms,
                })
                .Where(item =>
                    {
                        return (item.prefixStartsWith || item.startsWith) && item.temporary & item.includeUnreleased;
                    }
                )
                .OrderByDescending(item => item.startsWith)
                .ThenByDescending(item => item.prefixStartsWith)
                .Select(x => x.item);
            addFilteredItems<BaseMonsterFormViewModel>(FilteredMonsterForms, result);
        }
        private void updateIntrinsicData(string filter)
        {
            FilteredIntrinsicData.Clear();
            IEnumerable<IntrinsicViewModel> result;
            
            if (SearchIntrinsicFilter == "")
            {
                ResetIntrinsic();
                result = intrinsicData.Select(item => new
                    {
                        item,
                        learns = item.MonsterLearns,
                        includeUnreleased = IncludeUnreleasedIntrinsics || item.Released,
                        selected = item.Index == SelectedIntrinsicIndex,
                    })
                    .Where(item =>
                        {
                            return (item.learns || item.selected) && item.includeUnreleased;
                        }
                    )
                    .Select(x => x.item);
            }
            else
            {

                result = intrinsicData.Select(item => new
                    {
                        item,
                        startsWith = startsWith(item.Name, filter),
                        prefixStartsWith = prefixStartsWith(item.Name, filter),
                        includeUnreleased = IncludeUnreleasedIntrinsics || item.Released,
                        learns = item.MonsterLearns,
                    })
                    .Where(item =>
                        {
                            return (item.prefixStartsWith || item.startsWith) && item.includeUnreleased;
                        }
                    )
                    .OrderByDescending(item => item.learns)
                    .ThenByDescending(item => item.startsWith)
                    .ThenByDescending(item => item.prefixStartsWith)
                    .Select(x => x.item);
            }
            
            addFilteredItems<IntrinsicViewModel>(FilteredIntrinsicData, result);
        }
        
        public int FocusedSkillIndex;
        
        // TODO: Remove logic from view?
        public void UpdateSkillData(string filter)
        {
            CurrentSkillSearchFilter = filter;
            FilteredSkillData.Clear();
            IEnumerable<SkillDataViewModel> result;
            if (CurrentSkillSearchFilter == "")
            {
                ResetSkill();
                result = skillData.Select(item => new
                    {
                        item,
                        learns = item.MonsterLearns,
                        includeUnreleased = IncludeUnreleasedSkills || item.Released,
                        inSkillData = SkillIndex.Contains(item.Index),
                    })
                    .Where(item =>
                        {
                            return (item.learns || item.inSkillData) && item.includeUnreleased;
                        }
                    )
                    .Select(x => x.item);
            }
            else
            {
                
                result = skillData.Select(item => new
                    {
                        item,
                        startsWith = startsWith(item.Name, filter),
                        prefixStartsWith = prefixStartsWith(item.Name, filter),
                        includeUnreleased = IncludeUnreleasedSkills || item.Released,
                        learns = item.MonsterLearns,
                    })
                    .Where(item =>
                        {
                            return (item.prefixStartsWith || item.startsWith) && item.includeUnreleased;
                        }
                    )
                    .OrderByDescending(item => item.learns)
                    .ThenByDescending(item => item.startsWith)
                    .ThenByDescending(item => item.prefixStartsWith)
                    .Select(x => x.item);
            }

            addFilteredItems<SkillDataViewModel>(FilteredSkillData, result);
        }
        
        public int ChosenGender
        {
            get { return genderKeys.IndexOf((int) TeamSpawn.Spawn.BaseForm.Gender); }
            set
            {
                this.RaiseAndSetIfChanged(ref TeamSpawn.Spawn.BaseForm.Gender, (Gender) genderKeys[value]);
            }
        }

        public void SetMonster(int index)
        {
            SelectedMonsterIndex = index;
            BaseMonsterFormViewModel vm = monsterForms[index];
            SearchMonsterFilter = vm.Name;
        }

        private void UpdateMonster(int index)
        {
            BaseMonsterFormViewModel vm = monsterForms[index];
            
            SetFormPossibleIntrinsics(vm.Key, vm.FormId);
            updateIntrinsicData(_searchIntrinsicFilter);
            _applySearch = false;
            SelectedMonsterForm = vm;
            TeamSpawn.Spawn.BaseForm.Form = vm.FormId;
            TeamSpawn.Spawn.BaseForm.Species = vm.Key;
            SetFormPossibleSkills(vm.Key, vm.FormId);
            SkillIndex = new List<int> { -1, -1, -1, -1 };
            TeamSpawn.Spawn.SpecifiedSkills = new List<string>();
            SearchSkill0Filter = "";
            SearchSkill1Filter = "";
            SearchSkill2Filter = "";
            SearchSkill3Filter = "";
            SearchIntrinsicFilter = "";
            SelectedIntrinsic = null;
            SelectedIntrinsicIndex = -1;
            _applySearch = true;
            
        }
        private BaseMonsterFormViewModel _selectedMonsterForm;
        public BaseMonsterFormViewModel SelectedMonsterForm
        {
            get => _selectedMonsterForm;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedMonsterForm, value);
            }
        }
        
        private IntrinsicViewModel _selectedIntrinsic;
        public IntrinsicViewModel SelectedIntrinsic
        {
            get => _selectedIntrinsic;
            set { this.RaiseAndSetIfChanged(ref _selectedIntrinsic, value); }
        }
        
        
        private SkillDataViewModel _selectedSkillData;
        public SkillDataViewModel SelectedSkillData
        {
            get => _selectedSkillData;
            set { this.RaiseAndSetIfChanged(ref _selectedSkillData, value); }
        }
        
        public int ChosenSkin
        {
            get { return skinKeys.IndexOf(TeamSpawn.Spawn.BaseForm.Skin); }
            set { this.RaiseAndSetIfChanged(ref TeamSpawn.Spawn.BaseForm.Skin, skinKeys[value]); }
        }
        
        public int ChosenTactic
        {
            get { return tacticKeys.IndexOf(TeamSpawn.Spawn.Tactic); }
            set { this.RaiseAndSetIfChanged(ref TeamSpawn.Spawn.Tactic, tacticKeys[value]); }
        }
        
        public int ChosenRole
        {
            get { return Roles.IndexOf(TeamSpawn.Role.ToLocal()); }
            set { this.RaiseAndSetIfChanged(ref TeamSpawn.Role,  (TeamMemberSpawn.MemberRole) value); }
        }
        
        private int _focusIndex;
        public int FocusIndex
        {
            get => _focusIndex;
            set => _focusIndex = value;
        }
        
        public void SetFocusIndex(int index)
        {
            FocusIndex = index;
            UpdateDataGrid();
        }
        
        private List<string> tacticKeys;
        private List<string> monsterKeys;
        private List<string> skinKeys;
        private List<int> genderKeys;
        private List<string> intrinsicKeys;
        private List<string> skillKeys;
        
        private List<BaseMonsterFormViewModel> monsterForms;
        private List<SkillDataViewModel> skillData;
        private List<IntrinsicViewModel> intrinsicData;
        public List<int> SkillIndex;
        
        public ObservableCollection<string> Tactics { get; set; }
        
        public ObservableCollection<string> Skins { get; set; }
        public ObservableCollection<string> Genders { get; set; }
        public ObservableCollection<string> Roles { get; set;  }
        public ObservableCollection<BaseMonsterFormViewModel> FilteredMonsterForms { get; set; }
        public ObservableCollection<SkillDataViewModel> FilteredSkillData { get; set; }
        public ObservableCollection<IntrinsicViewModel> FilteredIntrinsicData { get; set; }
        
        
        private void InitializeGenders()
        {
            Genders = new ObservableCollection<string>();
            genderKeys = new List<int>();

            for (int ii = -1; ii <= (int)Gender.Female; ii++)
            {
                Genders.Add(((Gender)ii).ToLocal());
                genderKeys.Add(ii);
            }
            
        }

        private void InitializeIntrinsics()
        {
            FilteredIntrinsicData = new ObservableCollection<IntrinsicViewModel>();
            PossibleIntrinsicIndexes = new List<int>();
            intrinsicData = new List<IntrinsicViewModel>();
            intrinsicKeys = new List<string>();
            Dictionary<string, string> intrinsicNames = DataManager.Instance.DataIndices[DataManager.DataType.Intrinsic].GetLocalStringArray(true);

            int ii = 0;
            foreach (string key in intrinsicNames.Keys)
            {
                intrinsicKeys.Add(key);
                intrinsicData.Add(new IntrinsicViewModel(key, ii));
                ii++;
            }

        }


        public void ReplaceSkillIndex(int index, int skillIndex = -1)
        {
            if (skillIndex != -1)
            {
                SkillIndex[skillIndex] = index;
            }
            else
            {
                SkillIndex[FocusedSkillIndex] = index;
                
            }
            
            TeamSpawn.Spawn.SpecifiedSkills = getSkillList();
        }
        
        public void SetSkill(SkillDataViewModel skillData, int ind = -1)
        {
            ReplaceSkillIndex(skillData.Index, ind);

            int index = ind;
            
            if (ind == -1)
            {
                index = FocusedSkillIndex;
            }
            
            if (index == 0)
            {
                SearchSkill0Filter = skillData.Name;
            } else if (index == 1)
            {
                SearchSkill1Filter = skillData.Name;
            } else if (index == 2)
            {
                SearchSkill2Filter = skillData.Name;
            } else if (index == 3)
            {
                SearchSkill3Filter = skillData.Name;   
            }
        }
        
        public void SetIntrinsic(IntrinsicViewModel vm)
        {
            SelectedIntrinsicIndex = vm.Index;
            SearchIntrinsicFilter = vm.Name;
            TeamSpawn.Spawn.Intrinsic = intrinsicKeys[vm.Index];
        }
        
        public void ResetIntrinsic()
        {
            SelectedIntrinsicIndex = -1;
        }
        
        public void ResetSkill(int skillSlot = -1)
        {
            ReplaceSkillIndex(-1, skillSlot);
        }
        
        // public void ResetMonster()
        // {
        //     SelectedMonsterIndex = -1;
        // }
        
        public SkillDataViewModel SkillDataAtIndex(int index)
        {
            return skillData[index];
        }
        
        public BaseMonsterFormViewModel MonsterAtIndex(int index)
        {
            return monsterForms[index];
        }
        
        public IntrinsicViewModel IntrinsicAtIndex(int index)
        {
            return intrinsicData[index];
        }
        private void InitializeSkills()
        {
            PossibleSkillIndexes = new List<int>();
            skillData = new List<SkillDataViewModel>();
            FilteredSkillData = new ObservableCollection<SkillDataViewModel>();
            skillKeys = new List<string>();
            Dictionary<string, string> skillNames = DataManager.Instance.DataIndices[DataManager.DataType.Skill].GetLocalStringArray(true);
            
            int ii = 0;
            foreach (string key in skillNames.Keys)
            {
                skillKeys.Add(key);
                skillData.Add(new SkillDataViewModel(key, ii));
                ii++;
            }
            SkillIndex = new List<int> { -1, -1, -1, -1 };
        }

        private void InitializeMonsters()
        {
            monsterKeys = new List<string>();
            Dictionary<string, string> monsterNames = DataManager.Instance.DataIndices[DataManager.DataType.Monster].GetLocalStringArray(true);
            List<BaseMonsterFormViewModel> forms = new List<BaseMonsterFormViewModel>();

            int index = 0;
            foreach (string key in monsterNames.Keys)
            {
                monsterKeys.Add(key);
                MonsterEntrySummary summary = (MonsterEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Monster].Get(key);
               
                for (int jj = 0; jj < summary.Forms.Count; jj++)
                {
              
                    forms.Add(new BaseMonsterFormViewModel(key, jj, index));
                    index += 1;
                }
            }
            
            monsterForms = new List<BaseMonsterFormViewModel>(forms);
            FilteredMonsterForms = new ObservableCollection<BaseMonsterFormViewModel>();
        }

        
        private void InitializeSkins()
        {
            Skins = new ObservableCollection<string>();
            skinKeys = new List<string>();
            Skins.Add("**EMPTY**");
            skinKeys.Add("");
            
            Dictionary<string, string> skin_names = DataManager.Instance.DataIndices[DataManager.DataType.Skin].GetLocalStringArray(true);
            foreach (string key in skin_names.Keys)
            {
                Skins.Add(skin_names[key]);
                skinKeys.Add(key);
            }
        }
        
        private void InitializeTactics()
        {
            Tactics = new ObservableCollection<string>();
            Dictionary<string, string> tacticNames = DataManager.Instance.DataIndices[DataManager.DataType.AI].GetLocalStringArray();

            tacticKeys = new List<string>();

            foreach (string key in tacticNames.Keys)
            {
                tacticKeys.Add(key);
                Tactics.Add(tacticNames[key]);
            }

        }

        private void InitializeRoles()
        {   
            Roles = new ObservableCollection<string>();
            for (int ii = 0; ii <= (int)TeamMemberSpawn.MemberRole.Loner; ii++) {
                Roles.Add(((TeamMemberSpawn.MemberRole)ii).ToLocal());
            }
        }
        
     

        public void Initialize()
        {
            InitializeMonsters();
            InitializeGenders();
            InitializeIntrinsics();
            InitializeSkills();
            InitializeSkins();
            InitializeTactics();
            InitializeRoles();
            DevForm devForm = (DevForm)DiagManager.Instance.DevEditor;
            SpawnConditions = new CollectionBoxViewModel(devForm, new StringConv(typeof(MobSpawnCheck), new object[0]));
            SpawnConditions.OnMemberChanged += SpawnConditionsChanged;
            SpawnConditions.OnEditItem += SpawnConditionsEditItem;
            SpawnConditions.LoadFromList(TeamSpawn.Spawn.SpawnConditions);
            
            SpawnFeatures = new CollectionBoxViewModel(devForm, new StringConv(typeof(MobSpawnExtra), new object[0]));
            SpawnFeatures.OnMemberChanged += SpawnFeaturesChanged;
            SpawnFeatures.OnEditItem += SpawnFeaturesEditItem;
            SpawnFeatures.LoadFromList(TeamSpawn.Spawn.SpawnFeatures);
        }


        public List<int> PossibleSkillIndexes;
        
        private void SetFormPossibleSkills(string species, int formId)
        {

            foreach (int index in PossibleSkillIndexes)
            {
                skillData[index].SetMonsterLearns(false);
            }
            PossibleSkillIndexes.Clear();
            
            MonsterData entry = DataManager.Instance.GetMonster(species);
            MonsterFormData form = (MonsterFormData) entry.Forms[formId];
            List<string> possibleSkills = form.GetPossibleSkills();
            foreach (string skill in possibleSkills)
            {
                int index = skillKeys.BinarySearch(skill);
                skillData[index].SetMonsterLearns(true);
                PossibleSkillIndexes.Add(index);
            }
        }

        public List<int> PossibleIntrinsicIndexes;
        private void SetFormPossibleIntrinsics(string species, int formId)
        {
            foreach (int index in PossibleIntrinsicIndexes)
            {
                intrinsicData[index].SetMonsterLearns(false);
            }
            PossibleIntrinsicIndexes.Clear();
            
            MonsterData entry = DataManager.Instance.GetMonster(species);
            BaseMonsterForm form = entry.Forms[formId];

            List<string> possibleIntrinsics = new List<string>(
                    new string[] { form.Intrinsic1, form.Intrinsic2, form.Intrinsic3 })
                .Where(intrinsic => intrinsic != "none")
                .ToList();
            foreach (string intrinsic in possibleIntrinsics)
            {
                int index = intrinsicKeys.BinarySearch(intrinsic);
                intrinsicData[index].SetMonsterLearns(true);
                PossibleIntrinsicIndexes.Add(index);

            }
        }
        
        public void SpawnConditionsChanged()
        {
            TeamSpawn.Spawn.SpawnConditions = SpawnConditions.GetList<List<MobSpawnCheck>>();
        }
        
        public void SpawnFeaturesChanged()
        {
            TeamSpawn.Spawn.SpawnFeatures = SpawnFeatures.GetList<List<MobSpawnExtra>>();
            _checkSpawnFeatureDiff = false;
            
            // prevent an infinite loop caused by the change 
            Unrecruitable = containsType<MobSpawnExtra, MobSpawnUnrecruitable>(TeamSpawn.Spawn.SpawnFeatures);
            DisableUnusedSlots = containsType<MobSpawnExtra, MobSpawnMovesOff>(TeamSpawn.Spawn.SpawnFeatures);
            IsWeakMob = containsType<MobSpawnExtra, MobSpawnWeak>(TeamSpawn.Spawn.SpawnFeatures);
            _checkSpawnFeatureDiff = true;
        }


        public void SpawnConditionsEditItem(int index, object element, bool advancedEdit, CollectionBoxViewModel.EditElementOp op)
        {
            string elementName = "Spawn Conditions[" + index + "]";
            DataEditForm frmData = new DataEditRootForm();
            frmData.Title = DataEditor.GetWindowTitle("Spawn", elementName, element, typeof(MobSpawnCheck), new object[0]);

            DataEditor.LoadClassControls(frmData.ControlPanel, "Spawn", null, elementName, typeof(MobSpawnCheck), new object[0], element, true, new Type[0], advancedEdit);
            DataEditor.TrackTypeSize(frmData, typeof(MobSpawnCheck));
            
            frmData.SelectedOKEvent += async () =>
            {
                element = DataEditor.SaveClassControls(frmData.ControlPanel, elementName, typeof(MobSpawnCheck), new object[0], true, new Type[0], advancedEdit);
                op(index, element);
                return true;
            };
            
            frmData.Show();
        }
        
        public void SpawnFeaturesEditItem(int index, object element, bool advancedEdit, CollectionBoxViewModel.EditElementOp op)
        {
            string elementName = "Spawn Features[" + index + "]";
            DataEditForm frmData = new DataEditRootForm();
            frmData.Title = DataEditor.GetWindowTitle("Spawn", elementName, element, typeof(MobSpawnExtra), new object[0]);

            DataEditor.LoadClassControls(frmData.ControlPanel, "Spawn", null, elementName, typeof(MobSpawnExtra), new object[0], element, true, new Type[0], advancedEdit);
            DataEditor.TrackTypeSize(frmData, typeof(MobSpawnExtra));
            
            frmData.SelectedOKEvent += async () =>
            {
                element = DataEditor.SaveClassControls(frmData.ControlPanel, elementName, typeof(MobSpawnExtra), new object[0], true, new Type[0], advancedEdit);
                op(index, element);
                return true;
            };
            
            frmData.Show();
        }
        public TeamMemberSpawnModel()
        {
            TeamSpawn = new TeamMemberSpawn();
            TeamSpawn.Spawn = new MobSpawn();
            TeamSpawn.Spawn.Level.Min = 1;
            TeamSpawn.Spawn.Level.Max = 1;
            Initialize();
            ChosenTactic = tacticKeys.BinarySearch("wander_normal");
            MonsterData entry = DataManager.Instance.GetMonster("missingno");
            BaseMonsterForm form = entry.Forms[0];
            SearchMonsterFilter = form.FormName.ToLocal();
            SelectedMonsterIndex = findMonsterForm("missingno", 0);
        }
        public TeamMemberSpawnModel(TeamMemberSpawn spawn)
        {

            string species = spawn.Spawn.BaseForm.Species == "" ? "missingno" : spawn.Spawn.BaseForm.Species;
            MonsterData entry = DataManager.Instance.GetMonster(species);
            BaseMonsterForm form = entry.Forms[spawn.Spawn.BaseForm.Form];
            TeamSpawn = spawn;
            
            Initialize();
            
            _unrecruitable = containsType<MobSpawnExtra, MobSpawnUnrecruitable>(SpawnFeatures.GetList<List<MobSpawnExtra>>());
            _disableUnusedSlots = containsType<MobSpawnExtra, MobSpawnMovesOff>(SpawnFeatures.GetList<List<MobSpawnExtra>>());
            _isWeakMonster = containsType<MobSpawnExtra, MobSpawnWeak>(SpawnFeatures.GetList<List<MobSpawnExtra>>());
            
            _selectedMonsterIndex = findMonsterForm(TeamSpawn.Spawn.BaseForm.Species, TeamSpawn.Spawn.BaseForm.Form);
            SelectedMonsterForm = monsterForms[_selectedMonsterIndex];

            SearchMonsterFilter = form.FormName.ToLocal();
            
            if (TeamSpawn.Spawn.Intrinsic != "")
            {
                SelectedIntrinsicIndex = intrinsicKeys.BinarySearch(spawn.Spawn.Intrinsic);
                SearchIntrinsicFilter = DataManager.Instance.GetIntrinsic(spawn.Spawn.Intrinsic).Name.ToLocal();
            }
            
            SetFormPossibleSkills(TeamSpawn.Spawn.BaseForm.Species, TeamSpawn.Spawn.BaseForm.Form);
            SetFormPossibleIntrinsics(TeamSpawn.Spawn.BaseForm.Species, TeamSpawn.Spawn.BaseForm.Form);
            updateIntrinsicData(SearchIntrinsicFilter);
            
            for (int ii = 0; ii < CharData.MAX_SKILL_SLOTS; ii++)
            {
                string filter = "";
                if (ii < spawn.Spawn.SpecifiedSkills.Count)
                {
                    string skillKey = spawn.Spawn.SpecifiedSkills[ii];
                    filter = DataManager.Instance.GetSkill(skillKey).Name.ToLocal();
                    int index = skillKeys.BinarySearch(skillKey);
                    SkillIndex[ii] = index;
                }
                
                if (ii == 0)
                {
                    _searchSkill0Filter = filter;
                }
                else if (ii == 1)
                {
                    _searchSkill1Filter = filter;
                } 
                else if (ii == 2)
                {
                    _searchSkill2Filter = filter;
                    
                }
                else if (ii == 3)
                {
                    _searchSkill3Filter = filter;
                }
            }
        }

        public bool ShouldAddToFilter(string s, string filter)
        {
            return prefixStartsWith(s, filter) || startsWith(s, filter);
        }

        private bool containsType<T, V>(List<T> collection)
        {
            bool result = false;

            foreach (T item in collection)
            {
                if (item is V)
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        private bool _finishedAdding;

        public bool FinishedAdding
        {
            get => _finishedAdding;
        }
        private void addFilteredItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
        {
            if (items.Count() > 0)
            {
                _finishedAdding = false;
                foreach (T item in items.Take(items.Count() - 1))
                {
                    collection.Add(item);
                }

                _finishedAdding = true;
                collection.Add(items.Last());
            }
        }
        
        private List<string> getSkillList()
        {
            return SkillIndex.Where(i => i != -1).Select(i => skillKeys[i]).ToList();
        }
        
        private int findMonsterForm(string species, int form)
        {
            
            int result = -1;
            for (int ii = 0; ii < monsterForms.Count(); ii++)
            {
                BaseMonsterFormViewModel vm = monsterForms[ii];
                if (species == vm.Key && form == vm.FormId)
                {
                    result = ii;
                    break;
                }
            }

            return result;
        }

        public bool SanitizeStringEquals(string s1, string s2)
        {
            return sanitize(s1) == sanitize(s2);
        }

        private string sanitize(string s)
        {
            s = s.ToLower();
            s = Regex.Replace(s, @"\s+|_", "");
            return s;
        }

        private bool startsWith(string s, string filter)
        {
            return sanitize(s).StartsWith(sanitize(filter));
        }

        private bool prefixStartsWith(string s, string filter)
        {
            return s.Split(' ', '-').Any(prefix =>
                sanitize(prefix).StartsWith(sanitize(filter), StringComparison.OrdinalIgnoreCase)
            );
        }

        public void UpdateDataGrid()
        {
            DataGridType nextView = CurrentDataGridView;
            if (FocusIndex >= 6)
            {
                // nextView = DataGridType.Other;
            }
            else if (FocusIndex == 5)
            {
                nextView = DataGridType.Intrinsic;
            }
            else if (FocusIndex >= 1)
            {
                nextView = DataGridType.Skills;
            }
            else if (FocusIndex == 0)
            {
                nextView = DataGridType.Monster;
            }

            CurrentDataGridView = nextView;
        }

    }
    
    public enum DataGridType
    {
        Monster = 0,
        Skills = 1,
        Intrinsic = 2,
        Other = 3,
    } 
    
}