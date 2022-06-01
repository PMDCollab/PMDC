using System;
using System.Collections.Generic;
using System.Text;
using RogueEssence.Content;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using System.Drawing;
using RogueElements;
using Avalonia.Controls;
using RogueEssence.Dev.Views;
using System.Collections;
using Avalonia;
using System.Reactive.Subjects;

namespace RogueEssence.Dev
{
    public class MapTilesEditor : Editor<ITile[][]>
    {
        public override bool DefaultSubgroup => true;
        public override bool DefaultDecoration => false;

        public override void LoadWindowControls(StackPanel control, string parent, Type parentType, string name, Type type, object[] attributes, ITile[][] member, Type[] subGroupStack)
        {
            //for strings, use an edit textbox
            TextBox txtValue = new TextBox();
            txtValue.Height = 100;
            txtValue.AcceptsReturn = true;
            StringBuilder str = new StringBuilder();
            Tile floor = new Tile(0);
            Tile impassable = new Tile(1);
            Tile wall = new Tile(2);
            Tile water = new Tile(3);
            Tile lava = new Tile(4);
            Tile pit = new Tile(5);
            if (member != null && member.Length > 0)
            {
                for (int yy = 0; yy < member[0].Length; yy++)
                {
                    for (int xx = 0; xx < member.Length; xx++)
                    {
                        ITile tile = member[xx][yy];
                        if (tile.TileEquivalent(floor))
                            str.Append('.');
                        else if (tile.TileEquivalent(impassable))
                            str.Append('X');
                        else if (tile.TileEquivalent(wall))
                            str.Append('#');
                        else if (tile.TileEquivalent(water))
                            str.Append('~');
                        else if (tile.TileEquivalent(lava))
                            str.Append('^');
                        else if (tile.TileEquivalent(pit))
                            str.Append('_');
                        else
                            str.Append('?');
                    }
                    if (yy < member[0].Length - 1)
                        str.Append('\n');
                }
            }

            txtValue.Text = str.ToString();
            txtValue.FontFamily = new Avalonia.Media.FontFamily("Courier New");
            control.Children.Add(txtValue);
        }


        public override ITile[][] SaveWindowControls(StackPanel control, string name, Type type, object[] attributes, Type[] subGroupStack)
        {
            int controlIndex = 0;

            TextBox txtValue = (TextBox)control.Children[controlIndex];
            string[] level = txtValue.Text.Split('\n');

            Tile[][] tiles = new Tile[level[0].Length][];
            for (int xx = 0; xx < level[0].Length; xx++)
            {
                tiles[xx] = new Tile[level.Length];
                for (int yy = 0; yy < level.Length; yy++)
                {
                    if (level[yy][xx] == 'X')
                        tiles[xx][yy] = new Tile(1);
                    else if (level[yy][xx] == '#')
                        tiles[xx][yy] = new Tile(2);
                    else if (level[yy][xx] == '~')
                        tiles[xx][yy] = new Tile(3);
                    else if (level[yy][xx] == '^')
                        tiles[xx][yy] = new Tile(4);
                    else if (level[yy][xx] == '_')
                        tiles[xx][yy] = new Tile(5);
                    else
                        tiles[xx][yy] = new Tile(0);
                }
            }

            return tiles;
        }
    }
}
