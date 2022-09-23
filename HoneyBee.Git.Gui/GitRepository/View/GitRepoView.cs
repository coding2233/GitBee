using ImGuiNET;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.GitRepository.View
{
    internal class GitRepoView : ImGuiView
    {
        public GitRepoView(IContext context) : base(context)
        {
        }

        public override void OnDraw()
        {
            ImGui.Button("Git Repo View");
        }
    }
}
