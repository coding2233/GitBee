using CliWrap;
using strange.extensions.command.impl;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Wanderer.App.Service;
using Wanderer.App.View;
using Wanderer.Common;
using Wanderer.GitRepository;
using Wanderer.GitRepository.View;

namespace Wanderer.App.Controller
{
    public class StartCommand:EventCommand
    {
        [Inject(ContextKeys.CONTEXT_VIEW)]
        public MonoBehaviour contextView { get; set; }

        [Inject(ContextKeys.CONTEXT)]
        public IContext context { get; set; }
        
        [Inject]
        public IPluginService pluginService { get; set; }

        public override void Execute()
        {
            //主窗口
            ImGuiView.Create<AppImGuiView>(context,0);
            
            //内容主窗口
            ImGuiView.Create<HomeView>(context,0);

            ////运行插件
            //pluginService.Reload();

            //检查更新
            CheckUpdate();
        }

        private async void CheckUpdate()
        {
            try
            {
                if (!System.OperatingSystem.IsWindows())
                {
                    return;
                }

                //https://github.com/woodpecker-ci/woodpecker/releases/latest/download/checksums.txt

                string targetOS = "windows";
                if (System.OperatingSystem.IsLinux())
                {
                    targetOS = "linux";
                }
                else if (System.OperatingSystem.IsMacOS())
                {
                    targetOS = "osx";
                }
                
                // https://github.com/woodpecker-ci/woodpecker/releases/latest/download
                string remoteUrl = "https://gunfire-res.oss-cn-chengdu.aliyuncs.com/gitbee";
                string versionText = "checksums.txt";
                string targetPath = $"{remoteUrl}/{versionText}";

                HttpClient httpClient = new HttpClient();
                var response = await httpClient.SendAsync(new HttpRequestMessage(new HttpMethod("GET"), targetPath));
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        Log.Info(responseContent);
                        var resLines = responseContent.Split('\n');
                        if (resLines == null || resLines.Length == 0)
                        {
                            Log.Warn("Check update content error. {0} -> {1}", targetPath, responseContent);
                            return;
                        }

                        var targetLine = resLines.Where(x => x.Contains("Setup.exe")).FirstOrDefault();
                        if (!string.IsNullOrEmpty(targetLine))
                        {
                            var lineArgs = targetLine.Split(' ');
                            if (lineArgs != null&& lineArgs.Length>0)
                            {
                                string remoteMd5 = lineArgs[0].Trim();
                                string remoteVersion = lineArgs[lineArgs.Length-1].Trim();
                                string localVersion = $"GitBee_{Application.GetVersion()}_Setup.exe";
                                if (!string.IsNullOrEmpty(remoteVersion) && !localVersion.Equals(remoteVersion))
                                {
                                    Application.UpdateDownloadURL = $"{remoteUrl}/{remoteVersion}".Replace("\\","/");

                                    //string dialogContent = "Confirm the update.";
                                    //AppImGuiView.DisplayDialog("GitBee has a new version", dialogContent, "OK", "Cancel", async (dialogContentResult) => {
                                    //    if (dialogContentResult)
                                    //    {
                                    //        targetPath = $"{remoteUrl}/{remoteVersion}";
                                    //        response = await httpClient.SendAsync(new HttpRequestMessage(new HttpMethod("GET"), targetPath));
                                    //        if (response.StatusCode == HttpStatusCode.OK)
                                    //        {
                                    //            var bytes = await response.Content.ReadAsByteArrayAsync();
                                    //            if (bytes != null)
                                    //            {
                                    //                string md5 = Application.GetBytesMd5(bytes);
                                    //                Log.Info("Check update download md5: {0} {1}", remoteMd5, md5);
                                    //                if (md5.Equals(remoteMd5))
                                    //                {
                                    //                    string localTargetPath = Path.Combine(Application.TempDataPath, remoteVersion);
                                    //                    if (File.Exists(localTargetPath))
                                    //                    {
                                    //                        File.Delete(localTargetPath);
                                    //                    }
                                    //                    File.WriteAllBytes(localTargetPath, bytes);

                                    //                    Process.Start(localTargetPath);

                                    //                    //退出当前程序
                                    //                    System.Environment.Exit(0);
                                    //                }
                                    //            }
                                    //            else
                                    //            {
                                    //                Log.Warn("Check update download error. {0} -> {1} -> {2}", targetPath, responseContent, targetLine);
                                    //            }
                                    //        }
                                    //        else
                                    //        {
                                    //            Log.Warn("Check update download network error. {0} -> {1} -> {2}", targetPath, responseContent, targetLine);
                                    //        }

                                    //    }

                                    //});


                                }
                            }
                        }

                        Log.Info("No update.");

                    }
                    else
                    {
                        Log.Warn("Check update read content is null. {0}", targetPath);
                    }
                }
                else
                {
                    Log.Warn("Check update network error. {0}", targetPath);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Check update exception:{0}",e);
            }
            
        }

    }
}
