using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.GitRepository.Common
{
    public class GitRepoCommit
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public string Author { get; set; }
        public string Commit { get; set; }
        // 完整的信息
        public string Message { get; set; }
        //邮件
        public string Email { get; set; }
        //父级提交
        public List<string> Parents { get; set; }
    }

}
