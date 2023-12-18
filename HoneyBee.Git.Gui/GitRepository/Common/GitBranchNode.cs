using ImGuiNET;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.GitRepository.Common
{
    public abstract class TreeViewNode<T,TData> where T : TreeViewNode<T, TData>, new()
    { 
        public string Name { get; set; }
        public virtual string FullName { get; set; }
        public TData Data { get; set; }
        public List<T> Children { get; set; }

        public virtual bool NodeOpened { get; set; }


        public static void JoinTreeViewNode(List<T> nodes, List<string> paths, List<TData> datas)
        {
            if (paths != null)
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    TData data = default(TData);
                    if (datas != null && i < datas.Count)
                    {
                        data = datas[i];
                    }
                    JoinTreeViewNode(nodes, paths[i], data);
                }
            }
        }

        public static void JoinTreeViewNode(List<T> nodes, string path, TData data)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (nodes == null)
            {
                nodes = new List<T>();
            }
            string[] nameArgs = path.Split('/');
            Queue<string> nameTree = new Queue<string>();
            foreach (var item in nameArgs)
            {
                nameTree.Enqueue(item);
            }

            JoinTreeViewNode(nodes, nameTree, data);
        }


        private static void JoinTreeViewNode(List<T> nodes, Queue<string> nameTree, TData data)
        {
            if (nameTree.Count == 1)
            {
                T node = new T();
                node.Name = nameTree.Dequeue();
                node.Data = data;
                //branchNode.FullName = branch.FriendlyName;
                nodes.Add(node);
            }
            else
            {
                string name = nameTree.Dequeue();
                var findNode = nodes.Find(x => x.Name.Equals(name));
                if (findNode == null)
                {
                    findNode = new T();
                    findNode.Name = name;
                    findNode.Children = new List<T>();
                    nodes.Add(findNode);
                }
                JoinTreeViewNode(findNode.Children, nameTree, data);
            }
        }

    }

    public class BranchTreeViewNode: TreeViewNode<BranchTreeViewNode, Branch>
    {
        public override string FullName 
        {
            get
            {
                return Data != null ? Data.FriendlyName : null;
            }
        }
        public int BehindBy { get; set; }
        public int AheadBy { get; set; }

        public void UpdateByIndex()
        {
            AheadBy = 0;
            BehindBy = 0;
            if (Children != null)
            {
                foreach (var item in Children)
                {
                    item.UpdateByIndex();
                    AheadBy += item.AheadBy;
                    BehindBy += item.BehindBy;
                }
            }
            if (Data != null && Data.IsTracking)
            {
                var trackingDetails = Data.TrackingDetails;
                if (trackingDetails != null)
                {
                    if (trackingDetails.BehindBy != null)
                    {
                        BehindBy += (int)trackingDetails.BehindBy;
                    }
                    if (trackingDetails.AheadBy != null)
                    {
                        AheadBy += (int)trackingDetails.AheadBy;
                    }
                }
            }
        }
    }

    public class StatusEntryTreeViewNode : TreeViewNode<StatusEntryTreeViewNode, StatusEntry>
    {
        public override string FullName
        {
            get
            {
                return Data != null ? Data.FilePath : null;
            }
        }

        public override bool NodeOpened { get; set; } = true;
    }


	//public class GitBranchNode
	//{
	//    public string Name { get; set; }
	//    public string FullName { get; set; }
	//    public Branch Branch { get; set; }
	//    public List<GitBranchNode> Children { get; set; }
	//    public int BehindBy { get; set; }
	//    public int AheadBy { get; set; }

	//    public void UpdateByIndex()
	//    {
	//        AheadBy = 0;
	//        BehindBy = 0;
	//        if (Children != null)
	//        {
	//            foreach (var item in Children)
	//            {
	//                item.UpdateByIndex();
	//                AheadBy += item.AheadBy;
	//                BehindBy += item.BehindBy;
	//            }
	//        }
	//        if (Branch != null && Branch.IsTracking)
	//        {
	//            var trackingDetails = Branch.TrackingDetails;
	//            if (trackingDetails != null)
	//            {
	//                if (trackingDetails.BehindBy != null)
	//                {
	//                    BehindBy += (int)trackingDetails.BehindBy;
	//                }
	//                if (trackingDetails.AheadBy != null)
	//                {
	//                    AheadBy += (int)trackingDetails.AheadBy;
	//                }
	//            }
	//        }
	//    }
	//}
}
