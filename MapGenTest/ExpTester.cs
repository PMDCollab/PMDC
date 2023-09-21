using PMDC.Dungeon;
using RogueEssence.Content;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dungeon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Joins;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using RogueElements;

namespace MapGenTest
{
    public static class ExpTester
    {
        public static string EXP_DIR;

        static List<ExpLog> Logs;

        public static void Run()
        {
            Logs = new List<ExpLog>();


            foreach (string dir in Directory.GetFiles(EXP_DIR))
            {
                ExpLog log = new ExpLog();
                string file = Path.GetFileNameWithoutExtension(dir);
                log.Name = file;
                using (StreamReader reader = new StreamReader(dir))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        string[] cols = line.Split(',');
                        string zone = cols[0];
                        int segment = Int32.Parse(cols[1]);
                        int id = Int32.Parse(cols[2]);
                        int expYield = Int32.Parse(cols[3]);
                        int level = Int32.Parse(cols[4]);
                        if (!log.Zone.IsValid())
                            log.Zone = new ZoneLoc(zone, new SegLoc(segment, id));
                        log.Exps.Add((expYield, level));
                    }
                }
                Logs.Add(log);
            }

            int offset = 0;
            string state = "Logs";
            while (true)
            {
                Console.Clear();
                Console.WriteLine(state);
                Console.WriteLine("Choose a log|ESC=Exit");

                int longestWidth = 0;
                for (int ii = offset; ii < Logs.Count; ii++)
                {
                    string label = GetSelectionString(ii, Logs[ii].Name);
                    if (label.Length > longestWidth)
                        longestWidth = label.Length;
                }
                int cols = Math.Min(3, MathUtils.DivUp(Console.WindowWidth, longestWidth));
                int rows = Math.Max(Math.Min(12, Logs.Count - offset), MathUtils.DivUp(Logs.Count - offset, cols));

                for (int ii = 0; ii < rows; ii++)
                {
                    string choiceStr = "";
                    List<string> choiceList = new List<string>();
                    for (int jj = 0; jj < cols; jj++)
                    {
                        int index = ii + rows * jj;
                        if (index + offset < Logs.Count)
                        {
                            choiceStr += "{" + jj + "," + "-" + longestWidth + "}  ";
                            choiceList.Add(GetSelectionString(index, Logs[index + offset].Name));
                        }
                    }
                    Console.WriteLine(String.Format(choiceStr, choiceList.ToArray()));
                }

                int zoneIndex = -1;
                if (zoneIndex < 0)
                {
                    ConsoleKeyInfo key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Escape)
                        break;

                    if (key.KeyChar >= '0' && key.KeyChar <= '9')
                        zoneIndex = key.KeyChar - '0' + offset;
                    if (key.KeyChar >= 'a' && key.KeyChar <= 'z')
                        zoneIndex = key.KeyChar - 'a' + 10 + offset;
                    if (key.Key == ConsoleKey.UpArrow)
                        offset -= 10;
                    if (key.Key == ConsoleKey.DownArrow)
                        offset += 10;
                }
                if (zoneIndex >= 0)
                {
                    ComparisonMenu(state, Logs[zoneIndex]);
                }
            }
        }

        public static void ComparisonMenu(string state, ExpLog log)
        {
            state = log.Name;
            while (true)
            {
                Console.Clear();
                Console.WriteLine(state);
                Console.WriteLine("Showing with all exp curves|ESC=Exit");

                TestExp(log);


                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key == ConsoleKey.Escape)
                    break;
            }
        }

        public static void TestExp(ExpLog log)
        {
            Console.WriteLine(String.Format("{0,4}|{1,4}|{2,4}|{3,4}|{4,4}|{5,4}", "err", "fast", "fluc", "mfst", "mslo", "slow"));
            {
                ReportTestExp(new HandoutRelativeExpEvent(1, 7, 10, 2), log, "ORIGINAL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 5, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 7, 5, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), log, "HIGHER POWER OVERLEVEL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 5, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 7, 0, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), log, "HIGHER POWER NO BUFFER OVERLEVEL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 10, 2);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), log, "HIGHER DIV OVERLEVEL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 0);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 10, 0);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), log, "HIGHER DIV OVERLEVEL UNSCALED");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 10, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), log, "HIGHER DIV + POWER OVERLEVEL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 0, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), log, "HIGHER DIV + POWER OVERLEVEL NO BUFFER");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 0, 3);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 7, 0, 3);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 5, low, high), log, "5-LEVEL OVERLEVEL, OLD POWER NO BUFFER");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 0, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 7, 0, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 5, low, high), log, "5-LEVEL OVERLEVEL, POWER NO BUFFER");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 0, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 0, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 5, low, high), log, "5-LEVEL OVERLEVEL, DIV + POWER OVERLEVEL NO BUFFER");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 2);
                HandoutExpEvent high = new HandoutHarmonicExpEvent(1, 7, 5);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), log, "POWER HARMONIC");
            }
            {
                HandoutExpEvent low = new HandoutStackExpEvent(1, 7, 5);
                HandoutExpEvent high = new HandoutHarmonicExpEvent(1, 7, 5);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), log, "STACK HARMONIC");
            }
        }
        public static void ReportTestExp(HandoutExpEvent expEvent, ExpLog log, string comment)
        {
            string zoneId = log.Zone.ID;
            ZoneEntrySummary zoneSummary = (ZoneEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Zone].Get(zoneId);

            List<int> growthResults = new List<int>();
            foreach (string growth in DataManager.Instance.DataIndices[DataManager.DataType.GrowthGroup].GetOrderedKeys(false))
            {
                int recipientLv = zoneSummary.Level;
                GrowthData growthData = DataManager.Instance.GetGrowth(growth);
                int gainedExp = 0;

                foreach ((int expYield, int level) exp in log.Exps)
                {
                    int gain = expEvent.GetExp(exp.expYield, exp.level, recipientLv);
                    gainedExp += gain;

                    while (recipientLv < DataManager.Instance.Start.MaxLevel && gainedExp >= growthData.GetExpTo(recipientLv, recipientLv + 1))
                    {
                        gainedExp -= growthData.GetExpTo(recipientLv, recipientLv + 1);
                        recipientLv++;
                    }
                }
                growthResults.Add(recipientLv);
            }
            Console.WriteLine(String.Format("{0,4}|{1,4}|{2,4}|{3,4}|{4,4}|{5,4}  //{6}", growthResults[0], growthResults[1], growthResults[2], growthResults[3], growthResults[4], growthResults[5], comment));
        }


        public static string GetSelectionString(int index, string str)
        {
            char select = (char)(index > 9 ? 'A' + index - 10 : '0' + index);
            return select.ToString() + ") " + str;
        }
    }

    public class ExpLog
    {
        public string Name;
        public ZoneLoc Zone;
        public List<(int expYield, int level)> Exps;

        public ExpLog()
        {
            Zone = ZoneLoc.Invalid;
            Exps = new List<(int expYield, int level)>();
        }
    }
}
