using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.App
{
    public enum AppEvent
    {
        ShowGitRepo,
        SearchGitRepo,
        OpenFile,
        OpenFolder,
        //刷新主页的仓库展示
        RefreshGitRepo,
    }
}
