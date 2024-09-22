using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PMDC.Dev.ViewModels;
using RogueEssence.Dev;

namespace PMDC.Dev.Views
{
    public class TeamMemberSpawnView : UserControl
    {
               
        private List<string> _focusOrder = new List<string> { 
            "SpeciesTextBox", "SkillTextBox0", "SkillTextBox1", 
            "SkillTextBox2", "SkillTextBox3", "IntrinsicTextBox",
            "MinTextBox", "MaxTextBox"
        };
        
    
        public TeamMemberSpawnView()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

        }
        
        protected override void OnInitialized()
        {
            base.OnInitialized();
            if (DataContext is TeamMemberSpawnModel vm)
            {
                vm.FilteredSkillData.CollectionChanged += FilteredSkillDataOnCollectionChanged;
                vm.FilteredMonsterForms.CollectionChanged += MonsterDataOnCollectionChanged;
                vm.FilteredIntrinsicData.CollectionChanged += IntrinsicDataOnCollectionChanged;
            }
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            if (DataContext is TeamMemberSpawnModel vm)
            {
                vm.FilteredSkillData.CollectionChanged -= FilteredSkillDataOnCollectionChanged;
                vm.FilteredMonsterForms.CollectionChanged -= MonsterDataOnCollectionChanged;
                vm.FilteredIntrinsicData.CollectionChanged -= IntrinsicDataOnCollectionChanged;
            }
        }
        
        private void MonsterDataOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            if (vm.FinishedAdding)
            {
                UpdateMonsterHighlight();
            }
        }
        
        private void FilteredSkillDataOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            if (vm.FinishedAdding)
            {
                UpdateSkillHighlights();
            }
        }
        
        private void IntrinsicDataOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            if (vm.FinishedAdding)
            {
                UpdateIntrinsicHighlights();
            }
        }
        
        private TemplatedControl FocusNextTextBox()
        {
            TeamMemberSpawnModel vm = (TeamMemberSpawnModel) DataContext;
            vm.SetFocusIndex(Math.Min(vm.FocusIndex + 1, _focusOrder.Count - 1));
            string nextTextbox = _focusOrder[vm.FocusIndex];

            TemplatedControl nextFocus;
            
            if (vm.FocusIndex < 6) {
                nextFocus = this.FindControl<TextBox>(nextTextbox);
            }
            else
            {
                nextFocus = this.FindControl<NumericUpDown>(nextTextbox);
            }
            
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                nextFocus.Focus();
            });
            return nextFocus;
        }
        
        private void OnEnter()
        {
            TemplatedControl nextFocus = FocusNextTextBox();

            if (nextFocus is TextBox nf)
            {

                nf.CaretIndex = nf.Text.Length;
                nf.SelectionStart = 0;
                nf.SelectionEnd = nf.Text.Length;
            }
        }
        
        // TODO: For these OnEnterCommands, allows the user to choose the row with arrow keys
        private void SpeciesTextBox_OnEnterCommand()
        {
            TeamMemberSpawnModel vm = (TeamMemberSpawnModel) DataContext;
 
            if (vm.FilteredMonsterForms.Count > 0)
            {
                if (vm.SearchMonsterFilter != "")
                {
                    vm.SetMonster(vm.FilteredMonsterForms.First().Index);
                }
            }
            
            OnEnter();
        }
        
        private void SkillTextBox_OnEnterCommand()
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            if (vm.FilteredSkillData.Count > 0)
            {
                if (vm.CurrentSkillSearchFilter != "")
                {
                    vm.SetSkill(vm.FilteredSkillData.First());
                }
                else
                {
                    vm.ResetSkill();
                }
            }
            OnEnter();
        }
        
        private void IntrinsicTextBox_OnEnterCommand()
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            if (vm.FilteredIntrinsicData.Count > 0)
            {
                if (vm.SearchIntrinsicFilter != "")
                {
                    vm.SetIntrinsic(vm.FilteredIntrinsicData.First());
                }
                else
                {
                    vm.ResetSkill();
                }
            }
            OnEnter();
        }
        
        private void SpeciesTextBox_OnGotFocus(object sender, GotFocusEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            vm.SetFocusIndex(0);
        }
        
        private void SkillTextBox0_OnGotFocus(object sender, GotFocusEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            vm.SetFocusIndex(1);
            vm.FocusedSkillIndex = 0;
            vm.UpdateSkillData(vm.SearchSkill0Filter);
            UpdateSkillHighlights();
        }
        
        private void SkillTextBox1_OnGotFocus(object sender, GotFocusEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            vm.SetFocusIndex(2);
            vm.FocusedSkillIndex = 1;
            vm.UpdateSkillData(vm.SearchSkill1Filter);
            UpdateSkillHighlights();
        }

        private void SkillTextBox2_OnGotFocus(object sender, GotFocusEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            vm.SetFocusIndex(3);
            vm.FocusedSkillIndex = 2;
            vm.UpdateSkillData(vm.SearchSkill2Filter);
            UpdateSkillHighlights();;
        }

        private void SkillTextBox3_OnGotFocus(object sender, GotFocusEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            vm.SetFocusIndex(4);
            vm.FocusedSkillIndex = 3;
            vm.UpdateSkillData(vm.SearchSkill3Filter);
            UpdateSkillHighlights();
        }

        private void IntrinsicTextBox_OnGotFocus(object sender, GotFocusEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            vm.SetFocusIndex(5);
        }
        
        // TODO: Currently NumericUpDown doesn't get focused correctly
        private void MinTextBox_OnGotFocus(object sender, GotFocusEventArgs e)
        {
            // TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            // vm.SetFocusIndex(6);
        }
        
        private void MaxTextBox_OnGotFocus(object sender, GotFocusEventArgs e)
        {
            // TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            // vm.SetFocusIndex(7);
        }
        private void SpeciesTextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            int currFocusIndex = vm.FocusIndex;
            Dispatcher.UIThread.Post(() =>
            {
                if (vm.FilteredMonsterForms.Count > 0 && vm.FocusIndex != currFocusIndex)
                {
                    // if (vm.SearchMonsterFilter == "")
                    // {
                    //     vm.ResetMonster();
                    // }
                    if (vm.SanitizeStringEquals(vm.SearchMonsterFilter, vm.FilteredMonsterForms.First().Name))
                    {
                        vm.SetMonster(vm.FilteredMonsterForms.First().Index);
                    } 
                }
            });
        }
        
        private void SkillTextBox0_OnLostFocus(object sender, RoutedEventArgs e)
        {
            SkillTextBox_OnLostFocus(0);
        }

        private void SkillTextBox1_OnLostFocus(object sender, RoutedEventArgs e)
        {
            SkillTextBox_OnLostFocus(1);
        }
        
        private void SkillTextBox2_OnLostFocus(object sender, RoutedEventArgs e)
        {
            SkillTextBox_OnLostFocus(2);
        }
        
        private void SkillTextBox3_OnLostFocus(object sender, RoutedEventArgs e)
        {
            SkillTextBox_OnLostFocus(3);
        }
        private void SkillTextBox_OnLostFocus(int index)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            SkillDataViewModel skillData = null;
            
            int currFocusIndex = vm.FocusIndex;
            if (vm.FilteredSkillData.Count > 0)
            {
                skillData = vm.FilteredSkillData.First();
            }
            
            List<string> skillFilters = new List<string>{ vm.SearchSkill0Filter, vm.SearchSkill1Filter, vm.SearchSkill2Filter, vm.SearchSkill3Filter };
            string filter = skillFilters[index];
            Dispatcher.UIThread.Post(() =>
            {
                if (skillData != null && vm.FocusIndex != currFocusIndex)
                {
                    if (filter == "")
                    {
                        vm.ResetSkill(index);
                    }
        
                    else if (vm.SanitizeStringEquals(filter, skillData.Name))
                    {
                        vm.SetSkill(skillData, index);
                    } 
                }
            });
        }
        
        private void IntrinsicTextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            
            int currFocusIndex = vm.FocusIndex;
            
            Dispatcher.UIThread.Post(() =>
            {
                if (vm.FilteredIntrinsicData.Count > 0 && vm.FocusIndex != currFocusIndex)
                {
                    if (vm.SearchIntrinsicFilter == "")
                    {
                        vm.ResetIntrinsic();
                    }
                    else if (vm.SanitizeStringEquals(vm.SearchIntrinsicFilter, vm.FilteredIntrinsicData.First().Name))
                    {
                        vm.SetIntrinsic(vm.FilteredIntrinsicData.First());
                    } 
                }
            });
        }
        
        private void MonsterDataGrid_OnCellPointerPressed(object sender, DataGridCellPointerPressedEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            BaseMonsterFormViewModel dataContext = (BaseMonsterFormViewModel)e.Cell.DataContext;
            vm.SetMonster(dataContext.Index);
            FocusNextTextBox();
        }
        
        private void SkillsDataGrid_OnCellPointerPressed(object sender, DataGridCellPointerPressedEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            SkillDataViewModel skillData = (SkillDataViewModel)e.Cell.DataContext;
            vm.SetSkill(skillData);
            FocusNextTextBox();
        }
        private void IntrinsicDataGrid_OnCellPointerPressed(object sender, DataGridCellPointerPressedEventArgs e)
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            IntrinsicViewModel intrinsicData = (IntrinsicViewModel)e.Cell.DataContext;
            vm.SetIntrinsic(intrinsicData);
            FocusNextTextBox();
        }
        
        private void UpdateMonsterHighlight()
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            int index = vm.SelectedMonsterIndex;
            if (vm.FilteredMonsterForms.Count > 0 && vm.SearchMonsterFilter != null)
            {
                BaseMonsterFormViewModel monsterData = vm.MonsterAtIndex(index);
                if (vm.ShouldAddToFilter(monsterData.Name, vm.SearchMonsterFilter) &&
                    (vm.IncludeUnreleasedForms || monsterData.Released))
                {
                    vm.SelectedMonsterForm = monsterData;
                }
            }
        }

        private void UpdateSkillHighlights()
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            DataGrid datagrid = this.FindControl<DataGrid>("SkillsDataGrid");
            datagrid.SelectedItems.Clear();
            foreach (int index in vm.SkillIndex)
            {
                if (index != -1 && vm.FilteredSkillData.Count > 0)
                {
                    SkillDataViewModel skillData = vm.SkillDataAtIndex(index);
                    if (vm.ShouldAddToFilter(skillData.Name, vm.CurrentSkillSearchFilter) && (vm.IncludeUnreleasedSkills || skillData.Released))
                    {
                        datagrid.SelectedItems.Add(skillData);
                    }
                }
            }
        
        }
        
        private void UpdateIntrinsicHighlights()
        {
            TeamMemberSpawnModel vm = DataContext as TeamMemberSpawnModel;
            int index = vm.SelectedIntrinsicIndex;
            if (vm.SelectedIntrinsicIndex != -1 && vm.FilteredIntrinsicData.Count > 0 && vm.SearchIntrinsicFilter != null)
            {
                IntrinsicViewModel intrinsicData = vm.IntrinsicAtIndex(index);
                if (vm.ShouldAddToFilter(intrinsicData.Name, vm.SearchIntrinsicFilter) && (vm.IncludeUnreleasedIntrinsics || intrinsicData.Released))
                {
                    vm.SelectedIntrinsic = intrinsicData;
                }
            }
        }

        private void MinTextBox_OnValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            NumericUpDown nudMin = (NumericUpDown)sender;
            NumericUpDown nudMax = this.FindControl<NumericUpDown>("MaxTextBox");
            
            if (nudMin.Value > nudMax.Value)
                nudMax.Value = nudMin.Value;
        }
        
        private void MaxTextBox_OnValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            NumericUpDown nudMin = this.FindControl<NumericUpDown>("MinTextBox");
            NumericUpDown nudMax = (NumericUpDown)sender;
      
            
            if (nudMin.Value > nudMax.Value)
                nudMin.Value = nudMax.Value;
        }
    }
}