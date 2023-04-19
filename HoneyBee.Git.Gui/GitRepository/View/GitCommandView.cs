using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using ImGuiNET;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using SFB;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wanderer.App;
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer.GitRepository.View
{
    public struct GitRepoBranchInfo
    {
        public bool GetInstance;
        public string[] LocalBranchs;
        public int LocalBranchIndex;
        public string[] Remotes;
        public int RemoteIndex;
        public string[] RemoteBranchs;
        public int RemoteBranchIndex;
        public string CustomString;
        public int LastIndex;
    }

    public class GitPullCommandView : GitCommandView
    {
        public override string Name => LuaPlugin.GetText("Git Command - Pull");
        public GitPullCommandView(GitRepo gitRepo) : base(gitRepo)
        {
        }

        protected override void OnDrawCommandView()
        {
            if (DrawBranch(true))
            {
                string localBranch = m_gitRepoBranchInfo.LocalBranchs[m_gitRepoBranchInfo.LocalBranchIndex].Replace(".","");
                string remoteBranch = m_gitRepoBranchInfo.RemoteBranchs[m_gitRepoBranchInfo.RemoteBranchIndex].Replace(".", "");
                int remoteIndex = remoteBranch.IndexOf("/");
                string remote = "?";
                if (remoteIndex > 0 && remoteIndex < remoteBranch.Length)
                {
                    remote = remoteBranch.Substring(0, remoteIndex);
                    remoteIndex++;
                    remoteBranch = remoteBranch.Substring(remoteIndex, remoteBranch.Length - remoteIndex);
                }
                m_command = $"git pull {remote} {remoteBranch}:{localBranch}";
            }
        }
    }

    public class GitPushCommandView : GitCommandView
    {
        public override string Name => LuaPlugin.GetText("Git Command - Push");

        public GitPushCommandView(GitRepo gitRepo) : base(gitRepo)
        {
            
        }

        protected override void OnDrawCommandView()
        {
            if (DrawBranch())
            {
                string localBranch = m_gitRepoBranchInfo.LocalBranchs[m_gitRepoBranchInfo.LocalBranchIndex].Replace(".", "");
                string remoteBranch = m_gitRepoBranchInfo.RemoteBranchs[m_gitRepoBranchInfo.RemoteBranchIndex].Replace(".", "");
                int remoteIndex = remoteBranch.IndexOf("/");
                string remote = "?";
                if (remoteIndex > 0 && remoteIndex < remoteBranch.Length)
                {
                    remote = remoteBranch.Substring(0, remoteIndex);
                    remoteIndex++;
                    remoteBranch = remoteBranch.Substring(remoteIndex, remoteBranch.Length - remoteIndex);
                }
                m_command = $"git push {remote} {localBranch}:{remoteBranch}";
            }
        }
    }

    public abstract class GitCommandView : ImGuiView
    {
        public override string Name => "Git Command View";

        private static string s_gitInstallPath;
        private static string s_gitPath;
        private static string s_gitBashPath;

        internal static string GIT
        {
            get
            {
                if (string.IsNullOrEmpty(s_gitPath))
                {
                    if (System.OperatingSystem.IsWindows())
                    {
                        if (string.IsNullOrEmpty(GetGitInstallPath()))
                        {
                            s_gitPath = "git";
                        }
                        else
                        {
                            s_gitPath = Path.Combine(s_gitInstallPath, "bin/git.exe");
                        }
                    }
                    else
                    {
                        s_gitPath = "git";
                    }
                }

                return s_gitPath;
            }
        }

        internal static string GITBASH
        {
            get
            {
                if (string.IsNullOrEmpty(s_gitBashPath))
                {
                    if (System.OperatingSystem.IsWindows())
                    {
                        if (string.IsNullOrEmpty(GetGitInstallPath()))
                        {
                            s_gitBashPath = "git-bash";
                        }
                        else
                        {
                            s_gitBashPath = Path.Combine(s_gitInstallPath, "git-bash.exe");
                        }
                    }
                    else
                    {
                        s_gitBashPath = "bash";
                    }
                }

                return s_gitBashPath;
            }
        }

        protected GitRepo m_gitRepo;
        private bool m_showWindow;

        protected GitRepoBranchInfo m_gitRepoBranchInfo;
        protected string m_command;

        //public static List<ViewCommand> ViewCommands { get; internal set; } = new List<ViewCommand>();
        public GitCommandView(GitRepo gitRepo)
        {
            m_gitRepo = gitRepo; 
            m_showWindow = true;
        }

        public override void OnDraw()
        {
            ImGui.OpenPopup(Name);

            var viewport = ImGui.GetMainViewport();
            var workSize = viewport.WorkSize * 0.375f;

            ImGui.SetNextWindowSize(workSize,ImGuiCond.FirstUseEver);
            if (ImGui.BeginPopupModal(Name, ref m_showWindow))
            {
                OnDrawCommandView();
                if (OnDrawExecuteDraw())
                {
                    m_showWindow = false;
                }
            }
            ImGui.EndPopup();

            if (!m_showWindow)
            {
                ImGui.CloseCurrentPopup();
                AppContextView.RemoveView(this);
            }
        }

        protected virtual void OnDrawCommandView()
        {
            
        }

        protected virtual bool OnDrawExecuteDraw()
        {
            ImGui.SetCursorPosY(ImGui.GetWindowHeight() - ImGui.GetTextLineHeightWithSpacing() * 2f);
            ImGui.Separator();
            if (!string.IsNullOrEmpty(m_command))
            {
                ImGui.Text(m_command);
                ImGui.SameLine();
            }
            if (ImGui.Button(LuaPlugin.GetText("Execute")))
            {
                string gitBashFile = GITBASH;
                if (!string.IsNullOrEmpty(gitBashFile)
                    && !string.IsNullOrEmpty(m_command))
                {
                    RunGitBash(gitBashFile, m_command, m_gitRepo==null?null:m_gitRepo.RootPath);
                }

                return true;
            }
            return false;
        }

        protected virtual bool DrawBranch(bool reverse =false, bool disableCustom=false)
        {
            bool branchResult = false;

            if (!m_gitRepoBranchInfo.GetInstance)
            {
                branchResult = true;
            }

            GitRepoBranchInfo branchInfo = GetGitRepoBranchInfo(disableCustom);

            if (reverse)
            {
                if (DrawLocalBranch("Remote", ref branchInfo.RemoteBranchs, ref branchInfo.RemoteBranchIndex,ref branchInfo.CustomString, ref branchInfo.LastIndex, disableCustom))
                {
                    branchResult = true;
                }

                if (DrawLocalBranch("Branch", ref branchInfo.LocalBranchs, ref branchInfo.LocalBranchIndex, ref branchInfo.CustomString, ref branchInfo.LastIndex, disableCustom))
                {
                    branchResult = true;
                }
                
            }
            else
            {
                if (DrawLocalBranch("Branch", ref branchInfo.LocalBranchs, ref branchInfo.LocalBranchIndex, ref branchInfo.CustomString, ref branchInfo.LastIndex, disableCustom))
                {
                    branchResult = true;
                }

                if (DrawLocalBranch("Remote", ref branchInfo.RemoteBranchs, ref branchInfo.RemoteBranchIndex, ref branchInfo.CustomString, ref branchInfo.LastIndex, disableCustom))
                {
                    branchResult = true;
                }
            }
            
            m_gitRepoBranchInfo = branchInfo;
            return branchResult;
        }

        protected virtual bool DrawLocalBranch(string label,ref string[] items,ref int itemIndex,ref string customString,ref int lastIndex, bool disableCustom=false)
        {
            bool result = false;
            string popupKey = $"Draw_{label}_Popup";
            if (ImGui.Combo(LuaPlugin.GetText(label), ref itemIndex, items, items.Length))
            {
                if (!disableCustom && itemIndex == items.Length - 1)
                {
                    lastIndex = m_gitRepoBranchInfo.LocalBranchIndex;
                    customString = items[itemIndex];
                    ImGui.OpenPopup(popupKey);
                }
                result = true;
            }

            // ImGuiWindowFlags.MenuBar
            if (ImGui.BeginPopup(popupKey))
            {
                if (ImGui.InputText(LuaPlugin.GetText("New Branch"), ref customString, 200))
                {
                }
                if (ImGui.Button(LuaPlugin.GetText("OK")))
                {
                    items[itemIndex] = customString + "...";
                    result = true;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button(LuaPlugin.GetText("Cancel")))
                {
                    itemIndex = lastIndex;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            return result;
        }

        protected GitRepoBranchInfo GetGitRepoBranchInfo(bool disableCustom = false)
        {
            if (!m_gitRepoBranchInfo.GetInstance)
            {
                List<string> localBranch = new List<string>();
                List<string> remoteBranch = new List<string>();
                HashSet<string> remotes = new HashSet<string>();
                int localBranchIndex = 0;
                int remoteBranchIndex = 0;
                int remoteIndex = 0;
;                string remoteBranchName = null;

                foreach (var item in m_gitRepo.Repo.Branches)
                {
                    if (item.IsRemote)
                    {
                        remoteBranch.Add(item.FriendlyName);

                        int remoteKeyIndex = item.FriendlyName.IndexOf("/");
                        if (remoteKeyIndex > 0 && remoteKeyIndex < item.FriendlyName.Length)
                        {
                            remotes.Add(item.FriendlyName.Substring(0, remoteKeyIndex));
                        }
                    }
                    else
                    {
                        localBranch.Add(item.FriendlyName);
                        if (item.IsCurrentRepositoryHead)
                        {
                            localBranchIndex = localBranch.Count - 1;

                            remoteBranchName = item.TrackedBranch != null ? item.TrackedBranch.FriendlyName : null;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(remoteBranchName))
                {
                    remoteBranchIndex = remoteBranch.IndexOf(remoteBranchName);
                    foreach (var item in remotes)
                    {
                        if (remoteBranchName.StartsWith($"{item}/"))
                        {
                            break;
                        }
                        remoteIndex++;
                    }
                    
                }

                if (!disableCustom)
                {
                    localBranch.Add(LuaPlugin.GetText("custom..."));
                    remoteBranch.Add(LuaPlugin.GetText("custom..."));
                }

                GitRepoBranchInfo gitRepoBranchInfo = new GitRepoBranchInfo();
                gitRepoBranchInfo.LocalBranchs = localBranch.ToArray();
                gitRepoBranchInfo.LocalBranchIndex = localBranchIndex;
                gitRepoBranchInfo.RemoteBranchs = remoteBranch.ToArray();
                gitRepoBranchInfo.RemoteBranchIndex = remoteBranchIndex;
                gitRepoBranchInfo.Remotes = remotes.ToArray();
                gitRepoBranchInfo.RemoteIndex = remoteIndex;
                gitRepoBranchInfo.GetInstance = true;

                m_gitRepoBranchInfo = gitRepoBranchInfo;
            }

            return m_gitRepoBranchInfo;
        }

        public static void ShowTerminal(string repoPath)
        {
            string readLine = GITBASH;
            if (!string.IsNullOrEmpty(readLine) && readLine.Contains("git-bash"))
            {
                Log.Info("where git-bash : {0}", readLine);

                if (string.IsNullOrEmpty(repoPath))
                {
                    string userPath = Environment.GetEnvironmentVariable("USERPROFILE");
                    if (string.IsNullOrEmpty(userPath))
                    {
                        userPath = "./";
                    }
                    Process.Start(readLine, $"--cd={userPath}");
                }
                else
                {
                    Process.Start(readLine, $"--cd={Path.Combine(repoPath, "../")}");
                }
            }
            else
            {
                Log.Info("下载Git https://github.com/git-for-windows/git/releases/download/v2.38.0.windows.1/MinGit-2.38.0-64-bit.zip");
            }
        }

        public static string GetGitInstallPath()
        {
            if (s_gitInstallPath == null)
            {
                s_gitInstallPath = StandaloneFileBrowser.GetExecFullPath("GitForWindows");
            }
            return s_gitInstallPath;
        }

        internal static void RunGitBash(string fileName, string command, string workdir = null)
        {
            try
            {
                Process process = new Process();
                if (!string.IsNullOrEmpty(workdir))
                {
                    process.StartInfo.WorkingDirectory = workdir;
                }
                else
                {
                    workdir = "";
                }
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = $"-c \"echo '${workdir}';echo '${command}';{command};echo '\n exit =========>'; echo 'Please type enter to close the window'; read\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.Start();
                //Thread.Sleep(300);
                //MoveWindow(process.Handle, (int)ImGui.GetMousePos().X, (int)ImGui.GetMousePos().Y, 900, 600, true);
                process.WaitForExit();
            }
            catch (Exception e)
            {
                Log.Info("RunGitBase exception: {0} fileName:{1} command:{2} workdir:{3}", e, fileName, command, workdir);
            }
        }

        //[DllImport("user32.dll",EntryPoint = "MoveWindow")]
        //public static extern bool MoveWindow(System.IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    }




}
