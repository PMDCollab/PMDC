using System;
using System.Reflection;
using Avalonia.Controls;
using PMDC.Dev.ViewModels;
using PMDC.Dev.Views;
using RogueEssence.Dev;
using RogueEssence.LevelGen;

namespace PMDC.Dev
{
    public class TeamMemberSpawnSimpleEditor : Editor<TeamMemberSpawn>
    {
        public override bool SimpleEditor => true;
        public override string GetString(TeamMemberSpawn obj, Type type, object[] attributes)
        {
            MemberInfo[] spawnInfo = type.GetMember(nameof(obj.Spawn));
            return DataEditor.GetString(obj.Spawn, spawnInfo[0].GetMemberInfoType(), spawnInfo[0].GetCustomAttributes(false));
        }

        public override void LoadWindowControls(StackPanel control, string parent, Type parentType, string name, Type type, object[] attributes,
            TeamMemberSpawn obj, Type[] subGroupStack)
        {
            TeamMemberSpawnView view = new TeamMemberSpawnView();
            if (obj.Spawn != null)
            {
                view.DataContext = new TeamMemberSpawnModel(new TeamMemberSpawn(obj));
            }
            else
            { 
                view.DataContext = new TeamMemberSpawnModel();
            }
            
            control.Children.Add(view);
        }
        
        
        public override TeamMemberSpawn SaveWindowControls(StackPanel control, string name, Type type, object[] attributes, Type[] subGroupStack)
        {
            int controlIndex = 0;
            TeamMemberSpawnView view = (TeamMemberSpawnView)control.Children[controlIndex];
            TeamMemberSpawnModel mv = (TeamMemberSpawnModel)view.DataContext;
            return mv.TeamSpawn;
        }
        
    }
}