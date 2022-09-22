using ImGuiNET;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.App.View
{
    public class DemoView : ImGuiView
    {
        public DemoView(ContextView contextView) : base(contextView)
        {
        }

        public override void OnDraw()
        {
            ImGui.ShowDemoWindow();
        }
    }
}
