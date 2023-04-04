/*
 * Copyright 2013 ThirdMotion, Inc.
 *
 *	Licensed under the Apache License, Version 2.0 (the "License");
 *	you may not use this file except in compliance with the License.
 *	You may obtain a copy of the License at
 *
 *		http://www.apache.org/licenses/LICENSE-2.0
 *
 *		Unless required by applicable law or agreed to in writing, software
 *		distributed under the License is distributed on an "AS IS" BASIS,
 *		WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *		See the License for the specific language governing permissions and
 *		limitations under the License.
 */

/**
 * @class strange.extensions.mediation.impl.View
 * 
 * Parent class for all your Views. Extends MonoBehaviour.
 * Bubbles its Awake, Start and OnDestroy events to the
 * ContextView, which allows the Context to know when these
 * critical moments occur in the View lifecycle.
 */

using strange.extensions.context.api;
using strange.extensions.context.impl;
using strange.extensions.mediation.api;
using System;

namespace strange.extensions.mediation.impl
{
    public class View : IView,IDisposable
	{
            /// Leave this value true most of the time. If for some reason you want
            /// a view to exist outside a context you can set it to false. The only
            /// difference is whether an error gets generated.
            private bool _requiresContext = true;
            public bool requiresContext
            {
                get
                {
                    return _requiresContext;
                }
                set
                {
                    _requiresContext = value;
                }
            }

            /// A flag for allowing the View to register with the Context
            /// In general you can ignore this. But some developers have asked for a way of disabling
            ///  View registration with a checkbox from Unity, so here it is.
            /// If you want to expose this capability either
            /// (1) uncomment the commented-out line immediately below, or
            /// (2) subclass View and override the autoRegisterWithContext method using your own custom (public) field.
            //[SerializeField]
            protected bool registerWithContext = true;
            virtual public bool autoRegisterWithContext
            {
                get { return registerWithContext; }
                set { registerWithContext = value; }
            }

            public bool registeredWithContext { get; set; }

            public void Dispose()
            {
                OnDestroy();
            }

            internal virtual void OnAwake()
            {
                registeredWithContext = true;
            }
            public virtual void OnEnable() { }
            public virtual void OnDisable() { }
            protected virtual void OnDestroy() { }
        }
    }

    

