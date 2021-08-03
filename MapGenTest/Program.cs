using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence.Script;
using System.Runtime.Versioning;

namespace MapGenTest
{
    class Program
    {
        static void Main()
        {
            if (OperatingSystem.IsWindows())
                enlargeConsole();

            string[] args = Environment.GetCommandLineArgs();
            PathMod.InitExePath(System.IO.Path.GetDirectoryName(args[0]));
            DiagManager.InitInstance();


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
            }

            GraphicsManager.InitParams();
            Text.Init();
            Text.SetCultureCode("en");
            LuaEngine.InitInstance();
            DataManager.InitInstance();
            DataManager.Instance.InitData();
#if DEBUG
            GenContextDebug.OnInit += ExampleDebug.Init;
            GenContextDebug.OnStep += ExampleDebug.OnStep;
            GenContextDebug.OnStepIn += ExampleDebug.StepIn;
            GenContextDebug.OnStepOut += ExampleDebug.StepOut;
#endif
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
    }
}
