using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.GitRepository.Common
{
    public class GitBranchNode
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public Branch Branch { get; set; }
        public List<GitBranchNode> Children { get; set; }
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
            if (Branch != null && Branch.IsTracking)
            {
                var trackingDetails = Branch.TrackingDetails;
                BehindBy += (int)trackingDetails.BehindBy;
                AheadBy += (int)trackingDetails.AheadBy;
            }
        }
    }
}
