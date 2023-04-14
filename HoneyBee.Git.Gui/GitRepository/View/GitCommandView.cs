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
        public string[] LocalBranchs;
        public int LocalBranchIndex;
        public string[] RemoteBranchs;
        public int RemoteBranchIndex;
    }

    public abstract class GitCommandTabView : ImGuiTabView
    {
        public override string UniqueKey => Name;

        protected GitRepoBranchInfo m_branchInfo;

        public GitCommandTabView()
        {
        }

        public GitCommandTabView(GitRepoBranchInfo branchInfo)
        {
            m_branchInfo = branchInfo;
        }

        protected virtual void DrawBranch()
        {
            if (m_branchInfo.LocalBranchs != null)
            {
                DrawBranch(LuaPlugin.GetText("LocalBranch"),m_branchInfo.LocalBranchs,ref m_branchInfo.LocalBranchIndex);
            }

            if (m_branchInfo.RemoteBranchs != null)
            {
                DrawBranch(LuaPlugin.GetText("RemoteBranch"), m_branchInfo.RemoteBranchs, ref m_branchInfo.RemoteBranchIndex);
            }
        }

        protected virtual void DrawBranch(string label,string[] items,ref int index)
        {
            if (ImGui.Combo(label, ref index, items, items.Length))
            {
                
            }
        }

    }

    public class GitPushTabView : GitCommandTabView
    {
        public override string Name => "Git Push";

        public GitPushTabView(GitRepoBranchInfo branchInfo):base(branchInfo)
        {
            
        }

        public override void OnDraw()
        {
            ImGui.Text("Git push");

            DrawBranch();
        }
    }

    public class GitCommandView : ImGuiView
    {
        public override string Name => "Git Command View";
        private static GitCommandView s_gitCommandView;
        private Dictionary<Type, IGitCommand> m_gitCommands;
        private List<GitCommandTabView> m_gitCommandTabViews;
        private GitCommandTabView m_lastGitCommandTabView;

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

        private bool m_showWindow= true;

        //public static List<ViewCommand> ViewCommands { get; internal set; } = new List<ViewCommand>();
        public GitCommandView(GitRepo gitRepo)
        {
            s_gitCommandView = this;
            m_gitCommands = new Dictionary<Type, IGitCommand>();

            List<string> localBranch = new List<string>();
            List<string> remoteBranch = new List<string>();
            int localBranchIndex = 0;
            int remoteBranchIndex = 0;
            string remoteBranchName = null;

            foreach (var item in gitRepo.Repo.Branches)
            {
                if (item.IsRemote)
                {
                    remoteBranch.Add(item.FriendlyName);
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
            }
            localBranch.Add(LuaPlugin.GetText("Custom"));
            remoteBranch.Add(LuaPlugin.GetText("Custom"));

            GitRepoBranchInfo gitRepoBranchInfo=new GitRepoBranchInfo();
            gitRepoBranchInfo.LocalBranchs = localBranch.ToArray();
            gitRepoBranchInfo.LocalBranchIndex = localBranchIndex;
            gitRepoBranchInfo.RemoteBranchs = remoteBranch.ToArray();
            gitRepoBranchInfo.RemoteBranchIndex = remoteBranchIndex;

            m_gitCommandTabViews = new List<GitCommandTabView>();
            m_gitCommandTabViews.Add(new GitPushTabView(gitRepoBranchInfo));
        }

        public override void OnDraw()
        {
            ImGui.OpenPopup(Name);

            var viewport = ImGui.GetMainViewport();
            var workSize = viewport.WorkSize * 0.375f;

            ImGui.SetNextWindowSize(workSize,ImGuiCond.FirstUseEver);
            if (ImGui.BeginPopupModal(Name, ref m_showWindow))
            {
                if (m_gitCommandTabViews != null&& m_gitCommandTabViews.Count>0)
                {
                    if (ImGui.BeginTabBar("GitCommandView-Tab"))
                    {
                        foreach (var item in m_gitCommandTabViews)
                        {
                            if (ImGui.BeginTabItem(item.UniqueKey))
                            {
                                if (m_lastGitCommandTabView != item)
                                {
                                    if (m_lastGitCommandTabView != null)
                                    {
                                        m_lastGitCommandTabView.OnDisable();
                                    }
                                    item.OnEnable();
                                    m_lastGitCommandTabView = item;
                                }
                                item.OnDraw();
                                ImGui.EndTabItem();
                            }
                        }
                        ImGui.EndTabBar();
                    }

                    ImGui.SetCursorPosY(ImGui.GetWindowHeight()-ImGui.GetTextLineHeightWithSpacing()*3);
                    ImGui.Separator();
                    ImGui.Text("git pull");
                    ImGui.SameLine();
                    if (ImGui.Button("xxxx"))
                    {
                        
                    }
                }
            }
            ImGui.EndPopup();

            if (!m_showWindow)
            {
                ImGui.CloseCurrentPopup();
                AppContextView.RemoveView(this);
            }

            //if (m_gitCommands != null && m_gitCommands.Count > 0)
            //{
            //    var viewport = ImGui.GetMainViewport();

            //    var workSize = viewport.WorkSize * 0.375f;

            //    foreach (var gitCommandItem in m_gitCommands)
            //    {
            //        var gitCommand = gitCommandItem.Value;
            //        if (gitCommand.FirstTimeShow)
            //        {
            //            ImGui.SetNextWindowSize(workSize);
            //            gitCommand.FirstTimeShow = false;
            //        }
            //        ImGui.OpenPopup(gitCommand.Name);
            //        //ImGui.SetNextWindowPos(pos);
            //        bool openCommandDraw = true;
            //        bool openWindow = true;
            //        if (ImGui.BeginPopupModal(gitCommand.Name, ref openWindow))
            //        {
            //            openCommandDraw = gitCommand.Draw();
            //        }

            //        openCommandDraw = openCommandDraw && openWindow;
            //        if (!openCommandDraw)
            //        {
            //            m_gitCommands.Remove(gitCommandItem.Key);
            //            gitCommand.Dispose();
            //            gitCommand = null;
            //            ImGui.CloseCurrentPopup();
            //            break;
            //        }

            //        ImGui.EndPopup();
            //    }
            //}
            //else
            //{
            //    AppContextView.RemoveView(this);
            //}
        }

        private void SetGitCommand<T>(params object[] pArgs) where T : IGitCommand
        {
            if (m_gitCommands.ContainsKey(typeof(T)))
            {
                Log.Warn("Command {0} is runing.", typeof(T));
                return;
            }

            T gitCommand = default(T);
            if (pArgs == null)
            {
                gitCommand = (T)Activator.CreateInstance(typeof(T));
            }
            else
            {
                gitCommand = (T)Activator.CreateInstance(typeof(T), pArgs);
            }
            m_gitCommands.Add(typeof(T), gitCommand);
        }

        public static void RunGitCommandView<T>(params object[] pArgs) where T : IGitCommand
        {
            AppContextView.AddView<GitCommandView>(pArgs[1]);
            //if (s_gitCommandView != null)
            //{
            //    s_gitCommandView.SetGitCommand<T>(pArgs);
            //}
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


        public static void RunGitCommand(string command, GitRepo gitRepo, List<GitCommandViewInfo> viewInfos)
        {
            if (viewInfos != null && viewInfos.Count > 0)
            {
                RunGitCommandView<GitCommandWithUI>(command, gitRepo, viewInfos);
            }
            else
            {
                string gitBashFile = GITBASH;
                if (!string.IsNullOrEmpty(gitBashFile)
                    && !string.IsNullOrEmpty(command))
                {
                    RunGitBash(gitBashFile, command, gitRepo.RootPath);
                }
            }
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

    public enum GitCommandViewType
    {
        INT,
        STRING,
        OPTION,
        BRANCH,
        REMOTE,
    }


    public struct GitCommandViewInfo
    {
        public GitCommandViewType ViewType;
        public string Desc;
        public string OptionValue;
        public bool Amend;
    }

    public interface IGitCommand : IDisposable
    {
        string Name { get; }
        bool FirstTimeShow { get; set; }
        bool Draw();
    }

    public class GitCommandWithUI : IGitCommand
    {
        public interface IData
        {
            public string Result { get; set; }
            public bool AmendValue { get; set; }
        }

        public class NodeData<T> : IData
        {
            public T Value { get; set; }
            public string Result { get; set; }
            public bool AmendValue { get; set; }

        }

        public class NodeData<T1,T2> : IData
        {
            public T1 Value01 { get; set; }
            public T2 Value02 { get; set; }
            public bool AmendValue { get; set; }

            public string Result { get; set; } = "";
        }

        List<GitCommandViewInfo> m_viewInfos;
        List<IData> m_datas;

        string m_command;
        GitRepo m_gitRepo;
        string m_remoteName="";

        public GitCommandWithUI(string command, GitRepo gitRepo, List<GitCommandViewInfo> viewInfos)
        {
            m_command = command;
            m_gitRepo = gitRepo;
            m_viewInfos = viewInfos;
            if (m_viewInfos != null&& m_viewInfos.Count>0)
            {
                m_datas = new List<IData>();
                for (int i = 0; i < m_viewInfos.Count; i++)
                {
                    switch (m_viewInfos[i].ViewType)
                    {
                        case GitCommandViewType.INT:
                            m_datas.Add(new NodeData<int>());
                            break;
                        case GitCommandViewType.STRING:
                            m_datas.Add(new NodeData<string>());
                            break;
                        case GitCommandViewType.OPTION:
                            var optionData = new NodeData<bool, string>();
                            optionData.Value02 = m_viewInfos[i].OptionValue;
                            optionData.Result = "";
                            m_datas.Add(optionData);
                            break;
                        case GitCommandViewType.BRANCH:
                            var localBranchNodeData = new NodeData<string[], int>();
                            m_datas.Add(localBranchNodeData);
                            List<string> localBranch = new List<string>();
                            int localBranchIndex = 0;
                            foreach (var item in gitRepo.Repo.Branches)
                            {
                                if (!item.IsRemote)
                                {
                                    localBranch.Add(item.FriendlyName);
                                    if (item.IsCurrentRepositoryHead)
                                    {
                                        localBranchIndex = localBranch.Count - 1;
                                        localBranchNodeData.Result = item.FriendlyName;
                                    }
                                }
                            }
                            localBranchNodeData.Value01 = localBranch.ToArray();
                            localBranchNodeData.Value02 = localBranchIndex;
                            break;
                        case GitCommandViewType.REMOTE:
                            var remoteBranchNodeData = new NodeData<string[], int>();
                            m_datas.Add(remoteBranchNodeData);
                            List<string> remoteBranch = new List<string>();
                            string trackBranchName = null;
                            foreach (var item in gitRepo.Repo.Branches)
                            {
                                if (item.IsRemote)
                                {
                                    if (item.CanonicalName.EndsWith("/HEAD"))
                                    {
                                        continue;
                                    }
                                    remoteBranch.Add(item.FriendlyName);
                                }
                                else
                                {
                                    if (item.IsCurrentRepositoryHead)
                                    {
                                        trackBranchName = item.TrackedBranch != null ? item.TrackedBranch.FriendlyName : null;
                                    }
                                }
                            }
                            remoteBranchNodeData.Value01 = remoteBranch.ToArray();
                            remoteBranchNodeData.Value02 = string.IsNullOrEmpty(trackBranchName) ? 0 : remoteBranch.IndexOf(trackBranchName);
                            if (remoteBranchNodeData.Value01.Length > 0)
                            {
                                remoteBranchNodeData.Result = remoteBranchNodeData.Value01[remoteBranchNodeData.Value02];
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        
        public string Name => m_command==null? "GitCommandWithUI": m_command;

        public bool FirstTimeShow { get; set; } = true;

        public void Dispose()
        {
            m_viewInfos = null;
        }

        public bool Draw()
        {
            if (m_viewInfos == null|| m_viewInfos.Count==0)
            {
                return false;
            }

            for (int i = 0; i < m_viewInfos.Count; i++)
            {
                switch (m_viewInfos[i].ViewType)
                {
                    case GitCommandViewType.INT:
                        DrawInt(m_datas[i] as NodeData<int>, m_viewInfos[i].Desc);
                        break;
                    case GitCommandViewType.STRING:
                        DrawString(m_datas[i] as NodeData<string>, m_viewInfos[i].Desc);
                        break;
                    case GitCommandViewType.OPTION:
                        DrawOption(m_datas[i] as NodeData<bool,string>, m_viewInfos[i].Desc);
                        break;
                    case GitCommandViewType.BRANCH:
                        var branchNodeData = m_datas[i] as NodeData<string[], int>;
                        DrawCombo(branchNodeData, m_viewInfos[i].Desc, m_viewInfos[i].Amend);
                        break;
                    case GitCommandViewType.REMOTE:
                        var remoteNodeData = m_datas[i] as NodeData<string[], int>;
                        if (DrawCombo(remoteNodeData, m_viewInfos[i].Desc, m_viewInfos[i].Amend) || string.IsNullOrEmpty(m_remoteName))
                        {
                            int remoteNameIndex = remoteNodeData.Result.IndexOf("/");
                            if (remoteNameIndex > 0)
                            {
                                m_remoteName = remoteNodeData.Result.Substring(0, remoteNameIndex);
                                remoteNameIndex++;
                                remoteNodeData.Result = remoteNodeData.Result.Substring(remoteNameIndex, remoteNodeData.Result.Length - remoteNameIndex);
                            }
                        }
                        break;
                    default:
                        break;
                }

            }

            ImGui.Separator();
            string command = m_command;
            ImGui.Text(command);
            //命令替换
            if (m_datas != null)
            {
                command = command.Replace("$remote", m_remoteName);

                for (int i = 0; i < m_datas.Count; i++)
                {
                    command = command.Replace($"${i}", m_datas[i].Result);
                }
            }
            ImGui.Text(command);
            //ImGui.Separator();

            //ImGui.SetCursorPosX(ImGui.GetWindowSize().X-100);
            if (ImGui.Button("OK"))
            {
                GitCommandView.RunGitCommand(command,m_gitRepo,null);
                return false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                return false;
            }

            return true;
        }


        void DrawInt(NodeData<int> data,string desc)
        {
            int oldValue = data.Value;
            if (ImGui.InputInt(desc, ref oldValue))
            {
                data.Value = oldValue;
                data.Result = oldValue.ToString();
            }
        }

        void DrawString(NodeData<string> data, string desc)
        {
            string oldValue = data.Value;
            if (ImGui.InputText(desc, ref oldValue, 100))
            {
                data.Value = oldValue;
                data.Result = oldValue;
            }
        }


        void DrawOption(NodeData<bool, string> data, string desc)
        {
            bool oldValue = data.Value01;
            if (ImGui.Checkbox(desc, ref oldValue))
            {
                data.Value01 = oldValue;
                data.Result = oldValue ? data.Value02 : null;
            }
        }

        bool DrawCombo(NodeData<string[], int> data, string desc,bool amend)
        {
            bool change = false;
            string[] oldItems = data.Value01;
            int oldIndex = data.Value02;
            if (amend)
            {
                bool oldAmendValue = data.AmendValue;
                if (oldAmendValue)
                {
                    string oldResult = data.Result;
                    if (ImGui.InputText(desc, ref oldResult, 200))
                    {
                        data.Result = oldResult;
                        change = true;
                    }
                }
                else
                {
                    if (ImGui.Combo(desc, ref oldIndex, oldItems, oldItems.Length))
                    {
                        data.Value02 = oldIndex;
                        data.Result = oldItems[oldIndex];
                        change = true;
                    }
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"Amend##{desc}Amend", ref oldAmendValue))
                {
                    data.AmendValue = oldAmendValue;
                    change = true;
                }
            }
            else
            {
                if (ImGui.Combo(desc, ref oldIndex, oldItems, oldItems.Length))
                {
                    data.Value02 = oldIndex;
                    data.Result = oldItems[oldIndex];
                    change = true;
                }
            }
            return change;
        }

    }



    public class CloneGitCommand : ProcessGitCommand
    {
        public override  string Name => "Git Clone";
        private static string m_cloneURL="";
        private static string m_localPath="";
        private static string m_branch="";
        private static string m_repoName = "";
        private static bool m_amend = false;

        public CloneGitCommand()
        {
        }

        protected override async void Execute(string arguments, string cliTarget = null)
        {
            m_cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = default(CancellationToken);
            cancellationToken = m_cancellationTokenSource.Token;
            try
            {
                if (string.IsNullOrEmpty(cliTarget))
                {
                    cliTarget = GitCommandView.GIT;
                }
                m_executed = true;
                AddNewLine("$ " + cliTarget + " " + arguments);
                var result = await Cli.Wrap(cliTarget)
               .WithArguments(arguments)
               .WithStandardOutputPipe(PipeTarget.ToDelegate(AddNewLine, Encoding.UTF8))
               .WithStandardErrorPipe(PipeTarget.ToDelegate(AddNewLine, Encoding.UTF8))
               .WithValidation(CommandResultValidation.None)
               .ExecuteAsync(cancellationToken);

                AddNewLine($"{result.ExitTime} ExitCode {result.ExitCode}");
            }
            catch (Exception e)
            {
                AddNewLine($"git {arguments} exception: {e}");
            }
            finally
            {
                if (m_cancellationTokenSource != null)
                {
                    m_cancellationTokenSource.Dispose();
                    m_cancellationTokenSource = null;
                }
                m_exit = true;
            }
        }



        protected override bool OnDrawExecute()
        {
            ImGui.Text("Clone remote repository");
            if (ImGui.InputText("Remote",ref m_cloneURL,1024))
            {
                if (!m_amend)
                {
                    m_repoName = "";
                }
                if (!string.IsNullOrEmpty(m_cloneURL))
                {
                    m_cloneURL = m_cloneURL.Replace("\\", "/");
                    if (!m_amend)
                    {
                        m_repoName = Path.GetFileName(m_cloneURL.Replace(".git", ""));
                    }
                }
                if (m_cloneURL == null)
                {
                    m_cloneURL = "";
                }
            }
            if (ImGui.InputText("Branch", ref m_branch, 1024))
            {
                if (m_branch == null)
                {
                    m_branch = "";
                }
            }
            if (!m_amend)
            {
                ImGui.BeginDisabled();
            }
            ImGui.InputText("Name",ref m_repoName,200);
            if (!m_amend)
            {
                ImGui.EndDisabled();
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("Amend",ref m_amend))
            {
                //m_amend = !m_amend;
            }

            if (ImGui.InputText("Local", ref m_localPath, 1024)) 
            {
                if (m_localPath == null)
                {
                    m_localPath = "";
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Choose"))
            {
                var dirs = StandaloneFileBrowser.OpenFolderPanel("Git Clone Local Folder", "", false);
                if (dirs != null && dirs.Length > 0)
                {
                    m_localPath = dirs[0];
                }
            }



            ImGui.Separator();

            string localPath = Path.Combine(m_localPath, m_repoName);

            string arguments = $"clone {m_cloneURL} {localPath}";
            if (!string.IsNullOrEmpty(m_branch))
            {
                arguments = $"clone -b {m_branch} {m_cloneURL} {localPath}";
            }

            ImGui.TextWrapped(arguments);

            if (ImGui.Button("Clone"))
            {
                Log.Info(arguments);
                Execute(arguments);
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                return false;
            }
            return true;
        }


    }

    public class PushGitCommand : ProcessGitCommand
    {
        public override  string Name => "Git Push";
        protected bool m_pushTags;
        protected bool m_pushForce;

        public PushGitCommand(GitRepo gitRepo):base(gitRepo)
        {
            var head = gitRepo.Repo.Head;
            for (int i = 0; i < m_localBranchNames.Length; i++)
            {
                if (head.FriendlyName.Equals(m_localBranchNames[i]))
                {
                    m_localBranchIndex = i;
                    break;
                }
            }

            for (int i = 0; i < m_remoteBranchNames.Length; i++)
            {
                if (m_remoteBranchNames[i].EndsWith($"/{head.FriendlyName}"));
                {
                    m_remoteBranchIndex = i;
                    break;
                }
            }
        }

        protected override bool OnDrawExecute()
        {
            ImGui.Text("Push the local branch to the remote repository");
            ImGui.Combo("Branch", ref m_localBranchIndex, m_localBranchNames, m_localBranchNames.Length);
            ImGui.Combo("Remote", ref m_remoteBranchIndex, m_remoteBranchNames, m_remoteBranchNames.Length);
            ImGui.Checkbox("Force push", ref m_pushForce);
            ImGui.Checkbox("Push all tags", ref m_pushTags);
            ImGui.SetCursorPosX(ImGui.GetWindowSize().X * 0.55f);
            if (ImGui.Button("Push"))
            {
                string localBranch = m_localBranchNames[m_localBranchIndex];
                string remoteBranch = m_remoteBranchNames[m_remoteBranchIndex];
                int remoteIndex = remoteBranch.IndexOf("/");
                string remote = remoteBranch.Substring(0, remoteIndex);
                remoteIndex++;
                remoteBranch = remoteBranch.Substring(remoteIndex, remoteBranch.Length - remoteIndex);
                string arguments = "push";
                if (m_pushForce)
                {
                    arguments += " --force";
                }
                if (m_pushTags)
                {
                    arguments += " --tags";
                }
                arguments += $" {remote} {localBranch}:{remoteBranch}";

                Log.Info(arguments);
                Execute(arguments);
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                return false;
            }
            return true;
        }

      
    }

    public class PullGitCommand : ProcessGitCommand
    {
        public override  string Name => "Git Pull";

        public PullGitCommand(GitRepo gitRepo) : base(gitRepo)
        {
           string arguments = $"pull";
            Log.Info(arguments);
            Execute(arguments);
        }

    }

    public class FetchGitCommand : ProcessGitCommand
    {
        public override string Name => "Git Fetch";

        private bool m_fetchAll;
        private bool m_force;

        public FetchGitCommand(GitRepo gitRepo) : base(gitRepo)
        {
        }

        protected override bool OnDrawExecute()
        {
            ImGui.Text("Fetch the remote repository to the local branch");
            if (m_fetchAll)
            {
                ImGui.BeginDisabled();
            }
            ImGui.Combo("Remote", ref m_remoteBranchIndex, m_remoteBranchNames, m_remoteBranchNames.Length);
            ImGui.Combo("Branch", ref m_localBranchIndex, m_localBranchNames, m_localBranchNames.Length);
            ImGui.Checkbox("Force", ref m_force);
            if (m_fetchAll)
            {
                ImGui.EndDisabled();
            }
            ImGui.Checkbox("Fetch all", ref m_fetchAll);
            ImGui.SetCursorPosX(ImGui.GetWindowSize().X * 0.55f);
            if (ImGui.Button("Fetch"))
            {
                string arguments = "fetch";
                if (m_fetchAll)
                {
                    arguments += " --all";
                }
                else
                {
                    string localBranch = m_localBranchNames[m_localBranchIndex];
                    string remoteBranch = m_remoteBranchNames[m_remoteBranchIndex];
                    int remoteIndex = remoteBranch.IndexOf("/");
                    string remote = remoteBranch.Substring(0, remoteIndex);
                    remoteIndex++;
                    remoteBranch = remoteBranch.Substring(remoteIndex, remoteBranch.Length - remoteIndex);
                    if (m_force)
                    {
                        arguments += " --force";
                    }
                    arguments += $" {remote} {remoteBranch}:{localBranch}";
                }
                Log.Info(arguments);
                Execute(arguments);
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                return false;
            }
            return true;
        }


    }

    public class ProcessGitCommand : IGitCommand
    {
        public virtual bool FirstTimeShow { get; set; } = true;
        public virtual string Name { get; protected set; } = "Process Command";
        protected List<string> m_lines = new List<string>();
        protected CancellationTokenSource m_cancellationTokenSource;
        protected bool m_executed;
        protected bool m_exit;
        protected GitRepo m_gitRepo;

        protected string[] m_localBranchNames;
        protected int m_localBranchIndex;

        protected string[] m_remoteBranchNames;
        protected int m_remoteBranchIndex;

        public ProcessGitCommand()
        {
            
        }

        public ProcessGitCommand(GitRepo gitRepo)
        {
            m_gitRepo = gitRepo;
            m_exit = false;
            m_executed = false;

            List<string> remoteBranchs = new List<string>();
            List<string> localBranchs = new List<string>();
            foreach (var item in gitRepo.Repo.Branches)
            {
                if (item.IsRemote)
                {
                    if (item.CanonicalName.EndsWith("/HEAD"))
                    {
                        continue;
                    }
                    remoteBranchs.Add(item.FriendlyName);
                }
                else
                {
                    localBranchs.Add(item.FriendlyName);
                }
            }

            m_localBranchNames = localBranchs.ToArray();
            m_remoteBranchNames = remoteBranchs.ToArray();
        }

        protected void AddNewLine(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                m_lines.Add(line);
                Log.Info(line);
            }
        }

        protected virtual async void Execute(string arguments,string cliTarget=null)
        {
            m_cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = default(CancellationToken);
            cancellationToken = m_cancellationTokenSource.Token;
            try
            {
                if (string.IsNullOrEmpty(cliTarget))
                {
                    cliTarget = GitCommandView.GIT;
                }
                m_executed = true;
                AddNewLine(m_gitRepo.RootPath);
                AddNewLine("$ "+ cliTarget +" " + arguments);
                var result = await Cli.Wrap(cliTarget)
               .WithArguments(arguments)
               .WithWorkingDirectory(m_gitRepo.RootPath)
               .WithStandardOutputPipe(PipeTarget.ToDelegate(AddNewLine, Encoding.UTF8))
               .WithStandardErrorPipe(PipeTarget.ToDelegate(AddNewLine, Encoding.UTF8))
               .WithValidation(CommandResultValidation.None)
               .ExecuteAsync(cancellationToken);

                AddNewLine($"{result.ExitTime} ExitCode {result.ExitCode}");
            }
            catch (Exception e)
            {
                AddNewLine($"git {arguments} exception: {e}");
            }
            finally
            {
                if (m_cancellationTokenSource != null)
                {
                    m_cancellationTokenSource.Dispose();
                    m_cancellationTokenSource = null;
                }
                m_exit = true;
            }
        }

        protected virtual bool OnDrawOutput()
        {
            if (m_lines.Count > 0)
            {
                ImGui.BeginGroup();
                foreach (var item in m_lines)
                {
                    ImGui.Text(item);
                }
                ImGui.EndGroup();
            }

            if (!m_exit)
            {
                if (ImGui.Button("Cancel"))
                {
                    if (m_cancellationTokenSource != null)
                    {
                        m_cancellationTokenSource.Cancel(true);
                    }
                    m_exit = true;
                    return false;
                }
            }
            else
            {
                if (ImGui.Button("Close"))
                {
                    return false;
                }
            }
            return true;
        }

        protected virtual bool OnDrawExecute()
        {
            return true;
        }

        public virtual bool Draw()
        {
            try
            {
                if (m_executed)
                {
                    if (!OnDrawOutput())
                    {
                        return false;
                    }
                }
                else
                {
                    if (!OnDrawExecute())
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("ProcessGitCommand Draw exception: {0}",e);
                return false;
            }
            return true;
        }

      

        public virtual void Dispose()
        {
            if (m_cancellationTokenSource != null)
            {
                m_cancellationTokenSource.Dispose();
                m_cancellationTokenSource = null;
            }
        }

    }


    public class CommonGitCommand : ProcessGitCommand
    {
        public CommonGitCommand(GitRepo gitRepo,string command) : base(gitRepo)
        {
            Execute(command);
        }
    }


    public class HandleGitCommand : IGitCommand
    {
        public virtual bool FirstTimeShow { get; set; } = true;

        public string Name => "Common Git Command";

        private Func<bool> m_OnDrawCallback;

        public HandleGitCommand(Func<bool> onDrawCallback)
        {
            m_OnDrawCallback = onDrawCallback;
        }

        public void Dispose()
        {
            m_OnDrawCallback = null;
        }

        public bool Draw()
        {
            if (m_OnDrawCallback == null)
            {
                return false;
            }

            return m_OnDrawCallback();
        }


        private void ExecCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return;
        }

    }

    //public class LuaProcessGitCommand : ProcessGitCommand
    //{
    //    public override string Name { get; protected set; } = "Git Common Command - Lua";

    //    //public LuaProcessGitCommand(GitRepo gitRepo, ViewCommand command) : base(gitRepo)
    //    //{
    //    //    Name = command.Action;
    //    //    string action = gitRepo.FormatCommandAction(command);
    //    //    Log.Info(action);

    //    //    if (action.StartsWith("git"))
    //    //    {
    //    //        string arguments = action.Substring(3, action.Length - 3).Trim();
    //    //        Execute(arguments);
    //    //    }
    //    //    else
    //    //    {
    //    //        Log.Warn("Non-git commands");
    //    //        var args = action.Split(' ');
    //    //        string target = args[0];
    //    //        string arguments = action.Substring(target.Length, action.Length - target.Length).Trim();
    //    //        Execute(arguments, target);
    //    //    }
    //    //}
    //}

}
