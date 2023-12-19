using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using ImGuiNET;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
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
   
    public class GitNewTagCommandView : GitCommandView
    {
        public override string Name => "New Tag";

        protected string newInputText = "";

        protected string m_commit;

        public GitNewTagCommandView(GitRepo gitRepo,string commit) : base(gitRepo)
        {
            m_commit = commit;
        }

        protected override void OnDrawCommandView()
        {

            if (ImGui.InputText("New Tag", ref newInputText, 200))
            {
                if (!string.IsNullOrEmpty(newInputText)
                    && !string.IsNullOrEmpty(m_commit))
                {
                    m_command = $"git tag {newInputText} {m_commit}";
                }
            }
        }
    }

    public class GitNewBranchCommandView : GitCommandView
    {
        public override string Name => "New Branch";

        protected string newInputText = "";

        protected string m_commit;

        private bool m_checkOut;

        public GitNewBranchCommandView(GitRepo gitRepo, string commit) : base(gitRepo)
        {
            m_commit = commit;
        }

        protected override void OnDrawCommandView()
        {

            if (ImGui.InputText("New Branch", ref newInputText, 200))
            {
                if (!string.IsNullOrEmpty(newInputText)
                    && !string.IsNullOrEmpty(m_commit))
                {
                    if (m_checkOut)
                    {
                        m_command = $"git checkout -b {newInputText} {m_commit}";
                    }
                    else
                    {
                        m_command = $"git branch {newInputText} {m_commit}";
                    }
                }
            }

            if (ImGui.Checkbox("Checkout", ref m_checkOut))
            {
                
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
            if (ImGui.Button("Execute"))
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
            if (ImGui.Combo(label, ref itemIndex, items, items.Length))
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
                if (ImGui.InputText("New Branch", ref customString, 200))
                {
                }
                if (ImGui.Button("OK"))
                {
                    items[itemIndex] = customString + "...";
                    result = true;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
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
                    localBranch.Add("custom...");
                    remoteBranch.Add("custom...");
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
				s_gitInstallPath = "";
                if (System.OperatingSystem.IsWindows())
                {
                    var subKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($"SOFTWARE\\GitForWindows\\", false);
                    if (subKey != null)
                    {
                        var value = subKey.GetValue("InstallPath");
                        if (value != null)
                        {
                            s_gitInstallPath = (string)value;
                        }
                    }
                }
			}
            return s_gitInstallPath;
        }

        internal static void RunGitBash(string fileName, string command, string workdir = null, string waitCommand= ";echo '\n exit =========>'; echo 'Please type enter to close the window'; read")
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
                if (waitCommand == null)
                {
                    waitCommand = "";
				}
                process.StartInfo.Arguments = $"-c \"echo '${workdir}';echo '${command}';{command}{waitCommand}\"";
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

    public class GitView : GitCommandView
    {
        struct ColorLine
        {
            public string Line;
            public Vector4 Color;
        }

		public override string Name => "Git View";

        private static List<ColorLine> s_lines = new List<ColorLine>();
        private bool m_isDirty = false;
        private string m_inputCommand = "";
        private bool m_firstTimeShowInputCommand;
        private static bool m_showHead;
        private Action m_onCommandComplete;
        private string m_runCommand;

        public GitView(GitRepo gitRepo):base(gitRepo) 
        {
            AddRepoInfo();
		}

		public GitView Pull()
		{
			m_runCommand = "git pull";
			return this;
		}
		public GitView Fecth()
		{
			m_runCommand = "git fetch --all";
			return this;
		}
		public GitView Push()
		{
			m_runCommand = "git push";
			return this;
		}

        public GitView Revert(string commit)
        {
			m_runCommand = $"git revert {commit}";
			return this;
		}

		public GitView Add(IEnumerable<string> files)
        {
            string command = "git add -v -A";
            if (files != null && files.Count() > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append("git add -v");
				foreach (var item in files)
                {
					stringBuilder.Append(" ");
					stringBuilder.Append(item);
				}
				command = stringBuilder.ToString();
			}
            m_runCommand = command;
			return this;
		}

        public GitView Restore(IEnumerable<string> files)
        {
			m_runCommand = null;
			if (files != null && files.Count() > 0)
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append("git restore --stage");
				foreach (var item in files)
				{
					stringBuilder.Append(" ");
					stringBuilder.Append(item);
				}
				m_runCommand = stringBuilder.ToString();
			}
            return this;
		}

        public GitView Then(Action onRunComplete)
        {
            m_onCommandComplete = onRunComplete;
            return this;
		}


        public void Run()
        {
            RunCommand(m_runCommand, m_gitRepo.RootPath, (result) => {
                if (m_onCommandComplete != null)
                {
					m_onCommandComplete();
				}
                m_runCommand = null;
                m_onCommandComplete = null;
			});
        }


		private void AddRepoInfo(string newline="")
        {
            if (m_showHead)
            {
                return;
            }

            if (m_gitRepo == null || m_gitRepo.Repo == null)
            {
				AddLine($"{newline}{Directory.GetCurrentDirectory().Replace("\\","/")}", new Vector4(164 / 255.0f, 191 / 255.0f, 0, 1));
			}
            else
            {
                string branchName = m_gitRepo.Branches.Where((x) =>
                {
                    return !x.IsRemote && x.IsCurrentRepositoryHead;
                }).First().FriendlyName;
                AddLine($"{newline}{m_gitRepo.SignatureAuthor.Name}@{m_gitRepo.RootPath} ({branchName})", new Vector4(164 / 255.0f, 191 / 255.0f, 0, 1));
            }
            m_showHead = true;
		}

		private void AddLine(string line)
		{
            m_isDirty = true;
			AddLine(line, Vector4.One);
		}
		private void AddLine(string line, Vector4 color)
        {
            s_lines.Add(new ColorLine() {Line= line,Color= color });
		}
        private async void RunCommand(string command, string workDir,Action<int> commandCallback)
        {
            if(string.IsNullOrEmpty(command))
            {
				commandCallback?.Invoke(1);
				return;
			}

			AddLine($"$ {command}");
            try
            {
                int targetIndex = command.IndexOf(" ");
                string target = command.Substring(0, targetIndex);
                if (target.Equals("git"))
                {
                    target = GIT;
                }
                command = command.Substring(targetIndex + 1);
                var cmd = Cli.Wrap(target)
                    .WithWorkingDirectory(workDir)
                    .WithArguments(command);
                await foreach (var cmdEvent in cmd.ListenAsync())
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent started:
                            m_showHead = false;
                            //AddLine($"Process started; ID: {started.ProcessId}",new Vector4(0,190/255.0f, 190/255.0f,1));
                            break;
                        case StandardOutputCommandEvent stdOut:
                            AddLine($"{stdOut.Text}");
                            break;
                        case StandardErrorCommandEvent stdErr:
                            AddLine($"{stdErr.Text}", new Vector4(1, 0, 0, 1));
                            break;
                        case ExitedCommandEvent exited:
                            //AddLine($"Process exited; Code: {exited.ExitCode}\n\n", new Vector4(0, 190 / 255.0f, 190 / 255.0f, 1));
                            AddRepoInfo("\n");
                            break;
                    }
                }

				commandCallback?.Invoke(0);
			}
            catch (Exception ex)
            {
			    AddLine($"{ex}",new Vector4(1,0,0,1));
				m_showHead = false;
				AddRepoInfo("\n");
				commandCallback?.Invoke(2);
			}
		}

		protected override bool OnDrawExecuteDraw()
		{
            //if (ImGui.Button("ok"))
            //{
            //    return true;
            //}
            return false;
		}

		protected override void OnDrawCommandView()
		{
            if (s_lines != null)
            {
				foreach (var item in s_lines)
                {
					ImGui.TextColored(item.Color, item.Line);
                }

                if (m_isDirty)
                {
                    ImGui.SetScrollHereY(1);
                    m_firstTimeShowInputCommand = true;
					m_isDirty = false;
				}
            }

            if (!m_isDirty && m_showHead)
            {
				ImGui.PushStyleColor(ImGuiCol.FrameBg, 0);
                ImGui.PushStyleColor(ImGuiCol.FrameBgActive, 0);
                ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0);
                //
		
				Vector2 keySize = ImGui.CalcTextSize("$$");
                if (!ImGui.IsAnyItemActive() && !ImGui.IsMouseClicked(0))
                {
                    ImGui.SetKeyboardFocusHere(0);
                }
				ImGui.SetCursorPosX(keySize.X);
				ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - keySize.X);
                if (ImGui.InputText("", ref m_inputCommand, 200, ImGuiInputTextFlags.NoHorizontalScroll | ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (!string.IsNullOrEmpty(m_inputCommand))
                    {
                        RunCommand(m_inputCommand, m_gitRepo != null ? m_gitRepo.RootPath : "",null);
                        m_inputCommand = "";
                    }
				}
              
				ImGui.GetWindowDrawList().AddText(ImGui.GetItemRectMin()-new Vector2(keySize.X*0.5f,-3f), ImGui.ColorConvertFloat4ToU32(Vector4.One), "$");
				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
				ImGui.PopStyleColor();

				if (m_firstTimeShowInputCommand)
				{
					ImGui.SetScrollHereY(1);
					m_firstTimeShowInputCommand = false;
				}
			}
		}
	}

}
