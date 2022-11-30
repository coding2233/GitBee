using strange.extensions.command.impl;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.View;

namespace Wanderer.GitRepository.Controller
{
    public class GitViewStartCommand: EventCommand
    {
        [Inject(ContextKeys.CONTEXT)]
        public IContext context { get; set; }
        public override void Execute()
        {
            ImGuiView.Create<GitCommandView>(context,0);
        }
    }
}
