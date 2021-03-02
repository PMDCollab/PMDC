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
    public class CharSheetSwingOp : CharSheetOp
    {
        private static Loc[,] offsets = new Loc[8, 9] {
            { new Loc(0, 0), new Loc(6, 3), new Loc(8, 9), new Loc(7, 18), new Loc(0, 22), new Loc(-7, 18), new Loc(-8, 9), new Loc(-6, 3), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-11, 0), new Loc(-21, 3), new Loc(-26, 10), new Loc(-20, 18), new Loc(-11, 19), new Loc(-3, 15), new Loc(0, 7), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-5, -5), new Loc(-13, -6), new Loc(-20, -5), new Loc(-23, 0), new Loc(-20, 4), new Loc(-14, 7), new Loc(-6, 5), new Loc(-2, 0)},
            { new Loc(0, 0), new Loc(1, -6), new Loc(-4, -17), new Loc(-15, -22), new Loc(-21, -21), new Loc(-24, -13), new Loc(-19, -4), new Loc(-9, 0), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-8, -4), new Loc(-9, -12), new Loc(-7, -20), new Loc(0, -22), new Loc(7, -20), new Loc(9, -10), new Loc(8, -4), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-1, -6), new Loc(4, -17), new Loc(15, -22), new Loc(21, -21), new Loc(24, -13), new Loc(19, -4), new Loc(9, 0), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(5, -5), new Loc(13, -6), new Loc(20, -5), new Loc(23, 0), new Loc(20, 4), new Loc(14, 7), new Loc(6, 5), new Loc(2, 0)},
            { new Loc(0, 0), new Loc(11, 0), new Loc(21, 3), new Loc(26, 10), new Loc(20, 18), new Loc(11, 19), new Loc(3, 15), new Loc(0, 7), new Loc(0, 0)}
        };

        private static Loc[,] shadows = new Loc[8, 9] {
            { new Loc(0, 0), new Loc(6, 3), new Loc(8, 9), new Loc(7, 18), new Loc(0, 22), new Loc(-7, 18), new Loc(-8, 9), new Loc(-6, 3), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-11, 0), new Loc(-21, 3), new Loc(-26, 10), new Loc(-20, 18), new Loc(-11, 19), new Loc(-3, 15), new Loc(0, 7), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-5, -5), new Loc(-13, -6), new Loc(-20, -5), new Loc(-23, 0), new Loc(-20, 4), new Loc(-14, 7), new Loc(-6, 5), new Loc(-2, 0)},
            { new Loc(0, 0), new Loc(1, -6), new Loc(-4, -17), new Loc(-15, -22), new Loc(-21, -21), new Loc(-24, -13), new Loc(-19, -4), new Loc(-9, 0), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-8, -4), new Loc(-9, -12), new Loc(-7, -20), new Loc(0, -22), new Loc(7, -20), new Loc(9, -10), new Loc(8, -4), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(-1, -6), new Loc(4, -17), new Loc(15, -22), new Loc(21, -21), new Loc(24, -13), new Loc(19, -4), new Loc(9, 0), new Loc(0, 0)},
            { new Loc(0, 0), new Loc(5, -5), new Loc(13, -6), new Loc(20, -5), new Loc(23, 0), new Loc(20, 4), new Loc(14, 7), new Loc(6, 5), new Loc(2, 0)},
            { new Loc(0, 0), new Loc(11, 0), new Loc(21, 3), new Loc(26, 10), new Loc(20, 18), new Loc(11, 19), new Loc(3, 15), new Loc(0, 7), new Loc(0, 0)}
        };

        private static int[] durations = new int[] { 2, 1, 2, 2, 3, 2, 2, 1, 1 };

        public override string Name { get { return "Create Swing Anim"; } }
        public override void Apply(CharSheet sheet)
        {
            CharAnimGroup anim = new CharAnimGroup();
            anim.InternalIndex = 8;
            anim.HitFrame = 5;
            anim.ReturnFrame = 5;
            for (int ii = 0; ii < sheet.AnimData[GraphicsManager.IdleAction].Sequences.Count; ii++)
            {
                CharAnimSequence newSequence = new CharAnimSequence();
                int curEndTime = 0;
                for (int jj = 0; jj < DirExt.DIR8_COUNT + 1; jj++)
                {
                    curEndTime += durations[jj];
                    CharAnimSequence sequence = sheet.AnimData[GraphicsManager.IdleAction].Sequences[(ii + jj) % DirExt.DIR8_COUNT];
                    CharAnimFrame frame = new CharAnimFrame(sequence.Frames[0]);
                    frame.Offset = frame.Offset + offsets[ii, jj];
                    frame.ShadowOffset = frame.ShadowOffset + shadows[ii, jj];
                    frame.EndTime = curEndTime;
                    newSequence.Frames.Add(frame);
                }
                anim.Sequences.Add(newSequence);
            }
            sheet.AnimData[40] = anim;
        }
    }

}
