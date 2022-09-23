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
            Log.Info("Hello, Honey Bee - Git!");

            var gitGuiContextView =  new AppContextView();
            gitGuiContextView.Loop();
            gitGuiContextView.Dispose();
            gitGuiContextView = null;
        }
    }
}