using System;
using System.Collections.Generic;
using System.Text;
using RogueElements;
using RogueEssence.LevelGen;
using RogueEssence.Data;
using RogueEssence;
using System.Diagnostics;
using RogueEssence.Dungeon;
using Microsoft.Win32;

namespace MapGenTest
{
    public static class Example
    {
        static Dictionary<string, ZoneData> loadedZones;

        private static ZoneData getCachedZone(string zoneIndex)
        {
            ZoneData zone;
            if (loadedZones.TryGetValue(zoneIndex, out zone))
                return zone;
            zone = DataManager.Instance.GetZone(zoneIndex);
            loadedZones[zoneIndex] = zone;
            return zone;
        }

        public static void Run()
        {
            loadedZones = new Dictionary<string, ZoneData>();
            try
            {
                List<string> zoneNames = new List<string>();
                foreach(string key in DataManager.Instance.DataIndices[DataManager.DataType.Zone].Entries.Keys)
                    zoneNames.Add(key);

                string state = "Zones";
                while (true)
                {
                    Console.Clear();
                    Console.WriteLine(state);
                    Console.WriteLine("Choose a zone|ESC=Exit|F2=Stress Test");

                    int longestWidth = 0;
                    for (int ii = 0; ii < zoneNames.Count; ii++)
                    {
                        string label = GetSelectionString(ii, zoneNames[ii]);
                        if (label.Length > longestWidth)
                            longestWidth = label.Length;
                    }
                    int cols = Math.Min(3, MathUtils.DivUp(Console.WindowWidth, longestWidth));
                    int rows = Math.Max(Math.Min(12, zoneNames.Count), MathUtils.DivUp(zoneNames.Count, cols));

                    for (int ii = 0; ii < rows; ii++)
                    {
                        string choiceStr = "";
                        List<string> choiceList = new List<string>();
                        for (int jj = 0; jj < cols; jj++)
                        {
                            int index = ii + rows * jj;
                            if (index < zoneNames.Count)
                            {
                                choiceStr += "{" + jj + "," + "-" + longestWidth + "}  ";
                                choiceList.Add(GetSelectionString(index, zoneNames[index]));
                            }
                        }
                        Console.WriteLine(String.Format(choiceStr, choiceList.ToArray()));
                    }

                    string zoneIndex = (string)Registry.GetValue(DiagManager.REG_PATH, "ZoneChoice", "");
                    if (String.IsNullOrEmpty(zoneIndex))
                    {
                        ConsoleKeyInfo key = Console.ReadKey();
                        if (key.Key == ConsoleKey.Escape)
                            break;
                        else if (key.Key == ConsoleKey.F2)
                        {
                            while (true)
                            {
                                Console.Clear();
                                Console.WriteLine(state + ">Bulk Gen");
                                Console.WriteLine("Specify amount to bulk gen");
                                int amt = GetInt(false);
                                if (amt > -1)
                                {
                                    Console.WriteLine("Generating all zones " + amt + " times.");
                                    StressTestAll(amt);
                                    ConsoleKeyInfo afterKey = Console.ReadKey();
                                    if (afterKey.Key == ConsoleKey.Escape)
                                        break;
                                }
                                else if (amt == -1)
                                    break;
                            }
                        }

                        if (key.KeyChar >= '0' && key.KeyChar <= '9')
                            zoneIndex = zoneNames[key.KeyChar - '0'];
                        if (key.KeyChar >= 'a' && key.KeyChar <= 'z')
                            zoneIndex = zoneNames[key.KeyChar - 'a' + 10];
                    }
                    if (!String.IsNullOrEmpty(zoneIndex))
                    {
                        Registry.SetValue(DiagManager.REG_PATH, "ZoneChoice", zoneIndex);
                        StructureMenu(state, zoneIndex, getCachedZone(zoneIndex));
                        Registry.SetValue(DiagManager.REG_PATH, "ZoneChoice", "");
                    }
                }

            }
            catch (Exception ex)
            {
                PrintError(ex);
                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                Registry.SetValue(DiagManager.REG_PATH, "StructChoice", -1);
                Registry.SetValue(DiagManager.REG_PATH, "ZoneChoice", -1);
                Console.ReadKey();
            }
        }

        public static string GetSelectionString(int index, string str)
        {
            char select = (char)(index > 9 ? 'A' + index - 10 : '0' + index);
            return select.ToString() + ") " + str;
        }

        public static void StructureMenu(string prevState, string zoneIndex, ZoneData zone)
        {
            try
            {
                string state = prevState + ">" + zoneIndex + ": " + zone.Name.DefaultText;
                while (true)
                {
                    Console.Clear();
                    Console.WriteLine(state);
                    Console.WriteLine("Choose a structure|ESC=Back|F2=Stress Test");

                    int longestWidth = 0;
                    for (int ii = 0; ii < zone.Segments.Count; ii++)
                    {
                        string label = GetSelectionString(ii, zone.Segments[ii].FloorCount + " Floors");
                        if (label.Length > longestWidth)
                            longestWidth = label.Length;
                    }
                    int cols = Math.Min(3, MathUtils.DivUp(Console.WindowWidth, longestWidth));
                    int rows = Math.Max(Math.Min(12, zone.Segments.Count), MathUtils.DivUp(zone.Segments.Count, cols));

                    for (int ii = 0; ii < rows; ii++)
                    {
                        string choiceStr = "";
                        List<string> choiceList = new List<string>();
                        for (int jj = 0; jj < cols; jj++)
                        {
                            int index = ii + rows * jj;
                            if (index < zone.Segments.Count)
                            {
                                choiceStr += "{" + jj + "," + "-" + longestWidth + "}";
                                choiceList.Add(GetSelectionString(index, zone.Segments[index].FloorCount + " Floors"));
                            }
                        }
                        Console.WriteLine(String.Format(choiceStr, choiceList.ToArray()));
                    }

                    int structureIndex = (int)Registry.GetValue(DiagManager.REG_PATH, "StructChoice", -1);
                    if (structureIndex == -1)
                    {
                        ConsoleKeyInfo key = Console.ReadKey();
                        if (key.Key == ConsoleKey.Escape)
                        {
                            Registry.SetValue(DiagManager.REG_PATH, "StructChoice", -1);
                            break;
                        }
                        else if (key.Key == ConsoleKey.F2)
                        {
                            while (true)
                            {
                                Console.Clear();
                                Console.WriteLine(state + ">Bulk Gen");
                                Console.WriteLine("Specify amount to bulk gen");
                                int amt = GetInt(false);
                                if (amt > -1)
                                {
                                    Console.WriteLine("Generating zone " + amt + " times.");
                                    StressTestZone(zone, zoneIndex, amt);
                                    ConsoleKeyInfo afterKey = Console.ReadKey();
                                    if (afterKey.Key == ConsoleKey.Escape)
                                        break;
                                }
                                else if (amt == -1)
                                    break;
                            }
                        }

                        if (key.KeyChar >= '0' && key.KeyChar <= '9')
                            structureIndex = key.KeyChar - '0';
                        if (key.KeyChar >= 'a' && key.KeyChar <= 'z')
                            structureIndex = key.KeyChar - 'a' + 10;
                    }
                    if (structureIndex > -1 && structureIndex < zone.Segments.Count)
                    {
                        Registry.SetValue(DiagManager.REG_PATH, "StructChoice", structureIndex);
                        FloorMenu(state, zoneIndex, structureIndex, zone.Segments[structureIndex]);
                        Registry.SetValue(DiagManager.REG_PATH, "StructChoice", -1);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR at Zone " + zoneIndex);
                PrintError(ex);
                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                Registry.SetValue(DiagManager.REG_PATH, "StructChoice", -1);
                Console.ReadKey();
            }
        }

        public static void FloorMenu(string prevState, string zoneIndex, int structureIndex, ZoneSegmentBase structure)
        {
            try
            {
                string state = prevState + ">Structure " + structureIndex;
                while (true)
                {
                    Console.Clear();
                    Console.WriteLine(state);
                    Console.WriteLine("Choose a Floor: 0-{0}|ESC=Back|F2=Stress Test", (structure.FloorCount - 1).ToString());

                    int floorNum = (int)Registry.GetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                    if (floorNum == -1)
                    {
                        floorNum = GetInt(true);
                        if (floorNum == -1)
                        {
                            Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                            break;
                        }
                        else if (floorNum == -2)
                        {
                            while (true)
                            {
                                Console.Clear();
                                Console.WriteLine(state + ">Bulk Gen");
                                Console.WriteLine("Specify amount to bulk gen");
                                int amt = GetInt(false);
                                if (amt > -1)
                                {
                                    Console.WriteLine("Generating structure " + amt + " times.");
                                    StressTestStructure(structure, zoneIndex, structureIndex, amt);
                                    ConsoleKeyInfo afterKey = Console.ReadKey();
                                    if (afterKey.Key == ConsoleKey.Escape)
                                        break;
                                }
                                else if (amt == -1)
                                    break;
                            }
                        }
                    }
                    //TODO: map the floor number to map id
                    if (floorNum > -1 && floorNum < structure.FloorCount)
                    {
                        Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", floorNum);
                        MapMenu(state, zoneIndex, new SegLoc(structureIndex, floorNum), structure);
                        Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                    }
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR at Struct " + structureIndex);
                PrintError(ex);
                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                Console.ReadKey();
            }
        }


        public static void MapMenu(string prevState, string zoneIndex, SegLoc floorIndex, ZoneSegmentBase structure)
        {
            ulong zoneSeed = MathUtils.Rand.NextUInt64();
            try
            {
                ulong newSeed;
                if (UInt64.TryParse((string)Registry.GetValue(DiagManager.REG_PATH, "SeedChoice", ""), out newSeed))
                    zoneSeed = newSeed;

                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", zoneSeed.ToString());

                while (true)
                {
                    Console.Clear();

                    ConsoleKey key = ConsoleKey.Enter;
                    string state = prevState + ">" + floorIndex.ID + ": ";
                    bool threwException = false;
                    try
                    {
                        GameProgress save = new MainProgress(MathUtils.Rand.NextUInt64(), Guid.NewGuid().ToString());
                        DataManager.Instance.SetProgress(save);

                        ZoneGenContext newContext = CreateZoneGenContext(zoneSeed, zoneIndex, floorIndex, structure);

                        IGenContext context = structure.GetMap(newContext);

                        DataManager.Instance.SetProgress(null);

                        ExampleDebug.SteppingIn = false;

                        if (ExampleDebug.Error != null)
                            throw ExampleDebug.Error;

                        BaseMapGenContext stairsMap = context as BaseMapGenContext;
                        state += stairsMap.Map.Name.DefaultText.Replace('\n', ' ');
                        string seedMsg = "ZSeed: " + zoneSeed + "    MSeed: " + newContext.Seed;
                        //Console.WriteLine(state);

                        key = ExampleDebug.PrintTiles(context, state + "\n" + "Arrow Keys=Navigate|Enter=Retry|ESC=Back|F2=Stress Test|F3=Custom Seed|F4=Step In" + "\n" + seedMsg, true, true, true);


                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR at F" + floorIndex.ID + " SEED:" + zoneSeed);
                        PrintError(ex);
                        Console.WriteLine("Press Enter to retry error scenario.");
                        key = Console.ReadKey().Key;
                        threwException = true;
                    }


                    if (key == ConsoleKey.Escape)
                    {
                        Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                        break;
                    }
                    else if (key == ConsoleKey.F2)
                    {
                        while (true)
                        {
                            Console.Clear();
                            Console.WriteLine(state + ">Bulk Gen");
                            Console.WriteLine("Specify amount to bulk gen");
                            int amt = GetInt(false);
                            if (amt > -1)
                            {
                                Console.WriteLine("Generating floor " + amt + " times.");
                                StressTestFloor(structure, zoneIndex, floorIndex, amt);
                                ConsoleKeyInfo afterKey = Console.ReadKey();
                                if (afterKey.Key == ConsoleKey.Escape)
                                    break;
                            }
                            else if (amt == -1)
                                break;
                        }
                    }
                    else if (key == ConsoleKey.F3)
                    {
                        Console.Clear();
                        Console.WriteLine(state + ">Custom Seed");
                        Console.WriteLine("Specify a ZONE seed value");
                        string input = Console.ReadLine();
                        ulong customSeed;
                        if (UInt64.TryParse(input, out customSeed))
                            zoneSeed = customSeed;
                    }
                    else if (key == ConsoleKey.F4)
                    {
                        ExampleDebug.SteppingIn = true;
                    }
                    else if (key == ConsoleKey.Enter)
                    {
                        if (!threwException)
                            zoneSeed = MathUtils.Rand.NextUInt64();
                    }
                    Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", zoneSeed.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR at F"+floorIndex.ID+" ZSEED:" + zoneSeed);
                PrintError(ex);
                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                Console.ReadKey();
            }
        }

        public static int GetInt(bool includeAmt)
        {
            int result = 0;

            ConsoleKeyInfo key = Console.ReadKey(true);
            while(key.Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Escape)
                    return -1;
                if (includeAmt && key.Key == ConsoleKey.F2)
                    return -2;

                if (key.KeyChar >= '0' && key.KeyChar <= '9')
                {
                    Console.Write(key.KeyChar);
                    result = result * 10 + key.KeyChar - '0';
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    Console.Write("\b \b");
                    result = result / 10;
                }
                key = Console.ReadKey(true);
            }
            Console.WriteLine();
            return result;
        }

        public static ZoneGenContext CreateZoneGenContext(ulong zoneSeed, string zoneIndex, SegLoc floorIndex, ZoneSegmentBase structure)
        {
            ReNoise totalNoise = new ReNoise(zoneSeed);
            ulong[] doubleSeed = totalNoise.GetTwoUInt64((ulong)floorIndex.Segment);
            ZoneGenContext newContext = CreateZoneGenContextSegment(doubleSeed[0], zoneIndex, floorIndex.Segment, structure);

            INoise idNoise = new ReNoise(doubleSeed[1]);
            newContext.CurrentID = floorIndex.ID;
            newContext.Seed = idNoise.GetUInt64((ulong)floorIndex.ID);

            return newContext;
        }

        public static ZoneGenContext CreateZoneGenContextSegment(ulong structSeed, string zoneIndex, int structureIndex, ZoneSegmentBase structure)
        {
            INoise structNoise = new ReNoise(structSeed);

            ZoneGenContext newContext = new ZoneGenContext();
            newContext.CurrentZone = zoneIndex;
            newContext.CurrentSegment = structureIndex;
            foreach (ZoneStep zoneStep in structure.ZoneSteps)
            {
                //TODO: find a better way to feed ZoneSteps into full zone segments.
                //Is there a way for them to be stateless?
                //Additionally, the ZoneSteps themselves sometimes hold IGenSteps that are copied over to the layouts.
                //Is that really OK? (I would guess yes because there is no chance by design for them to be mutated when generating...)
                ZoneStep newStep = zoneStep.Instantiate(structNoise.GetUInt64((ulong)newContext.ZoneSteps.Count));
                newContext.ZoneSteps.Add(newStep);
            }
            return newContext;
        }

        public static void StressTestAll(int amount)
        {
            ExampleDebug.Printing = -1;
            string zoneIndex = "";
            int structureIndex = 0;
            ulong zoneSeed = 0;
            int floor = 0;
            try
            {
                Dictionary<string, List<TimeSpan>> generationTimes = new Dictionary<string, List<TimeSpan>>();
                foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Zone].Entries.Keys)
                    generationTimes[key] = new List<TimeSpan>();

                Stopwatch watch = new Stopwatch();

                for (int ii = 0; ii < amount; ii++)
                {
                    zoneSeed = MathUtils.Rand.NextUInt64();
                    ReNoise totalNoise = new ReNoise(zoneSeed);

                    foreach(string key in DataManager.Instance.DataIndices[DataManager.DataType.Zone].Entries.Keys)
                    {
                        zoneIndex = key;
                        ZoneData zone = getCachedZone(key);

                        for (int nn = 0; nn < zone.Segments.Count; nn++)
                        {
                            structureIndex = nn;
                            ZoneSegmentBase structure = zone.Segments[nn];

                            ulong[] doubleSeed = totalNoise.GetTwoUInt64((ulong)structureIndex);
                            ZoneGenContext zoneContext = CreateZoneGenContextSegment(doubleSeed[0], zoneIndex, structureIndex, structure);

                            GameProgress save = new MainProgress(MathUtils.Rand.NextUInt64(), Guid.NewGuid().ToString());

                            INoise idNoise = new ReNoise(doubleSeed[1]);

                            foreach (int floorId in structure.GetFloorIDs())
                            {
                                floor = floorId;
                                zoneContext.CurrentID = floorId;
                                zoneContext.Seed = idNoise.GetUInt64((ulong)floorId);

                                TestFloor(watch, save, structure, zoneContext, null, null, generationTimes[key]);
                            }
                        }
                    }
                }

                PrintTimeAnalysisTier2(generationTimes, "Z");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR at Z"+zoneIndex+" S" + structureIndex + " F" + floor + " ZSeed:" + zoneSeed);
                PrintError(ex);
            }
            finally
            {
                ExampleDebug.Printing = 0;
            }
        }

        public static void StressTestZone(ZoneData zone, string zoneIndex, int amount)
        {
            ExampleDebug.Printing = -1;
            int structureIndex = 0;
            ulong zoneSeed = 0;
            int floor = 0;
            try
            {
                Dictionary<string, List<TimeSpan>> generationTimes = new Dictionary<string, List<TimeSpan>>();
                for (int ii = 0; ii < zone.Segments.Count; ii++)
                    generationTimes[ii.ToString("D3")] = new List<TimeSpan>();

                Stopwatch watch = new Stopwatch();

                for (int ii = 0; ii < amount; ii++)
                {
                    zoneSeed = MathUtils.Rand.NextUInt64();
                    ReNoise totalNoise = new ReNoise(zoneSeed);

                    for (int nn = 0; nn < zone.Segments.Count; nn++)
                    {
                        structureIndex = nn;
                        ZoneSegmentBase structure = zone.Segments[nn];

                        ulong[] doubleSeed = totalNoise.GetTwoUInt64((ulong)structureIndex);
                        ZoneGenContext zoneContext = CreateZoneGenContextSegment(doubleSeed[0], zoneIndex, structureIndex, structure);

                        GameProgress save = new MainProgress(MathUtils.Rand.NextUInt64(), Guid.NewGuid().ToString());

                        INoise idNoise = new ReNoise(doubleSeed[1]);

                        foreach (int floorId in structure.GetFloorIDs())
                        {
                            floor = floorId;
                            zoneContext.CurrentID = floorId;
                            zoneContext.Seed = idNoise.GetUInt64((ulong)floorId);

                            TestFloor(watch, save, structure, zoneContext, null, null, generationTimes[nn.ToString("D3")]);
                        }
                    }
                }

                PrintTimeAnalysisTier2(generationTimes, "S");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR at S" + structureIndex + " F" + floor + " ZSeed:" + zoneSeed);
                PrintError(ex);
            }
            finally
            {
                ExampleDebug.Printing = 0;
            }
        }


        public static void StressTestStructure(ZoneSegmentBase structure, string zoneIndex, int structureIndex, int amount)
        {
            ExampleDebug.Printing = -1;
            ulong zoneSeed = 0;
            int floor = 0;
            try
            {
                List<Dictionary<int, int>> generatedItems = new List<Dictionary<int, int>>();
                List<Dictionary<int, int>> generatedEnemies = new List<Dictionary<int, int>>();
                Dictionary<string, List<TimeSpan>> generationTimes = new Dictionary<string, List<TimeSpan>>();
                for (int ii = 0; ii < structure.FloorCount; ii++)
                {
                    generatedItems.Add(new Dictionary<int, int>());
                    generatedEnemies.Add(new Dictionary<int, int>());
                    generationTimes[ii.ToString("D3")] = new List<TimeSpan>();
                }

                Stopwatch watch = new Stopwatch();

                for (int ii = 0; ii < amount; ii++)
                {
                    zoneSeed = MathUtils.Rand.NextUInt64();

                    ReNoise totalNoise = new ReNoise(zoneSeed);
                    ulong[] doubleSeed = totalNoise.GetTwoUInt64((ulong)structureIndex);
                    INoise idNoise = new ReNoise(doubleSeed[1]);

                    GameProgress save = new MainProgress(MathUtils.Rand.NextUInt64(), Guid.NewGuid().ToString());
                    ZoneGenContext zoneContext = CreateZoneGenContextSegment(doubleSeed[0], zoneIndex, structureIndex, structure);

                    foreach (int floorId in structure.GetFloorIDs())
                    {
                        floor = floorId;
                        zoneContext.CurrentID = floorId;
                        zoneContext.Seed = idNoise.GetUInt64((ulong)floorId);

                        TestFloor(watch, save, structure, zoneContext, generatedItems[floorId], generatedEnemies[floorId], generationTimes[floorId.ToString("D3")]);
                    }
                }


                Dictionary<int, int> totalGeneratedItems = new Dictionary<int, int>();
                Dictionary<int, int> totalGeneratedEnemies = new Dictionary<int, int>();
                for (int ii = 0; ii < structure.FloorCount; ii++)
                {
                    Debug.WriteLine("F"+ii+":");
                    PrintContentAnalysis(generatedItems[ii], generatedEnemies[ii]);
                    
                    foreach(int key in generatedItems[ii].Keys)
                        MathUtils.AddToDictionary<int>(totalGeneratedItems, key, generatedItems[ii][key]);

                    foreach (int key in generatedEnemies[ii].Keys)
                        MathUtils.AddToDictionary<int>(totalGeneratedEnemies, key, generatedEnemies[ii][key]);
                }

                Debug.WriteLine("Overall:");
                PrintContentAnalysis(totalGeneratedItems, totalGeneratedEnemies);

                PrintTimeAnalysisTier2(generationTimes, "F");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR at F" + floor + " ZSeed:" + zoneSeed);
                PrintError(ex);
            }
            finally
            {
                ExampleDebug.Printing = 0;
            }
        }


        public static void StressTestFloor(ZoneSegmentBase structure, string zoneIndex, SegLoc floorIndex, int amount)
        {
            ExampleDebug.Printing = -1;
            ulong zoneSeed = 0;
            try
            {
                Dictionary<int, int> generatedItems = new Dictionary<int, int>();
                Dictionary<int, int> generatedEnemies = new Dictionary<int, int>();
                List<TimeSpan> generationTimes = new List<TimeSpan>();
                Stopwatch watch = new Stopwatch();

                for (int ii = 0; ii < amount; ii++)
                {
                    zoneSeed = MathUtils.Rand.NextUInt64();

                    GameProgress save = new MainProgress(MathUtils.Rand.NextUInt64(), Guid.NewGuid().ToString());
                    ZoneGenContext zoneContext = CreateZoneGenContext(zoneSeed, zoneIndex, floorIndex, structure);

                    TestFloor(watch, save, structure, zoneContext, generatedItems, generatedEnemies, generationTimes);

                }

                PrintContentAnalysis(generatedItems, generatedEnemies);

                PrintTimeAnalysis(generationTimes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: " + zoneSeed);
                PrintError(ex);
            }
            finally
            {
                ExampleDebug.Printing = 0;
            }
        }

        public static void TestFloor(Stopwatch watch, GameProgress save, ZoneSegmentBase structure, ZoneGenContext zoneContext, Dictionary<int, int> generatedItems, Dictionary<int, int> generatedEnemies, List<TimeSpan> generationTimes)
        {
            DataManager.Instance.SetProgress(save);

            TimeSpan before = watch.Elapsed;
            watch.Start();
            IGenContext context = structure.GetMap(zoneContext);
            watch.Stop();
            TimeSpan diff = watch.Elapsed - before;
            generationTimes.Add(diff);

            DataManager.Instance.SetProgress(null);

            if (ExampleDebug.Error != null)
                throw ExampleDebug.Error;

            BaseMapGenContext mapContext = context as BaseMapGenContext;
            if (generatedItems != null)
            {
                foreach (MapItem mapItem in mapContext.Map.Items)
                {
                    if (mapItem.IsMoney)
                    {
                        MathUtils.AddToDictionary<int>(generatedItems, -1, mapItem.HiddenValue);
                        MathUtils.AddToDictionary<int>(generatedItems, 0, 1);
                    }
                    else
                        MathUtils.AddToDictionary<int>(generatedItems, mapItem.Value, 1);
                }
            }
            if (generatedEnemies != null)
            {
                foreach (Team team in mapContext.Map.MapTeams)
                {
                    foreach (Character character in team.Players)
                        MathUtils.AddToDictionary<int>(generatedEnemies, character.BaseForm.Species, 1);
                }
            }
        }

        public static void PrintContentAnalysis(Dictionary<int, int> GeneratedItems, Dictionary<int, int> GeneratedEnemies)
        {
            StringBuilder finalString = new StringBuilder();

            finalString.Append(String.Format("Items:") + "\n");
            List<string> printout = new List<string>();
            int total = 0;
            foreach (int key in GeneratedItems.Keys)
            {
                if (key > -1)
                    total += GeneratedItems[key];
            }
            foreach (int key in GeneratedItems.Keys)
            {
                if (key > 0)
                {
                    ItemData entry = DataManager.Instance.GetItem(key);
                    printout.Add(String.Format("    {0:D5} {1:F5} #{2:0000} {3}", GeneratedItems[key], ((float)GeneratedItems[key] / total), key, entry.Name.DefaultText));
                }
                else if (key == 0)
                    printout.Add(String.Format("    {0:D5} {1:F5} {2}", GeneratedItems[key], ((float)GeneratedItems[key] / total), "Money Spawns"));
                else
                    finalString.Append(String.Format("Money: {0}", GeneratedItems[key]) + "\n");
            }
            printout.Sort();

            foreach (string print in printout)
                finalString.Append(print + "\n");
            finalString.Append("\n");

            finalString.Append("Species:" + "\n");
            foreach (int key in GeneratedEnemies.Keys)
            {
                MonsterData data = DataManager.Instance.GetMonster(key);
                finalString.Append(String.Format("    {0:D5} #{1:000} {2}", GeneratedEnemies[key], key, data.Name) + "\n");
            }
            finalString.Append("\n");
            //System.Diagnostics.Debug.WriteLine(String.Format("Gen Logs Printed"));

            System.Diagnostics.Debug.WriteLine(finalString.ToString());
        }

        public static void PrintTimeAnalysis(List<TimeSpan> generationTimes)
        {
            generationTimes.Sort();

            TimeSpan minTime = generationTimes[0];
            TimeSpan medTime = generationTimes[generationTimes.Count / 2];
            TimeSpan maxTime = generationTimes[generationTimes.Count - 1];

            Console.WriteLine("MIN: {0}    MED: {1}    MAX: {2}", minTime.ToString(), medTime.ToString(), maxTime.ToString());

            TimeSpan totalTime = new TimeSpan();
            for (int ii = 0; ii < generationTimes.Count; ii++)
                totalTime += generationTimes[ii];
            Console.WriteLine("Completed in {0}.  View debug log for more details.", totalTime);
        }

        public static void PrintTimeAnalysisTier2(Dictionary<string, List<TimeSpan>> generationTimes, string category)
        {
            List<TimeSpan> flatTimes = new List<TimeSpan>();
            foreach(string key in generationTimes.Keys)
            {
                List<TimeSpan> genTime = generationTimes[key];
                if (genTime.Count > 0)
                {
                    genTime.Sort();

                    TimeSpan minTime = genTime[0];
                    TimeSpan medTime = genTime[genTime.Count / 2];
                    TimeSpan maxTime = genTime[genTime.Count - 1];

                    Debug.WriteLine(String.Format("{3}{4:D3}    MIN: {0}    MED: {1}    MAX: {2}", minTime.ToString(), medTime.ToString(), maxTime.ToString(), category, key));

                    flatTimes.AddRange(genTime);
                }
            }

            {
                flatTimes.Sort();

                TimeSpan minTime = flatTimes[0];
                TimeSpan medTime = flatTimes[flatTimes.Count / 2];
                TimeSpan maxTime = flatTimes[flatTimes.Count - 1];

                Console.WriteLine("ALL    MIN: {0}    MED: {1}    MAX: {2}", minTime.ToString(), medTime.ToString(), maxTime.ToString());

                TimeSpan totalTime = new TimeSpan();
                for (int ii = 0; ii < flatTimes.Count; ii++)
                    totalTime += flatTimes[ii];
                Console.WriteLine("Completed in {0}.  View debug log for more details.", totalTime);
            }
        }

        public static void PrintError(Exception ex)
        {
            Exception innerException = ex;
            int depth = 0;
            while (innerException != null)
            {
                Console.WriteLine("Exception Depth: " + depth);
                Console.WriteLine(innerException.ToString());
                Console.WriteLine();
                innerException = innerException.InnerException;
                depth++;
            }
        }


    }
}
