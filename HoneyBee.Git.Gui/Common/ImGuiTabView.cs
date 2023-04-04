﻿using strange.extensions.context.api;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public class ImGuiTabView : ImGuiView
    {
        public virtual bool Unsave { get; protected set; }

        public virtual string UniqueKey { get; }

        public ImGuiTabView()
        {
        }

        public override void OnDraw()
        {
            base.OnDraw();
        }

    }
}
