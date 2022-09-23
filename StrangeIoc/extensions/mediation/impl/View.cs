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

namespace strange.extensions.mediation.impl
{
	public class View :  IView, System.IDisposable
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

		/// Determines the type of event the View is bubbling to the Context
		protected enum BubbleType
		{
			Add,
			Remove,
			Enable,
			Disable
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

		private bool m_enable = true;
		public bool enabled 
		{
			get
			{
				return m_enable;
			}
			set
			{
				m_enable = value;
				if (m_enable)
				{
					OnEnable();
				}
				else
				{
					OnDisable();
				}
			}
		}


		public bool registeredWithContext { get; set; }

		public Mediator mediator { get; set; }

		protected IContext context;
	
		public View(IContext context)
		{
			this.context = context==null ? Context.firstContext:context;

			OnAwake();
		}

		public void Dispose()
		{
			OnDispose();
		}

		protected virtual void OnAwake()
		{
			if (autoRegisterWithContext && !registeredWithContext && shouldRegister)
				bubbleToContext(this, BubbleType.Add);

			OnEnable();
		}


		protected virtual void OnDispose()
		{
			OnDisable();

			bubbleToContext(this, BubbleType.Remove);
		}


		/// A MonoBehaviour OnEnable handler
		/// The View will inform the Context that it was enabled
		protected virtual void OnEnable()
		{
			bubbleToContext(this, BubbleType.Enable);
		}

		/// A MonoBehaviour OnDisable handler
		/// The View will inform the Context that it was disabled
		protected virtual void OnDisable()
		{
			bubbleToContext(this, BubbleType.Disable);
		}

		/// Recurses through Transform.parent to find the GameObject to which ContextView is attached
		/// Has a loop limit of 100 levels.
		/// By default, raises an Exception if no Context is found.
		virtual protected void bubbleToContext(View view, BubbleType type)
		{
		

			if (context != null)
			{
				switch (type)
				{
					case BubbleType.Add:
						context.AddView(view);
						registeredWithContext = true;
						break;
					case BubbleType.Remove:
						context.RemoveView(view);
						break;
					case BubbleType.Enable:
						context.EnableView(view);
						break;
					case BubbleType.Disable:
						context.DisableView(view);
						break;
					default:
						break;
				}

			}
			else
			{
				string msg = "A view couldn't find a context. Loop limit reached.";
				msg += "\nView: " + view.ToString();
				throw new MediationException(msg,
					MediationExceptionType.NO_CONTEXT);
			}
		}

 

        public bool shouldRegister { get { return enabled; } }
	}
}

