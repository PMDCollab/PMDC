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
using SDL2;
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

            string[] args = System.Environment.GetCommandLineArgs();
            PathMod.InitExePath(System.IO.Path.GetDirectoryName(args[0]));
            DiagManager.InitInstance();
            DiagManager.Instance.UpgradeBinder = new UpgradeBinder();

            try
            {
                DiagManager.Instance.LogInfo("=========================================");
                DiagManager.Instance.LogInfo(String.Format("SESSION STARTED: {0}", String.Format("{0:yyyy/MM/dd HH:mm:ss}", DateTime.Now)));
                DiagManager.Instance.LogInfo("Version: " + Versioning.GetVersion().ToString());
                DiagManager.Instance.LogInfo(Versioning.GetDotNetInfo());
                DiagManager.Instance.LogInfo("=========================================");


                bool logInput = true;
                bool guideBook = false;
                GraphicsManager.AssetType convertAssets = GraphicsManager.AssetType.None;
                DataManager.DataType convertIndices = DataManager.DataType.None;
                DataManager.DataType reserializeIndices = DataManager.DataType.None;
                string langArgs = "";
                bool dev = false;
                string playInputs = null;
                for (int ii = 1; ii < args.Length; ii++)
                {
                    if (args[ii] == "-dev")
                        dev = true;
                    else if (args[ii] == "-play" && args.Length > ii + 1)
                    {
                        playInputs = args[ii + 1];
                        ii++;
                    }
                    else if (args[ii] == "-lang" && args.Length > ii + 1)
                    {
                        langArgs = args[ii + 1];
                        ii++;
                    }
                    else if (args[ii] == "-nolog")
                        logInput = false;
                    else if (args[ii] == "-guide")
                        guideBook = true;
                    else if (args[ii] == "-asset")
                    {
                        PathMod.ASSET_PATH = System.IO.Path.GetFullPath(PathMod.ExePath + args[ii + 1]);
                        ii++;
                    }
                    else if (args[ii] == "-raw")
                    {
                        PathMod.DEV_PATH = System.IO.Path.GetFullPath(PathMod.ExePath + args[ii + 1]);
                        ii++;
                    }
                    else if (args[ii] == "-mod")
                    {
                        PathMod.Mod = PathMod.MODS_FOLDER + args[ii + 1];
                        ii++;
                    }
                    else if (args[ii] == "-convert")
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
                        ii += jj - 1;
                    }
                    else if (args[ii] == "-index")
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
                        ii += jj - 1;
                    }
                    else if (args[ii] == "-reserialize")
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
                        ii += jj - 1;
                    }
                }


                GraphicsManager.InitParams();

                DiagManager.Instance.DevMode = dev;
                if (playInputs != null)
                    DiagManager.Instance.LoadInputs(playInputs);


                Text.Init();
                if (langArgs != "" && DiagManager.Instance.CurSettings.Language == "")
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

                if (reserializeIndices != DataManager.DataType.None)
                {
                    DiagManager.Instance.LogInfo("Beginning Reserialization");
                    //we need the datamanager for this, but only while data is hardcoded
                    //TODO: remove when data is no longer hardcoded
                    LuaEngine.InitInstance();
                    DataManager.InitInstance();
                    RogueEssence.Dev.DevHelper.ReserializeBase();
                    DiagManager.Instance.LogInfo("Reserializing main data");
                    RogueEssence.Dev.DevHelper.Reserialize(reserializeIndices);
                    DiagManager.Instance.LogInfo("Reserializing map data");
                    RogueEssence.Dev.DevHelper.ReserializeData(DataManager.DATA_PATH + "Map/", DataManager.MAP_EXT);
                    RogueEssence.Dev.DevHelper.ReserializeData(DataManager.DATA_PATH + "Ground/", DataManager.GROUND_EXT);
                    DiagManager.Instance.LogInfo("Reserializing indices");
                    RogueEssence.Dev.DevHelper.RunIndexing(reserializeIndices);
                    return;
                }

                if (convertIndices != DataManager.DataType.None)
                {
                    //we need the datamanager for this, but only while data is hardcoded
                    //TODO: remove when data is no longer hardcoded
                    LuaEngine.InitInstance();
                    DataManager.InitInstance();
                    RogueEssence.Dev.DevHelper.RunIndexing(convertIndices);
                    return;
                }


                if (guideBook)
                {
                    //print the guidebook in the chosen language
                    //we need the datamanager for this
                    LuaEngine.InitInstance();
                    DataManager.InitInstance();
                    DataManager.Instance.InitData();
                    //just print a guidebook and exit
                    StrategyGuide.PrintMoveGuide();
                    StrategyGuide.PrintItemGuide();
                    StrategyGuide.PrintAbilityGuide();
                    StrategyGuide.PrintEncounterGuide();
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

        // TheSpyDog's branch on resolving dllmap for DotNetCore
        // https://github.com/FNA-XNA/FNA/pull/315
        public static void InitDllMap()
        {
            CoreDllMap.Init();
            Assembly fnaAssembly = Assembly.GetAssembly(typeof(Game));
            CoreDllMap.Register(fnaAssembly);
            //load SDL first before FNA3D to sidestep multiple dylibs problem
            SDL.SDL_GetPlatform();
        }

        public static void InitDataEditor()
        {
            DataEditor.Init();
            //DataEditor.AddConverter(new AutoTileBaseConverter());
            DataEditor.AddEditor(new AnimDataEditor());
            DataEditor.AddEditor(new SoundEditor());
            DataEditor.AddEditor(new MusicEditor());
            DataEditor.AddEditor(new EntryDataEditor());
            DataEditor.AddEditor(new FrameTypeEditor());


            DataEditor.AddEditor(new MapTilesEditor());
            DataEditor.AddEditor(new BaseEmitterEditor());
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
            DataEditor.AddEditor(new SpawnListEditor());
            DataEditor.AddEditor(new SpawnRangeListEditor());
            DataEditor.AddEditor(new PriorityListEditor());
            DataEditor.AddEditor(new PriorityEditor());
            DataEditor.AddEditor(new SegLocEditor());
            DataEditor.AddEditor(new LocEditor());
            DataEditor.AddEditor(new IntRangeEditor());
            DataEditor.AddEditor(new FlagTypeEditor());
            DataEditor.AddEditor(new ColorEditor());
            DataEditor.AddEditor(new TypeEditor());
            DataEditor.AddEditor(new ArrayEditor());
            DataEditor.AddEditor(new DictionaryEditor());
            DataEditor.AddEditor(new ListEditor());
            DataEditor.AddEditor(new EnumEditor());
            DataEditor.AddEditor(new StringEditor());
            DataEditor.AddEditor(new DoubleEditor());
            DataEditor.AddEditor(new SingleEditor());
            DataEditor.AddEditor(new BooleanEditor());
            DataEditor.AddEditor(new IntEditor());
            DataEditor.AddEditor(new ByteEditor());
            DataEditor.AddEditor(new ObjectEditor());

            DataEditor.AddConverter(new MapItemConv());
        }
    }
}