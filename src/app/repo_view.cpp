#include "repo_view.h"
#include "../ui/workspace_panel.h"
#include "../ui/worktree_panel.h"
#include "../ui/log_panel.h"
#include "../ui/diff_panel.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>

RepoView::RepoView(std::shared_ptr<GitRepository> repo)
    : m_repository(std::move(repo))
{
    m_repoPath = m_repository->GetRootPath();
    m_workspacePanel = std::make_unique<WorkspacePanel>();
    m_worktreePanel = std::make_unique<WorkTreePanel>();
    m_logPanel = std::make_unique<LogPanel>();
    m_diffPanel = std::make_unique<DiffPanel>();

    m_workspacePanel->SetRepository(m_repository);
    m_worktreePanel->SetRepository(m_repository);
    m_logPanel->SetRepository(m_repository);
    m_diffPanel->SetRepository(m_repository);

    m_logPanel->OnCommitSelected = [this](const GitCommit& commit) {
        m_diffPanel->ShowCommitDetail(commit);
        m_activeSection = Section::Changes;
    };
}

RepoView::~RepoView() = default;

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
    RenderToolbar();
    m_splitView.Begin();
    RenderSidebar();
    m_splitView.Separate();
    RenderContent();
    m_splitView.End();
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
        if (ImGui::Button(tools[i].label))
            DoGitAction(tools[i].label);
        if (ImGui::IsItemHovered())
            ImGui::SetTooltip("%s", tools[i].tip);
        if (i < 5) ImGui::SameLine();
    }

    ImGui::EndChild();
    ImGui::Separator();
}

void RepoView::DoGitAction(const char* action)
{
    std::string act = action;

    if (act == "Sync") RefreshAll();
    else if (act == "Pull") { m_repository->Pull(); RefreshAll(); }
    else if (act == "Push") { m_repository->Push(); RefreshAll(); }
    else if (act == "Fetch") { m_repository->Fetch(); RefreshAll(); m_branchDataDirty = true; }
    else if (act == "Terminal")
    {
        std::string cmd = "start cmd /K cd /D \"" + m_repoPath + "\"";
        system(cmd.c_str());
    }
    else if (act == "Explorer")
    {
        std::string cmd = "explorer \"" + m_repoPath + "\"";
        system(cmd.c_str());
    }
}

void RepoView::RenderSidebar()
{
    ImGui::BeginChild("##sidebar", ImVec2(0, 0), true);
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(4, 4));

    RenderSidebarSection("Workspace", Section::Workspace);
    RenderSidebarSection("Files", Section::Files);
    RenderSidebarSection("Changes", Section::Changes);
    RenderSidebarSection("History", Section::History);

    if (m_branchDataDirty) RefreshBranchData();

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
                if (ImGui::MenuItem(b.name.c_str()))
                {
                    m_repository->CheckoutBranch(b.name);
                    RefreshAll();
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
        case Section::Changes:
            if (m_diffPanel) m_diffPanel->Render();
            break;
        case Section::History:
            if (m_logPanel) m_logPanel->Render();
            break;
    }
}

void RepoView::RefreshBranchData()
{
    m_localBranches.clear();
    m_remoteBranches.clear();
    m_tagNames.clear();

    auto branches = m_repository->GetBranches();
    for (auto& b : branches)
        m_localBranches.push_back({b.name, b.isHead, false});

    auto remotes = m_repository->GetRemoteBranches();
    for (auto& r : remotes)
        m_remoteBranches.push_back({r.name, false, true});

    auto tags = m_repository->GetTags();
    for (auto& t : tags)
        m_tagNames.push_back(t.name);

    m_stashCount = 0;
    m_branchDataDirty = false;
}

void RepoView::RefreshAll()
{
    if (m_workspacePanel) m_workspacePanel->Refresh();
    if (m_worktreePanel) m_worktreePanel->Refresh();
    if (m_logPanel) m_logPanel->Refresh();
    m_branchDataDirty = true;
}
