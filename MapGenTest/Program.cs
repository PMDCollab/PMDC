using System;
using System.Collections.Generic;
using System.Text;
using RogueElements;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Content;

namespace MapGenTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WindowWidth = Console.LargestWindowWidth;
            Console.WindowHeight = Console.LargestWindowHeight;
            //Console.OutputEncoding = Encoding.UTF8;

            DiagManager.InitInstance();
            GraphicsManager.InitParams();
            Text.Init();
            Text.SetCultureCode("en");
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

    }
}
