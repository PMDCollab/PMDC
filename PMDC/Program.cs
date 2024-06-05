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
using RogueEssence.LevelGen;
#endregion

namespace PMDC
{
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        //[System.Runtime.InteropServices.DllImport("user32.dll")]
        //static extern bool SetProcessDPIAware();

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

            string[] args = Environment.GetCommandLineArgs();
            PathMod.InitPathMod(args[0], "origin");
            DiagManager.InitInstance();
            Serializer.InitSettings(new SerializerContractResolver(), new UpgradeBinder());
            DiagManager.Instance.CurSettings = DiagManager.Instance.LoadSettings();

            try
            {
                DiagManager.Instance.LogInfo("=========================================");
                DiagManager.Instance.LogInfo(String.Format("SESSION STARTED: {0}", String.Format("{0:yyyy/MM/dd HH:mm:ss}", DateTime.Now)));
                DiagManager.Instance.LogInfo("Version: " + Versioning.GetVersion().ToString());
                DiagManager.Instance.LogInfo(Versioning.GetDotNetInfo());
                DiagManager.Instance.LogInfo("=========================================");


                bool logInput = true;
                bool guideBook = false;
                bool guideCsv = false;
                GraphicsManager.AssetType convertAssets = GraphicsManager.AssetType.None;
                DataManager.DataType convertIndices = DataManager.DataType.None;
                DataManager.DataType reserializeIndices = DataManager.DataType.None;
                string langArgs = "";
                bool dev = false;
                bool devLua = false;
                string quest = "";
                List<string> mod = new List<string>();
                bool buildQuest = false;
                bool loadModXml = true;
                string playInputs = null;
                bool preConvert = false;
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
                        Console.WriteLine("-asset [path]: Specify a custom path for assets.");
                        Console.WriteLine("-raw [path]: Specify a custom path for raw assets.");
                        Console.WriteLine("-quest [folder]: Specify the folder in MODS/ to load as the quest.");
                        Console.WriteLine("-mod [mod] [...]: Specify the list of folders in MODS/ to load as additional mods.");
                        Console.WriteLine("-index [monster/skill/item/intrinsic/status/mapstatus/terrain/tile/zone/emote/autotile/element/growthgroup/skillgroup/ai/rank/skin/all]: Reindexes the selected list of data assets.");
                        Console.WriteLine("-reserialize [monster/skill/item/intrinsic/status/mapstatus/terrain/tile/zone/emote/autotile/element/growthgroup/skillgroup/ai/rank/skin/all]: Reserializes the selected list of data assets.");
                        Console.WriteLine("-convert [font/chara/portrait/tile/item/particle/beam/icon/object/bg/autotile/all]: Converts graphics from the raw asset folder and saves it to the asset folder.");
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
                            foreach (GraphicsManager.AssetType type in Enum.GetValues(typeof(GraphicsManager.AssetType)))
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

                DiagManager.Instance.SetupInputs();
                GraphicsManager.InitParams();

                DiagManager.Instance.DevMode = dev;
                DiagManager.Instance.DebugLua = devLua;

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
                        DiagManager.Instance.LogInfo(String.Format("Cannot find quest \"{0}\" in {1}. Falling back to base game.", quest, PathMod.MODS_PATH));
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
                            DiagManager.Instance.LogInfo(String.Format("Queued mod for loading: \"{0}\".", String.Join(", ", mod[ii])));
                        }
                        else
                        {
                            DiagManager.Instance.LogInfo(String.Format("Cannot find mod \"{0}\" in {1}. It will be ignored.", mod, PathMod.MODS_PATH));
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
                                    errorMsgs.Add(String.Format("{0} is incompatible with {1}.", involved[0].Namespace, involved[1].Namespace));
                                    errorMsgs.Add("\n");
                                }
                                break;
                            case ModRelationship.DependsOn:
                                {
                                    if (String.IsNullOrEmpty(involved[1].Namespace))
                                        errorMsgs.Add(String.Format("{0} depends on game version {1}.", involved[0].Namespace, involved[1].Version));
                                    else
                                        errorMsgs.Add(String.Format("{0} depends on missing mod {1}.", involved[0].Namespace, involved[1].Namespace));
                                    errorMsgs.Add("\n");
                                }
                                break;
                            case ModRelationship.LoadBefore:
                            case ModRelationship.LoadAfter:
                                {
                                    List<string> cycle = new List<string>();
                                    foreach (ModHeader header in involved)
                                        cycle.Add(header.Namespace);
                                    errorMsgs.Add(String.Format("Load-order loop: {0}.", String.Join(", ", cycle.ToArray())));
                                    errorMsgs.Add("\n");
                                }
                                break;
                        }
                    }
                    DiagManager.Instance.LogError(new Exception("Errors detected in mod load:\n" + String.Join("", errorMsgs.ToArray())));
                    DiagManager.Instance.LogInfo(String.Format("The game will continue execution with mods loaded, but order will be broken!"));
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
                Text.SetCultureCode(DiagManager.Instance.CurSettings.Language == "" ? "" : DiagManager.Instance.CurSettings.Language.ToString());

                if (buildQuest)
                {
                    if (!PathMod.Quest.IsValid())
                    {
                        DiagManager.Instance.LogInfo("No quest specified to build.");
                        return;
                    }
                    RogueEssence.Dev.DevHelper.MergeQuest(quest);

                    return;
                }

                if (convertAssets != GraphicsManager.AssetType.None)
                {
                    //run conversions
                    using (GameBase game = new GameBase())
                    {
                        GraphicsManager.InitSystem(game.GraphicsDevice);
                        GraphicsManager.RunConversions(convertAssets);
                    }
                    return;
                }

                if (preConvert)
                {
                    using (GameBase game = new GameBase())
                    {
                        GraphicsManager.InitSystem(game.GraphicsDevice);
                        GraphicsManager.RebuildIndices(GraphicsManager.AssetType.All);
                    }

                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DataManager.InitInstance();
                    DataManager.Instance.LoadConversions();
                    RogueEssence.Dev.DevHelper.PrepareAssetConversion();
                    return;
                }

                if (reserializeIndices != DataManager.DataType.None)
                {
                    DiagManager.Instance.LogInfo("Beginning Reserialization");

                    using (GameBase game = new GameBase())
                    {
                        GraphicsManager.InitSystem(game.GraphicsDevice);
                        GraphicsManager.RebuildIndices(GraphicsManager.AssetType.All);
                    }

                    //we need the datamanager for this, but only while data is hardcoded
                    //TODO: remove when data is no longer hardcoded
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DataManager.InitInstance();
                    DataManager.Instance.LoadConversions();

                    DataManager.InitDataDirs(PathMod.ModPath(""));
                    //RogueEssence.Dev.DevHelper.ConvertAssetNames();

                    //load conversions a second time because it mightve changed
                    DataManager.Instance.LoadConversions();
                    RogueEssence.Dev.DevHelper.ReserializeBase();
                    DiagManager.Instance.LogInfo("Reserializing main data");
                    RogueEssence.Dev.DevHelper.Reserialize(reserializeIndices);
                    DiagManager.Instance.LogInfo("Reserializing map data");
                    if ((reserializeIndices & DataManager.DataType.Zone) != DataManager.DataType.None)
                    {
                        RogueEssence.Dev.DevHelper.ReserializeData<Map>(DataManager.DATA_PATH + "Map/", DataManager.MAP_EXT);
                        RogueEssence.Dev.DevHelper.ReserializeData<GroundMap>(DataManager.DATA_PATH + "Ground/", DataManager.GROUND_EXT);
                    }
                    DiagManager.Instance.LogInfo("Reserializing indices");
                    RogueEssence.Dev.DevHelper.RunIndexing(reserializeIndices);

                    DataManager.InitInstance();
                    DataManager.Instance.LoadConversions();

                    DataManager.Instance.UniversalData = DataManager.LoadData<TypeDict<BaseData>>(DataManager.MISC_PATH, "Index", DataManager.DATA_EXT);
                    RogueEssence.Dev.DevHelper.RunExtraIndexing(reserializeIndices);
                    return;
                }

                if (convertIndices != DataManager.DataType.None)
                {
                    //we need the datamanager for this, but only while data is hardcoded
                    //TODO: remove when data is no longer hardcoded
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DataManager.InitInstance();
                    DiagManager.Instance.LogInfo("Reserializing indices");
                    DataManager.InitDataDirs(PathMod.ModPath(""));
                    RogueEssence.Dev.DevHelper.RunIndexing(convertIndices);

                    DataManager.Instance.UniversalData = DataManager.LoadData<TypeDict<BaseData>>(DataManager.MISC_PATH, "Index", DataManager.DATA_EXT);
                    RogueEssence.Dev.DevHelper.RunExtraIndexing(convertIndices);
                    return;
                }


                if (guideBook || guideCsv)
                {
                    //print the guidebook in the chosen language
                    //we need the datamanager for this
                    LuaEngine.InitInstance();
                    LuaEngine.Instance.LoadScripts();
                    DataManager.InitInstance();
                    DataManager.Instance.InitData();
                    //just print a guidebook and exit
                    StrategyGuide.PrintMoveGuide(guideCsv);
                    StrategyGuide.PrintItemGuide(guideCsv);
                    StrategyGuide.PrintAbilityGuide(guideCsv);
                    StrategyGuide.PrintEncounterGuide(guideCsv);
                    return;
                }


                //Dev.ImportManager.PrintMoveUsers(PathMod.DEV_PATH+"moves.txt");
                //Dev.ImportManager.PrintAbilityUsers(PathMod.DEV_PATH+"abilities.txt");

                logInput = false; //this feature is disabled for now...
                if (DiagManager.Instance.ActiveDebugReplay == null && logInput)
                    DiagManager.Instance.BeginInput();

                if (DiagManager.Instance.DevMode)
                {
                    InitDataEditor();
                    AppBuilder builder = RogueEssence.Dev.Program.BuildAvaloniaApp();
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

        public static void InitDataEditor()
        {
            DataEditor.Init();

            DataEditor.AddEditor(new StatusEffectEditor());
            DataEditor.AddEditor(new MapStatusEditor());

            DataEditor.AddEditor(new SaveVarsZoneStepEditor());
            DataEditor.AddEditor(new FloorNameDropZoneStepEditor());
            DataEditor.AddEditor(new MoneySpawnZoneStepEditor());
            DataEditor.AddEditor(new SpreadHouseZoneStepEditor());
            DataEditor.AddEditor(new SpreadVaultZoneStepEditor());
            DataEditor.AddEditor(new SpreadPlanSpacedEditor());
            DataEditor.AddEditor(new SpreadPlanQuotaEditor());
            DataEditor.AddEditor(new SpreadPlanBaseEditor());

            DataEditor.AddEditor(new CombinedGridRoomStepEditor());
            DataEditor.AddEditor(new BlobWaterStepEditor());
            DataEditor.AddEditor(new PerlinWaterStepEditor());
            DataEditor.AddEditor(new ItemSpawnZoneStepEditor());
            DataEditor.AddEditor(new TeamSpawnZoneStepEditor());
            DataEditor.AddEditor(new TileSpawnZoneStepEditor());
            DataEditor.AddEditor(new AutoTileBaseEditor());
            DataEditor.AddEditor(new DataFolderEditor());
            DataEditor.AddEditor(new AnimDataEditor());
            DataEditor.AddEditor(new SoundEditor());
            DataEditor.AddEditor(new MusicEditor());
            DataEditor.AddEditor(new EntryDataEditor());
            DataEditor.AddEditor(new FrameTypeEditor());
            DataEditor.AddEditor(new MapItemEditor());
            DataEditor.AddEditor(new InvItemEditor());

            DataEditor.AddEditor(new MultiStepSpawnerEditor());
            DataEditor.AddEditor(new PickerSpawnerEditor());
            DataEditor.AddEditor(new TeamContextSpawnerEditor());
            DataEditor.AddEditor(new LoopedTeamSpawnerEditor());
            DataEditor.AddEditor(new ContextSpawnerEditor());
            DataEditor.AddEditor(new MoneyDivSpawnerEditor());
            DataEditor.AddEditor(new TeamStepSpawnerEditor());
            DataEditor.AddEditor(new StepSpawnerEditor());

            DataEditor.AddEditor(new GridPathGridEditor());
            DataEditor.AddEditor(new GridPathCircleEditor());
            DataEditor.AddEditor(new GridPathBranchEditor());

            DataEditor.AddEditor(new AddConnectedRoomsStepEditor());
            DataEditor.AddEditor(new AddDisconnectedRoomsStepEditor());
            DataEditor.AddEditor(new ConnectRoomStepEditor());
            DataEditor.AddEditor(new FloorPathBranchEditor());

            DataEditor.AddEditor(new BaseSpawnStepEditor());
            DataEditor.AddEditor(new MoneySpawnStepEditor());
            DataEditor.AddEditor(new PlaceMobsStepEditor());

            DataEditor.AddEditor(new RoomGenCrossEditor());
            DataEditor.AddEditor(new SizedRoomGenEditor());
            DataEditor.AddEditor(new RoomGenDefaultEditor());

            DataEditor.AddEditor(new BasePowerStateEditor());
            DataEditor.AddEditor(new AdditionalEffectStateEditor());

            DataEditor.AddEditor(new PromoteBranchEditor());

            DataEditor.AddEditor(new MonsterIDEditor());

            DataEditor.AddEditor(new TeamMemberSpawnEditor());
            DataEditor.AddEditor(new MobSpawnEditor());
            DataEditor.AddEditor(new MobSpawnWeakEditor());
            DataEditor.AddEditor(new MobSpawnAltColorEditor());
            DataEditor.AddEditor(new MobSpawnMovesOffEditor());
            DataEditor.AddEditor(new MobSpawnBoostEditor());
            DataEditor.AddEditor(new MobSpawnScaledBoostEditor());
            DataEditor.AddEditor(new MobSpawnItemEditor());
            DataEditor.AddEditor(new MobSpawnInvEditor());
            DataEditor.AddEditor(new MobSpawnLevelScaleEditor());
            DataEditor.AddEditor(new MobSpawnLocEditor());
            DataEditor.AddEditor(new MobSpawnUnrecruitableEditor());
            DataEditor.AddEditor(new MobSpawnFoeConflictEditor());
            DataEditor.AddEditor(new MobSpawnInteractableEditor());
            DataEditor.AddEditor(new MobSpawnLuaTableEditor());
            DataEditor.AddEditor(new MobSpawnDiscriminatorEditor());
            DataEditor.AddEditor(new MobSpawnStatusEditor());

            DataEditor.AddEditor(new MapTilesEditor());
            DataEditor.AddEditor(new BaseEmitterEditor());
            DataEditor.AddEditor(new ZoneDataEditor());
            DataEditor.AddEditor(new BattleDataEditor());
            DataEditor.AddEditor(new BattleFXEditor());
            DataEditor.AddEditor(new CircleSquareEmitterEditor());
            DataEditor.AddEditor(new CombatActionEditor());
            DataEditor.AddEditor(new ExplosionDataEditor());
            //DataEditor.AddConverter(new ItemDataConverter());
            //DataEditor.AddConverter(new TileLayerConverter());
            DataEditor.AddEditor(new ShootingEmitterEditor());
            DataEditor.AddEditor(new SkillDataEditor());
            DataEditor.AddEditor(new ColumnAnimEditor());
            DataEditor.AddEditor(new StaticAnimEditor());
            DataEditor.AddEditor(new TypeDictEditor());
            DataEditor.AddEditor(new CategorySpawnEditor(true));
            DataEditor.AddEditor(new DictSpawnEditor());
            DataEditor.AddEditor(new RangeDictEditor(false, true));
            DataEditor.AddEditor(new SpawnListEditor());
            DataEditor.AddEditor(new SpawnRangeListEditor(false, true));
            DataEditor.AddEditor(new PriorityListEditor());
            DataEditor.AddEditor(new PriorityEditor());
            DataEditor.AddEditor(new SegLocEditor());
            DataEditor.AddEditor(new LocEditor());
            DataEditor.AddEditor(new MultiplierEditor());
            DataEditor.AddEditor(new LoopedRandEditor());
            DataEditor.AddEditor(new PresetMultiRandEditor());
            DataEditor.AddEditor(new MoneySpawnRangeEditor(false, true));
            DataEditor.AddEditor(new RandRangeEditor(false, true));
            DataEditor.AddEditor(new RandPickerEditor());
            DataEditor.AddEditor(new MultiRandPickerEditor());
            DataEditor.AddEditor(new IntRangeEditor(false, true));
            DataEditor.AddEditor(new FlagTypeEditor());
            DataEditor.AddEditor(new ColorEditor());
            DataEditor.AddEditor(new TypeEditor());
            DataEditor.AddEditor(new AliasDataEditor());

            //TODO: there is no parameterless interface for hashset
            //so instead we have to do the painful process of manually adding every hashset of every type we actually use.  ugh
            DataEditor.AddEditor(new HashSetEditor<int>());
            DataEditor.AddEditor(new HashSetEditor<string>());

            DataEditor.AddEditor(new StringKeyEditor());

            DataEditor.AddEditor(new ArrayEditor());
            DataEditor.AddEditor(new DictionaryEditor());
            DataEditor.AddEditor(new NoDupeListEditor());
            DataEditor.AddEditor(new ListEditor());
            DataEditor.AddEditor(new EnumEditor());
            DataEditor.AddEditor(new GuidEditor());
            DataEditor.AddEditor(new StringEditor());
            DataEditor.AddEditor(new CharEditor());
            DataEditor.AddEditor(new DoubleEditor());
            DataEditor.AddEditor(new SingleEditor());
            DataEditor.AddEditor(new BooleanEditor());
            DataEditor.AddEditor(new IntEditor());
            DataEditor.AddEditor(new ByteEditor());
            DataEditor.AddEditor(new ObjectEditor());
        }
    }
}