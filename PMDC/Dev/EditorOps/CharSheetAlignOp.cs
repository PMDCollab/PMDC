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
    public class CharSheetAlignOp : CharSheetOp
    {
        public override int[] Anims
        {
            get
            {
                List<int> all = new List<int>();
                for (int ii = 0; ii < GraphicsManager.Actions.Count; ii++)
                {
                    if (ii != 4 && ii != 6)
                        all.Add(ii);
                }
                return all.ToArray();
            }
        }

        private static Loc StandardCenter = new Loc(0, 4);

        public override string Name { get => "Standardize Alignment"; }
        public override void Apply(CharSheet sheet, int anim)
        {
            for (int ii = 0; ii < sheet.AnimData[anim].Sequences.Count; ii++)
            {
                CharAnimFrame firstFrame = sheet.AnimData[anim].Sequences[ii].Frames[0];
                Loc diff = StandardCenter - firstFrame.ShadowOffset;
                for (int jj = 0; jj < sheet.AnimData[anim].Sequences[ii].Frames.Count; jj++)
                {
                    CharAnimFrame origFrame = sheet.AnimData[anim].Sequences[ii].Frames[jj];
                    CharAnimFrame newFrame = new CharAnimFrame(origFrame);
                    newFrame.Offset = newFrame.Offset + diff;
                    newFrame.ShadowOffset = newFrame.ShadowOffset + diff;
                    sheet.AnimData[anim].Sequences[ii].Frames[jj] = newFrame;
                }
            }
            sheet.Collapse(false, true);
        }
    }

}
