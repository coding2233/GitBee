using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public class TaskQueue
    {
        private Dictionary<string,Queue<Task>> m_waitTasks = new Dictionary<string,Queue<Task>>();
        private Dictionary<string,Task> m_cureentTasks = new Dictionary<string, Task>();
        public static void Run(string key,Task task)
        {
            try
            {

            }
            catch (Exception e)
            {
                
            }
        }
    }
}
