using ImGuiNET;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
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
        public bool Build(PatchEntryChanges patchEntryChanges, GitRepo gitRepo)
        {
            if (CheckTexture(patchEntryChanges.OldPath) || CheckTexture(patchEntryChanges.Path))
            {
                m_oldGLTexture = GetTextureFromBlob(patchEntryChanges.OldOid, gitRepo);
                m_newGLTexture = GetTextureFromBlob(patchEntryChanges.Oid, gitRepo);
               
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
        }

        public void Dispose()
        {
        }

        public void OnDraw()
        {
            var targetSize = ImGui.GetWindowSize()*0.5f;
            ImGui.Image(m_oldGLTexture.Image, ResizeImage(m_oldGLTexture.Size, targetSize));
            ImGui.SameLine();
            ImGui.SetCursorPosX(targetSize.X);
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
        private struct DiffText
        {
            public string Text;
            public int Status;
            public string RemoveText;
            public string AddText;
        }

        List<DiffText> m_diffTexts = new List<DiffText>();
        //private string m_diffContext;
        private float m_diffNumberWidth;

        public bool Build(PatchEntryChanges patchEntryChanges, GitRepo gitRepo)
        {
            BuildDiffTexts(patchEntryChanges.Patch);
            return true;
        }

        public void OnDraw()
        {
            DrawDiffStatus();
        }

        public void Clear()
        { }

        private void BuildDiffTexts(string content)
        {
            m_diffNumberWidth = 0;
            m_diffTexts.Clear();
            if (string.IsNullOrEmpty(content))
                return;

            ////windows换行符 替换为linux换行符
            //content = content.Replace("\r\n", "\n");

            int removeIndex = -1;
            int addIndex = -1;

            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                DiffText diffText = new DiffText();
                diffText.Text = line;
                diffText.RemoveText = " ";
                diffText.AddText = " ";
                if (line.StartsWith("+++") || line.StartsWith("---"))
                { }
                else if (line.StartsWith("+"))
                {
                    if (addIndex >= 0)
                    {
                        diffText.Status = 1;
                        addIndex++;
                        //diffText.RemoveText = " ";
                        diffText.AddText = $"{(addIndex - 1)} ";
                        GetNumberItemWidth(diffText.AddText);
                    }
                }
                else if (line.StartsWith("-"))
                {
                    if (removeIndex >= 0)
                    {
                        diffText.Status = 2;
                        removeIndex++;

                        //diffText.AddText = " ";
                        diffText.RemoveText = $"{(removeIndex - 1)} ";
                        GetNumberItemWidth(diffText.RemoveText);
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

                if (diffText.Status == 0)
                {
                    if (!line.StartsWith("@@") && addIndex >= 0 && removeIndex >= 0)
                    {
                        addIndex++;
                        removeIndex++;
                        diffText.AddText = (addIndex - 1).ToString();
                        GetNumberItemWidth(diffText.AddText);
                        diffText.RemoveText = (removeIndex - 1).ToString();
                        GetNumberItemWidth(diffText.RemoveText);
                    }
                    else
                    {
                        //diffText.AddText = " ";
                        //diffText.RemoveText = " ";
                    }
                }


                m_diffTexts.Add(diffText);
            }
        }


        /// <summary>
        /// 绘制不同的状态
        /// </summary>
        private void DrawDiffStatus()
        {
            if (m_diffTexts != null && m_diffTexts.Count > 0)
            {
                foreach (var item in m_diffTexts)
                {
                    RenderDiffTextLine(item);
                }

                var min = ImGui.GetWindowPos() + new Vector2(m_diffNumberWidth * 2, 0);
                var max = min + new Vector2(2, ImGui.GetWindowHeight());
                ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(ImGuiCol.Border));
            }
        }

        private void RenderDiffTextLine(DiffText line)
        {
            uint col = 0;
            if (line.Status == 1)
            {
                var styleColorsValue = ImGuiView.StyleColors == 0 ? 0.5f : 0.0f;
                col = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2235f, 0.3607f, 0.2431f, 1f) + Vector4.One * styleColorsValue);
            }
            else if (line.Status == 2)
            {
                var styleColorsValue = ImGuiView.StyleColors == 0 ? 0.5f : 0.0f;
                col = ImGui.ColorConvertFloat4ToU32(new Vector4(0.3725f, 0.2705f, 0.3019f, 1f) + Vector4.One * styleColorsValue);
            }
            if (col != 0)
            {
                //ImGui.GetBackgroundDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), col);
                //var min = ImGui.GetItemRectMin();
                //var max = ImGui.GetItemRectMax();
                var min = ImGui.GetWindowPos() + ImGui.GetCursorPos() - new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());
                var max = min + new Vector2(ImGui.GetWindowWidth(), ImGui.GetTextLineHeightWithSpacing());
                ImGui.GetWindowDrawList().AddRectFilled(min, max, col);
            }

            ImGui.SetCursorPosX(0);
            //ImGui.SetNextItemWidth(50);
            ImGui.Text(line.RemoveText);
            ImGui.SameLine();
            ImGui.SetCursorPosX(m_diffNumberWidth);
            //ImGui.SetNextItemWidth(50);
            ImGui.Text(line.AddText);
            ImGui.SameLine();
            ImGui.SetCursorPosX(m_diffNumberWidth * 2 + 5);
            ImGui.TextUnformatted(line.Text);
        }


        private void GetNumberItemWidth(string text)
        {
            var textSize = ImGui.CalcTextSize(text);
            if (textSize.X > m_diffNumberWidth)
            {
                m_diffNumberWidth = textSize.X;
            }
        }

        public void Dispose()
        {
        }
    }
}
