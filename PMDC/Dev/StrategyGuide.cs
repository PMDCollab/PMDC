using System;
using System.Collections.Generic;
using System.IO;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;
using RogueEssence;

namespace PMDC.Dev
{
    public static class StrategyGuide
    {


        private static void writeHTMLGuide(string name, List<string[]> stats)
        {
            string table =
                "		<table class=\"base-table\">" + 
                "			<thead>" +
                "				<tr>";

            foreach (string title in stats[0])
                table += "					<th scope=\"col\">" + title + "</th>";

            table += "				</tr>" +
                "			</thead>" +
                "			<tbody>";

            for (int ii = 1; ii < stats.Count; ii++)
            {
                table += "				<tr>";
                foreach (string content in stats[ii])
                    table += "					<td>" + content + "</td>";
                table += "				</tr>";
            }

            table += "			</tbody>" +
                "		</table>";

            string html = 
                "<!DOCTYPE html>\n" +
                "<html>\n" +
                "	<head>\n" +
                "		<title>"+name+"</title>\n" +
                "        <meta charset=\"UTF-8\">\n" +
                "        <style type=\"text/css\">\n" +
                "            @import url('https://fonts.googleapis.com/css2?family=Merriweather+Sans:wght@350;700&display=swap');\n" +
                "            @import url('https://fonts.googleapis.com/css2?family=Acme&display=swap');\n" +
                "\n" +
                "            body {\n" +
                "                font-family: \"Merriweather Sans\", \"Tahoma\", sans-serif;\n" +
                "                font-weight: 350;\n" +
                "                margin: 0;\n" +
                "            }\n" +
                "\n" +
                "            header {\n" +
                "                background-color: lightyellow;\n" +
                "                border-bottom: 1px solid #cc9;\n" +
                "                padding: 13px 5px;\n" +
                "                text-align: center;\n" +
                "                color: darkblue;	\n" +
                "            }\n" +
                "            h1 {\n" +
                "                font-size: 32pt;	\n" +
                "                font-weight: bold;	\n" +
                "                font-family: \"Acme\", \"Verdana\", sans-serif;\n" +
                "                margin: 0;\n" +
                "            }\n" +
                "\n" +
                "            nav {\n" +
                "                margin-top: 0.5em;\n" +
                "                font-size: 12pt;\n" +
                "            }\n" +
                "            nav a, nav a:visited {\n" +
                "                margin: 6pt 6pt 0 6pt;\n" +
                "                text-decoration: none;\n" +
                "                border-bottom: none;\n" +
                "                color: darkblue;\n" +
                "                border-bottom-color: lightyellow;\n" +
                "                transition: border-bottom-color 0.2s;\n" +
                "            }\n" +
                "            nav a:hover {\n" +
                "                border-bottom: 1px darkblue solid;\n" +
                "                color: darkblue;\n" +
                "            }\n" +
                "            nav a:active {\n" +
                "                border-bottom: 1px lightskyblue solid;\n" +
                "                color: lightskyblue;\n" +
                "                transition: border-bottom-color 0s;\n" +
                "            }\n" +
                "            nav a.current, nav a.current:hover {\n" +
                "                font-weight: bold;\n" +
                "                border-bottom: none;\n" +
                "            }\n" +
                "\n" +
                "            table.base-table {	\n" +
                "                border: 1px solid #999;	\n" +
                "                font-size: 10pt;	\n" +
                "                width: calc(100% - 16px);\n" +
                "                border-collapse: collapse;\n" +
                "                margin: 8px;\n" +
                "            }\n" +
                "            .base-table td {\n" +
                "                padding: 5px;\n" +
                "                margin: 3px;\n" +
                "                /*text-align: center;*/\n" +
                "            }\n" +
                "            .base-table tr:nth-child(odd)\n" +
                "            {\n" +
                "                background-color: #ddd;\n" +
                "                transition: background-color 0.2s;\n" +
                "            }\n" +
                "            .base-table tr:nth-child(even)\n" +
                "            {\n" +
                "                background-color: #fff;\n" +
                "                transition: background-color 0.2s;\n" +
                "            }\n" +
                "            .base-table tr:hover {\n" +
                "                background-color: #c9d5f0;\n" +
                "                transition: background-color 0.2s;\n" +
                "            }\n" +
                "            .base-table th {\n" +
                "                padding: 5px;\n" +
                "                margin: 3px;\n" +
                "                background-color: #104E8B;\n" +
                "                border-bottom: 1px #013e73 solid;\n" +
                "                color: #FFF;	\n" +
                "                font-weight: bold;	\n" +
                "                text-align: left;\n" +
                "                top: 0;\n" +
                "                position: sticky;\n" +
                "            }\n" +
                "\n" +
                "            footer {	\n" +
                "                background-color:lightgreen;\n" +
                "                border-top: 1px #9c9 solid;	\n" +
                "                color:black;	\n" +
                "                font-style: italic;	\n" +
                "                font-size: 8pt;		\n" +
                "                text-align: right;	\n" +
                "                padding: 8px 5px;\n" +
                "            }	\n" +
                "            </style>\n" +
                "	</head>\n" +
                "	<body>\n" +
                "        <header>\n" +
                "		    <h1>"+name+"</h1>\n" +
                "            <nav>\n" +
                "                <a href=\"Moves.html\" " + currentIfEqual(name, "Moves") + ">Moves</a>\n" +
                "                <a href=\"Items.html\" " + currentIfEqual(name, "Items") + ">Items</a>\n" +
                "                <a href=\"Abilities.html\" " + currentIfEqual(name, "Abilities") + ">Abilities</a>\n" +
                "                <a href=\"Encounters.html\" " + currentIfEqual(name, "Encounters") + ">Encounters</a>\n" +
                "            </nav>\n" +
                "        </header>\n" +
                "		<br>\n" +

                table +

                "		<br>\n" +
                "		<footer>PMDC v"+ Versioning.GetVersion().ToString() + "</footer>\n" +
                "	</body>\n" +
                "</html>\n";

            if (!Directory.Exists(PathMod.ExePath + "GUIDE/"))
                Directory.CreateDirectory(PathMod.ExePath + "GUIDE/");

            using (StreamWriter file = new StreamWriter(PathMod.ExePath + "GUIDE/" + name + ".html"))
                file.Write(html);

            DiagManager.Instance.LogInfo("Printed " + name);
        }

        public static string currentIfEqual(string name, string title)
        {
            return name == title ? "class=\"current\"" : "";
        }

        public static void PrintItemGuide()
        {
            List<string[]> stats = new List<string[]>();
            stats.Add(new string[4] { "Name", "Type", "Price", "Description" });
            for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Item].Count; ii++)
            {
                ItemData entry = DataManager.Instance.GetItem(ii);
                if (entry.Released)
                    stats.Add(new string[4] { entry.Name.ToLocal(), entry.UsageType.ToString(), entry.Price.ToString(), entry.Desc.ToLocal() });
            }
            writeHTMLGuide("Items", stats);
        }

        public static void PrintMoveGuide()
        {
            List<string[]> stats = new List<string[]>();
            stats.Add(new string[8] { "Name", "Type", "Category", "Power", "Accuracy", "PP", "Range", "Description" });
            for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Skill].Count; ii++)
            {
                SkillData entry = DataManager.Instance.GetSkill(ii);
                if (entry.Released)
                {
                    ElementData elementEntry = DataManager.Instance.GetElement(entry.Data.Element);
                    BasePowerState powerState = entry.Data.SkillStates.GetWithDefault<BasePowerState>();
                    stats.Add(new string[8] { entry.Name.ToLocal(),
                        elementEntry.Name.ToLocal(),
                        entry.Data.Category.ToLocal(),
                        powerState != null ? powerState.Power.ToString() : "---",
                        entry.Data.HitRate.ToString(),
                        entry.BaseCharges.ToString(),
                        entry.HitboxAction.GetDescription(),
                        entry.Desc.ToLocal()});
                }
                else
                    stats.Add(new string[8] { entry.Name.ToLocal(), "???", "None", "---", "---", "N/A", "No One", "NO DATA" });
                //effect chance
                //additional flags
            }
            writeHTMLGuide("Moves", stats);
        }

        public static void PrintAbilityGuide()
        {
            List<string[]> stats = new List<string[]>();
            stats.Add(new string[2] { "Name", "Description" });
            for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Intrinsic].Count; ii++)
            {
                IntrinsicData entry = DataManager.Instance.GetIntrinsic(ii);
                if (entry.Released)
                    stats.Add(new string[2] { entry.Name.ToLocal(), entry.Desc.ToLocal() });
                else
                    stats.Add(new string[2] { entry.Name.ToLocal(), "NO DATA" });
            }
            writeHTMLGuide("Abilities", stats);
        }

        private static List<string> combineFloorRanges(HashSet<int> floors)
        {
            List<string> rangeStrings = new List<string>();

            List<int> sortedFloors = new List<int>();
            sortedFloors.AddRange(floors);
            sortedFloors.Sort();

            int curStart = sortedFloors[0];
            int curEnd = sortedFloors[0];
            for (int ii = 1; ii < sortedFloors.Count; ii++)
            {
                int floor = sortedFloors[ii];
                if (floor > curEnd + 1)
                {
                    if (curStart == curEnd)
                        rangeStrings.Add((curStart + 1).ToString());
                    else
                        rangeStrings.Add((curStart + 1).ToString()+"-"+(curEnd + 1).ToString());

                    curStart = floor;
                    curEnd = floor;
                }
                else
                    curEnd = floor;
            }

            if (curStart == curEnd)
                rangeStrings.Add((curStart + 1).ToString());
            else
                rangeStrings.Add((curStart + 1).ToString() + "-" + (curEnd + 1).ToString());


            return rangeStrings;
        }

        public static void PrintEncounterGuide()
        {
            Dictionary<int, HashSet<(string, ZoneLoc)>> foundSpecies = DevHelper.GetAllAppearingMonsters(true);

            foreach ((MonsterID mon, string name) startchar in DataManager.Instance.StartChars)
                DevHelper.AddEvoFamily(foundSpecies, startchar.mon.Species, "STARTER", ZoneLoc.Invalid);

            List<string[]> stats = new List<string[]>();
            stats.Add(new string[4] { "###", "Name", "Join %", "Found In" });

            for (int ii = 1; ii < DataManager.Instance.DataIndices[DataManager.DataType.Monster].Count; ii++)
            {
                MonsterData data = DataManager.Instance.GetMonster(ii);
                if (DataManager.Instance.DataIndices[DataManager.DataType.Monster].Entries[ii].Released)
                {
                    string encounterStr = "UNKNOWN";
                    if (foundSpecies.ContainsKey(ii))
                    {
                        bool evolve = false;
                        bool starter = false;
                        // = new Dictionary<int, HashSet<int>>();
                        // = new Dictionary<int, Dictionary<int, HashSet<int>>>();
                        Dictionary<string, (Dictionary<int, HashSet<int>> specialDict, Dictionary<int, Dictionary<int, HashSet<int>>> floorDict)> foundDict = new Dictionary<string, (Dictionary<int, HashSet<int>> specialDict, Dictionary<int, Dictionary<int, HashSet<int>>> floorDict)>();

                        foreach ((string tag, ZoneLoc encounter) in foundSpecies[ii])
                        {
                            if (!foundDict.ContainsKey(tag))
                                foundDict[tag] = (new Dictionary<int, HashSet<int>>(), new Dictionary<int, Dictionary<int, HashSet<int>>>());
                            Dictionary<int, HashSet<int>> specialDict = foundDict[tag].specialDict;
                            Dictionary<int, Dictionary<int, HashSet<int>>> floorDict = foundDict[tag].floorDict;

                            if (tag == "STARTER")
                                starter = true;
                            else if (tag == "EVOLVE")
                                evolve = true;
                            else if (encounter.StructID.ID == -1)
                            {
                                if (!specialDict.ContainsKey(encounter.ID))
                                    specialDict[encounter.ID] = new HashSet<int>();
                                specialDict[encounter.ID].Add(encounter.StructID.Segment);
                            }
                            else
                            {
                                if (!floorDict.ContainsKey(encounter.ID))
                                    floorDict[encounter.ID] = new Dictionary<int, HashSet<int>>();
                                if (!floorDict[encounter.ID].ContainsKey(encounter.StructID.Segment))
                                    floorDict[encounter.ID][encounter.StructID.Segment] = new HashSet<int>();
                                floorDict[encounter.ID][encounter.StructID.Segment].Add(encounter.StructID.ID);
                            }
                        }

                        List<string> encounterMsg = new List<string>();

                        foreach (string tag in foundDict.Keys)
                        {
                            Dictionary<int, HashSet<int>> specialDict = foundDict[tag].specialDict;
                            Dictionary<int, Dictionary<int, HashSet<int>>> floorDict = foundDict[tag].floorDict;

                            for (int zz = 0; zz < DataManager.Instance.DataIndices[DataManager.DataType.Zone].Count; zz++)
                            {
                                ZoneData mainZone = DataManager.Instance.GetZone(zz);
                                for (int yy = 0; yy < mainZone.Structures.Count; yy++)
                                {
                                    if (specialDict.ContainsKey(zz) && specialDict[zz].Contains(yy))
                                    {
                                        string locString = String.Format("{0} {1}S", mainZone.Name.ToLocal(), yy + 1);
                                        foreach (var step in mainZone.Structures[yy].PostProcessingSteps)
                                        {
                                            var startStep = step as FloorNameIDPostProc;
                                            if (startStep != null)
                                            {
                                                locString = LocalText.FormatLocalText(startStep.Name, "?").ToLocal().Replace('\n', ' ');
                                                break;
                                            }
                                        }
                                        if (tag != "")
                                            locString = String.Format("[{0}] {1}", tag, locString);
                                        encounterMsg.Add(locString);
                                    }

                                    if (floorDict.ContainsKey(zz) && floorDict[zz].ContainsKey(yy))
                                    {
                                        List<string> ranges = combineFloorRanges(floorDict[zz][yy]);
                                        string rangeString = String.Join(",", ranges.ToArray());
                                        string locString = String.Format("{0} {1}S {2}F", mainZone.Name.ToLocal(), yy + 1, rangeString);
                                        foreach (var step in mainZone.Structures[yy].PostProcessingSteps)
                                        {
                                            var startStep = step as FloorNameIDPostProc;
                                            if (startStep != null)
                                            {
                                                locString = LocalText.FormatLocalText(startStep.Name, rangeString).ToLocal().Replace('\n', ' ');
                                                break;
                                            }
                                        }
                                        if (tag != "")
                                            locString = String.Format("[{0}] {1}", tag, locString);
                                        encounterMsg.Add(locString);
                                    }
                                }
                            }
                        }

                        if (evolve && encounterMsg.Count == 0)
                            encounterMsg.Add("Evolve");
                        else if (starter && encounterMsg.Count == 0)
                            encounterMsg.Add("Starter");

                        if (encounterMsg.Count > 0)
                            encounterStr = String.Join(", ", encounterMsg.ToArray());
                    }
                    stats.Add(new string[4] { ii.ToString("D3"), data.Name.ToLocal(), data.JoinRate.ToString() + "%", encounterStr });
                }
                else
                    stats.Add(new string[4] { ii.ToString("D3"), data.Name.ToLocal(), "--%", "NO DATA" });
            }
            writeHTMLGuide("Encounters", stats);
        }
    }
}
