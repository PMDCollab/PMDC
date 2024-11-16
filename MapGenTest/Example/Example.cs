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
using PMDC.Dungeon;

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

        private static T getRegValueOrDefault<T>(string key, T defaultVal)
        {
            try
            {
                return (T)Registry.GetValue(DiagManager.REG_PATH, key, defaultVal);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            return defaultVal;
        }

        public static void Run()
        {
            loadedZones = new Dictionary<string, ZoneData>();
            try
            {
                List<string> zoneNames = new List<string>();
                foreach(string key in DataManager.Instance.DataIndices[DataManager.DataType.Zone].GetOrderedKeys(false))
                    zoneNames.Add(key);

                int offset = 0;
                string state = "Zones";
                while (true)
                {
                    Console.Clear();
                    Console.WriteLine(state);
                    Console.WriteLine("Choose a zone|ESC=Exit|F2=Stress Test");

                    int longestWidth = 0;
                    for (int ii = offset; ii < zoneNames.Count; ii++)
                    {
                        string label = GetSelectionString(ii, zoneNames[ii]);
                        if (label.Length > longestWidth)
                            longestWidth = label.Length;
                    }
                    int cols = Math.Min(3, MathUtils.DivUp(Console.WindowWidth, longestWidth));
                    int rows = Math.Max(Math.Min(12, zoneNames.Count - offset), MathUtils.DivUp(zoneNames.Count - offset, cols));

                    for (int ii = 0; ii < rows; ii++)
                    {
                        string choiceStr = "";
                        List<string> choiceList = new List<string>();
                        for (int jj = 0; jj < cols; jj++)
                        {
                            int index = ii + rows * jj;
                            if (index + offset < zoneNames.Count)
                            {
                                choiceStr += "{" + jj + "," + "-" + longestWidth + "}  ";
                                choiceList.Add(GetSelectionString(index, zoneNames[index + offset]));
                            }
                        }
                        Console.WriteLine(String.Format(choiceStr, choiceList.ToArray()));
                    }

                    string zoneIndex = getRegValueOrDefault<string>("ZoneChoice", "");
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
                            zoneIndex = zoneNames[key.KeyChar - '0' + offset];
                        if (key.KeyChar >= 'a' && key.KeyChar <= 'z')
                            zoneIndex = zoneNames[key.KeyChar - 'a' + 10 + offset];
                        if (key.Key == ConsoleKey.UpArrow)
                            offset -= 10;
                        if (key.Key == ConsoleKey.DownArrow)
                            offset += 10;
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

                    int structureIndex = getRegValueOrDefault<int>("StructChoice", -1);
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

                    int floorNum = getRegValueOrDefault<int>("FloorChoice", -1);
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
            int mapCount = 0;
            try
            {
                ulong newSeed;
                if (UInt64.TryParse(getRegValueOrDefault<string>("SeedChoice", ""), out newSeed))
                    zoneSeed = newSeed;

                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", zoneSeed.ToString());

                int newMapCount;
                if (Int32.TryParse(getRegValueOrDefault<string>("MapCountChoice", ""), out newMapCount))
                    mapCount = newMapCount;

                Registry.SetValue(DiagManager.REG_PATH, "MapCountChoice", newMapCount.ToString());

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

                        ZoneGenContext newContext = CreateZoneGenContext(zoneSeed, zoneIndex, floorIndex, structure, mapCount);

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
                        Registry.SetValue(DiagManager.REG_PATH, "MapCountChoice", "");
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
                        Console.WriteLine("Specify a n existing mapCount (default 0)");
                        input = Console.ReadLine();
                        int customMapCount;
                        if (Int32.TryParse(input, out customMapCount))
                            mapCount = customMapCount;
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
                    Registry.SetValue(DiagManager.REG_PATH, "MapCountChoice", "");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR at F"+floorIndex.ID+" ZSEED:" + zoneSeed);
                PrintError(ex);
                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                Registry.SetValue(DiagManager.REG_PATH, "MapCountChoice", "");
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

        public static ZoneGenContext CreateZoneGenContext(ulong zoneSeed, string zoneIndex, SegLoc floorIndex, ZoneSegmentBase structure, int mapsLoaded)
        {
            ReNoise totalNoise = new ReNoise(zoneSeed);
            ulong[] doubleSeed = totalNoise.GetTwoUInt64((ulong)floorIndex.Segment);
            ZoneGenContext newContext = CreateZoneGenContextSegment(doubleSeed[0], zoneIndex, floorIndex.Segment, structure);

            SetFloorSeed(newContext, doubleSeed[1], floorIndex, mapsLoaded);

            return newContext;
        }

        public static void SetFloorSeed(ZoneGenContext newContext, ulong noiseSeed, SegLoc floorIndex, int mapsLoaded)
        {
            INoise idNoise = new ReNoise(noiseSeed);
            newContext.CurrentID = floorIndex.ID;
            ulong finalSeed = (ulong)floorIndex.ID;
            finalSeed <<= 32;
            finalSeed |= (ulong)mapsLoaded;
            newContext.Seed = idNoise.GetUInt64(finalSeed);
        }

        public static ZoneGenContext CreateZoneGenContextSegment(ulong structSeed, string zoneIndex, int structureIndex, ZoneSegmentBase structure)
        {
            INoise structNoise = new ReNoise(structSeed);

            ZoneGenContext newContext = new ZoneGenContext();
            newContext.CurrentZone = zoneIndex;
            newContext.CurrentSegment = structureIndex;
            for (int ii = 0; ii < structure.ZoneSteps.Count; ii++)
            {
                ZoneStep zoneStep = structure.ZoneSteps[ii];
                //TODO: find a better way to feed ZoneSteps into full zone segments.
                //Is there a way for them to be stateless?
                //Additionally, the ZoneSteps themselves sometimes hold IGenSteps that are copied over to the layouts.
                //Is that really OK? (I would guess yes because there is no chance by design for them to be mutated when generating...)
                ZoneStep newStep = zoneStep.Instantiate(structNoise.GetUInt64((ulong)ii));
                newContext.ZoneSteps.Add(newStep);
            }
            return newContext;
        }

        public static void StressTestAll(int amount)
        {
            ExampleDebug.Printing = -1;
            List<string> releasedKeys = new List<string>();
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Zone].GetOrderedKeys(false))
            {
                if (DataManager.Instance.DataIndices[DataManager.DataType.Zone].Get(key).Released)
                    releasedKeys.Add(key);
            }

            Dictionary<string, List<TimeSpan>> generationTimes = new Dictionary<string, List<TimeSpan>>();
            foreach (string key in releasedKeys)
                generationTimes[key] = new List<TimeSpan>();
            List<SeedFailure> failingSeeds = new List<SeedFailure>();

            Stopwatch watch = new Stopwatch();

            foreach (string key in releasedKeys)
            {
                Console.WriteLine("Generating zone " + key);
                string zoneIndex = key;
                ZoneData zone = getCachedZone(key);

                for (int ii = 0; ii < amount; ii++)
                {
                    ulong zoneSeed = MathUtils.Rand.NextUInt64();
                    ReNoise totalNoise = new ReNoise(zoneSeed);

                    for (int nn = 0; nn < zone.Segments.Count; nn++)
                    {
                        int structureIndex = nn;
                        ZoneSegmentBase structure = zone.Segments[nn];

                        ulong[] doubleSeed = totalNoise.GetTwoUInt64((ulong)structureIndex);
                        ZoneGenContext zoneContext = CreateZoneGenContextSegment(doubleSeed[0], zoneIndex, structureIndex, structure);

                        GameProgress save = new MainProgress(MathUtils.Rand.NextUInt64(), Guid.NewGuid().ToString());

                        INoise idNoise = new ReNoise(doubleSeed[1]);

                        foreach (int floorId in structure.GetFloorIDs())
                        {
                            int floor = floorId;

                            try
                            {
                                SetFloorSeed(zoneContext, doubleSeed[1], new SegLoc(nn, floorId), 0);

                                TestFloor(watch, save, structure, zoneContext, null, null, null, null, generationTimes[key]);
                            }
                            catch (Exception ex)
                            {
                                failingSeeds.Add(new SeedFailure(ex, new ZoneLoc(zoneIndex, new SegLoc(structureIndex, floor)), zoneSeed));
                            }
                        }
                    }
                }
            }

            PrintTimeAnalysisTier2(generationTimes, failingSeeds, "Z");

            ExampleDebug.Printing = 0;
        }

        public static void StressTestZone(ZoneData zone, string zoneIndex, int amount)
        {
            ExampleDebug.Printing = -1;
            Dictionary<string, List<TimeSpan>> generationTimes = new Dictionary<string, List<TimeSpan>>();
            for (int ii = 0; ii < zone.Segments.Count; ii++)
                generationTimes[ii.ToString("D3")] = new List<TimeSpan>();
            List<SeedFailure> failingSeeds = new List<SeedFailure>();

            Stopwatch watch = new Stopwatch();

            for (int ii = 0; ii < amount; ii++)
            {
                ulong zoneSeed = MathUtils.Rand.NextUInt64();
                ReNoise totalNoise = new ReNoise(zoneSeed);

                for (int nn = 0; nn < zone.Segments.Count; nn++)
                {
                    int structureIndex = nn;
                    ZoneSegmentBase structure = zone.Segments[nn];

                    ulong[] doubleSeed = totalNoise.GetTwoUInt64((ulong)structureIndex);
                    ZoneGenContext zoneContext = CreateZoneGenContextSegment(doubleSeed[0], zoneIndex, structureIndex, structure);

                    GameProgress save = new MainProgress(MathUtils.Rand.NextUInt64(), Guid.NewGuid().ToString());

                    INoise idNoise = new ReNoise(doubleSeed[1]);

                    foreach (int floorId in structure.GetFloorIDs())
                    {
                        int floor = floorId;

                        try
                        {
                            SetFloorSeed(zoneContext, doubleSeed[1], new SegLoc(nn, floorId), 0);

                            TestFloor(watch, save, structure, zoneContext, null, null, null, null, generationTimes[nn.ToString("D3")]);
                        }
                        catch (Exception ex)
                        {
                            failingSeeds.Add(new SeedFailure(ex, new ZoneLoc("", new SegLoc(structureIndex, floor)), zoneSeed));
                        }
                    }
                }
            }

            PrintTimeAnalysisTier2(generationTimes, failingSeeds, "S");

            ExampleDebug.Printing = 0;
        }


        public static void StressTestStructure(ZoneSegmentBase structure, string zoneIndex, int structureIndex, int amount)
        {
            ExampleDebug.Printing = -1;
            List<Dictionary<string, int>> generatedTerrain = new List<Dictionary<string, int>>();
            List<Dictionary<string, int>> generatedItems = new List<Dictionary<string, int>>();
            List<Dictionary<string, int>> generatedEnemies = new List<Dictionary<string, int>>();
            List<Dictionary<string, int>> generatedStats = new List<Dictionary<string, int>>();
            Dictionary<string, List<TimeSpan>> generationTimes = new Dictionary<string, List<TimeSpan>>();
            List<SeedFailure> failingSeeds = new List<SeedFailure>();
            for (int ii = 0; ii < structure.FloorCount; ii++)
            {
                generatedTerrain.Add(new Dictionary<string, int>());
                generatedItems.Add(new Dictionary<string, int>());
                generatedEnemies.Add(new Dictionary<string, int>());
                generatedStats.Add(new Dictionary<string, int>());
                generationTimes[ii.ToString("D3")] = new List<TimeSpan>();
            }

            Stopwatch watch = new Stopwatch();

            int total = 0;
            for (int ii = 0; ii < amount; ii++)
            {
                ulong zoneSeed = MathUtils.Rand.NextUInt64();

                ReNoise totalNoise = new ReNoise(zoneSeed);
                ulong[] doubleSeed = totalNoise.GetTwoUInt64((ulong)structureIndex);
                INoise idNoise = new ReNoise(doubleSeed[1]);

                GameProgress save = new MainProgress(MathUtils.Rand.NextUInt64(), Guid.NewGuid().ToString());
                ZoneGenContext zoneContext = CreateZoneGenContextSegment(doubleSeed[0], zoneIndex, structureIndex, structure);

                foreach (int floorId in structure.GetFloorIDs())
                {
                    int floor = floorId;
                    try
                    {
                        SetFloorSeed(zoneContext, doubleSeed[1], new SegLoc(structureIndex, floorId), 0);

                        TestFloor(watch, save, structure, zoneContext, generatedTerrain[floorId], generatedItems[floorId], generatedEnemies[floorId], generatedStats[floorId], generationTimes[floorId.ToString("D3")]);
                    }
                    catch (Exception ex)
                    {
                        failingSeeds.Add(new SeedFailure(ex, new ZoneLoc("", new SegLoc(-1, floor)), zoneSeed));
                    }
                    total++;
                }
            }


            Dictionary<string, int> totalGeneratedTerrain = new Dictionary<string, int>();
            Dictionary<string, int> totalGeneratedItems = new Dictionary<string, int>();
            Dictionary<string, int> totalGeneratedEnemies = new Dictionary<string, int>();
            Dictionary<string, int> totalGeneratedStats = new Dictionary<string, int>();
            for (int ii = 0; ii < structure.FloorCount; ii++)
            {
                DiagManager.Instance.LogInfo("F" + ii + ":");
                PrintContentAnalysis(amount, generatedTerrain[ii], generatedItems[ii], generatedEnemies[ii], generatedStats[ii]);

                foreach (string key in generatedTerrain[ii].Keys)
                    MathUtils.AddToDictionary<string>(totalGeneratedTerrain, key, generatedTerrain[ii][key]);

                foreach (string key in generatedItems[ii].Keys)
                    MathUtils.AddToDictionary<string>(totalGeneratedItems, key, generatedItems[ii][key]);

                foreach (string key in generatedEnemies[ii].Keys)
                    MathUtils.AddToDictionary<string>(totalGeneratedEnemies, key, generatedEnemies[ii][key]);

                foreach (string key in generatedStats[ii].Keys)
                    MathUtils.AddToDictionary<string>(totalGeneratedStats, key, generatedStats[ii][key]);
            }

            DiagManager.Instance.LogInfo("Overall:");
            PrintContentAnalysis(total, totalGeneratedTerrain, totalGeneratedItems, totalGeneratedEnemies, totalGeneratedStats);

            PrintTimeAnalysisTier2(generationTimes, failingSeeds, "F");
            ExampleDebug.Printing = 0;
        }


        public static void StressTestFloor(ZoneSegmentBase structure, string zoneIndex, SegLoc floorIndex, int amount)
        {
            ExampleDebug.Printing = -1;
            ulong zoneSeed = 0;
            Dictionary<string, int> generatedTerrain = new Dictionary<string, int>();
            Dictionary<string, int> generatedItems = new Dictionary<string, int>();
            Dictionary<string, int> generatedEnemies = new Dictionary<string, int>();
            Dictionary<string, int> generatedStats = new Dictionary<string, int>();
            List<TimeSpan> generationTimes = new List<TimeSpan>();
            List<SeedFailure> failingSeeds = new List<SeedFailure>();
            Stopwatch watch = new Stopwatch();

            for (int ii = 0; ii < amount; ii++)
            {
                try
                {
                    zoneSeed = MathUtils.Rand.NextUInt64();

                    GameProgress save = new MainProgress(MathUtils.Rand.NextUInt64(), Guid.NewGuid().ToString());
                    ZoneGenContext zoneContext = CreateZoneGenContext(zoneSeed, zoneIndex, floorIndex, structure, 0);

                    TestFloor(watch, save, structure, zoneContext, generatedTerrain, generatedItems, generatedEnemies, generatedStats, generationTimes);
                }
                catch (Exception ex)
                {
                    failingSeeds.Add(new SeedFailure(ex, new ZoneLoc("", new SegLoc(-1, -1)), zoneSeed));
                }
            }

            PrintContentAnalysis(amount, generatedTerrain, generatedItems, generatedEnemies, generatedStats);

            PrintTimeAnalysis(generationTimes, failingSeeds);

            ExampleDebug.Printing = 0;
        }

        public static void TestFloor(Stopwatch watch, GameProgress save, ZoneSegmentBase structure, ZoneGenContext zoneContext, Dictionary<string, int> generatedTerrain, Dictionary<string, int> generatedItems, Dictionary<string, int> generatedEnemies, Dictionary<string, int> generatedStats, List<TimeSpan> generationTimes)
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
            if (generatedTerrain != null)
            {
                for (int xx = 0; xx < mapContext.Map.Width; xx++)
                {
                    for (int yy = 0; yy < mapContext.Map.Height; yy++)
                    {
                        MathUtils.AddToDictionary<string>(generatedTerrain, mapContext.Map.Tiles[xx][yy].ID, 1);
                    }
                }
            }
            if (generatedItems != null)
            {
                foreach (MapItem mapItem in mapContext.Map.Items)
                {
                    if (mapItem.IsMoney)
                    {
                        MathUtils.AddToDictionary<string>(generatedItems, "**MONEY**", mapItem.Amount);
                        MathUtils.AddToDictionary<string>(generatedItems, "**MONEYPILE**", 1);
                    }
                    else
                        MathUtils.AddToDictionary<string>(generatedItems, mapItem.Value, 1);
                }
            }
            if (generatedEnemies != null)
            {
                foreach (Team team in mapContext.Map.MapTeams)
                {
                    foreach (Character character in team.Players)
                        MathUtils.AddToDictionary<string>(generatedEnemies, character.BaseForm.Species, 1);
                }
            }
            if (generatedStats != null)
            {
                int unblockTotal = 0;
                for (int xx = 0; xx < mapContext.Map.Width; xx++)
                {
                    for (int yy = 0; yy < mapContext.Map.Height; yy++)
                    {
                        if (!mapContext.TileBlocked(new Loc(xx, yy)))
                            unblockTotal++;
                    }
                }
                MathUtils.AddToDictionary<string>(generatedStats, "unblocked", unblockTotal);

                foreach (SingleCharEvent effect in mapContext.Map.MapEffect.OnMapTurnEnds.EnumerateInOrder())
                {
                    RespawnBaseEvent respawn = effect as RespawnBaseEvent;
                    if (respawn != null)
                    {
                        MathUtils.AddToDictionary<string>(generatedStats, "npc_cap", respawn.MaxFoes);
                        break;
                    }
                }
            }
        }

        public static void PrintContentAnalysis(int gens, Dictionary<string, int> generatedTiles, Dictionary<string, int> generatedItems, Dictionary<string, int> generatedEnemies, Dictionary<string, int> miscStats)
        {
            StringBuilder finalString = new StringBuilder();
            {
                finalString.Append(String.Format("Terrain:") + "\n");
                int terrainTotal = 0;
                foreach (string key in generatedTiles.Keys)
                    terrainTotal += generatedTiles[key];

                foreach (string key in generatedTiles.Keys)
                {
                    finalString.Append(String.Format("    {0:D7} AVG:{1:D4} {2:F5}% {3}", generatedTiles[key], generatedTiles[key] / gens, ((float)generatedTiles[key] * 100 / terrainTotal), key) + "\n");
                }
                finalString.Append("\n");
            }

            {
                finalString.Append(String.Format("Items:") + "\n");
                List<string> printout = new List<string>();
                int total = 0;
                foreach (string key in generatedItems.Keys)
                {
                    if (!String.IsNullOrEmpty(key))
                        total += generatedItems[key];
                }
                foreach (string key in generatedItems.Keys)
                {
                    if (key == "**MONEY**")
                    {
                        finalString.Append(String.Format("Money: {0}", generatedItems[key]) + "\n");
                    }
                    else if (key == "**MONEYPILE**")
                    {
                        printout.Add(String.Format("    {0:D5} {1:F5}% {2}", generatedItems[key], ((float)generatedItems[key] * 100 / total), "Money Spawns"));
                    }
                    else if (!String.IsNullOrEmpty(key))
                    {
                        ItemData entry = DataManager.Instance.GetItem(key);
                        printout.Add(String.Format("    {0:D5} {1:F5}% {2} {3}", generatedItems[key], ((float)generatedItems[key] * 100 / total), key, entry.Name.DefaultText));
                    }
                }
                printout.Sort();

                foreach (string print in printout)
                    finalString.Append(print + "\n");
                finalString.Append("\n");
            }

            {
                finalString.Append("Species:" + "\n");
                int enemyTotal = 0;
                int npc_cap = 0;
                miscStats.TryGetValue("npc_cap", out npc_cap);
                if (npc_cap == 0)
                    npc_cap = 1;
                finalString.Append(String.Format("    Tiles Per NPC: {0}", miscStats["unblocked"] / npc_cap) + "\n");
                foreach (string key in generatedEnemies.Keys)
                    enemyTotal += generatedEnemies[key];
                foreach (string key in generatedEnemies.Keys)
                {
                    MonsterData data = DataManager.Instance.GetMonster(key);
                    finalString.Append(String.Format("    {0:D5} {1:F3}% #{2:000} {3}", generatedEnemies[key], ((float)generatedEnemies[key] * 100 / enemyTotal), data.IndexNum, data.Name) + "\n");
                }
                finalString.Append("\n");
            }
            
            DiagManager.Instance.LogInfo(finalString.ToString());
        }

        public static void PrintTimeAnalysis(List<TimeSpan> generationTimes, List<SeedFailure> failingSeeds)
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

            Console.WriteLine("{0} Failing Seeds", failingSeeds.Count);
            DiagManager.Instance.LogInfo(String.Format("{0} Failing Seeds", failingSeeds.Count));
            foreach (SeedFailure fail in failingSeeds)
                DiagManager.Instance.LogInfo(String.Format("  {0} {1} {2} {3}\n    {4}", fail.loc.ID, fail.loc.StructID.Segment, fail.loc.StructID.ID, fail.zoneSeed, fail.ex.Message));
        }

        public static void PrintTimeAnalysisTier2(Dictionary<string, List<TimeSpan>> generationTimes, List<SeedFailure> failingSeeds, string category)
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


                    TimeSpan totalFlatTime = new TimeSpan();
                    for (int ii = 0; ii < genTime.Count; ii++)
                        totalFlatTime += genTime[ii];

                    DiagManager.Instance.LogInfo(String.Format("{3}{4:D3}    MIN: {0}    MED: {1}    MAX: {2}\nTOTAL:{5}", minTime.ToString(), medTime.ToString(), maxTime.ToString(), category, key, totalFlatTime));

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

                Console.WriteLine("{0} Failing Seeds", failingSeeds.Count);
                DiagManager.Instance.LogInfo(String.Format("{0} Failing Seeds", failingSeeds.Count));
                foreach (SeedFailure fail in failingSeeds)
                    DiagManager.Instance.LogInfo(String.Format("  {0} {1} {2} {3}\n    {4}", fail.loc.ID, fail.loc.StructID.Segment, fail.loc.StructID.ID, fail.zoneSeed, fail.ex.Message));
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

    public class SeedFailure
    {
        public Exception ex;
        public ZoneLoc loc;
        public ulong zoneSeed;

        public SeedFailure(Exception ex, ZoneLoc loc, ulong zoneSeed)
        {
            this.ex = ex;
            this.loc = loc;
            this.zoneSeed = zoneSeed;
        }
    }
}
