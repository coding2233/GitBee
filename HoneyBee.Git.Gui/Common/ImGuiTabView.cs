using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public class ImGuiTabView : ImGuiView
    {
        public virtual bool Unsave { get; protected set; }

        public virtual string UniqueKey { get; }

        public ImGuiTabView(IContext context) : base(context)
        {
        }

        public override void OnDraw()
        {
            base.OnDraw();
        }

    }
}
