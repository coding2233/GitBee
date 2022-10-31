using ImGuiNET;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Wanderer.App
{
    internal class Program
    {
        //static string updateUrl = "https://github.com/coding2233/HoneyBee_Git/releases/latest/download/";
        static string updateUrl = "https://github.com/woodpecker-ci/woodpecker/releases/latest/download/";
        static string checksumText = "checksums.txt";

        static string s_processDir;
        static string s_processName;
        static string s_processFileName;

        static void Main(string[] args)
        {
            args = System.Environment.GetCommandLineArgs();
            s_processDir = Path.GetDirectoryName(args[0]);
            s_processName = Process.GetCurrentProcess().ProcessName;
            s_processFileName = $"{s_processName}.exe";

            foreach (var item in args)
            {
                Console.WriteLine($"Program--Main--CommandLineArgs: {item}");
            }

            int isLaunchWindowType = 0;
            string updateURL = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("WindowType="))
                {
                    var splitArgs = args[i].Split('=');
                    if (splitArgs != null && splitArgs.Length == 2)
                    {
                        if (splitArgs[1].Equals("Launch"))
                        {
                            isLaunchWindowType = 1;
                        }
                        else if (splitArgs[1].Equals("Update"))
                        {
                            isLaunchWindowType = 2;
                        }
                    }
                }
                else if (args[i].StartsWith("UpdateURL="))
                {
                    var splitArgs = args[i].Split('=');
                    if (splitArgs != null && splitArgs.Length == 2)
                    {
                        updateURL = splitArgs[1];
                    }
                }
            }


            IMainLoop mainLoop = null;
            if (isLaunchWindowType != 0)
            {
                Log.Info("Hello, Honey Bee - Git - Launch!");

                if (isLaunchWindowType == 1)
                {
                    var launchGraphicsWindow = new LaunchGraphicsWindow();
                    launchGraphicsWindow.SetWindowState(WindowState.Normal);
                    mainLoop = launchGraphicsWindow;
                }
            }
            else
            {
                
                Log.Info("Hello, Honey Bee - Git!");
                string zipFilePath = Path.Combine(s_processDir, "HoneyBee.Git.Gui.zip");
                if (!File.Exists(zipFilePath) || !ExtractUpdate(zipFilePath))
                {
                    var launchProcess = LoadOtherProcess(Path.Combine(s_processDir,s_processFileName), "WindowType=Launch");
                    launchProcess.Start();
                    //标准的场景
                    if (launchProcess != null)
                    {
                        var gitGuiContextView = new AppContextView();
                        //gitGuiContextView.SetWindowState(WindowState.Maximized);
                        launchProcess.Kill();
                        mainLoop = gitGuiContextView;
                    }
                }
                
            }

        
            if (mainLoop != null)
            {
                mainLoop.OnMainLoop();
            }
        }

        static Process LoadOtherProcess(string filePath, string arguments)
        {
            var launchProcess = new Process();
            launchProcess.StartInfo.FileName = filePath;
            launchProcess.StartInfo.Arguments = arguments;
            launchProcess.StartInfo.UseShellExecute = false;
            launchProcess.StartInfo.CreateNoWindow = false;
            launchProcess.StartInfo.RedirectStandardInput = true;
            launchProcess.StartInfo.RedirectStandardOutput = true;
            launchProcess.Start();
            return launchProcess;
        }

        static async void CheckUpdate(Action<string> onCheckCallback)
        {
            string updateURL = null;
            try
            {
                string url = Path.Combine(updateUrl, checksumText);
                HttpClientHandler clientHandler = new HttpClientHandler();
                clientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
                clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
                HttpClient client = new HttpClient(clientHandler);
                //string readText = await client.GetStringAsync(Path.Combine(updateUrl, "checksumText"));
                //Console.WriteLine(readText);
                using HttpResponseMessage response = await client.GetAsync(url);
                Console.WriteLine(response);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var lines = content.Split('\n');
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        if (line.StartsWith("windows_amd64"))
                        {
                            //updateURL = Path.Combine(updateUrl, lineArgs[2]);
                        }

                        //var lineArgs = line.Split(' ');
                        //if (lineArgs[2].StartsWith("woodpecker-agent_windows_amd64"))
                        //{
                        //    url = Path.Combine(updateUrl, lineArgs[2]);
                        //    var downloadResponse = await client.GetAsync(url);
                        //    using (var fs = File.Create(lineArgs[2]))
                        //    {
                        //        await downloadResponse.Content.CopyToAsync(fs);
                        //    }
                        //}
                    }

                    //string platform = Environment.OSVersion.Platform.ToString();
                    //bool is64Os = Environment.Is64BitOperatingSystem;
                    Console.WriteLine(content);
                }


            }
            catch (Exception e)
            {
                Console.WriteLine($"网络错误: {e}");
            }
            finally
            {
                onCheckCallback?.Invoke(updateURL);
            }
        }

        static bool ExtractUpdate(string zipPath)
        {
            //最新的解压程序
            string eufSrcDir = Path.Combine(s_processDir, "ExtractUpdateFiles");
            string eufTargetDir = Path.Combine(s_processDir, "temp/ExtractUpdateFiles");
            if (!CopyDirToTarget(eufSrcDir, eufTargetDir))
            {
                return false;
            }
            //执行解压程序
            string extractUpdateFiles = Path.Combine(eufTargetDir, "ExtractUpdateFiles.exe");
            if (!File.Exists(extractUpdateFiles))
            {
                return false;
            }
            string mainExecPath = Path.Combine(s_processDir, $"{s_processName}.exe");
            LoadOtherProcess(extractUpdateFiles, $"ZipFilePath={zipPath} ExtractDir={s_processDir} ExecPath={mainExecPath}");
            return true;
        }

        static bool CopyDirToTarget(string srcDir, string targetDir)
        {
            bool result = true;

            srcDir= srcDir.Replace("\\","/");
            targetDir = targetDir.Replace("\\", "/");

            if (Directory.Exists(srcDir))
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var files = Directory.GetFiles(srcDir);
                foreach (var item in files)
                {
                    string itemName = Path.GetFileName(item);
                    string itemTarget = Path.Combine(targetDir, itemName);
                    File.Copy(item, itemTarget, true);
                }

                var dirs = Directory.GetDirectories(srcDir);
                foreach (var item in dirs)
                {
                    string dirName = Path.GetFileName(item);
                    bool childResult = CopyDirToTarget(item, Path.Combine(targetDir, dirName));
                    result = result && childResult;
                }
            }
            else
            {
                result = false;
            }

            return result;
        }

    }


    internal interface IMainLoop
    {
        void OnMainLoop();
    }

    internal class LaunchGraphicsWindow : IGraphicsRender,IDisposable, IMainLoop
    {
        GraphicsWindow m_graphicsWindow;
        internal LaunchGraphicsWindow()
        {
            m_graphicsWindow = new GraphicsWindow(this);
        }

        public void Dispose()
        {
            m_graphicsWindow?.Dispose();
        }

        public void OnMainLoop()
        {
            m_graphicsWindow?.Loop();
            Dispose();
        }

        public void OnRender()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);
            ImGui.SetNextWindowViewport(viewport.ID);

            var workSize = viewport.WorkSize;
            if (ImGui.Begin("Diff", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBringToFrontOnFocus))
            {
                //int iconSize = 128;
                //float offset = ImGui.GetStyle().ItemSpacing.Y * 10;
                //ImGui.SetCursorPos(new Vector2((workSize.X - iconSize) * 0.5f, offset));
                //var tptr = DiffProgram.GetOrCreateTexture("bee.png");
                //ImGui.Image(tptr, Vector2.One * 128);

                //ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offset);

                string text = "Honeybee - Git";
                var textSize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPosX((workSize.X - textSize.X) * 0.5f);
                ImGui.Text(text);

                text = "Lightweight git gui tool.";
                textSize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPosX((workSize.X - textSize.X) * 0.5f);
                ImGui.TextDisabled(text);

                ImGui.End();
            }
        }

        public void SetWindowState(WindowState windowState)
        {
            m_graphicsWindow?.SetWindowState(windowState);
        }
    }
}