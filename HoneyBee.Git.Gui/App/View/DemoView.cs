using ImGuiNET;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.App.View
{
    public class DemoView : ImGuiView
    {
        public override string Name => "Demo";


        public DemoView(IContext context) : base(context)
        {
        }

        public override void OnDraw()
        {
            ImGui.ShowDemoWindow();
        }
    }
}
