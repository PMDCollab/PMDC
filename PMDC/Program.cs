#region Using Statements

using System;
using System.Threading;
using System.Globalization;
using RogueEssence.Content;
using RogueEssence.Data;
using RogueEssence.Dev;
using RogueEssence.Dungeon;
using RogueEssence.Script;
using RogueEssence;
using PMDC.Dev;
using System.Reflection;
using Microsoft.Xna.Framework;
using Avalonia;
using RogueEssence.Ground;
using SDL2;
using RogueElements;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using RogueEssence.LevelGen;

#endregion

namespace PMDC
{
    public static class PMDCServiceExtensions
    {
        public static void AddPMDCEditors(this IServiceCollection collection)
        {
            collection.AddTransient<StatusEffectEditor>();
            collection.AddTransient<MapStatusEditor>();
            collection.AddTransient<SaveVarsZoneStepEditor>();
            collection.AddTransient<FloorNameDropZoneStepEditor>();
            collection.AddTransient<MoneySpawnZoneStepEditor>();
            collection.AddTransient<SpreadHouseZoneStepEditor>();
            collection.AddTransient<SpreadVaultZoneStepEditor>();
            collection.AddTransient<SpreadPlanSpacedEditor>();
            collection.AddTransient<SpreadPlanQuotaEditor>();
            collection.AddTransient<SpreadPlanBaseEditor>();
            collection.AddTransient<CombinedGridRoomStepEditor>();
            collection.AddTransient<BlobWaterStepEditor>();
            collection.AddTransient<PerlinWaterStepEditor>();
            collection.AddTransient<ItemSpawnZoneStepEditor>();
            collection.AddTransient<TeamSpawnZoneStepEditor>();
            collection.AddTransient<TileSpawnZoneStepEditor>();
            collection.AddTransient<AutoTileBaseEditor>();
            collection.AddTransient<DataFolderEditor>();
            collection.AddTransient<AnimDataEditor>();
            collection.AddTransient<SoundEditor>();
            collection.AddTransient<MusicEditor>();
            collection.AddTransient<EntryDataEditor>();
            collection.AddTransient<FrameTypeEditor>();
            collection.AddTransient<MapItemEditor>();
            collection.AddTransient<InvItemEditor>();
            collection.AddTransient<MultiStepSpawnerEditor>();
            collection.AddTransient<PickerSpawnerEditor>();
            collection.AddTransient<TeamContextSpawnerEditor>();
            collection.AddTransient<LoopedTeamSpawnerEditor>();
            collection.AddTransient<ContextSpawnerEditor>();
            collection.AddTransient<MoneyDivSpawnerEditor>();
            collection.AddTransient<TeamStepSpawnerEditor>();
            collection.AddTransient<StepSpawnerEditor>();
            collection.AddTransient<GridPathGridEditor>();
            collection.AddTransient<GridPathCircleEditor>();
            collection.AddTransient<GridPathBranchEditor>();
            collection.AddTransient<AddConnectedRoomsStepEditor>();
            collection.AddTransient<AddDisconnectedRoomsStepEditor>();
            collection.AddTransient<ConnectRoomStepEditor>();
            collection.AddTransient<FloorPathBranchEditor>();
            collection.AddTransient<BaseSpawnStepEditor>();
            collection.AddTransient<MoneySpawnStepEditor>();
            collection.AddTransient<PlaceMobsStepEditor>();
            collection.AddTransient<RoomGenCrossEditor>();
            collection.AddTransient<SizedRoomGenEditor>();
            collection.AddTransient<RoomGenDefaultEditor>();
            collection.AddTransient<BasePowerStateEditor>();
            collection.AddTransient<AdditionalEffectStateEditor>();
            collection.AddTransient<PromoteBranchEditor>();
            collection.AddTransient<MonsterIDEditor>();
            collection.AddTransient<TeamMemberSpawnEditor>();
            collection.AddTransient<TeamMemberSpawnSimpleEditor>();
            collection.AddTransient<MobSpawnEditor>();
            collection.AddTransient<MobSpawnWeakEditor>();
            collection.AddTransient<MobSpawnAltColorEditor>();
            collection.AddTransient<MobSpawnMovesOffEditor>();
            collection.AddTransient<MobSpawnBoostEditor>();
            collection.AddTransient<MobSpawnScaledBoostEditor>();
            collection.AddTransient<MobSpawnItemEditor>();
            collection.AddTransient<MobSpawnInvEditor>();
            collection.AddTransient<MobSpawnLevelScaleEditor>();
            collection.AddTransient<MobSpawnLocEditor>();
            collection.AddTransient<MobSpawnUnrecruitableEditor>();
            collection.AddTransient<MobSpawnFoeConflictEditor>();
            collection.AddTransient<MobSpawnInteractableEditor>();
            collection.AddTransient<MobSpawnLuaTableEditor>();
            collection.AddTransient<MobSpawnDiscriminatorEditor>();
            collection.AddTransient<MobSpawnStatusEditor>();
            collection.AddTransient<MobSpawnScriptEditor>();
            collection.AddTransient<MapTilesEditor>();
            collection.AddTransient<BaseEmitterEditor>();
            collection.AddTransient<ZoneDataEditor>();
            collection.AddTransient<BattleDataEditor>();
            collection.AddTransient<BattleFXEditor>();
            collection.AddTransient<CircleSquareEmitterEditor>();
            collection.AddTransient<CombatActionEditor>();
            collection.AddTransient<ExplosionDataEditor>();
            collection.AddTransient<ShootingEmitterEditor>();
            collection.AddTransient<SkillDataEditor>();
            collection.AddTransient<ColumnAnimEditor>();
            collection.AddTransient<StaticAnimEditor>();
            collection.AddTransient<TypeDictEditor>();
            collection.AddTransient<DictSpawnEditor>();
            collection.AddTransient<PriorityListEditor>();
            collection.AddTransient<PriorityEditor>();
            collection.AddTransient<SegLocEditor>();
            collection.AddTransient<LocEditor>();
            collection.AddTransient<MultiplierEditor>();
            collection.AddTransient<LoopedRandEditor>();
            collection.AddTransient<PresetMultiRandEditor>();
            collection.AddTransient<RandPickerEditor>();
            collection.AddTransient<MultiRandPickerEditor>();
            collection.AddTransient<FlagTypeEditor>();
            collection.AddTransient<ColorEditor>();
            collection.AddTransient<TypeEditor>();
            collection.AddTransient<AliasDataEditor>();
            collection.AddTransient<StringKeyEditor>();
            collection.AddTransient<ArrayEditor>();
            collection.AddTransient<DictionaryEditor>();
            collection.AddTransient<NoDupeListEditor>();
            collection.AddTransient<ListEditor>();
            collection.AddTransient<EnumEditor>();
            collection.AddTransient<GuidEditor>();
            collection.AddTransient<StringEditor>();
            collection.AddTransient<CharEditor>();
            collection.AddTransient<DoubleEditor>();
            collection.AddTransient<SingleEditor>();
            collection.AddTransient<BooleanEditor>();
            collection.AddTransient<IntEditor>();
            collection.AddTransient<ByteEditor>();
            collection.AddTransient<ObjectEditor>();

            collection.AddTransient<CategorySpawnEditor>(sp => new CategorySpawnEditor(sp.GetRequiredService<EditorContext>(), true));
            collection.AddTransient<RangeDictEditor>(sp => new RangeDictEditor(sp.GetRequiredService<EditorContext>(), false, true));
            collection.AddTransient<SpawnListEditor>(sp => new SpawnListEditor(sp.GetRequiredService<EditorContext>()));
            collection.AddTransient<SpawnRangeListEditor>(sp => new SpawnRangeListEditor(sp.GetRequiredService<EditorContext>(), false, true));
            collection.AddTransient<MoneySpawnRangeEditor>(sp => new MoneySpawnRangeEditor(sp.GetRequiredService<EditorContext>(), false, true));
            collection.AddTransient<RandRangeEditor>(sp => new RandRangeEditor(sp.GetRequiredService<EditorContext>(), false, true));
            collection.AddTransient<IntRangeEditor>(sp => new IntRangeEditor(sp.GetRequiredService<EditorContext>(), false, true));
            
            collection.AddTransient<HashSetEditor<BattleActionType>>();
            collection.AddTransient<HashSetEditor<int>>();
            collection.AddTransient<HashSetEditor<string>>();
        }
    }

    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        //[System.Runtime.InteropServices.DllImport("user32.dll")]
        //static extern bool SetProcessDPIAware();

        public static AppBuilder BuildAvaloniaApp() => RogueEssence.Dev.Program.BuildAvaloniaApp();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            InitDllMap();
            //SetProcessDPIAware();
            //TODO: figure out how to set this switch in appconfig
            AppContext.SetSwitch("Switch.System.Runtime.Serialization.SerializationGuard.AllowFileWrites", true);
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Serializer.InitSettings(new SerializerContractResolver(), new UpgradeBinder());

            string[] args = Environment.GetCommandLineArgs();
            PathMod.InitPathMod(args[0]);

            bool logInput = true;
            bool guideBook = false;
            bool guideCsv = false;
            bool printWiki = false;
            GraphicsManager.AssetType convertAssets = GraphicsManager.AssetType.None;
            DataManager.DataType convertIndices = DataManager.DataType.None;
            DataManager.DataType reserializeIndices = DataManager.DataType.None;
            bool resaveDiff = false;
            DataManager.DataType resaveIndices = DataManager.DataType.None;
            string langArgs = "";
            bool dev = false;
            bool devLua = false;
            string quest = "";
            List<string> mod = new List<string>();
            bool buildQuest = false;
            bool loadModXml = true;
            string playInputs = null;
            bool preConvert = false;

            try
            {
                for (int ii = 1; ii < args.Length; ii++)
                {
                    if (args[ii].ToLower() == "-dev")
                    {
                        dev = true;
                        devLua = true;
                        //if (args.Length > ii + 1 && args[ii + 1] == "lua")
                        //{
                        //    devLua = true;
                        //    ii++;
                        //}
                    }
                    else if (args[ii].ToLower() == "-help")
                    {
                        Console.WriteLine("PMDO OPTIONS:");
                        Console.WriteLine("-dev: Runs the game in dev mode.");
                        Console.WriteLine("-lang [en/es/de/zh/ko]: Specify language.");
                        Console.WriteLine("-guide: Print a strategy guide to GUIDE/ as html");
                        Console.WriteLine("-csv: Print a strategy guide to GUIDE/ as csv");
                        Console.WriteLine("-wiki: Print wiki-ready pages.");
                        Console.WriteLine("-asset [path]: Specify a custom path for assets.");
                        Console.WriteLine("-raw [path]: Specify a custom path for raw assets.");
                        Console.WriteLine(
                            "-appdata [path]: Specify a custom path for app data such as saves, mods, logs, configs.  Specify no path and the AppData environment variable will be used.");
                        Console.WriteLine("-quest [folder]: Specify the folder in MODS/ to load as the quest.");
                        Console.WriteLine(
                            "-mod [mod] [...]: Specify the list of folders in MODS/ to load as additional mods.");
                        Console.WriteLine(
                            "-index [monster/skill/item/intrinsic/status/mapstatus/terrain/tile/zone/emote/autotile/element/growthgroup/skillgroup/ai/rank/skin/all]: Reindexes the selected list of data assets.");
                        Console.WriteLine(
                            "-reserialize [monster/skill/item/intrinsic/status/mapstatus/terrain/tile/zone/emote/autotile/element/growthgroup/skillgroup/ai/rank/skin/all]: Reserializes the selected list of data assets.");
                        Console.WriteLine(
                            "-modfile [monster/skill/item/intrinsic/status/mapstatus/terrain/tile/zone/emote/autotile/element/growthgroup/skillgroup/ai/rank/skin/all]: Resaves the selected list of data assets as files. Must specify quest.");
                        Console.WriteLine(
                            "-modpatch [monster/skill/item/intrinsic/status/mapstatus/terrain/tile/zone/emote/autotile/element/growthgroup/skillgroup/ai/rank/skin/all]: Resaves the selected list of data assets as patch files. Must specify quest.");
                        Console.WriteLine(
                            "-convert [font/chara/portrait/tile/item/particle/beam/icon/object/bg/autotile/all]: Converts graphics from the raw asset folder and saves it to the asset folder.");
                        return;
                    }
                    else if (args[ii].ToLower() == "-play" && args.Length > ii + 1)
                    {
                        playInputs = args[ii + 1];
                        ii++;
                    }
                    else if (args[ii].ToLower() == "-nolog")
                        logInput = false;
                    else if (args[ii].ToLower() == "-lang" && args.Length > ii + 1)
                    {
                        langArgs = args[ii + 1];
                        ii++;
                    }
                    else if (args[ii].ToLower() == "-guide")
                        guideBook = true;
                    else if (args[ii].ToLower() == "-csv")
                        guideCsv = true;
                    else if (args[ii].ToLower() == "-wiki")
                        printWiki = true;
                    else if (args[ii].ToLower() == "-asset")
                    {
                        PathMod.ASSET_PATH = Path.GetFullPath(args[ii + 1]);
                        ii++;
                    }
                    else if (args[ii].ToLower() == "-raw")
                    {
                        PathMod.DEV_PATH = Path.GetFullPath(args[ii + 1]);
                        ii++;
                    }
                    else if (args[ii].ToLower() == "-appdata")
                    {
                        string appName = Path.GetFileNameWithoutExtension(args[0]);
                        PathMod.APP_PATH =
                            Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName) +
                            "/";
                        if (args.Length > ii + 1 && !args[ii + 1].StartsWith("-"))
                        {
                            PathMod.APP_PATH = Path.GetFullPath(args[ii + 1]);
                            ii++;
                        }
                    }
                    else if (args[ii].ToLower() == "-quest")
                    {
                        quest = args[ii + 1];
                        loadModXml = false;
                        ii++;
                    }
                    else if (args[ii].ToLower() == "-mod")
                    {
                        int jj = 1;
                        while (args.Length > ii + jj)
                        {
                            if (args[ii + jj].StartsWith("-"))
                                break;
                            else
                                mod.Add(args[ii + jj]);
                            jj++;
                        }

                        loadModXml = false;
                        ii += jj - 1;
                    }
                    else if (args[ii].ToLower() == "-build")
                    {
                        buildQuest = true;
                        loadModXml = false;
                        ii++;
                    }
                    else if (args[ii].ToLower() == "-convert")
                    {
                        int jj = 1;
                        while (args.Length > ii + jj)
                        {
                            GraphicsManager.AssetType conv = GraphicsManager.AssetType.None;
                            foreach (GraphicsManager.AssetType type in
                                     Enum.GetValues(typeof(GraphicsManager.AssetType)))
                            {
                                if (args[ii + jj].ToLower() == type.ToString().ToLower())
                                {
                                    conv = type;
                                    break;
                                }
                            }

                            if (conv != GraphicsManager.AssetType.None)
                                convertAssets |= conv;
                            else
                                break;
                            jj++;
                        }

                        loadModXml = false;
                        ii += jj - 1;
                    }
                    else if (args[ii] == "-modfile")
                    {
                        int jj = 1;
                        while (args.Length > ii + jj)
                        {
                            DataManager.DataType conv = DataManager.DataType.None;
                            foreach (DataManager.DataType type in Enum.GetValues(typeof(DataManager.DataType)))
                            {
                                if (args[ii + jj].ToLower() == type.ToString().ToLower())
                                {
                                    conv = type;
                                    break;
                                }
                            }

                            if (conv != DataManager.DataType.None)
                                resaveIndices |= conv;
                            else
                                break;
                            jj++;
                        }

                        loadModXml = false;
                        ii += jj - 1;
                    }
                    else if (args[ii] == "-modpatch")
                    {
                        resaveDiff = true;
                        int jj = 1;
                        while (args.Length > ii + jj)
                        {
                            DataManager.DataType conv = DataManager.DataType.None;
                            foreach (DataManager.DataType type in Enum.GetValues(typeof(DataManager.DataType)))
                            {
                                if (args[ii + jj].ToLower() == type.ToString().ToLower())
                                {
                                    conv = type;
                                    break;
                                }
                            }

                            if (conv != DataManager.DataType.None)
                                resaveIndices |= conv;
                            else
                                break;
                            jj++;
                        }

                        loadModXml = false;
                        ii += jj - 1;
                    }
                    else if (args[ii].ToLower() == "-index")
                    {
                        int jj = 1;
                        while (args.Length > ii + jj)
                        {
                            DataManager.DataType conv = DataManager.DataType.None;
                            foreach (DataManager.DataType type in Enum.GetValues(typeof(DataManager.DataType)))
                            {
                                if (args[ii + jj].ToLower() == type.ToString().ToLower())
                                {
                                    conv = type;
                                    break;
                                }
                            }

                            if (conv != DataManager.DataType.None)
                                convertIndices |= conv;
                            else
                                break;
                            jj++;
                        }

                        loadModXml = false;
                        ii += jj - 1;
                    }
                    else if (args[ii].ToLower() == "-reserialize")
                    {
                        int jj = 1;
                        while (args.Length > ii + jj)
                        {
                            DataManager.DataType conv = DataManager.DataType.None;
                            foreach (DataManager.DataType type in Enum.GetValues(typeof(DataManager.DataType)))
                            {
                                if (args[ii + jj].ToLower() == type.ToString().ToLower())
                                {
                                    conv = type;
                                    break;
                                }
                            }

                            if (conv != DataManager.DataType.None)
                                reserializeIndices |= conv;
                            else
                                break;
                            jj++;
                        }

                        loadModXml = false;
                        ii += jj - 1;
                    }
                    else if (args[ii].ToLower() == "-preconvert")
                    {
                        preConvert = true;
                        ii++;
                    }
                }

                DiagManager.InitInstance();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
                return;
            }

            try
            {
                DiagManager.Instance.CurSettings = DiagManager.Instance.LoadSettings();

                DiagManager.Instance.DevMode = dev;
                DiagManager.Instance.DebugLua = devLua;

                DiagManager.Instance.LogInfo("=========================================");
                DiagManager.Instance.LogInfo(String.Format("SESSION STARTED: {0}",
                    String.Format("{0:yyyy/MM/dd HH:mm:ss}", DateTime.Now)));
                DiagManager.Instance.LogInfo("Version: " + Versioning.GetVersion().ToString());
                DiagManager.Instance.LogInfo(Versioning.GetDotNetInfo());
                DiagManager.Instance.LogInfo("=========================================");


                PathMod.InitNamespaces();
                GraphicsManager.InitParams();
                DiagManager.Instance.SetupInputs();

                ModHeader newQuest = ModHeader.Invalid;
                ModHeader[] newMods = new ModHeader[0] { };
                if (quest != "")
                {
                    ModHeader header = PathMod.GetModDetails(Path.Combine(PathMod.MODS_PATH, quest));
                    if (header.IsValid())
                    {
                        newQuest = header;
                        DiagManager.Instance.LogInfo(String.Format("Queued quest for loading: \"{0}\".", quest));
                    }
                    else
                        DiagManager.Instance.LogInfo(String.Format(
                            "Cannot find quest \"{0}\" in {1}. Falling back to base game.", quest, PathMod.MODS_PATH));
                }

                if (mod.Count > 0)
                {
                    List<ModHeader> workingMods = new List<ModHeader>();
                    for (int ii = 0; ii < mod.Count; ii++)
                    {
                        ModHeader header = PathMod.GetModDetails(Path.Combine(PathMod.MODS_PATH, mod[ii]));
                        if (header.IsValid())
                        {
                            workingMods.Add(header);
                            DiagManager.Instance.LogInfo(String.Format("Queued mod for loading: \"{0}\".",
                                String.Join(", ", mod[ii])));
                        }
                        else
                        {
                            DiagManager.Instance.LogInfo(String.Format(
                                "Cannot find mod \"{0}\" in {1}. It will be ignored.", mod, PathMod.MODS_PATH));
                            mod.RemoveAt(ii);
                            ii--;
                        }
                    }

                    newMods = workingMods.ToArray();
                }

                if (loadModXml)
                    (newQuest, newMods) = DiagManager.Instance.LoadModSettings();

                List<int> loadOrder = new List<int>();
                List<(ModRelationship, List<ModHeader>)> loadErrors = new List<(ModRelationship, List<ModHeader>)>();
                PathMod.ValidateModLoad(newQuest, newMods, loadOrder, loadErrors);
                PathMod.SetMods(newQuest, newMods, loadOrder);
                if (loadErrors.Count > 0)
                {
                    List<string> errorMsgs = new List<string>();
                    foreach ((ModRelationship, List<ModHeader>) loadError in loadErrors)
                    {
                        List<ModHeader> involved = loadError.Item2;
                        switch (loadError.Item1)
                        {
                            case ModRelationship.Incompatible:
                            {
                                errorMsgs.Add(String.Format("{0} is incompatible with {1}.", involved[0].Namespace,
                                    involved[1].Namespace));
                                errorMsgs.Add("\n");
                            }
                                break;
                            case ModRelationship.DependsOn:
                            {
                                if (String.IsNullOrEmpty(involved[1].Namespace))
                                    errorMsgs.Add(String.Format("{0} depends on game version {1}.",
                                        involved[0].Namespace, involved[1].Version));
                                else
                                    errorMsgs.Add(String.Format("{0} depends on missing mod {1}.",
                                        involved[0].Namespace, involved[1].Namespace));
                                errorMsgs.Add("\n");
                            }
                                break;
                            case ModRelationship.LoadBefore:
                            case ModRelationship.LoadAfter:
                            {
                                List<string> cycle = new List<string>();
                                foreach (ModHeader header in involved)
                                    cycle.Add(header.Namespace);
                                errorMsgs.Add(
                                    String.Format("Load-order loop: {0}.", String.Join(", ", cycle.ToArray())));
                                errorMsgs.Add("\n");
                            }
                                break;
                        }
                    }

                    DiagManager.Instance.LogError(new Exception("Errors detected in mod load:\n" +
                                                                String.Join("", errorMsgs.ToArray())));
                    DiagManager.Instance.LogInfo(
                        String.Format("The game will continue execution with mods loaded, but order will be broken!"));
                }

                DiagManager.Instance.PrintModSettings();


                if (playInputs != null)
                    DiagManager.Instance.LoadInputs(playInputs);

                Text.Init();
                if (langArgs != "")
                {
                    if (langArgs.Length > 0)
                    {
                        DiagManager.Instance.CurSettings.Language = langArgs.ToLower();
                        Text.SetCultureCode(langArgs.ToLower());
                    }
                    else
                        DiagManager.Instance.CurSettings.Language = "en";
                }

                Text.SetCultureCode(DiagManager.Instance.CurSettings.Language == ""
                    ? ""
                    : DiagManager.Instance.CurSettings.Language.ToString());

                if (buildQuest)
                {
                    if (!PathMod.Quest.IsValid())
                    {
                        DiagManager.Instance.LogInfo("No quest specified to build.");
                        return;
                    }

                    //we need the datamanager for this, but only while data is hardcoded
                    //TODO: remove when data is no longer hardcoded
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DataManager.InitInstance();

                    RogueEssence.Dev.DevHelper.MergeQuest(quest);

                    return;
                }

                if (convertAssets != GraphicsManager.AssetType.None)
                {
                    //run conversions
                    using (GameBase game = new GameBase())
                    {
                        GraphicsManager.SetWindowMode(1);
                        GraphicsManager.InitSystem(game.GraphicsDevice);
                        GraphicsManager.RunConversions(convertAssets);
                    }
                    return;
                }

                if (preConvert)
                {
                    using (GameBase game = new GameBase())
                    {
                        GraphicsManager.SetWindowMode(1);
                        GraphicsManager.InitSystem(game.GraphicsDevice);
                        GraphicsManager.RebuildIndices(GraphicsManager.AssetType.All);
                    }

                    DataManager.InitInstance();
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DataManager.Instance.LoadConversions();
                    RogueEssence.Dev.DevHelper.PrepareAssetConversion();
                    return;
                }

                if (resaveIndices != DataManager.DataType.None)
                {
                    if (!PathMod.Quest.IsValid())
                    {
                        DiagManager.Instance.LogInfo("No quest specified to resave.");
                        return;
                    }

                    if (resaveDiff)
                        DiagManager.Instance.LogInfo("Re-saving modded files as jsonpatch");
                    else
                        DiagManager.Instance.LogInfo("Re-saving modded files as json");

                    //we need the datamanager for this, but only while data is hardcoded
                    //TODO: remove when data is no longer hardcoded
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DataManager.InitInstance();
                    DiagManager.Instance.LogInfo("Resaving files");
                    DataManager.InitDataDirs(PathMod.ModPath(""));
                    if (resaveIndices == DataManager.DataType.All)
                        RogueEssence.Dev.DevHelper.ResaveBase(resaveDiff);
                    RogueEssence.Dev.DevHelper.Resave(resaveIndices, resaveDiff);

                    //reindex must follow

                    RogueEssence.Dev.DevHelper.RunIndexing(resaveIndices);

                    DataManager.Instance.UniversalData =
                        DataManager.LoadData<TypeDict<BaseData>>(DataManager.MISC_PATH, "Index", DataManager.DATA_EXT);
                    RogueEssence.Dev.DevHelper.RunExtraIndexing(resaveIndices);
                    return;
                }

                if (reserializeIndices != DataManager.DataType.None)
                {
                    DiagManager.Instance.LogInfo("Beginning Reserialization");

                    using (GameBase game = new GameBase())
                    {
                        GraphicsManager.SetWindowMode(1);
                        GraphicsManager.InitSystem(game.GraphicsDevice);
                        GraphicsManager.RebuildIndices(GraphicsManager.AssetType.All);
                    }

                    //we need the datamanager for this, but only while data is hardcoded
                    //TODO: remove when data is no longer hardcoded
                    DataManager.InitInstance();
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DataManager.Instance.LoadConversions();

                    DataManager.InitDataDirs(PathMod.ModPath(""));
                    //RogueEssence.Dev.DevHelper.ConvertAssetNames();
                    PMDC.Dev.DevHelper.ConvertLua();

                    //load conversions a second time because it mightve changed
                    DataManager.Instance.LoadConversions();
                    RogueEssence.Dev.DevHelper.ReserializeBase();
                    DiagManager.Instance.LogInfo("Reserializing main data");
                    RogueEssence.Dev.DevHelper.Reserialize(reserializeIndices);
                    DiagManager.Instance.LogInfo("Reserializing map data");
                    if ((reserializeIndices & DataManager.DataType.Zone) != DataManager.DataType.None)
                    {
                        RogueEssence.Dev.DevHelper.ReserializeData<Map>(DataManager.DATA_PATH + "Map/",
                            DataManager.MAP_EXT);
                        RogueEssence.Dev.DevHelper.ReserializeData<GroundMap>(DataManager.DATA_PATH + "Ground/",
                            DataManager.GROUND_EXT);
                    }

                    DiagManager.Instance.LogInfo("Reserializing indices");
                    RogueEssence.Dev.DevHelper.RunIndexing(reserializeIndices);

                    DataManager.InitInstance();
                    DataManager.Instance.LoadConversions();

                    DataManager.Instance.UniversalData =
                        DataManager.LoadData<TypeDict<BaseData>>(DataManager.MISC_PATH, "Index", DataManager.DATA_EXT);
                    RogueEssence.Dev.DevHelper.RunExtraIndexing(reserializeIndices);
                    return;
                }

                if (convertIndices != DataManager.DataType.None)
                {
                    //we need the datamanager for this, but only while data is hardcoded
                    //TODO: remove when data is no longer hardcoded
                    DataManager.InitInstance();
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DiagManager.Instance.LogInfo("Reserializing indices");
                    DataManager.InitDataDirs(PathMod.ModPath(""));
                    RogueEssence.Dev.DevHelper.RunIndexing(convertIndices);

                    DataManager.Instance.UniversalData =
                        DataManager.LoadData<TypeDict<BaseData>>(DataManager.MISC_PATH, "Index", DataManager.DATA_EXT);
                    RogueEssence.Dev.DevHelper.RunExtraIndexing(convertIndices);
                    return;
                }


                if (guideBook || guideCsv)
                {
                    //print the guidebook in the chosen language
                    //we need the datamanager for this
                    DataManager.InitInstance();
                    DataManager.Instance.InitData();
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    //just print a guidebook and exit
                    StrategyGuide.PrintMoveGuide(guideCsv);
                    StrategyGuide.PrintItemGuide(guideCsv);
                    StrategyGuide.PrintAbilityGuide(guideCsv);
                    StrategyGuide.PrintEncounterGuide(guideCsv);
                    return;
                }

                if (printWiki)
                {
                    //print the guidebook in the chosen language
                    //we need the datamanager for this
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DataManager.InitInstance();
                    DataManager.Instance.InitData();
                    //just print a guidebook and exit
                    //StrategyGuide.PrintMoveWiki();
                    StrategyGuide.PrintItemWiki();
                    //StrategyGuide.PrintAbilityWiki();
                    //StrategyGuide.PrintMonsterWiki();
                    //StrategyGuide.PrintDungeonWiki();
                    return;
                }


                //Dev.ImportManager.PrintMoveUsers(PathMod.DEV_PATH+"moves.txt");
                //Dev.ImportManager.PrintAbilityUsers(PathMod.DEV_PATH+"abilities.txt");

                logInput = false; //this feature is disabled for now...
                if (DiagManager.Instance.ActiveDebugReplay == null && logInput)
                    DiagManager.Instance.BeginInput();

                if (DiagManager.Instance.DevMode)
                {
                    App.RegisterServices = collection => { collection.AddPMDCEditors(); };

                    App.OnServicesReady = provider => { InitDataEditor(provider); };

                    AppBuilder builder = BuildAvaloniaApp();
                    builder.StartWithClassicDesktopLifetime(args);
                }
                else
                {
                    DiagManager.Instance.DevEditor = new EmptyEditor();
                    using (GameBase game = new GameBase())
                        game.Run();
                }
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogError(ex);
                throw;
            }
        }

        // We used to have to map dlls manually, but FNA has a provisional solution now.
        // Keep these comments for clarity
        public static void InitDllMap()
        {
            //CoreDllMap.Init();
            //Assembly fnaAssembly = Assembly.GetAssembly(typeof(Game));
            //CoreDllMap.Register(fnaAssembly);
            //load SDL first before FNA3D to sidestep multiple dylibs problem
            SDL.SDL_GetPlatform();
        }

        public static void InitDataEditor(IServiceProvider provider)
        {
            DataEditor.Init();

            DataEditor.AddEditor(provider.GetRequiredService<StatusEffectEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MapStatusEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<SaveVarsZoneStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<FloorNameDropZoneStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MoneySpawnZoneStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SpreadHouseZoneStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SpreadVaultZoneStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SpreadPlanSpacedEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SpreadPlanQuotaEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SpreadPlanBaseEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<CombinedGridRoomStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<BlobWaterStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<PerlinWaterStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ItemSpawnZoneStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<TeamSpawnZoneStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<TileSpawnZoneStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<AutoTileBaseEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<DataFolderEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<AnimDataEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SoundEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MusicEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<EntryDataEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<FrameTypeEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MapItemEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<InvItemEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<MultiStepSpawnerEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<PickerSpawnerEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<TeamContextSpawnerEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<LoopedTeamSpawnerEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ContextSpawnerEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MoneyDivSpawnerEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<TeamStepSpawnerEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<StepSpawnerEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<GridPathGridEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<GridPathCircleEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<GridPathBranchEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<AddConnectedRoomsStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<AddDisconnectedRoomsStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ConnectRoomStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<FloorPathBranchEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<BaseSpawnStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MoneySpawnStepEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<PlaceMobsStepEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<RoomGenCrossEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SizedRoomGenEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<RoomGenDefaultEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<BasePowerStateEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<AdditionalEffectStateEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<PromoteBranchEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<MonsterIDEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<TeamMemberSpawnEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<TeamMemberSpawnSimpleEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnWeakEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnAltColorEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnMovesOffEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnBoostEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnScaledBoostEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnItemEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnInvEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnLevelScaleEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnLocEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnUnrecruitableEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnFoeConflictEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnInteractableEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnLuaTableEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnDiscriminatorEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnStatusEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MobSpawnScriptEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<MapTilesEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<BaseEmitterEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ZoneDataEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<BattleDataEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<BattleFXEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<CircleSquareEmitterEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<CombatActionEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ExplosionDataEditor>());
            //DataEditor.AddConverter(new ItemDataConverter());
            //DataEditor.AddConverter(new TileLayerConverter());
            DataEditor.AddEditor(provider.GetRequiredService<ShootingEmitterEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SkillDataEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ColumnAnimEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<StaticAnimEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<TypeDictEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<CategorySpawnEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<DictSpawnEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<RangeDictEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SpawnListEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SpawnRangeListEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<PriorityListEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<PriorityEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SegLocEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<LocEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MultiplierEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<LoopedRandEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<PresetMultiRandEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MoneySpawnRangeEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<RandRangeEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<RandPickerEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<MultiRandPickerEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<IntRangeEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<FlagTypeEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ColorEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<TypeEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<AliasDataEditor>());

            //TODO: there is no parameterless interface for hashset
            //so instead we have to do the painful process of manually adding every hashset of every type we actually use.  ugh
            DataEditor.AddEditor(provider.GetRequiredService<HashSetEditor<BattleActionType>>());
            DataEditor.AddEditor(provider.GetRequiredService<HashSetEditor<int>>());
            DataEditor.AddEditor(provider.GetRequiredService<HashSetEditor<string>>());

            DataEditor.AddEditor(provider.GetRequiredService<StringKeyEditor>());

            DataEditor.AddEditor(provider.GetRequiredService<ArrayEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<DictionaryEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<NoDupeListEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ListEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<EnumEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<GuidEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<StringEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<CharEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<DoubleEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<SingleEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<BooleanEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<IntEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ByteEditor>());
            DataEditor.AddEditor(provider.GetRequiredService<ObjectEditor>());
        }
    }
}