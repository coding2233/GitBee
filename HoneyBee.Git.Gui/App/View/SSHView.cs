using ImGuiNET;
using Renci.SshNet;
using strange.extensions.context.api;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer.App.View
{
    public class SSHView : ImGuiTabView
    {
        public override string Name => "SSH ImGui View";

        SplitView m_splitView = new SplitView(SplitView.SplitType.Horizontal,200);
        private string m_logText="";
        private string m_inputText="";
        private const int m_textMaxLength = 1024 * 100;

        private SshClient m_sshClient;
        private StringBuilder m_stringBuilder = new StringBuilder();
        private bool m_resetScrollToDown;

        public SSHView(IContext context) : base(context)
        {
            try
            {
                m_sshClient = new SshClient("192.168.31.11", "root", "coding2580");
                m_sshClient.HostKeyReceived += (sender, e) =>
                {
                    e.CanTrust = true;
                };

                m_sshClient.Connect();
            }
            catch (Exception e)
            {
                Log.Info(e);
            }
            //using (var cmd = m_sshClient.RunCommand("ls"))
            //{
            //    Log.Info("cmd:{0} ExitStatus:{1} Result:{2}", cmd.CommandText, cmd.ExitStatus, cmd.Result);

            //}
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
            ImGui.Text("host name");
        }

        private unsafe void OnSShTerminalDraw()
        {
            ImGui.BeginChild("OnSShTerminalDraw-Log", ImGui.GetWindowSize() - new Vector2(0, ImGui.GetTextLineHeightWithSpacing()*2),false);
            ImGui.Text(m_logText);
            float maxY = ImGui.GetScrollMaxY();
            if (m_resetScrollToDown)
            {
                ImGui.SetScrollY(maxY);
                m_resetScrollToDown = false;
            }
            ImGui.EndChild();

            ImGui.BeginChild("OnSShTerminalDraw-Input");
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth());
            if (ImGui.InputTextWithHint("","command", ref m_inputText, m_textMaxLength, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (!string.IsNullOrEmpty(m_inputText))
                {
                    if (m_sshClient != null && m_sshClient.IsConnected)
                    {
                        using (var cmd = m_sshClient.RunCommand(m_inputText))
                        {
                            Log.Info("cmd:{0} ExitStatus:{1} Result:{2}", cmd.CommandText, cmd.ExitStatus, cmd.Result);
                            if (!string.IsNullOrEmpty(cmd.Result))
                            {
                                m_logText += cmd.Result;
                                m_resetScrollToDown = true;
                            }
                            m_inputText = "";
                        }
                    }
                }
            }
            ImGui.EndChild();
        }


        private unsafe int ImGuiInputTextCallback(ImGuiInputTextCallbackData* data)
        {
            

            return 0;
        }


    }


    public class SSHMediator : EventMediator
    { 
    }


}
