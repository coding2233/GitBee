using strange.extensions.mediation.api;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnityEngine
{
    public abstract class MonoBehaviour:IDisposable
    {
        public bool enable { get; set; }


        public MonoBehaviour()
        {
            Awake();
            Start();
        }

        protected virtual void OnEnable()
        {}
        protected virtual void OnDisable()
        { }

        /// A MonoBehaviour Awake handler.
		/// The View will attempt to connect to the Context at this moment.
		protected virtual void Awake()
        {
         
        }

        /// A MonoBehaviour Start handler
        /// If the View is not yet registered with the Context, it will 
        /// attempt to connect again at this moment.
        protected virtual void Start()
        {
          
        }

        /// A MonoBehaviour OnDestroy handler
        /// The View will inform the Context that it is about to be
        /// destroyed.
        protected virtual void OnDestroy()
        {
        }

        public void Dispose()
        {
            OnDestroy();
        }
    }
}
