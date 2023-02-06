using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
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


        internal static async void DownloadNetworkTexture(string url, string localPath)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                var response = await httpClient.SendAsync(new HttpRequestMessage(new HttpMethod("GET"), url));
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (bytes != null)
                    {
                        if (File.Exists(localPath))
                        {
                            File.Delete(localPath);
                        }
                        File.WriteAllBytes(localPath, bytes);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("DownloadNetworkTexture e:{0} url:{1} path:{2}",e,url,localPath);
            }
        }

    }
}
