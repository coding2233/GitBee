using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
  
    public class Pool<T> where T :class, IPool,new()
    {
        private static Queue<T> m_objects=new Queue<T> ();

        public static T Get()
        {
            T t = null;
            if (m_objects.Count > 0)
            {
                t = m_objects.Dequeue();
            }
            if(null == t)
            {
                t = new T();
            }
            t.OnGet();
            return t;
        }

        public static void Release(T t)
        {
            if (t != null)
            {
                t.OnRelease();
                m_objects.Enqueue(t);
            }
        }


        public static void Release(IEnumerable<T> ts)
        {
            if (ts != null)
            {
                foreach (var item in ts)
                {
                    Release(item);
                }
            }
        }

    }


    public interface IPool
    {
        void OnGet();
        void OnRelease();
    }

}
