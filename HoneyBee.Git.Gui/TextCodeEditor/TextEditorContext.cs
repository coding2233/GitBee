using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App;
using Wanderer.TextCodeEditor.Controller;
using Wanderer.TextCodeEditor.Mediator;

namespace Wanderer.TextCodeEditor
{
    internal class TextEditorContext: MVCSContext
    {
        public TextEditorContext(ContextView view) : base(view)
        {

        }

        protected override void mapBindings()
        {
            base.mapBindings();

            //injectionBinder.Bind<IGitRepoService>().To<GitRepoService>().ToSingleton();

            //commandBinder.Bind(AppEvent.ShowGitRepo).To<ShowGitRepoCommand>();
            //commandBinder.Bind(ContextEvent.START).To<GitViewStartCommand>().Once();

            commandBinder.Bind(AppEvent.OpenFile).To<OpenFileCommand>();

            mediationBinder.Bind<TextEditorView>().To<TextEditorMediator>();
        }
    }
}
