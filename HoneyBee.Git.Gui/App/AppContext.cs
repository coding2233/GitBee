using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.App
{
    internal class AppContext : MVCSContext
    {
        public AppContext(ContextView contextView) : base(contextView)
        {

        }
    }
}
