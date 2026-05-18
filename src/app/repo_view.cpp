#include "repo_view.h"
#include "../ui/workspace_panel.h"
#include "../ui/worktree_panel.h"
#include "../ui/log_panel.h"
#include "../ui/config_panel.h"
#include "../ui/LoadingSpinner.h"
#include "../gitcore/git_repository.h"
#include "../gitcore/git_process.h"
#include <sstream>
#include <imgui.h>

RepoView::RepoView(std::shared_ptr<GitRepository> repo)
    : m_repository(std::move(repo))
{
    m_repoPath = m_repository->GetRootPath();
    m_workspacePanel = std::make_unique<WorkspacePanel>();
    m_worktreePanel = std::make_unique<WorkTreePanel>();
    m_logPanel = std::make_unique<LogPanel>();
    m_configPanel = std::make_unique<ConfigPanel>();

    m_workspacePanel->SetRepository(m_repository);
    m_worktreePanel->SetRepository(m_repository);
    m_logPanel->SetRepository(m_repository);
    m_configPanel->SetRepository(m_repository);
}

RepoView::~RepoView()
{
    if (m_branchThread.joinable())
        m_branchThread.join();
    if (m_checkoutThread.joinable())
        m_checkoutThread.join();
}

std::string RepoView::GetName() const
{
    std::string path = m_repoPath;
    auto slash = path.find_last_of("\\/");
    if (slash != std::string::npos)
        return path.substr(slash + 1);
    return path;
}

void RepoView::Render()
{
    ProcessAsyncResult();
    ProcessCheckoutResult();
    RenderToolbar();
    m_splitView.Begin();
    RenderSidebar();
    m_splitView.Separate();
    RenderContent();
    m_splitView.End();
}

void RepoView::ProcessAsyncResult()
{
    if (m_processingAsyncResult) return;
    if (m_asyncTask.running) return;
    if (m_asyncTask.name.empty()) return;

    m_processingAsyncResult = true;

    if (m_asyncTask.result)
    {
        if (OnStatusMessage)
            OnStatusMessage(m_asyncTask.name + " completed");
    }
    else
    {
        std::string errMsg = m_asyncTask.name + " failed";
        if (!m_asyncTask.error.empty())
            errMsg += ": " + m_asyncTask.error;
        if (OnStatusMessage)
            OnStatusMessage(errMsg);
    }

    RefreshAll();
    m_asyncTask.running = false;
    m_asyncTask.name.clear();
    m_processingAsyncResult = false;
}

void RepoView::RenderToolbar()
{
    ImGui::BeginChild("##toolbar", ImVec2(0, ImGui::GetFrameHeight() + 4), false);

    struct { const char* label; const char* tip; } tools[] = {
        {"Sync", "Sync all changes"},
        {"Pull", "Pull from remote"},
        {"Push", "Push to remote"},
        {"Fetch", "Fetch from all remotes"},
        {"Terminal", "Open terminal here"},
        {"Explorer", "Open in Explorer"},
    };

    for (int i = 0; i < 6; i++)
    {
        bool isGitAction = (i <= 3);
        bool disabled = isGitAction && m_asyncTask.running;

        if (disabled) { ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true); ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f); }
        // Show spinner on sync button when branch data is loading
        if (i == 0 && m_branchDataLoading)
        {
            LoadingSpinner(6.0f, 2.0f);
            ImGui::SameLine();
        }

        if (ImGui::Button(tools[i].label))
            DoGitAction(tools[i].label);
        if (ImGui::IsItemHovered())
            ImGui::SetTooltip("%s", tools[i].tip);

        if (disabled) { ImGui::PopStyleVar(); ImGui::PopItemFlag(); }

        if (i < 5) ImGui::SameLine();
    }

    ImGui::EndChild();
    ImGui::Separator();
}

void RepoView::DoGitAction(const char* action)
{
    std::string act = action;

    if (act == "Sync") { RefreshAll(); return; }
    if (act == "Terminal")
    {
#ifdef _WIN32
        std::string cmd = "start cmd /K cd /D \"" + m_repoPath + "\"";
#else
        std::string cmd = "x-terminal-emulator --working-directory=\"" + m_repoPath + "\"";
#endif
        system(cmd.c_str());
        return;
    }
    if (act == "Explorer")
    {
#ifdef _WIN32
        std::string cmd = "explorer \"" + m_repoPath + "\"";
#elif defined(__APPLE__)
        std::string cmd = "open \"" + m_repoPath + "\"";
#else
        std::string cmd = "xdg-open \"" + m_repoPath + "\"";
#endif
        system(cmd.c_str());
        return;
    }

    if (m_asyncTask.running) return;

    m_asyncTask.running = true;
    m_asyncTask.result = false;
    m_asyncTask.error.clear();
    m_asyncTask.name = act;

    if (OnStatusMessage) OnStatusMessage(act + "ing...");

    auto repo = m_repository;
    std::string repoName = GetName();

    m_asyncTask.thread = std::thread([this, repo, act, repoName]() {
        std::vector<std::string> gitArgs;
        if (act == "Pull")      gitArgs = {"pull"};
        else if (act == "Push") gitArgs = {"push"};
        else if (act == "Fetch") gitArgs = {"fetch", "--all"};

        auto r = GitProcess::Execute(repo->GetPath(), gitArgs);

        m_asyncTask.result = r.ok;
        m_asyncTask.error = r.err;
        m_asyncTask.running = false;

        // Build summary from output
        std::string summary;
        if (r.ok)
        {
            if (!r.out.empty())
            {
                // Pick the most informative line from output
                std::istringstream stream(r.out);
                std::string line;
                while (std::getline(stream, line))
                {
                    if (line.find("From ") == 0 || line.find("-> ") != std::string::npos ||
                        line.find("Updating") != std::string::npos || line.find("Fast-forward") != std::string::npos ||
                        line.find("Already up") != std::string::npos || line.find("Done") != std::string::npos ||
                        line.find("remote:") == 0 || line.find(" * ") != std::string::npos)
                        summary += line + "\n";
                }
                if (summary.empty()) summary = r.out.substr(0, std::min(r.out.size(), size_t(200)));
            }
            else
            {
                summary = act + " completed successfully";
            }
        }
        else
        {
            summary = r.err.empty() ? (act + " failed") : r.err;
        }

        // Report through callback
        if (OnOperationLog)
            OnOperationLog(act, r.ok, summary, r.out + "\n--- stderr ---\n" + r.err);
    });
    m_asyncTask.thread.detach();
}

void RepoView::RenderSidebar()
{
    ImGui::BeginChild("##sidebar", ImVec2(0, 0), true);
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(4, 4));

    RenderSidebarSection("Workspace", Section::Workspace);
    RenderSidebarSection("Files", Section::Files);
    RenderSidebarSection("History", Section::History);
    RenderSidebarSection("Config", Section::Config);

    // Process async branch results
    if (!m_branchDataLoading && m_branchDataDirty && !m_pendingBranchData.localBranches.empty())
    {
        if (m_branchThread.joinable())
            m_branchThread.join();

        std::lock_guard<std::mutex> lock(m_branchMutex);
        m_localBranches = std::move(m_pendingBranchData.localBranches);
        m_remoteBranches = std::move(m_pendingBranchData.remoteBranches);
        m_tagNames = std::move(m_pendingBranchData.tagNames);
        m_stashCount = m_pendingBranchData.stashCount;
        m_branchDataDirty = false;
    }

    if (m_branchDataDirty && !m_branchDataLoading) RefreshBranchData();

    if (m_branchDataLoading)
    {
        LoadingSpinnerWithText("Loading branches...");
    }
    else
    {
        bool branchOpen = ImGui::CollapsingHeader("Branch", ImGuiTreeNodeFlags_DefaultOpen);
        if (branchOpen)
        {
            ImGui::Indent(8);
            for (auto& b : m_localBranches)
            {
                ImGui::PushID(b.name.c_str());
                if (b.isHead)
                {
                    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.3f, 0.8f, 0.3f, 1.0f));
                    ImGui::MenuItem(b.name.c_str(), nullptr, true, false);
                    ImGui::PopStyleColor();
                }
                else
                {
                    bool isCheckouting = m_checkoutLoading && m_checkoutBranchName == b.name;
                    if (isCheckouting)
                    {
                        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.5f, 0.5f, 1.0f));
                        ImGui::MenuItem(b.name.c_str(), nullptr, false, false);
                        ImGui::PopStyleColor();
                        ImGui::SameLine();
                        LoadingSpinner(5.0f, 1.5f);
                    }
                    else
                    {
                        bool disabled = m_checkoutLoading;
                        if (disabled) ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true);
                        if (ImGui::MenuItem(b.name.c_str()))
                        {
                            // single click does nothing
                        }
                        if (ImGui::IsItemHovered() && ImGui::IsMouseDoubleClicked(0))
                            StartAsyncCheckout(b.name);
                        if (disabled) ImGui::PopItemFlag();
                    }
                }
                ImGui::PopID();
            }
            ImGui::Unindent(8);
        }

        bool remoteOpen = ImGui::CollapsingHeader("Remote");
        if (remoteOpen)
        {
            ImGui::Indent(8);
            for (auto& b : m_remoteBranches)
            {
                ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.7f, 1.0f, 1.0f));
                ImGui::TextUnformatted(b.name.c_str());
                ImGui::PopStyleColor();
            }
            ImGui::Unindent(8);
        }

        bool tagOpen = ImGui::CollapsingHeader("Tag");
        if (tagOpen)
        {
            ImGui::Indent(8);
            for (auto& t : m_tagNames)
                ImGui::TextUnformatted(t.c_str());
            ImGui::Unindent(8);
        }

        bool stashOpen = ImGui::CollapsingHeader("Stashes");
        if (stashOpen)
        {
            ImGui::Indent(8);
            if (m_stashCount <= 0)
                ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No stashes");
            else
                ImGui::Text("%d stash entries", m_stashCount);
            ImGui::Unindent(8);
        }
    }

    ImGui::PopStyleVar();
    ImGui::EndChild();
}

void RepoView::RenderSidebarSection(const char* name, Section section)
{
    bool selected = (m_activeSection == section);
    if (ImGui::Selectable(name, selected))
        m_activeSection = section;
}

void RepoView::RenderContent()
{
    switch (m_activeSection)
    {
        case Section::Workspace:
            if (m_workspacePanel) m_workspacePanel->Render();
            break;
        case Section::Files:
            if (m_worktreePanel) m_worktreePanel->Render();
            break;
        case Section::History:
            if (m_logPanel) m_logPanel->Render();
            break;
        case Section::Config:
            if (m_configPanel) m_configPanel->Render();
            break;
    }
}

void RepoView::StartAsyncCheckout(const std::string& branchName)
{
    if (m_checkoutLoading) return;

    m_checkoutLoading = true;
    m_checkoutBranchName = branchName;
    m_checkoutError.clear();
    if (OnStatusMessage) OnStatusMessage("Checking out " + branchName + "...");

    auto repo = m_repository;
    std::string repoName = GetName();

    m_checkoutThread = std::thread([this, repo, branchName, repoName]() {
        auto r = GitProcess::Execute(repo->GetPath(), {"checkout", branchName});
        if (!r.ok)
            m_checkoutError = r.err;

        m_checkoutLoading = false;

        std::string summary;
        if (r.ok)
        {
            summary = "Switched to branch '" + branchName + "'";
            if (!r.out.empty())
            {
                auto nl = r.out.find('\n');
                summary = (nl != std::string::npos) ? r.out.substr(0, nl) : r.out;
            }
        }
        else
        {
            summary = r.err.empty() ? ("Checkout '" + branchName + "' failed") : r.err;
        }

        if (OnOperationLog)
            OnOperationLog("Checkout", r.ok, summary, r.out + "\n--- stderr ---\n" + r.err);
    });
    m_checkoutThread.detach();
}

void RepoView::ProcessCheckoutResult()
{
    if (m_checkoutLoading) return;
    if (m_checkoutBranchName.empty()) return;
    if (m_checkoutThread.joinable())
        m_checkoutThread.join();

    if (m_checkoutError.empty())
    {
        if (OnStatusMessage) OnStatusMessage("Switched to " + m_checkoutBranchName);
        RefreshAll();
    }
    else
    {
        if (OnStatusMessage) OnStatusMessage("Checkout " + m_checkoutBranchName + " failed: " + m_checkoutError);
    }

    m_checkoutBranchName.clear();
    m_checkoutError.clear();
}

void RepoView::RefreshBranchData()
{
    if (m_branchDataLoading || !m_branchDataDirty) return;
    m_branchDataLoading = true;

    auto repo = m_repository;
    m_branchThread = std::thread([this, repo]() {
        BranchData data;

        auto branches = repo->GetBranches();
        for (auto& b : branches)
            data.localBranches.push_back({b.name, b.isHead, false});

        auto remotes = repo->GetRemoteBranches();
        for (auto& r : remotes)
            data.remoteBranches.push_back({r.name, false, true});

        auto tags = repo->GetTags();
        for (auto& t : tags)
            data.tagNames.push_back(t.name);

        data.stashCount = 0;

        {
            std::lock_guard<std::mutex> lock(m_branchMutex);
            m_pendingBranchData = std::move(data);
        }
        m_branchDataLoading = false;
    });
    m_branchThread.detach();
}

void RepoView::RefreshAll()
{
    if (m_workspacePanel) m_workspacePanel->Refresh();
    if (m_worktreePanel) m_worktreePanel->Refresh();
    if (m_logPanel) m_logPanel->Refresh();
    if (m_configPanel) m_configPanel->Refresh();
    m_branchDataDirty = true;
}
