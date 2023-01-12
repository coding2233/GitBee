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

            //运行插件
            pluginService.Reload();

            //检查更新
            CheckUpdate();
        }

        private async void CheckUpdate()
        {
            try
            {
               
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
                        var resLines = responseContent.Split('\n');
                        if (resLines == null || resLines.Length < 3)
                        {
                            Log.Warn("Check update content error. {0} -> {1}", targetPath, responseContent);
                            return;
                        }

                        string commitId = resLines[0].Trim();
                        string commitDesc = resLines[1].Trim();
                        string targetLine = null;
                        for (int i = 2; i < resLines.Length; i++)
                        {
                            var line = resLines[i].Trim();
                            if (!string.IsNullOrEmpty(line) && line.Contains(targetOS, StringComparison.OrdinalIgnoreCase))
                            {
                                targetLine = line;
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(targetLine) || string.IsNullOrEmpty(commitId))
                        {
                            Log.Warn("Check update not find target platform. {0} -> {1}", targetPath, responseContent);
                            return;
                        }

                        string localPath = Path.Combine(Application.UserPath, versionText);
                        string localCommitId = null;
                        if (File.Exists(localPath))
                        {
                            var lines = File.ReadAllLines(localPath);
                            if (lines != null && lines.Length > 0)
                            {
                                localCommitId = lines[0].Trim();
                            }
                        }

                        if (commitId.Equals(localCommitId))
                        {
                            Log.Info("No update.");
                        }
                        //更新
                        else
                        {
                            var targetLineArgs = targetLine.Split(' ');
                            if (targetLineArgs != null)
                            {
                                List<string> tempArgs = new List<string>();
                                foreach (var tempItem in targetLineArgs)
                                {
                                    if (!string.IsNullOrEmpty(tempItem) && !string.IsNullOrEmpty(tempItem.Trim()))
                                    {
                                        tempArgs.Add(tempItem.Trim());
                                    }
                                }
                                targetLineArgs = tempArgs.ToArray();
                            }

                            if (targetLineArgs == null|| targetLineArgs.Length != 2)
                            {
                                Log.Warn("Check update not target line error. {0} -> {1} -> {2}", targetPath, responseContent, targetLine);
                                return;
                            }

                            targetPath = $"{remoteUrl}/{targetLineArgs[1]}";
                            response = await httpClient.SendAsync(new HttpRequestMessage(new HttpMethod("GET"), targetPath));
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                var bytes = await response.Content.ReadAsByteArrayAsync();
                                if (bytes != null)
                                {
                                    string md5 = Application.GetBytesMd5(bytes);
                                    Log.Info("Check update download md5: {0} {1}", targetLineArgs[0], md5);
                                    if (md5.Equals(targetLineArgs[0]))
                                    {
                                        string localTargetPath = Path.Combine(Application.UserPath, targetLineArgs[1]);
                                        if (File.Exists(localTargetPath))
                                        {
                                            File.Delete(localTargetPath);
                                        }
                                        File.WriteAllBytes(localTargetPath, bytes);
                                        string localVersionPath = Path.Combine(Application.UserPath, versionText);
                                        File.WriteAllText(localVersionPath, responseContent);

                                        var stdOutBuffer = new StringBuilder();
                                        var stdErrBuffer = new StringBuilder();
                                        var result = await Cli.Wrap("dotnet").WithArguments("--version")
                                            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                                            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                                            .ExecuteAsync();

                                        var stdOut = stdOutBuffer.ToString();
                                        var stdErr = stdErrBuffer.ToString();
                                        if (!string.IsNullOrEmpty(stdErr))
                                        {
                                            Log.Warn(stdErr);
                                            return;
                                        }

                                        if (string.IsNullOrEmpty(stdOut) || stdOut.Length <= 0)
                                        {
                                            Log.Warn("Can't find dotnet6.");
                                            return;
                                        }

                                        int versionIndex = stdOut.IndexOf(".");
                                        int version = 0;
                                        if (int.TryParse(stdOut.Substring(0, versionIndex), out version))
                                        {

                                        }

                                        Log.Info(stdOut);
                                        if (version < 6)
                                        {
                                            Log.Warn("dotnet version nonsupport: dotnet{0}, please use dotnet6 +", version);
                                            return;
                                        }

                                        //提示更新 -> 强制更新
                                        string extractDir = Path.Combine(Application.DataPath, "update");
                                        foreach (var item in Directory.GetFiles(extractDir))
                                        {
                                            string itemTargetPath = Path.Combine(Application.UserPath,Path.GetFileName(item));
                                            if (File.Exists(itemTargetPath))
                                            {
                                                File.Delete(itemTargetPath);
                                            }
                                            File.Copy(item, itemTargetPath,true);
                                        }
                                        
                                        string extractUpdate = Path.Combine(Application.UserPath, "ExtractUpdateFiles.dll");
                                        try
                                        {
                                            string execPath = System.Environment.GetCommandLineArgs()[0];
                                            Process.Start("dotnet", $"{extractUpdate} ZipFilePath={localTargetPath} ExtractDir={Application.DataPath} ExecPath={execPath}");

                                            //退出当前程序
                                            System.Environment.Exit(0);
                                        }
                                        catch (Exception e)
                                        {
                                            Log.Error("extractUpdate error:{0}",e);
                                        }
                                    }
                                }
                                else
                                {
                                    Log.Warn("Check update download error. {0} -> {1} -> {2}", targetPath, responseContent, targetLine);
                                }
                            }
                            else
                            {
                                Log.Warn("Check update download network error. {0} -> {1} -> {2}", targetPath, responseContent, targetLine);
                            }
                        }
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
