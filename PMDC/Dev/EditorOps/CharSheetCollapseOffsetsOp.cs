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
    public class CharSheetCollapseOffsetsOp : CharSheetOp
    {
        public override int[] Anims { get { return new int[0]; } }
        public override string Name { get { return "Collapse Offsets"; } }
        public override void Apply(CharSheet sheet, int anim)
        {
            sheet.CollapseOffsets();
        }
    }

}
