using strange.extensions.command.impl;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.View;

namespace Wanderer.TextCodeEditor.Controller
{
    internal class OpenFileCommand : EventCommand
    {
        [Inject(ContextKeys.CONTEXT)]
        public IContext context { get; set; }

        public override void Execute()
        {
            var path = (string)evt.data;
            Log.Info(path);

            ImGuiView.Create<TextEditorView>(context, 0, path);
        }
    }
}
