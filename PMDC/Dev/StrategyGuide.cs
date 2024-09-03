using System;
using System.Collections.Generic;
using System.IO;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;
using RogueEssence;
using PMDC.Data;
using PMDC.Dungeon;

namespace PMDC.Dev
{
    public static class StrategyGuide
    {
        private const int TOTAL_CHUNKS = 60;

        private static void WriteToWiki(string name, string content)
        {
            if (!Directory.Exists(PathMod.APP_PATH + "WIKI/"))
                Directory.CreateDirectory(PathMod.APP_PATH + "WIKI/");

            string endPath = Path.Join(PathMod.APP_PATH, "WIKI/", name + ".txt");
            string endDirectory = Path.GetDirectoryName(endPath);

            if (!Directory.Exists(endDirectory))
                Directory.CreateDirectory(endDirectory);

            using (var fstream = File.CreateText(endPath))
            {
                fstream.WriteLine(content);

                fstream.Flush();
                fstream.Close();
            }
        }

        private static void writeCSVGuide(string name, List<string[]> stats)
        {

            if (!Directory.Exists(PathMod.APP_PATH + "GUIDE/"))
                Directory.CreateDirectory(PathMod.APP_PATH + "GUIDE/");

            using (StreamWriter file = new StreamWriter(PathMod.APP_PATH + "GUIDE/" + name + ".csv"))
            {
                foreach (string[] stat in stats)
                    file.WriteLine(String.Join("\t", stat));
            }

            Console.WriteLine();
        }

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
            if (!Directory.Exists(PathMod.APP_PATH + "GUIDE/"))
                Directory.CreateDirectory(PathMod.APP_PATH + "GUIDE/");

            using (StreamWriter file = new StreamWriter(PathMod.APP_PATH + "GUIDE/" + name + ".html"))
                file.Write(html);

            Console.WriteLine();
        }
        
        public static string currentIfEqual(string name, string title)
        {
            return name == title ? "class=\"current\"" : "";
        }

        public static void PrintItemGuide(bool csv)
        {
            List<string[]> stats = new List<string[]>();
            stats.Add(new string[5] { "###", "Name", "Type", "Price", "Description" });
            List<string> itemKeys = DataManager.Instance.DataIndices[DataManager.DataType.Item].GetOrderedKeys(true);
            for (int ii = 0; ii < itemKeys.Count; ii++)
            {
                ProgressBar("Creating item guide...", "Done.", TOTAL_CHUNKS, ii, itemKeys.Count);
                string key = itemKeys[ii];
                ItemData entry = DataManager.Instance.GetItem(key);
                if (entry.Released)
                    stats.Add(new string[5] { key, entry.Name.ToLocal(), entry.UsageType.ToString(), entry.Price.ToString(), entry.Desc.ToLocal() });
            }

            if (csv)
                writeCSVGuide("Items", stats);
            else
                writeHTMLGuide("Items", stats);
        }

        public static void PrintItemWiki()
        {
            List<string> itemKeys = DataManager.Instance.DataIndices[DataManager.DataType.Item].GetOrderedKeys(true);
            for (int ii = 0; ii < itemKeys.Count; ii++)
            {
                ProgressBar("Creating item pages...", "Done.", TOTAL_CHUNKS, ii, itemKeys.Count);
                string key = itemKeys[ii];
                ItemData entry = DataManager.Instance.GetItem(key);
                if (entry.Released)
                {
                    string localName = entry.Name.ToLocal();
                    string fileContent = "{{{{{1|ItemData}}}" +
                        "\r\n|item_name=" + localName +
                        "\r\n|sprite=" + entry.Sprite +
                        "\r\n|item_id=" + key +
                        "\r\n|is_edible=" + entry.ItemStates.Contains<EdibleState>() +
                        "\r\n|value=" + entry.Price +
                        "\r\n}}";

                    WriteToWiki(localName + "/Data", fileContent);
                }
            }
        }

        public static void PrintMoveGuide(bool csv)
        {
            List<string[]> stats = new List<string[]>();
            stats.Add(new string[9] { "###", "Name", "Type", "Category", "Power", "Accuracy", "PP", "Range", "Description" });
            List<string> moves = DataManager.Instance.DataIndices[DataManager.DataType.Skill].GetOrderedKeys(true);
            for (int ii = 0; ii < moves.Count; ii++)
            {
                ProgressBar("Creating moves guide...", "Done.", TOTAL_CHUNKS, ii, moves.Count);
                string key = moves[ii];
                SkillData entry = DataManager.Instance.GetSkill(key);
                if (entry.Released)
                {
                    ElementData elementEntry = DataManager.Instance.GetElement(entry.Data.Element);
                    BasePowerState powerState = entry.Data.SkillStates.GetWithDefault<BasePowerState>();
                    stats.Add(new string[9] { key, entry.Name.ToLocal(),
                        elementEntry.Name.ToLocal(),
                        entry.Data.Category.ToLocal(),
                        powerState != null ? powerState.Power.ToString() : "---",
                        entry.Data.HitRate.ToString(),
                        entry.BaseCharges.ToString(),
                        entry.HitboxAction.GetDescription(),
                        entry.Desc.ToLocal()});
                }
                else
                    stats.Add(new string[9] { key, entry.Name.ToLocal(), "???", "None", "---", "---", "N/A", "No One", "NO DATA" });
                //effect chance
                //additional flags
            }
            if (csv)
                writeCSVGuide("Moves", stats);
            else
                writeHTMLGuide("Moves", stats);
        }

        public static void PrintAbilityGuide(bool csv)
        {
            List<string[]> stats = new List<string[]>();
            stats.Add(new string[3] { "###", "Name", "Description" });

            List<string> abilities =
                DataManager.Instance.DataIndices[DataManager.DataType.Intrinsic].GetOrderedKeys(true);
            
            for (int ii = 0; ii < abilities.Count; ii++)
            {
                ProgressBar("Creating abilities guide...", "Done.", TOTAL_CHUNKS, ii, abilities.Count);
                string key = abilities[ii];
                IntrinsicData entry = DataManager.Instance.GetIntrinsic(key);
                if (entry.Released)
                    stats.Add(new string[3] { key, entry.Name.ToLocal(), entry.Desc.ToLocal() });
                else
                    stats.Add(new string[3] { key, entry.Name.ToLocal(), "NO DATA" });
            }

            if (csv)
                writeCSVGuide("Abilities", stats);
            else
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

        public static void PrintEncounterGuide(bool csv)
        {
            List<string> monsterKeys = DataManager.Instance.DataIndices[DataManager.DataType.Monster].GetOrderedKeys(true);
            ProgressBar("Creating encounters guide...", "Done.", TOTAL_CHUNKS, 0, monsterKeys.Count);
            
            Dictionary<MonsterID, HashSet<(string tag, ZoneLoc encounter)>> foundSpecies = DevHelper.GetAllAppearingMonsters(true);

            foreach (StartChar startchar in DataManager.Instance.Start.Chars)
                DevHelper.AddWithEvos(foundSpecies, new MonsterID(startchar.ID.Species, startchar.ID.Form, "", Gender.Unknown), "STARTER", ZoneLoc.Invalid);

            List<string[]> stats = new List<string[]>();
            stats.Add(new string[4] { "###", "Name", "Join %", "Found In" });

            for (int ii = 0; ii < monsterKeys.Count; ii++ )
            {
                ProgressBar("Creating encounters guide...", "Done.", TOTAL_CHUNKS, ii, monsterKeys.Count);
                string key = monsterKeys[ii];
                MonsterEntrySummary summary = (MonsterEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Monster].Get(key);
                MonsterData data = DataManager.Instance.GetMonster(key);
                for (int jj = 0; jj < summary.Forms.Count; jj++)
                {
                    MonsterFormData formData = (MonsterFormData)data.Forms[jj];
                    if (formData.Temporary)
                        continue;

                    if (summary.Released && formData.Released)
                    {
                        string encounterStr = "UNKNOWN";
                        MonsterID monId = new MonsterID(key, jj, "", Gender.Unknown);
                        if (foundSpecies.ContainsKey(monId))
                        {
                            bool evolve = false;
                            bool starter = false;

                            Dictionary<string, (Dictionary<string, HashSet<int>> specialDict, Dictionary<string, Dictionary<int, HashSet<int>>> floorDict)> foundDict = new Dictionary<string, (Dictionary<string, HashSet<int>> specialDict, Dictionary<string, Dictionary<int, HashSet<int>>> floorDict)>();

                            foreach ((string tag, ZoneLoc encounter) in foundSpecies[monId])
                            {
                                if (!foundDict.ContainsKey(tag))
                                    foundDict[tag] = (new Dictionary<string, HashSet<int>>(), new Dictionary<string, Dictionary<int, HashSet<int>>>());
                                Dictionary<string, HashSet<int>> specialDict = foundDict[tag].specialDict;
                                Dictionary<string, Dictionary<int, HashSet<int>>> floorDict = foundDict[tag].floorDict;

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
                                Dictionary<string, HashSet<int>> specialDict = foundDict[tag].specialDict;
                                Dictionary<string, Dictionary<int, HashSet<int>>> floorDict = foundDict[tag].floorDict;

                                foreach (string zz in DataManager.Instance.DataIndices[DataManager.DataType.Zone].GetOrderedKeys(true))
                                {
                                    ZoneData mainZone = DataManager.Instance.GetZone(zz);
                                    for (int yy = 0; yy < mainZone.Segments.Count; yy++)
                                    {
                                        if (specialDict.ContainsKey(zz) && specialDict[zz].Contains(yy))
                                        {
                                            string locString = String.Format("{0} {1}S", mainZone.Name.ToLocal(), yy + 1);
                                            foreach (var step in mainZone.Segments[yy].ZoneSteps)
                                            {
                                                var startStep = step as FloorNameIDZoneStep;
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
                                            foreach (var step in mainZone.Segments[yy].ZoneSteps)
                                            {
                                                var startStep = step as FloorNameIDZoneStep;
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
                        stats.Add(new string[4] { summary.SortOrder.ToString("D3"), formData.FormName.ToLocal(), data.JoinRate.ToString() + "%", encounterStr });
                    }
                    else
                        stats.Add(new string[4] { summary.SortOrder.ToString("D3"), formData.FormName.ToLocal(), "--%", "NO DATA" });
                }
            }
            if (csv)
                writeCSVGuide("Encounters", stats);
            else
                writeHTMLGuide("Encounters", stats);
        }

        public static void ProgressBar(string message, string ending, int totalChunks, int progress, int total)
        {
#if !DEBUG
            // offset the progress by 1;
            progress++;
            Console.CursorVisible = false;

            double progressCompleted = (double)(progress) / total;
            int numChunksComplete = (int)(totalChunks * progressCompleted);
            string progressString = String.Format("Progress: [{0, 3}%]", (int)(progressCompleted * 100));
            string spacer = " ";

            int offset = progressString.Length + spacer.Length;

            Console.CursorLeft = 0;
            Console.BackgroundColor = ConsoleColor.Green;
            Console.Write(progressString);
            
            Console.ResetColor();
            Console.Write(spacer);
            Console.CursorLeft = offset;
            Console.Write("[");
            Console.CursorLeft = offset + totalChunks + 1;
            Console.Write("|");
            Console.CursorLeft = offset + 1;
            
            if (numChunksComplete > 0 && progress != total)
                Console.Write("".PadRight(numChunksComplete - 1, '=') + ">");
            else
                Console.Write("".PadRight(numChunksComplete, '='));

            Console.Write("".PadRight(totalChunks - numChunksComplete, '-'));
            Console.CursorLeft = offset + totalChunks + 2;

            int totalDigits = total.ToString().Length;
            string display = String.Format(" {0}/{1} ]", progress.ToString().PadLeft(totalDigits), total);

 
            string currMessage;
            if (progress < total)
                currMessage = message;
            else
            {
                string spacePadding = new string(' ', Math.Abs(ending.Length - message.Length));
                currMessage = ending + spacePadding;
            }

            Console.Write(display + spacer + currMessage);
#endif
        }
    }
}
