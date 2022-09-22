using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.Controller;

namespace Wanderer.App
{
    public class AppContext : MVCSContext
    {
        public AppContext(ContextView contextView) : base(contextView)
        {

        }

        protected override void mapBindings()
        {

            commandBinder.Bind(ContextEvent.START).To<StartCommand>().Once();
        }
    }
}
