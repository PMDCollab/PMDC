using System;
using RogueElements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using RogueEssence.Content;
using RogueEssence.Dev;

namespace PMDO.Dev
{
    [Serializable]
    public class CharSheetRotateOp : CharSheetOp
    {
        public override string Name { get { return "Create Rotate Anim"; } }
        public override void Apply(CharSheet sheet)
        {
            CharAnimGroup anim = new CharAnimGroup();
            anim.InternalIndex = 12;
            anim.HitFrame = 8;
            for(int ii = 0; ii < sheet.AnimData[GraphicsManager.IdleAction].Sequences.Count; ii++)
            {
                CharAnimSequence newSequence = new CharAnimSequence();
                int curEndTime = 0;
                for (int jj = 0; jj < DirExt.DIR8_COUNT + 1; jj++)
                {
                    curEndTime += 2;
                    CharAnimSequence sequence = sheet.AnimData[GraphicsManager.IdleAction].Sequences[(ii + jj) % DirExt.DIR8_COUNT];
                    CharAnimFrame frame = new CharAnimFrame(sequence.Frames[0]);
                    frame.EndTime = curEndTime;
                    newSequence.Frames.Add(frame);
                }
                anim.Sequences.Add(newSequence);
            }
            sheet.AnimData[42] = anim;
        }
    }

}
