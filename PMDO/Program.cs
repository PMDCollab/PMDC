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
using PMDO.Dev;
using System.Reflection;
using Microsoft.Xna.Framework;
using Avalonia;
using SDL2;
#endregion

namespace PMDO
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
            DiagManager.InitInstance();
            GraphicsManager.InitParams();
            Text.Init();
            Text.SetCultureCode("");


            try
            {
                DiagManager.Instance.LogInfo("=========================================");
                DiagManager.Instance.LogInfo(String.Format("SESSION STARTED: {0}", String.Format("{0:yyyy/MM/dd HH:mm:ss}", DateTime.Now)));
                DiagManager.Instance.LogInfo("Version: " + Versioning.GetVersion().ToString());
                DiagManager.Instance.LogInfo(Versioning.GetDotNetInfo());
                DiagManager.Instance.LogInfo("=========================================");

                string[] args = System.Environment.GetCommandLineArgs();
                bool logInput = true;
                bool guideBook = false;
                GraphicsManager.AssetType convertAssets = GraphicsManager.AssetType.None;
                DataManager.DataType convertIndices = DataManager.DataType.None;
                DataManager.DataType reserializeIndices = DataManager.DataType.None;
                string langArgs = "";
                for (int ii = 1; ii < args.Length; ii++)
                {
                    if (args[ii] == "-dev")
                        DiagManager.Instance.DevMode = true;
                    else if (args[ii] == "-play" && args.Length > ii + 1)
                    {
                        DiagManager.Instance.LoadInputs(args[ii + 1]);
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
                    else if (args[ii] == "-mod")
                    {
                        PathMod.Mod = PathMod.MODS_PATH + args[ii + 1];
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
                    //we need the datamanager for this, but only while data is hardcoded
                    //TODO: remove when data is no longer hardcoded
                    LuaEngine.InitInstance();
                    DataManager.InitInstance();
                    RogueEssence.Dev.DevHelper.Reserialize(reserializeIndices, new UpgradeBinder());
                    RogueEssence.Dev.DevHelper.ReserializeData(DataManager.DATA_PATH + "Map/", DataManager.MAP_EXT, new UpgradeBinder());
                    RogueEssence.Dev.DevHelper.ReserializeData(DataManager.DATA_PATH + "Ground/", DataManager.GROUND_EXT, new UpgradeBinder());
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
                throw ex;
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
            DataEditor.AddConverter(new AnimDataEditor());
            DataEditor.AddConverter(new SoundEditor());
            DataEditor.AddConverter(new MusicEditor());
            DataEditor.AddConverter(new EntryDataEditor());
            DataEditor.AddConverter(new FrameTypeEditor());


            DataEditor.AddConverter(new MapTilesEditor());
            DataEditor.AddConverter(new BaseEmitterEditor());
            DataEditor.AddConverter(new BattleDataEditor());
            DataEditor.AddConverter(new BattleFXEditor());
            DataEditor.AddConverter(new CircleSquareEmitterEditor());
            DataEditor.AddConverter(new CombatActionEditor());
            DataEditor.AddConverter(new ExplosionDataEditor());
            //DataEditor.AddConverter(new ItemDataConverter());
            //DataEditor.AddConverter(new TileLayerConverter());
            DataEditor.AddConverter(new ShootingEmitterEditor());
            DataEditor.AddConverter(new SkillDataEditor());
            DataEditor.AddConverter(new ColumnAnimEditor());
            DataEditor.AddConverter(new StaticAnimEditor());
            DataEditor.AddConverter(new TypeDictEditor());
            DataEditor.AddConverter(new SpawnListEditor());
            DataEditor.AddConverter(new SpawnRangeListEditor());
            DataEditor.AddConverter(new PriorityListEditor());
            DataEditor.AddConverter(new PriorityEditor());
            DataEditor.AddConverter(new SegLocEditor());
            DataEditor.AddConverter(new LocEditor());
            DataEditor.AddConverter(new IntRangeEditor());
            DataEditor.AddConverter(new FlagTypeEditor());
            DataEditor.AddConverter(new ColorEditor());
            DataEditor.AddConverter(new TypeEditor());
            DataEditor.AddConverter(new ArrayEditor());
            DataEditor.AddConverter(new DictionaryEditor());
            DataEditor.AddConverter(new ListEditor());
            DataEditor.AddConverter(new EnumEditor());
            DataEditor.AddConverter(new StringEditor());
            DataEditor.AddConverter(new DoubleEditor());
            DataEditor.AddConverter(new SingleEditor());
            DataEditor.AddConverter(new BooleanEditor());
            DataEditor.AddConverter(new IntEditor());
            DataEditor.AddConverter(new ByteEditor());
            DataEditor.AddConverter(new ObjectEditor());
        }
    }
}