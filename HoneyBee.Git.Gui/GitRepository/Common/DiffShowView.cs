using ImGuiNET;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.GitRepository.Common
{
    public class DiffShowView:IDisposable
    {
        List<IShowDiff> m_showDiffs;
        IShowDiff m_showDiff;

        public DiffShowView()
        {
            m_showDiffs = new List<IShowDiff>();
            //m_showDiffs.Add(new ShowDiffConflicted());
            m_showDiffs.Add(new ShowDiffTexture());
            m_showDiffs.Add(new ShowDiffText());
        }

        public void Build(PatchEntryChanges patchEntryChanges,GitRepo gitRepo)
        {
            if (m_showDiff != null)
            {
                m_showDiff.Clear();
                m_showDiff = null;
            }

            //查找合适的
            foreach (var item in m_showDiffs)
            {
                if (item.Build(patchEntryChanges,gitRepo))
                {
                    m_showDiff = item;
                    break;
                }
            }
        }

        public void Draw()
        {
            if (m_showDiff != null)
            {
                m_showDiff.OnDraw();
            }
        }

        public void Dispose()
        {
            m_showDiff = null;
            if (m_showDiffs != null)
            {
                foreach (var item in m_showDiffs)
                {
                    item.Dispose();
                }
                m_showDiffs.Clear();
                m_showDiffs = null;
            }
        }
    }

    
    public interface IShowDiff: IDisposable
    {
        bool Build(PatchEntryChanges patchEntryChanges, GitRepo gitRepo);
        void Clear();
        void OnDraw();
    }

    public class ShowDiffTexture : IShowDiff
    {
        private HashSet<string> m_textureExtension = new HashSet<string>() { ".jpg", ".png", ".tga", ".bmp", ".psd", ".gif", ".hdr", ".pic" };
        private GLTexture m_oldGLTexture;
        private GLTexture m_newGLTexture;
        private string m_patchText;

        public bool Build(PatchEntryChanges patchEntryChanges, GitRepo gitRepo)
        {
            if (CheckTexture(patchEntryChanges.OldPath) || CheckTexture(patchEntryChanges.Path))
            {
                m_patchText = patchEntryChanges.Patch;
                m_oldGLTexture = GetTextureFromBlob(patchEntryChanges.OldOid, gitRepo);
                m_newGLTexture = GetTextureFromBlob(patchEntryChanges.Oid, gitRepo);
                if (m_newGLTexture.Image ==IntPtr.Zero 
                    && (patchEntryChanges.Status == ChangeKind.Added || patchEntryChanges.Status == ChangeKind.Modified || patchEntryChanges.Status ==ChangeKind.Conflicted))
                {
                    m_newGLTexture = Application.LoadTextureFromFile(Path.Combine(gitRepo.RootPath, patchEntryChanges.Path));
                }
                //m_oldGLTexture = Application.LoadTextureFromFile(Path.Combine(gitRepo.RootPath, patchEntryChanges.OldPath));
                //m_newGLTexture = Application.LoadTextureFromFile(Path.Combine(gitRepo.RootPath, patchEntryChanges.Path));
                return true;
            }
            return false;
        }

        public void Clear()
        {
            Application.DeleteTexture(m_oldGLTexture);
            Application.DeleteTexture(m_newGLTexture);

            m_oldGLTexture = default(GLTexture);
            m_newGLTexture = default(GLTexture);

            m_patchText = null;
        }

        public void Dispose()
        {
            Clear();
        }

        public void OnDraw()
        {
            if (!string.IsNullOrEmpty(m_patchText))
            {
                ImGui.Text(m_patchText);
                ImGui.Separator();
            }

            float minTargetSize = Math.Min(ImGui.GetWindowWidth(), ImGui.GetWindowHeight()) *0.5f;
            var targetSize = Vector2.One * minTargetSize;
            ImGui.Image(m_oldGLTexture.Image, ResizeImage(m_oldGLTexture.Size, targetSize));
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetWindowWidth()*0.5f);
            ImGui.Image(m_newGLTexture.Image, ResizeImage(m_newGLTexture.Size, targetSize));
        }

        private bool CheckTexture(string texturePath)
        {
            string extension = Path.GetExtension(texturePath).ToLower();
            if (m_textureExtension.Contains(extension))
            {
                return true;
            }
            return false;
        }

        private GLTexture GetTextureFromBlob(ObjectId objectId,GitRepo gitRepo)
        {
            if (objectId != ObjectId.Zero)
            {
                var blob = gitRepo.Repo.Lookup<Blob>(objectId);
                if (blob != null)
                {
                    using (var blobStream = blob.GetContentStream())
                    {
                        if (blobStream.Length > 0)
                        {
                            byte[] buffer = new byte[blobStream.Length];
                            blobStream.Read(buffer, 0, buffer.Length);
                            var glTexture = Application.LoadTextureFromMemory(buffer);
                            return glTexture;
                        }
                    }
                }
            }
            return default(GLTexture);
        }

        private Vector2 ResizeImage(Vector2 textureSize, Vector2 targetSize)
        {
            if (textureSize == Vector2.Zero)
            {
                return targetSize;
            }

            var scaleSize = targetSize / textureSize;
            float scale = Math.Min(scaleSize.X, scaleSize.Y);
            targetSize = textureSize * scale;
            return targetSize;
        }
    }

    public class ShowDiffText: IShowDiff
    {
        private struct LineIndex
        {
            public int Index;
            public string Line;
        }

        private struct DiffText
        {
            public string Text;
            public Dictionary<int,string> AddLines;
            public Dictionary<int, string> RemoveLines;
            public Dictionary<int, string[]> NormalLines;
        }

        DiffText m_diffTexts;
        //private string m_diffContext;
        private float m_diffNumberWidth;

        public bool Build(PatchEntryChanges patchEntryChanges, GitRepo gitRepo)
        {
            BuildDiffTexts(patchEntryChanges, gitRepo);
            return true;
        }

        public void OnDraw()
        {
            DrawDiffStatus();
        }

        public void Clear()
        {
            m_diffTexts.Text = null;
            m_diffTexts.AddLines = null;
            m_diffTexts.RemoveLines = null;
            m_diffTexts.NormalLines = null;
        }

        private void BuildDiffTexts(PatchEntryChanges patchEntryChanges, GitRepo gitRepo)
        {
            string content = patchEntryChanges.Patch;
            m_diffNumberWidth = 0;
            if (string.IsNullOrEmpty(content))
                return;

            if (patchEntryChanges.Status == ChangeKind.Conflicted)
            {
                string textPath = Path.Combine(gitRepo.RootPath, patchEntryChanges.Path);
                if (File.Exists(textPath))
                {
                    content += File.ReadAllText(textPath);
                }
            }

            ////windows换行符 替换为linux换行符
            //content = content.Replace("\r\n", "\n");

            m_diffTexts = new DiffText();
            m_diffTexts.AddLines = new Dictionary<int, string>();
            m_diffTexts.RemoveLines = new Dictionary<int, string>();
            m_diffTexts.NormalLines = new Dictionary<int, string[]>();
            m_diffTexts.Text = content;

            int removeIndex = -1;
            int addIndex = -1;

            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("+++") || line.StartsWith("---"))
                { }
                else if (line.StartsWith("+"))
                {
                    if (addIndex >= 0)
                    {
                        string addIndexStr = addIndex.ToString();
                        m_diffTexts.AddLines.Add(i, addIndexStr);
                        GetNumberItemWidth(addIndexStr);
                        addIndex++;
                    }
                }
                else if (line.StartsWith("-"))
                {
                    if (removeIndex >= 0)
                    {
                        string removeIndexStr = removeIndex.ToString();
                        m_diffTexts.RemoveLines.Add(i, removeIndexStr);
                        GetNumberItemWidth(removeIndexStr);
                        removeIndex++;
                    }
                }
                else if (line.StartsWith("@@"))
                {
                    var lineArgs = line.Split(' ');
                    var removeArgs = lineArgs[1].Split(',');
                    var addArgs = lineArgs[2].Split(',');

                    removeIndex = int.Parse(removeArgs[0]) * -1;
                    addIndex = int.Parse(addArgs[0]);
                }
                else
                {
                    if (addIndex >= 0|| removeIndex>=0)
                    {
                        m_diffTexts.NormalLines.Add(i, new string[] { addIndex.ToString(), removeIndex.ToString() });
                        if (addIndex >= 0)
                        {
                            addIndex++;
                        }
                        if (removeIndex >= 0)
                        {
                            removeIndex++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 绘制不同的状态
        /// </summary>
        private void DrawDiffStatus()
        {
            if (!string.IsNullOrEmpty(m_diffTexts.Text))
            {
                var startPos = ImGui.GetWindowPos() + ImGui.GetCursorPos() - new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());
                float textHeight = ImGui.GetTextLineHeight();

                foreach (var item in m_diffTexts.AddLines)
                {
                    var bgMin = startPos + new Vector2(0, textHeight * item.Key);
                    var bgMax = bgMin + new Vector2(ImGui.GetWindowWidth(), textHeight);
                    ImGui.GetWindowDrawList().AddRectFilled(bgMin, bgMax, LuaPlugin.GetColorU32("NewTextBg"));
                    ImGui.GetWindowDrawList().AddText(bgMin + new Vector2(0, 0), ImGui.GetColorU32(ImGuiCol.Text), item.Value.ToString());
                }

                foreach (var item in m_diffTexts.RemoveLines)
                {
                    var bgMin = startPos + new Vector2(0, textHeight * item.Key);
                    var bgMax = bgMin + new Vector2(ImGui.GetWindowWidth(), textHeight);
                    ImGui.GetWindowDrawList().AddRectFilled(bgMin, bgMax, LuaPlugin.GetColorU32("DeleteTextBg"));
                    ImGui.GetWindowDrawList().AddText(bgMin + new Vector2(m_diffNumberWidth, 0), ImGui.GetColorU32(ImGuiCol.Text), item.Value.ToString());
                }

                foreach (var item in m_diffTexts.NormalLines)
                {
                    var bgMin = startPos + new Vector2(0, textHeight * item.Key);
                    ImGui.GetWindowDrawList().AddText(bgMin + new Vector2(0, 0), ImGui.GetColorU32(ImGuiCol.Text), item.Value[0].ToString());
                    ImGui.GetWindowDrawList().AddText(bgMin + new Vector2(m_diffNumberWidth, 0), ImGui.GetColorU32(ImGuiCol.Text), item.Value[1].ToString());
                }

                ImGui.SetCursorPosX(m_diffNumberWidth * 2 + 5);
                ImGui.TextUnformatted(m_diffTexts.Text);

                var min = ImGui.GetWindowPos() + new Vector2(m_diffNumberWidth * 2, 0);
                var max = min + new Vector2(2, ImGui.GetWindowHeight());
                ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(ImGuiCol.Border));
            }
        }

        private void GetNumberItemWidth(string text)
        {
            var textSize = ImGui.CalcTextSize(text+" ");
            if (textSize.X > m_diffNumberWidth)
            {
                m_diffNumberWidth = textSize.X;
            }
        }

        public void Dispose()
        {
        }
    }


    public class ShowDiffConflicted : IShowDiff
    {
        private string m_content;
        public bool Build(PatchEntryChanges patchEntryChanges, GitRepo gitRepo)
        {
            if (patchEntryChanges.Status == ChangeKind.Conflicted)
            {
                m_content = patchEntryChanges.Patch;
                if (!patchEntryChanges.IsBinaryComparison)
                {
                    string textPath = Path.Combine(gitRepo.RootPath, patchEntryChanges.Path);
                    if (File.Exists(textPath))
                    {
                        m_content += File.ReadAllText(textPath);
                    }
                }
                return true;
            }
            return false;
        }

        public void Clear()
        {
        }

        public void Dispose()
        {
        }

        public void OnDraw()
        {
            if (!string.IsNullOrEmpty(m_content))
            {
                ImGui.TextUnformatted(m_content);
            }
        }
    }

}

