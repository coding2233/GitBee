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
        public SSHView(IContext context) : base(context)
        {
            m_sshClient = new SshClient("localhost","root","root");
            m_sshClient.HostKeyReceived += (sender, e) =>
            {
                e.CanTrust = true;
            };

            m_sshClient.Connect();
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
            //ImGui.Text(m_logText);

            //ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
            //if (ImGui.InputTextWithHint("","sss", ref m_inputText, m_textMaxLength, ImGuiInputTextFlags.EnterReturnsTrue))
            //{
            //    if (!string.IsNullOrEmpty(m_inputText))
            //    {
            //        if (m_sshClient != null && m_sshClient.IsConnected)
            //        {
            //            using (var cmd = m_sshClient.RunCommand(m_inputText))
            //            {
            //                Log.Info("cmd:{0} ExitStatus:{1} Result:{2}", cmd.CommandText, cmd.ExitStatus, cmd.Result);
            //                if (!string.IsNullOrEmpty(cmd.Result))
            //                {
            //                    m_logText += cmd.Result;
            //                }
            //                m_inputText = "";
            //            }
            //        }
            //    }
            //}

            //ImGui.PopStyleColor(); 
            //ImGuiInputTextFlags.CallbackEdit |
            m_inputText = m_stringBuilder.ToString();

            if (ImGui.InputTextMultiline("#OnSShTerminalDraw-InputTextMultiline", ref m_inputText, m_textMaxLength, ImGui.GetWindowSize()))
            {
            
                if (ImGui.IsKeyDown(ImGuiKey.Enter))
                {
                    Log.Info(m_inputText);
                    int index = m_inputText.LastIndexOf("\n",1) + 1;
                    if (index >= 0& index< m_inputText.Length)
                    {
                        string command = m_inputText.Substring(index, m_inputText.Length - index - 1);
                        if (!string.IsNullOrEmpty(command))
                        {
                            m_stringBuilder.AppendLine(command);

                            if (m_sshClient != null && m_sshClient.IsConnected)
                            {
                                using (var cmd = m_sshClient.RunCommand(command))
                                {
                                    Log.Info("cmd:{0} ExitStatus:{1} Result:{2}", cmd.CommandText, cmd.ExitStatus, cmd.Result);
                                    if (!string.IsNullOrEmpty(cmd.Result))
                                    {
                                        m_stringBuilder.AppendLine(cmd.Result);
                                    }
                                }
                            }

                        }
                    }
                }
            }
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
