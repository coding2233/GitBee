using strange.extensions.command.impl;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.View;
using Wanderer.Common;

namespace Wanderer.App.Controller
{
    public class StartCommand:EventCommand
    {
        [Inject(ContextKeys.CONTEXT_VIEW)]
        public ContextView contextView { get; set; }

        public override void Execute()
        {
            ImGuiView.Create<DemoView>(contextView);
        }
    }
}
