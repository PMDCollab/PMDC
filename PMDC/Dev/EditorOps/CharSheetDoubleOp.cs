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
    public class CharSheetDoubleOp : CharSheetOp
    {
        private static Loc[,] offsets = new Loc[8, 16] {
            { new Loc(0, 0), new Loc(6, 0), new Loc(-6, 0), new Loc(10, 0), new Loc(-10, 0), new Loc(12, 0), new Loc(-12, 0), new Loc(13, 0), new Loc(-13, 0), new Loc(12, 0), new Loc(-12, 0), new Loc(10, 0), new Loc(-10, 0), new Loc(6, 0), new Loc(-6, 0), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-6, -6), new Loc(6, 6), new Loc(-10, -10), new Loc(10, 10), new Loc(-12, -12), new Loc(12, 12), new Loc(-13, -13), new Loc(13, 13), new Loc(-12, -12), new Loc(12, 12), new Loc(-10, -10), new Loc(10, 10), new Loc(-6, -6), new Loc(6, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(0, -6), new Loc(0, 6), new Loc(0, -10), new Loc(0, 10), new Loc(0, -12), new Loc(0, 12), new Loc(0, -13), new Loc(0, 13), new Loc(0, -12), new Loc(0, 12), new Loc(0, -10), new Loc(0, 10), new Loc(0, -6), new Loc(0, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(6, -6), new Loc(-6, 6), new Loc(10, -10), new Loc(-10, 10), new Loc(12, -12), new Loc(-12, 12), new Loc(13, -13), new Loc(-13, 13), new Loc(12, -12), new Loc(-12, 12), new Loc(10, -10), new Loc(-10, 10), new Loc(6, -6), new Loc(-6, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(6, 0), new Loc(-6, 0), new Loc(10, 0), new Loc(-10, 0), new Loc(12, 0), new Loc(-12, 0), new Loc(13, 0), new Loc(-13, 0), new Loc(12, 0), new Loc(-12, 0), new Loc(10, 0), new Loc(-10, 0), new Loc(6, 0), new Loc(-6, 0), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-6, -6), new Loc(6, 6), new Loc(-10, -10), new Loc(10, 10), new Loc(-12, -12), new Loc(12, 12), new Loc(-13, -13), new Loc(13, 13), new Loc(-12, -12), new Loc(12, 12), new Loc(-10, -10), new Loc(10, 10), new Loc(-6, -6), new Loc(6, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(0, -6), new Loc(0, 6), new Loc(0, -10), new Loc(0, 10), new Loc(0, -12), new Loc(0, 12), new Loc(0, -13), new Loc(0, 13), new Loc(0, -12), new Loc(0, 12), new Loc(0, -10), new Loc(0, 10), new Loc(0, -6), new Loc(0, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(6, -6), new Loc(-6, 6), new Loc(10, -10), new Loc(-10, 10), new Loc(12, -12), new Loc(-12, 12), new Loc(13, -13), new Loc(-13, 13), new Loc(12, -12), new Loc(-12, 12), new Loc(10, -10), new Loc(-10, 10), new Loc(6, -6), new Loc(-6, 6), new Loc(0, 0)},
        };

        private static Loc[,] shadows = new Loc[8, 16] {
            { new Loc(0, 0), new Loc(6, 0), new Loc(-6, 0), new Loc(10, 0), new Loc(-10, 0), new Loc(12, 0), new Loc(-12, 0), new Loc(13, 0), new Loc(-13, 0), new Loc(12, 0), new Loc(-12, 0), new Loc(10, 0), new Loc(-10, 0), new Loc(6, 0), new Loc(-6, 0), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-6, -6), new Loc(6, 6), new Loc(-10, -10), new Loc(10, 10), new Loc(-12, -12), new Loc(12, 12), new Loc(-13, -13), new Loc(13, 13), new Loc(-12, -12), new Loc(12, 12), new Loc(-10, -10), new Loc(10, 10), new Loc(-6, -6), new Loc(6, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(0, -6), new Loc(0, 6), new Loc(0, -10), new Loc(0, 10), new Loc(0, -12), new Loc(0, 12), new Loc(0, -13), new Loc(0, 13), new Loc(0, -12), new Loc(0, 12), new Loc(0, -10), new Loc(0, 10), new Loc(0, -6), new Loc(0, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(6, -6), new Loc(-6, 6), new Loc(10, -10), new Loc(-10, 10), new Loc(12, -12), new Loc(-12, 12), new Loc(13, -13), new Loc(-13, 13), new Loc(12, -12), new Loc(-12, 12), new Loc(10, -10), new Loc(-10, 10), new Loc(6, -6), new Loc(-6, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(6, 0), new Loc(-6, 0), new Loc(10, 0), new Loc(-10, 0), new Loc(12, 0), new Loc(-12, 0), new Loc(13, 0), new Loc(-13, 0), new Loc(12, 0), new Loc(-12, 0), new Loc(10, 0), new Loc(-10, 0), new Loc(6, 0), new Loc(-6, 0), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-6, -6), new Loc(6, 6), new Loc(-10, -10), new Loc(10, 10), new Loc(-12, -12), new Loc(12, 12), new Loc(-13, -13), new Loc(13, 13), new Loc(-12, -12), new Loc(12, 12), new Loc(-10, -10), new Loc(10, 10), new Loc(-6, -6), new Loc(6, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(0, -6), new Loc(0, 6), new Loc(0, -10), new Loc(0, 10), new Loc(0, -12), new Loc(0, 12), new Loc(0, -13), new Loc(0, 13), new Loc(0, -12), new Loc(0, 12), new Loc(0, -10), new Loc(0, 10), new Loc(0, -6), new Loc(0, 6), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(6, -6), new Loc(-6, 6), new Loc(10, -10), new Loc(-10, 10), new Loc(12, -12), new Loc(-12, 12), new Loc(13, -13), new Loc(-13, 13), new Loc(12, -12), new Loc(-12, 12), new Loc(10, -10), new Loc(-10, 10), new Loc(6, -6), new Loc(-6, 6), new Loc(0, 0)},
        };

        private static int[] durations = new int[] { 2, 2, 2, 2, 2, 2, 3, 3, 3, 2, 3, 2, 2, 2, 2, 2 };

        public override string Name { get { return "Create Double Anim"; } }
        public override void Apply(CharSheet sheet)
        {
            CharAnimGroup anim = new CharAnimGroup();
            anim.InternalIndex = 9;
            for (int ii = 0; ii < sheet.AnimData[GraphicsManager.IdleAction].Sequences.Count; ii++)
            {
                CharAnimSequence sequence = sheet.AnimData[GraphicsManager.IdleAction].Sequences[ii];
                CharAnimSequence newSequence = new CharAnimSequence();
                int curEndTime = 0;
                for (int jj = 0; jj < durations.Length; jj++)
                {
                    curEndTime += durations[jj];
                    CharAnimFrame frame = new CharAnimFrame(sequence.Frames[0]);
                    frame.Offset = frame.Offset + offsets[ii, jj];
                    frame.ShadowOffset = frame.ShadowOffset + shadows[ii, jj];
                    frame.EndTime = curEndTime;
                    newSequence.Frames.Add(frame);
                }
                anim.Sequences.Add(newSequence);
            }
            sheet.AnimData[41] = anim;
        }
    }

}
