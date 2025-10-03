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

            Serializer.InitSettings(new SerializerContractResolver(), new UpgradeBinder());

            string[] args = Environment.GetCommandLineArgs();
            PathMod.InitPathMod(args[0]);
            bool testExp = false;

            string quest = "";
            List<string> mod = new List<string>();
            bool devLua = false;

            for (int ii = 1; ii < args.Length; ii++)
            {
                if (args[ii].ToLower() == "-lua")
                {
                    devLua = true;
                }
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
                else if (args[ii] == "-exp")
                {
                    //run exp test
                    testExp = true;
                }
                else if (args[ii] == "-expdir")
                {
                    ExpTester.EXP_DIR = System.IO.Path.GetFullPath(PathMod.ExePath + args[ii + 1]);
                    ii++;
                }
                else if (args[ii].ToLower() == "-quest")
                {
                    quest = args[ii + 1];
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
                    ii += jj - 1;
                }
            }

            DiagManager.InitInstance();
            DiagManager.Instance.DevMode = true;
            DiagManager.Instance.DebugLua = devLua;

            PathMod.InitNamespaces();
            GraphicsManager.InitParams();


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

            if (quest != "" || mod.Count > 0)
            {
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
                Console.WriteLine("Press Any Key");
                Console.ReadKey();
            }

            DiagManager.Instance.DevMode = false;

            Text.Init();
            Text.SetCultureCode("en");
            DataManager.InitInstance();
            LuaEngine.InitInstance();
            LuaEngine.Instance.LoadScripts();
            DataManager.Instance.InitData();

            if (testExp)
            {
                ExpTester.Run();
            }
            else
            {
                GenContextDebug.OnInit += ExampleDebug.Init;
                GenContextDebug.OnStep += ExampleDebug.OnStep;
                GenContextDebug.OnStepIn += ExampleDebug.StepIn;
                GenContextDebug.OnStepOut += ExampleDebug.StepOut;
                GenContextDebug.OnError += ExampleDebug.OnError;

                Example.Run();
            }

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


    }
}
