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
    }

    //public abstract class GitCommandTabView : ImGuiTabView
    //{
    //    public override string UniqueKey => Name;

    //    protected GitRepoBranchInfo m_branchInfo;

    //    public GitCommandTabView()
    //    {
    //    }

    //    public GitCommandTabView(GitRepoBranchInfo branchInfo)
    //    {
    //        m_branchInfo = branchInfo;
    //    }

    //    protected virtual void DrawBranch()
    //    {
    //        if (m_branchInfo.LocalBranchs != null)
    //        {
    //            DrawBranch(LuaPlugin.GetText("LocalBranch"),m_branchInfo.LocalBranchs,ref m_branchInfo.LocalBranchIndex);
    //        }

    //        if (m_branchInfo.RemoteBranchs != null)
    //        {
    //            DrawBranch(LuaPlugin.GetText("RemoteBranch"), m_branchInfo.RemoteBranchs, ref m_branchInfo.RemoteBranchIndex);
    //        }
    //    }

    //    protected virtual void DrawBranch(string label,string[] items,ref int index)
    //    {
    //        if (ImGui.Combo(label, ref index, items, items.Length))
    //        {

    //        }
    //    }

    //}

    //public class GitPushTabView : GitCommandTabView
    //{
    //    public override string Name => "Git Push";

    //    public GitPushTabView(GitRepoBranchInfo branchInfo):base(branchInfo)
    //    {

    //    }

    //    public override void OnDraw()
    //    {
    //        ImGui.Text("Git push");

    //        DrawBranch();
    //    }
    //}

    public class GitPullCommandView : GitCommandView
    {
        public override string Name => LuaPlugin.GetText("Git Command - Pull");
        public GitPullCommandView(GitRepo gitRepo) : base(gitRepo)
        {
        }

        protected override void OnDrawCommandView()
        {
            if (DrawBranch())
            {
                string localBranch = m_gitRepoBranchInfo.LocalBranchs[m_gitRepoBranchInfo.LocalBranchIndex];
                string remoteBranch = m_gitRepoBranchInfo.RemoteBranchs[m_gitRepoBranchInfo.RemoteBranchIndex];
                m_command = $"git pull {remoteBranch} {localBranch}";
            }

            //    ImGui.SetCursorPosY(ImGui.GetWindowHeight()-ImGui.GetTextLineHeightWithSpacing()*3);
            //    ImGui.Separator();
            //    ImGui.Text("git pull");
            //    ImGui.SameLine();
            //    if (ImGui.Button("xxxx"))
            //    {

            //    }
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
            ImGui.SetCursorPosY(ImGui.GetWindowHeight() - ImGui.GetTextLineHeightWithSpacing() * 2);
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

        protected virtual bool DrawBranch()
        {
            bool branchResult = false;
            GitRepoBranchInfo branchInfo = GetGitRepoBranchInfo();

            if (ImGui.Combo(LuaPlugin.GetText("Branch"), ref branchInfo.LocalBranchIndex, branchInfo.LocalBranchs, branchInfo.LocalBranchs.Length))
            {
                branchResult = true;
            }

            if (ImGui.Combo(LuaPlugin.GetText("Remote"), ref branchInfo.RemoteBranchIndex, branchInfo.RemoteBranchs, branchInfo.RemoteBranchs.Length))
            {
                branchResult = true;
            }

            return branchResult;
        }

        protected GitRepoBranchInfo GetGitRepoBranchInfo()
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
                localBranch.Add(LuaPlugin.GetText("custom..."));
                remoteBranch.Add(LuaPlugin.GetText("custom..."));

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
                process.StartInfo.Arguments = $"-c \"echo '${workdir}';echo '${command}';{command};echo '\n=========>'; echo 'Please type enter to close the window'; read\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.Start();
                process.WaitForExit();
            }
            catch (Exception e)
            {
                Log.Info("RunGitBase exception: {0} fileName:{1} command:{2} workdir:{3}", e, fileName, command, workdir);
            }
        }

    }




}
