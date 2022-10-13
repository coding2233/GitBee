using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.GitRepository.Common
{
    public class GitBranchCommit
    {
        public int Id { get; set; }
        public string BranchName { get; set; }
        public string Commit { get; set; }
    }
}
