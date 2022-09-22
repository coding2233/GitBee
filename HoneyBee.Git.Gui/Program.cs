using System;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Wanderer.App
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
       
            var gitGuiContextView =  new AppContextView();
            gitGuiContextView.Loop();
            gitGuiContextView.Dispose();
            gitGuiContextView = null;
        }
    }
}