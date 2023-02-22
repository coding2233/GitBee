using strange.extensions.context.api;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.GitRepository.Common;

namespace Wanderer.TextCodeEditor.View
{
    internal class TextEditorFolderView : EventView
    {
        string m_folderPath;

        FolderTreeViewNode m_folderTreeView;

        public TextEditorFolderView(IContext context,string folderPath) : base(context)
        {
            m_folderPath = folderPath;

            m_folderTreeView.ad
        }


        public void OnDraw()
        {
            //dispatcher.Dispatch("OpenFile",)
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }


        private void BuildFolderNode(FolderTreeViewNode viewNode, string folderPath)
        {
            viewNode.FullName = folderPath;
            View
        }
        
    }

    public class FolderTreeViewNode : TreeViewNode<FolderTreeViewNode,string>
    {
        
    }
}
