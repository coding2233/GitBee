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
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer.GitRepository.View
{
    public class GitCommandView : ImGuiView
    {
        private static GitCommandView s_gitCommandView;
        private Dictionary<Type, IGitCommand> m_gitCommands;

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


        //public static List<ViewCommand> ViewCommands { get; internal set; } = new List<ViewCommand>();
        public GitCommandView(IContext context) : base(context)
        {
            s_gitCommandView = this;
            m_gitCommands = new Dictionary<Type, IGitCommand>();
        }

        public override void OnDraw()
        {
            if (m_gitCommands != null && m_gitCommands.Count>0)
            {
                var viewport = ImGui.GetMainViewport();

                var workSize = viewport.WorkSize * 0.375f;

                foreach (var gitCommandItem in m_gitCommands)
                {
                    var gitCommand = gitCommandItem.Value;
                    if (gitCommand.FirstTimeShow)
                    {
                        ImGui.SetNextWindowSize(workSize);
                        gitCommand.FirstTimeShow = false;
                    }
                    ImGui.OpenPopup(gitCommand.Name);
                    //ImGui.SetNextWindowPos(pos);
                    bool openCommandDraw = true;
                    bool openWindow = true;
                    if (ImGui.BeginPopupModal(gitCommand.Name, ref openWindow))
                    {
                        openCommandDraw = gitCommand.Draw();
                    }

                    openCommandDraw = openCommandDraw && openWindow;
                    if (!openCommandDraw)
                    {
                        m_gitCommands.Remove(gitCommandItem.Key);
                        gitCommand.Dispose();
                        gitCommand = null;
                        ImGui.CloseCurrentPopup();
                        break;
                    }

                    ImGui.EndPopup();
                }
            }
        }

        private void SetGitCommand<T>(params object[] pArgs)  where T : IGitCommand
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
            if (s_gitCommandView != null)
            {
               s_gitCommandView.SetGitCommand<T>(pArgs);
            }
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
                    Process.Start(readLine,$"--cd={userPath}");
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

    }

    public interface IGitCommand:IDisposable
    {
        string Name { get; }
        bool FirstTimeShow { get; set; }
        bool Draw();
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
