using System;
using RogueElements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using RogueEssence.Content;
using RogueEssence.Dev;

namespace PMDC.Dev
{
    [Serializable]
    public class CharSheetMirrorOp : CharSheetOp
    {
        public bool StartRight;
        public override int[] Anims
        {
            get
            {
                List<int> all = new List<int>();
                for (int ii = 0; ii < GraphicsManager.Actions.Count; ii++)
                    all.Add(ii);
                return all.ToArray();
            }
        }

        public override string Name { get { return StartRight ? "Mirror Right->Left" : "Mirror Left->Right"; } }
        public override void Apply(CharSheet sheet, int anim)
        {
            for (int ii = 0; ii < sheet.AnimData[anim].Sequences.Count; ii++)
            {
                DirH dirH;
                DirV dirV;
                DirExt.Separate((Dir8)ii, out dirH, out dirV);
                if (dirH == DirH.Left && !StartRight || dirH == DirH.Right && StartRight)
                {
                    dirH = dirH.Reverse();
                    Dir8 flipDir = DirExt.Combine(dirH, dirV);
                    List<CharAnimFrame> frames = new List<CharAnimFrame>();
                    for (int jj = 0; jj < sheet.AnimData[anim].Sequences[ii].Frames.Count; jj++)
                    {
                        CharAnimFrame origFrame = sheet.AnimData[anim].Sequences[ii].Frames[jj];
                        CharAnimFrame newFrame = new CharAnimFrame(origFrame);
                        newFrame.Flip = !newFrame.Flip;
                        newFrame.Offset = new Loc(-newFrame.Offset.X, newFrame.Offset.Y);
                        newFrame.ShadowOffset = new Loc(-newFrame.ShadowOffset.X, newFrame.ShadowOffset.Y);
                        frames.Add(newFrame);
                    }
                    sheet.AnimData[anim].Sequences[(int)flipDir].Frames = frames;
                }
            }
            sheet.RemoveUnused();
        }
    }

}
