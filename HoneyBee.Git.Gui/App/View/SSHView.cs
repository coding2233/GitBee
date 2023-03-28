using ImGuiNET;
using Renci.SshNet;
using strange.extensions.context.api;
using strange.extensions.dispatcher.eventdispatcher.api;
using strange.extensions.mediation.impl;
using strange.extensions.signal.impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wanderer.App.Service;
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer.App.View
{
    public class SSHView : ImGuiTabView
    {
        public override string Name => "SSH ImGui View";

        SplitView m_splitView = new SplitView(SplitView.SplitType.Horizontal, 200);
        private string m_inputText = "";
        private const int m_textMaxLength = 256 ;

        private StringBuilder m_stringBuilder = new StringBuilder();
        private bool m_resetScrollToDown;
        private bool m_newHost = false;
        private SSHHostInfo m_newHostInfo;

        public Signal<SSHHostInfo,bool> OnCreateNewHost=new Signal<SSHHostInfo,bool>();

        private List<SSHHostInfo> m_sshHostInfos;

        //private Dictionary<string, SshClient> s_sshClients = new Dictionary<string, SshClient>();
        //private Dictionary<SshClient, StringBuilder> s_sshClientStrBuilders = new Dictionary<SshClient, StringBuilder>();

        private SSHHostInfo m_selectSssHost;

        private TextEditor m_texteditor;

        public override bool Unsave 
        {
            get
            {
                if (m_sshHostInfos != null)
                {
                    foreach (var item in m_sshHostInfos)
                    {
                        if (item.sshClient != null && item.sshClient.IsConnected)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public SSHView(IContext context) : base(context)
        {
            m_texteditor = new TextEditor();
        }


        public void SetSSHHostInfo(List<SSHHostInfo> sshHostInfos)
        {
            m_sshHostInfos = sshHostInfos;
        }

        public override void OnDraw()
        {
            base.OnDraw();

            m_splitView.Begin();
            OnHostNameDraw();
            m_splitView.Separate();
            OnSShTerminalDraw();
            m_splitView.End();
        }


        private void OnHostNameDraw()
        {
            if (ImGui.Button(Icon.Get(Icon.Material_post_add) + "Create New SSh Host"))
            {
                m_newHost = !m_newHost;
                if (m_newHost && m_newHostInfo == null)
                {
                    m_newHostInfo = new SSHHostInfo();
                }
            }

            if (m_newHost)
            {
                ImGui.Separator();
                ImGui.Text("New SSh Host");
                string tempShowStr = m_newHostInfo.name;
                if (ImGui.InputText("Name", ref tempShowStr, 200))
                {
                    m_newHostInfo.name = tempShowStr;
                }
                tempShowStr = m_newHostInfo.host;
                if (ImGui.InputText("Host",ref tempShowStr, 200))
                {
                    m_newHostInfo.host = tempShowStr;
                }
                int tempShowPort = m_newHostInfo.port;
                if (ImGui.InputInt("Port", ref tempShowPort))
                {
                    m_newHostInfo.port = tempShowPort;
                }
                tempShowStr = m_newHostInfo.username;
                if (ImGui.InputText("UserName", ref tempShowStr, 200))
                {
                    m_newHostInfo.username = tempShowStr;
                }
                tempShowStr = m_newHostInfo.password;
                if (ImGui.InputText("Password", ref tempShowStr, 200,ImGuiInputTextFlags.Password))
                {
                    m_newHostInfo.password = tempShowStr;

                }
                if (ImGui.Button("OK"))
                {
                    if (string.IsNullOrEmpty(m_newHostInfo.name))
                    {
                        m_newHostInfo.name = $"{m_newHostInfo.username}@{m_newHostInfo.host}:{m_newHostInfo.port}";
                    }
                    OnCreateNewHost.Dispatch(m_newHostInfo,true);
                    
                    m_newHostInfo = new SSHHostInfo();
                    m_newHost = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    m_newHost = false;
                }

                ImGui.Separator();
            }

            if (m_sshHostInfos != null && m_sshHostInfos.Count > 0)
            {
                foreach (var item in m_sshHostInfos)
                {
                    string sshItemName = item.sshClient==null || !item.sshClient.IsConnected ? item.name: $"{Icon.Get(Icon.Material_cast_connected)}{item.name}";
                    bool select = m_selectSssHost == item && item.sshClient!=null && item.sshClient.IsConnected;

                    //读取信息
                    if (item.Read())
                    {
                        if (select && m_texteditor!=null)
                        {
                            m_texteditor.text = item.stringBuilder.ToString();
                        }
                    }

                    if (ImGui.Checkbox(sshItemName,ref select))
                    {
                        if (select)
                        {
                           
                            //if (item.sshClient == null)
                            //{
                            //    item.sshClient = new SshClient(item.host, item.username, item.password);
                            //    //var shellStream = sshClient.CreateShellStream("myShell", 100, 200, 1000, 1000, 2048);
                            //    //var writer = new StreamWriter(shellStream) { AutoFlush = true };
                            //    item.sshClient.HostKeyReceived += (sender, e) =>
                            //    {
                            //        e.CanTrust = true;
                            //    };
                            //    item.sshClient.ErrorOccurred += (sender, e) =>
                            //    {
                            //        Log.Info("ErrorOccurred  ssh connect fail. {e}", e);
                            //    };

                            //    item.stringBuilder = new StringBuilder();
                            //}

                            try
                            {
                                item.CreateShellStream();
                            }
                            catch (System.Exception e)
                            {
                                string exceptionMsg = string.Format("ssh connect fail. {0}", e);
                                Log.Info(exceptionMsg);
                                item.stringBuilder.AppendLine(exceptionMsg);
                            }

                            m_selectSssHost = item;
                            ReBuildLogText(m_selectSssHost.stringBuilder);
                        }
                        else
                        {
                            if (item.sshClient != null && item.sshClient.IsConnected)
                            {
                                item.sshClient.Disconnect();
                            }
                            
                            m_selectSssHost = null;
                        }
                    }

                    if (ImGui.BeginPopupContextItem())
                    {
                        if (ImGui.MenuItem("Delete"))
                        {
                            if (item.sshClient != null && item.sshClient.IsConnected)
                            {
                                item.sshClient.Disconnect();
                                item.sshClient.Dispose();
                            }
                            item.stringBuilder = null;

                            OnCreateNewHost.Dispatch(item,false);
                            break;
                        }
                        ImGui.EndPopup();
                    }

                   
                }
            }
            
        }

      

        private unsafe void OnSShTerminalDraw()
        {
            ImGui.BeginChild("OnSShTerminalDraw-Log", new Vector2(0,-ImGui.GetStyle().ItemSpacing.Y-ImGui.GetFrameHeightWithSpacing()), false,ImGuiWindowFlags.HorizontalScrollbar);
            m_texteditor.Render("termianl-log",ImGui.GetContentRegionAvail());
            m_texteditor.readOnly = true;
            m_texteditor.ignoreChildWindow = true;
            //string logText = m_selectSshClient!=null? s_sshClientStrBuilders[m_selectSshClient].ToString():"";
            //ImGui.Text(logText);
            float maxY = ImGui.GetScrollMaxY();
            if (m_resetScrollToDown)
            {
                ImGui.SetScrollHereY(1.0f);
                m_resetScrollToDown = false;
            }
            ImGui.EndChild();

            ImGui.BeginChild("OnSShTerminalDraw-Input");
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth());
            if (ImGui.InputTextWithHint("","command", ref m_inputText, m_textMaxLength, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (!string.IsNullOrEmpty(m_inputText))
                {
                    if (m_selectSssHost != null)
                    {
                        m_selectSssHost.Write(m_inputText);
                        //if (RunCommand(m_selectSssHost, m_inputText))
                        //{
                        //    m_inputText = "";
                        //}
                        m_inputText = "";
                    }
                    
                }
            }
            ImGui.EndChild();
        }

        private bool RunCommand(SSHHostInfo hostInfo,string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return false;
            }
            bool result = false;

            if (hostInfo != null)
            {
                var sshClient = hostInfo.sshClient;
                var strBuilder = hostInfo.stringBuilder;

                if (sshClient != null && sshClient.IsConnected)
                {
                    strBuilder.AppendLine(command);
                    if (command.Equals("exit"))
                    {
                        sshClient.Disconnect();
                    }
                    else
                    {
                        hostInfo.Write(command);
                        //using (var cmd = sshClient.RunCommand(command))
                        //{
                        //    Log.Info("cmd:{0} ExitStatus:{1} Result:{2}", cmd.CommandText, cmd.ExitStatus, cmd.Result);
                        //    if (!string.IsNullOrEmpty(cmd.Result))
                        //    {
                        //        strBuilder.AppendLine(cmd.Result);
                        //    }
                        //    result = true;
                        //}

                        result = true;
                    }

                    ReBuildLogText(strBuilder);
                }
            }

            return result;
        }

        private void ReBuildLogText(StringBuilder stringBuilder)
        {
            if (m_texteditor != null && stringBuilder != null)
            {
                m_texteditor.text = stringBuilder.ToString();
            }
        }

    }


  

    public class SSHMediator : EventMediator
    {
        [Inject]
        public IDatabaseService databaseService { get; set; }

        [Inject]
        public SSHView sshView { get; set; }

        private List<SSHHostInfo> m_sshHostInfos = new List<SSHHostInfo>();

        public override void OnRegister()
        {
            base.OnRegister();

            sshView.OnCreateNewHost.AddListener(OnCreateNewHost);


            m_sshHostInfos = databaseService.GetCustomerData<SSHHostInfo>();

            sshView.SetSSHHostInfo(m_sshHostInfos);
        }

        public override void OnRemove()
        {
            sshView.OnCreateNewHost.RemoveListener(OnCreateNewHost);

            base.OnRemove();
        }


        private void OnCreateNewHost(SSHHostInfo e,bool createNew)
        {
            if (createNew)
            {
                m_sshHostInfos.Insert(0, e);
                databaseService.SetCustomerData<SSHHostInfo>(e.name, e);
            }
            else
            {
                m_sshHostInfos.Remove(e);
                databaseService.RemoveCustomerData<SSHHostInfo>(e.name);
            }
            sshView.SetSSHHostInfo(m_sshHostInfos);
            
            //SshClient sshClient = new SshClient(e.host, e.username, e.password);
        }



    }

    [System.Serializable]
    public class SSHHostInfo
    {
        public string name { get; set; }
        public string host { get; set; }
        public int port { get; set; }
        public string username { get; set; }
        public string password { get; set; }

        public SshClient sshClient;

        public StringBuilder stringBuilder;

        public ShellStream shellStream;

        private StreamReader reader;
        private StreamWriter writer;
        public SSHHostInfo()
        {
            name = "";
            host = "";
            port = 22;
            username = "";
            password = "";
        }

        public void CreateShellStream()
        {
            if (sshClient == null)
            {
                sshClient = new SshClient(host, username, password);
                //var shellStream = sshClient.CreateShellStream("myShell", 100, 200, 1000, 1000, 2048);
                //var writer = new StreamWriter(shellStream) { AutoFlush = true };
                sshClient.HostKeyReceived += (sender, e) =>
                {
                    e.CanTrust = true;
                };
                sshClient.ErrorOccurred += (sender, e) =>
                {
                    Log.Info("ErrorOccurred  ssh connect fail. {e}", e);
                };

                stringBuilder = new StringBuilder();
            }


            if (!sshClient.IsConnected)
            {
                sshClient.Connect();
                
                if (shellStream != null)
                {
                    shellStream.Close();
                    shellStream.Dispose();
                }

                shellStream = sshClient.CreateShellStream(name, 100, 200, 1000, 1000, 2048);

                reader = new StreamReader(shellStream);
                writer = new StreamWriter(shellStream) { AutoFlush = true };
            }
        }


        public bool Read()
        {
            if (sshClient != null && sshClient.IsConnected)
            {
                if (shellStream.CanRead && shellStream.DataAvailable)
                {
                    var r = reader.ReadToEnd();
                    stringBuilder.Append(r);
                    return true;
                }
            }
            return false;
        }

        public void Write(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                if (sshClient != null && sshClient.IsConnected)
                {

                    writer.WriteLine(line);
                }
            }
        }

    }


}
