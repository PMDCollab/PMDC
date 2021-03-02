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
    public class CharSheetCollapseOffsetsOp : CharSheetOp
    {
        public override string Name { get { return "Collapse Offsets"; } }
        public override void Apply(CharSheet sheet)
        {
            sheet.CollapseOffsets();
        }
    }

}
