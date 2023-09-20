using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence.Script;
using System.Runtime.Versioning;
using PMDC.Dev;
using RogueEssence.Dungeon;
using System.Collections.Generic;
using System.IO;
using PMDC.Dungeon;
using System.Xml.Linq;

namespace MapGenTest
{
    class Program
    {
        static void Main()
        {
            if (OperatingSystem.IsWindows())
                enlargeConsole();

            string[] args = Environment.GetCommandLineArgs();
            PathMod.InitPathMod(args[0], "origin");
            DiagManager.InitInstance();
            Serializer.InitSettings(new SerializerContractResolver(), new UpgradeBinder());
            //DiagManager.Instance.DevMode = true;
            string expDir = "";
            for (int ii = 1; ii < args.Length; ii++)
            {
                if (args[ii] == "-asset")
                {
                    PathMod.ASSET_PATH = System.IO.Path.GetFullPath(PathMod.ExePath + args[ii + 1]);
                    ii++;
                }
                else if (args[ii] == "-raw")
                {
                    PathMod.DEV_PATH = System.IO.Path.GetFullPath(PathMod.ExePath + args[ii + 1]);
                    ii++;
                }
                else if (args[ii] == "-exp")
                {
                    //run exp test
                    expDir = System.IO.Path.GetFullPath(PathMod.ExePath + args[ii + 1]);
                    ii++;
                }
            }

            GraphicsManager.InitParams();
            Text.Init();
            Text.SetCultureCode("en");
            LuaEngine.InitInstance();
            DataManager.InitInstance();
            DataManager.Instance.InitData();

            if (!String.IsNullOrEmpty(expDir))
            {
                Exps = new List<(ZoneLoc zone, int expYield, int level)>();
                using (StreamReader reader = new StreamReader(expDir))
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
                        Exps.Add((new ZoneLoc(zone, new SegLoc(segment, id)), expYield, level));
                    }
                }
                string zoneId = Exps[0].zone.ID;
                ZoneEntrySummary zoneSummary = (ZoneEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Zone].Get(zoneId);
                TestExp(zoneSummary.Level);
                return;
            }

            GenContextDebug.OnInit += ExampleDebug.Init;
            GenContextDebug.OnStep += ExampleDebug.OnStep;
            GenContextDebug.OnStepIn += ExampleDebug.StepIn;
            GenContextDebug.OnStepOut += ExampleDebug.StepOut;
            GenContextDebug.OnError += ExampleDebug.OnError;

            Example.Run();

            Console.Clear();
            Console.WriteLine("Bye.");
            Console.ReadKey();
        }

        [SupportedOSPlatform("windows")]
        private static void enlargeConsole()
        {
            Console.WindowWidth = Console.LargestWindowWidth;
            Console.WindowHeight = Console.LargestWindowHeight;
            //Console.OutputEncoding = Encoding.UTF8;
        }


        public static List<(ZoneLoc zone, int expYield, int level)> Exps;
        public static void TestExp(int level)
        {
            Console.WriteLine(String.Format("{0,4}|{1,4}|{2,4}|{3,4}|{4,4}|{5,4}", "err", "fast", "fluc", "mfst", "mslo", "slow"));
            {
                ReportTestExp(new HandoutRelativeExpEvent(1, 7, 10, 2), level, "ORIGINAL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 5, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 7, 5, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), level, "HIGHER POWER OVERLEVEL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 5, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 7, 0, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), level, "HIGHER POWER NO BUFFER OVERLEVEL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 10, 2);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), level, "HIGHER DIV OVERLEVEL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 0);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 10, 0);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), level, "HIGHER DIV OVERLEVEL UNSCALED");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 10, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), level, "HIGHER DIV + POWER OVERLEVEL");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 0, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), level, "HIGHER DIV + POWER OVERLEVEL NO BUFFER");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 0, 2);
                HandoutExpEvent high = new HandoutRelativeExpEvent(1, 15, 0, 4);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 5, low, high), level, "5-LEVEL OVERLEVEL, DIV + POWER OVERLEVEL NO BUFFER");
            }
            {
                HandoutExpEvent low = new HandoutRelativeExpEvent(1, 7, 10, 2);
                HandoutExpEvent high = new HandoutHarmonicExpEvent(1, 7, 5);

                ReportTestExp(new HandoutPiecewiseExpEvent(5, 0, low, high), level, "PURE HARMONIC");
            }
        }
        public static void ReportTestExp(HandoutExpEvent expEvent, int startLevel, string comment)
        {
            List<int> growthResults = new List<int>();
            foreach (string growth in DataManager.Instance.DataIndices[DataManager.DataType.GrowthGroup].GetOrderedKeys(false))
            {
                int recipientLv = startLevel;
                GrowthData growthData = DataManager.Instance.GetGrowth(growth);
                int gainedExp = 0;

                foreach ((ZoneLoc loc, int expYield, int level) exp in Exps)
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
    }
}
