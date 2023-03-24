using ImGuiNET;
using Renci.SshNet;
using strange.extensions.context.api;
using strange.extensions.dispatcher.eventdispatcher.api;
using strange.extensions.mediation.impl;
using strange.extensions.signal.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
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

        public Signal<SSHHostInfo> OnCreateNewHost=new Signal<SSHHostInfo>();

        private List<SSHHostInfo> m_sshHostInfos;

        private Dictionary<string, SshClient> s_sshClients = new Dictionary<string, SshClient>();
        private Dictionary<SshClient, StringBuilder> s_sshClientStrBuilders = new Dictionary<SshClient, StringBuilder>();

        private SshClient m_selectSshClient;

        private TextEditor m_texteditor;

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
            if (ImGui.Button(Icon.Get(Icon.Material_open_in_new) + "Create New SSh Host"))
            {
                m_newHostInfo = new SSHHostInfo();
                m_newHost = true;
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
                    OnCreateNewHost.Dispatch(m_newHostInfo);
                    m_newHost = false;
                }

                ImGui.Separator();
            }

            if (m_sshHostInfos != null && m_sshHostInfos.Count > 0)
            {
                foreach (var item in m_sshHostInfos)
                {
                    string sshItemName = s_sshClients.ContainsKey(item.name) ? $"{Icon.Get(Icon.Material_cast_connected)}{item.name}" : item.name;
                    if (ImGui.Button(sshItemName))
                    {
                        SshClient sshClient;
                        if (!s_sshClients.TryGetValue(item.name, out sshClient))
                        {
                            sshClient = new SshClient(item.host, item.username, item.password);
                            try
                            {
                                sshClient.HostKeyReceived += (sender, e) => {
                                    e.CanTrust = true;
                                };
                                sshClient.ErrorOccurred += (sender, e) => {
                                    Log.Info("ErrorOccurred  ssh connect fail. {e}", e);
                                };
                                sshClient.Connect();
                                s_sshClients.Add(item.name, sshClient);
                                s_sshClientStrBuilders.Add(sshClient, new StringBuilder());
                            }
                            catch (Exception e)
                            {
                                Log.Info("ssh connect fail. {e}", e);
                            }

                        }

                        m_selectSshClient = sshClient;

                        
                    }
                }
            }
        
        }

      

        private unsafe void OnSShTerminalDraw()
        {
            ImGui.BeginChild("OnSShTerminalDraw-Log", ImGui.GetWindowSize() - new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 2), false);
            if (m_selectSshClient == null || !m_selectSshClient.IsConnected)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0, 1.0f), "The ssh client is disconnected. Procedure.");
            }
            m_texteditor.Render("termianl-log",ImGui.GetContentRegionAvail());
            //string logText = m_selectSshClient!=null? s_sshClientStrBuilders[m_selectSshClient].ToString():"";
            //ImGui.Text(logText);
            float maxY = ImGui.GetScrollMaxY();
            if (m_resetScrollToDown)
            {
                ImGui.SetScrollY(0);
                m_resetScrollToDown = false;
            }
            ImGui.EndChild();

            ImGui.BeginChild("OnSShTerminalDraw-Input");
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth());
            if (ImGui.InputTextWithHint("","command", ref m_inputText, m_textMaxLength, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (!string.IsNullOrEmpty(m_inputText))
                {
                    if (m_selectSshClient != null && m_selectSshClient.IsConnected)
                    {
                        var strBuilder = s_sshClientStrBuilders[m_selectSshClient];
                        strBuilder.AppendLine(m_inputText);
                        using (var cmd = m_selectSshClient.RunCommand(m_inputText))
                        {
                            Log.Info("cmd:{0} ExitStatus:{1} Result:{2}", cmd.CommandText, cmd.ExitStatus, cmd.Result);
                            if (!string.IsNullOrEmpty(cmd.Result))
                            {
                                strBuilder.AppendLine(cmd.Result);
                                m_resetScrollToDown = true;
                            }
                            m_inputText = "";
                        }

                        m_texteditor.text = strBuilder.ToString();
                    }
                }
            }
            ImGui.EndChild();
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


        private void OnCreateNewHost(SSHHostInfo e)
        {
            m_sshHostInfos.Insert(0,e);
            databaseService.SetCustomerData<SSHHostInfo>(e.name, e);
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

        public SSHHostInfo()
        {
            name = "";
            host = "";
            port = 22;
            username = "";
            password = "";
        }
    }


}
