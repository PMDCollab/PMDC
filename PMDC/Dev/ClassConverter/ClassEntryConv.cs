using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Dev;
using RogueEssence.Dev.ViewModels;
using RogueEssence.Dev.Views;
using RogueEssence.Dungeon;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;

namespace PMDC.Dev
{
    public class MapItemConv : ClassConverter<MapItem>
    {
        public override string GetClassString(MapItem obj)
        {
            if (obj.IsMoney)
                return String.Format("{0}P", obj.Value);
            else
            {
                ItemData entry = DataManager.Instance.GetItem(obj.Value);
                if (entry.MaxStack > 1)
                    return (obj.Cursed ? "[X]" : "") + entry.Name.ToLocal() + " (" + obj.HiddenValue + ")";
                else
                    return (obj.Cursed ? "[X]" : "") + entry.Name.ToLocal();
            }
        }
    }
}
