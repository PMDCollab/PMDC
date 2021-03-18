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
            string table = "			<thead>" +
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

            table += "			</tbody>";

            string html = "<!DOCTYPE html>" +
                "<html>" +
                "	<head>" +
                "		<title>" +
                "			" + name +
                "		</title>" +
                "	</head>" +
                "	<style type=\"text/css\">" +
                "header {" +
                "	background-color:lightyellow;" +
                "	color:darkblue;" +
                "	text-align:center;" +
                "	font-size: 30px;" +
                "	font-weight: bold;" +
                "	padding:5px;" +
                "}" +
                "table.base-table {" +
                "	border: 1px solid #CCC;" +
                "	font-size: 12px;" +
                "	width: 100%;" +
                "}" +
                ".base-table td {" +
                "	padding: 4px;" +
                "	margin: 3px;" +
                "	border: 1px solid #ccc;" +
                "	text-align: center;" +
                "}" +
                ".base-table th {" +
                "	background-color: #104E8B;" +
                "	color: #FFF;" +
                "	font-weight: bold;" +
                "	text-align: center;" +
                "}" +
                "footer {" +
                "	background-color:lightgreen;" +
                "	color:black;" +
                "	font-style: italic;" +
                "	font-size:10px;" +
                "	clear:both;" +
                "	text-align:right;" +
                "	padding:5px;" +
                "}" +
                "	</style>" +
                "	<body>" +
                "		<header>" +
                "			" + name +
                "		</header>" +
                "		<br />" +
                "		<table class=\"base-table\">" +
                table +
                "		</table>" +
                "		<br />" +
                "		<footer>" +
                "			v1.0.0" +
                "		</footer>" +
                "	</body>" +
                "</html>";

            using (StreamWriter file = new StreamWriter(name + ".html"))
                file.Write(html);
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
            Dictionary<int, HashSet<ZoneLoc>> foundSpecies = DevHelper.GetAllAppearingMonsters(true);

            foreach (int startchar in DataManager.Instance.StartChars)
                DevHelper.AddEvoFamily(foundSpecies, startchar, new ZoneLoc(0, SegLoc.Invalid));

            List<string[]> stats = new List<string[]>();
            stats.Add(new string[3] { "###", "Name", "Found In" });

            for (int ii = 1; ii < DataManager.Instance.DataIndices[DataManager.DataType.Monster].Count; ii++)
            {
                string encounterStr = "UNKNOWN";
                if (foundSpecies.ContainsKey(ii))
                {
                    bool evolve = false;
                    bool starter = false;
                    Dictionary<int, HashSet<int>> specialDict = new Dictionary<int, HashSet<int>>();
                    Dictionary<int, Dictionary<int, HashSet<int>>> floorDict = new Dictionary<int, Dictionary<int, HashSet<int>>>();

                    foreach (ZoneLoc encounter in foundSpecies[ii])
                    {
                        if (encounter.ID == 0)
                            starter = true;
                        else if (encounter.ID == -1)
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

                    for (int zz = 0; zz < DataManager.Instance.DataIndices[DataManager.DataType.Zone].Count; zz++)
                    {
                        ZoneData mainZone = DataManager.Instance.GetZone(zz);
                        for (int yy = 0; yy < mainZone.Structures.Count; yy++)
                        {
                            if (specialDict.ContainsKey(zz) && specialDict[zz].Contains(yy))
                                encounterMsg.Add(mainZone.Name.ToLocal() + " S:" + yy);


                            if (floorDict.ContainsKey(zz) && floorDict[zz].ContainsKey(yy))
                            {
                                List<string> ranges = combineFloorRanges(floorDict[zz][yy]);
                                encounterMsg.Add(mainZone.Name.ToLocal() + " S:" + yy + " F:"+String.Join(",",ranges.ToArray()));
                            }
                        }
                        
                    }


                    if (starter && encounterMsg.Count == 0)
                        encounterMsg.Add("Starter");
                    if (evolve && encounterMsg.Count == 0)
                        encounterMsg.Add("Evolve");

                    if (encounterMsg.Count > 0)
                        encounterStr = String.Join("; ", encounterMsg.ToArray());
                }
                MonsterData data = DataManager.Instance.GetMonster(ii);
                stats.Add(new string[3] { ii.ToString(), data.Name.ToLocal(), encounterStr });
            }
            writeHTMLGuide("Encounters", stats);
        }
    }
}
