#include "workspace_panel.h"
#include "LoadingSpinner.h"
#include "../gitcore/git_repository.h"
#include "../gitcore/git_process.h"
#include <imgui.h>
#include <algorithm>
#include <sstream>

WorkspacePanel::WorkspacePanel() {}

WorkspacePanel::~WorkspacePanel()
{
    if (m_statusThread.joinable())
        m_statusThread.join();
}

void WorkspacePanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = std::move(repo);
    m_selectedStagedPaths.clear();
    m_selectedUnstagedPaths.clear();
    Refresh();
}

void WorkspacePanel::StartAsyncRefresh()
{
    if (m_statusLoading || !m_repository) return;
    m_statusLoading = true;

    auto repo = m_repository;
    m_statusThread = std::thread([this, repo]() {
        GitStatus status;
        auto r1 = GitProcess::Execute(repo->GetPath(), {"rev-parse", "--abbrev-ref", "HEAD"});
        if (r1.ok) status.currentBranch = r1.out;

        auto r2 = GitProcess::Execute(repo->GetPath(), {"rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}"});
        if (r2.ok) status.upstreamBranch = r2.out;

        auto r3 = GitProcess::Execute(repo->GetPath(), {"rev-list", "--left-right", "--count", "HEAD...@{upstream}"});
        if (r3.ok) {
            auto space = r3.out.find('\t');
            if (space != std::string::npos) {
                status.aheadCount = std::stoi(r3.out.substr(0, space));
                status.behindCount = std::stoi(r3.out.substr(space + 1));
            }
        }

        auto r4 = GitProcess::Execute(repo->GetPath(), {"status", "--porcelain", "-u"});
        if (r4.ok) {
            std::istringstream stream(r4.out);
            std::string line;
            while (std::getline(stream, line)) {
                if (line.size() < 3) continue;
                GitFileEntry entry;
                entry.filename = line.substr(3);
                char x = line[0], y = line[1];

                if (x == '?' && y == '?') {
                    entry.status = GitFileStatus::Untracked;
                    entry.isStaged = false;
                    status.untrackedFiles.push_back(entry);
                    status.totalChanges++;
                    continue;
                }
                if (x == 'U' || y == 'U') status.hasMergeConflict = true;

                if (x != ' ' && x != '?') {
                    entry.isStaged = true;
                    switch (x) {
                        case 'M': entry.status = GitFileStatus::StagedModified; break;
                        case 'A': entry.status = GitFileStatus::StagedAdded; break;
                        case 'D': entry.status = GitFileStatus::StagedDeleted; break;
                        case 'R': entry.status = GitFileStatus::Renamed; break;
                        default: entry.status = GitFileStatus::Unknown; break;
                    }
                    if (entry.status == GitFileStatus::Renamed) {
                        auto arrow = entry.filename.find(" -> ");
                        if (arrow != std::string::npos) {
                            entry.oldFilename = entry.filename.substr(0, arrow);
                            entry.filename = entry.filename.substr(arrow + 4);
                        }
                    }
                    status.stagedFiles.push_back(entry);
                    status.totalChanges++;
                }
                if (y != ' ' && x != '?' && x != '!') {
                    entry.isStaged = false;
                    switch (y) {
                        case 'M': entry.status = GitFileStatus::Modified; break;
                        case 'A': entry.status = GitFileStatus::Added; break;
                        case 'D': entry.status = GitFileStatus::Deleted; break;
                        default: entry.status = GitFileStatus::Unknown; break;
                    }
                    if (x == ' ' || x == 'R') {
                        auto arrow = entry.filename.find(" -> ");
                        if (arrow != std::string::npos) {
                            entry.oldFilename = entry.filename.substr(0, arrow);
                            entry.filename = entry.filename.substr(arrow + 4);
                        }
                    }
                    status.unstagedFiles.push_back(entry);
                    status.totalChanges++;
                }
            }
        }

        {
            std::lock_guard<std::mutex> lock(m_statusMutex);
            m_pendingStatus = std::move(status);
        }
        m_statusLoading = false;
    });
    m_statusThread.detach();
}

void WorkspacePanel::ProcessAsyncResult()
{
    if (m_statusLoading) return;
    if (!m_updating) return;

    GitStatus pending;
    {
        std::lock_guard<std::mutex> lock(m_statusMutex);
        pending = std::move(m_pendingStatus);
        m_pendingStatus = GitStatus{};
    }

    m_status = std::move(pending);
    m_updating = false;
}

void WorkspacePanel::Refresh()
{
    if (m_repository) {
        m_updating = true;
        StartAsyncRefresh();
    }
}

void WorkspacePanel::Render()
{
    if (!m_repository) return;

    ProcessAsyncResult();

    float commitAreaH = 80;
    ImGui::BeginChild("##workspace_content",
        ImVec2(0, ImGui::GetContentRegionAvail().y - commitAreaH), true);

    if (m_updating) {
        LoadingSpinnerWithText("Loading workspace status...");
        ImGui::EndChild();
        return;
    }

    if (m_diffOpen) {
        m_hSplit.Begin();

        m_vSplit.Begin();
        RenderStagedArea();
        m_vSplit.Separate();
        RenderUnstagedArea();
        m_vSplit.End();

        m_hSplit.Separate();

        RenderDiff(m_diffFilePath, m_diffIsStaged);

        m_hSplit.End();
    } else {
        m_vSplit.Begin();
        RenderStagedArea();
        m_vSplit.Separate();
        RenderUnstagedArea();
        m_vSplit.End();
    }

    ImGui::EndChild();
    RenderCommitArea();
}

// ---- Status icon helpers ----

const char* WorkspacePanel::StatusIcon(GitFileStatus s) const
{
    switch (s) {
        case GitFileStatus::Modified:
        case GitFileStatus::StagedModified: return "M";
        case GitFileStatus::Added:
        case GitFileStatus::StagedAdded:
        case GitFileStatus::Untracked:      return "A";
        case GitFileStatus::Deleted:
        case GitFileStatus::StagedDeleted:  return "D";
        case GitFileStatus::Renamed:        return "R";
        case GitFileStatus::Unmerged:       return "!";
        default:                            return " ";
    }
}

ImU32 WorkspacePanel::StatusColor(GitFileStatus s) const
{
    switch (s) {
        case GitFileStatus::Modified:
        case GitFileStatus::StagedModified: return IM_COL32(200, 200, 0, 255);
        case GitFileStatus::Added:
        case GitFileStatus::StagedAdded:
        case GitFileStatus::Untracked:      return IM_COL32(0, 180, 0, 255);
        case GitFileStatus::Deleted:
        case GitFileStatus::StagedDeleted:  return IM_COL32(200, 60, 60, 255);
        case GitFileStatus::Renamed:        return IM_COL32(100, 200, 255, 255);
        case GitFileStatus::Unmerged:       return IM_COL32(255, 0, 255, 255);
        default:                            return IM_COL32(128, 128, 128, 255);
    }
}

// ---- Build recursive tree ----

void WorkspacePanel::BuildTree(const std::vector<GitFileEntry>& files, FileTreeNode& root)
{
    for (auto& f : files) {
        FileTreeNode* node = &root;
        size_t start = 0;
        while (start < f.filename.size()) {
            auto slash = f.filename.find('/', start);
            std::string part;
            bool isLeaf = (slash == std::string::npos);
            if (isLeaf) {
                part = f.filename.substr(start);
            } else {
                part = f.filename.substr(start, slash - start);
            }

            bool found = false;
            for (auto& child : node->children) {
                if (child.name == part) {
                    node = &child;
                    found = true;
                    break;
                }
            }

            if (!found) {
                node->children.push_back({});
                auto& newChild = node->children.back();
                newChild.name = part;
                if (node->fullPath.empty())
                    newChild.fullPath = part;
                else
                    newChild.fullPath = node->fullPath + "/" + part;

                if (isLeaf) {
                    newChild.file = &f;
                }
                node = &newChild;
            } else if (isLeaf) {
                node->file = &f;
            }

            if (isLeaf) break;
            start = slash + 1;
        }
    }
}

// ---- Staged / Unstaged areas ----

void WorkspacePanel::RenderStagedArea()
{
    ImGui::TextColored(ImVec4(0.3f, 0.8f, 0.3f, 1.0f), "Staged Changes  (%d)",
        (int)m_status.stagedFiles.size());
    ImGui::SameLine(ImGui::GetWindowWidth() - 70);
    if (ImGui::SmallButton(m_treeView ? "List" : "Tree"))
        m_treeView = !m_treeView;
    ImGui::Separator();

    if (ImGui::Button("Unstage All")) {
        m_repository->Unstage();
        Refresh();
    }
    ImGui::SameLine();
    if (!m_selectedStagedPaths.empty()) {
        if (ImGui::Button("Unstage Selected")) {
            std::vector<std::string> v(m_selectedStagedPaths.begin(),
                m_selectedStagedPaths.end());
            m_repository->Unstage(v);
            m_selectedStagedPaths.clear();
            Refresh();
        }
    }

    ImGui::BeginChild("##staged_list", ImVec2(0, 0), true);
    if (m_status.stagedFiles.empty()) {
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No staged changes");
    } else if (m_treeView) {
        FileTreeNode root;
        BuildTree(m_status.stagedFiles, root);
        ImGui::Indent(0);
        for (auto& child : root.children)
            RenderTreeNode(child, true);
    } else {
        for (auto& f : m_status.stagedFiles)
            RenderFileRow(f.filename, f.oldFilename, f.status, true);
    }
    ImGui::EndChild();
}

void WorkspacePanel::RenderUnstagedArea()
{
    ImGui::TextColored(ImVec4(1.0f, 1.0f, 0.3f, 1.0f), "Unstaged Changes  (%d)",
        (int)(m_status.unstagedFiles.size() + m_status.untrackedFiles.size()));
    ImGui::SameLine(ImGui::GetWindowWidth() - 70);
    if (ImGui::SmallButton(m_treeView ? "List" : "Tree"))
        m_treeView = !m_treeView;
    ImGui::Separator();

    if (ImGui::Button("Stage All")) {
        m_repository->Stage();
        Refresh();
    }
    ImGui::SameLine();
    if (!m_selectedUnstagedPaths.empty()) {
        if (ImGui::Button("Stage Selected")) {
            std::vector<std::string> v(m_selectedUnstagedPaths.begin(),
                m_selectedUnstagedPaths.end());
            m_repository->Stage(v);
            m_selectedUnstagedPaths.clear();
            Refresh();
        }
        ImGui::SameLine();
        if (ImGui::Button("Discard Selected")) {
            std::vector<std::string> v(m_selectedUnstagedPaths.begin(),
                m_selectedUnstagedPaths.end());
            m_repository->Discard(v);
            m_selectedUnstagedPaths.clear();
            Refresh();
        }
    }

    ImGui::BeginChild("##unstaged_list", ImVec2(0, 0), true);

    std::vector<GitFileEntry> allUnstaged = m_status.unstagedFiles;
    allUnstaged.insert(allUnstaged.end(),
        m_status.untrackedFiles.begin(), m_status.untrackedFiles.end());

    if (allUnstaged.empty()) {
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No unstaged changes");
    } else if (m_treeView) {
        FileTreeNode root;
        BuildTree(allUnstaged, root);
        ImGui::Indent(0);
        for (auto& child : root.children)
            RenderTreeNode(child, false);
    } else {
        for (auto& f : allUnstaged)
            RenderFileRow(f.filename, f.oldFilename, f.status, false);
    }
    ImGui::EndChild();
}

// ---- File row (list view) ----

void WorkspacePanel::RenderFileRow(const std::string& path, const std::string& oldPath,
                                    GitFileStatus status, bool isStaged)
{
    ImGui::PushID(path.c_str());

    auto& selSet = isStaged ? m_selectedStagedPaths : m_selectedUnstagedPaths;
    bool selected = selSet.count(path) > 0;

    bool isDiffTarget = m_diffOpen && m_diffFilePath == path && m_diffIsStaged == isStaged;
    if (isDiffTarget) {
        ImGui::PushStyleColor(ImGuiCol_Header, ImVec4(0.18f, 0.30f, 0.45f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_HeaderHovered, ImVec4(0.22f, 0.35f, 0.50f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_HeaderActive, ImVec4(0.25f, 0.40f, 0.55f, 1.0f));
    }

    ImU32 color = StatusColor(status);
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(
        ((color >> IM_COL32_R_SHIFT) & 0xFF) / 255.0f,
        ((color >> IM_COL32_G_SHIFT) & 0xFF) / 255.0f,
        ((color >> IM_COL32_B_SHIFT) & 0xFF) / 255.0f, 1.0f));
    ImGui::Text("[%s]", StatusIcon(status));
    ImGui::SameLine();
    ImGui::PopStyleColor();

    if (ImGui::Selectable(path.c_str(), selected)) {
        if (ImGui::GetIO().KeyCtrl) {
            if (selected) selSet.erase(path);
            else          selSet.insert(path);
        } else {
            if (isDiffTarget && !ImGui::GetIO().KeyCtrl) {
                m_diffOpen = false;
                m_diffLines.clear();
                m_diffFilePath.clear();
            } else {
                selSet.clear();
                selSet.insert(path);
                LoadDiff(path, isStaged);
            }
        }
    }

    if (isDiffTarget)
        ImGui::PopStyleColor(3);

    if (!oldPath.empty()) {
        ImGui::SameLine();
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), " <- %s", oldPath.c_str());
    }

    ImGui::PopID();
}

// ---- Tree node (tree view) ----

void WorkspacePanel::RenderTreeNode(const FileTreeNode& node, bool isStaged)
{
    if (node.file) {
        RenderFileRow(node.name, node.file->oldFilename, node.file->status, isStaged);
        return;
    }

    int flags = ImGuiTreeNodeFlags_OpenOnArrow | ImGuiTreeNodeFlags_OpenOnDoubleClick;
    if (node.expanded) flags |= ImGuiTreeNodeFlags_DefaultOpen;

    bool open = ImGui::TreeNodeEx(node.name.c_str(), flags);
    if (open) {
        for (auto& child : node.children)
            RenderTreeNode(child, isStaged);
        ImGui::TreePop();
    }
}

// ---- Diff ----

void WorkspacePanel::LoadDiff(const std::string& path, bool isStaged)
{
    std::vector<std::string> args = {"diff", "-p"};
    if (isStaged) args.push_back("--cached");
    args.push_back("--");
    args.push_back(path);

    auto r = GitProcess::Execute(m_repository->GetPath(), args);
    std::string content;
    if (r.ok) content = r.out;
    else      content = "Error: " + r.err;

    m_diffFilePath = path;
    m_diffIsStaged = isStaged;
    m_diffLines = ParseDiff(content);
    m_diffOpen = true;

    auto& selSet = isStaged ? m_selectedStagedPaths : m_selectedUnstagedPaths;
    if (!ImGui::GetIO().KeyCtrl) selSet.clear();
    selSet.insert(path);
}

void WorkspacePanel::RenderDiff(const std::string& path, bool isStaged)
{
    ImGui::TextColored(
        isStaged ? ImVec4(0.3f, 0.8f, 0.3f, 1.0f) : ImVec4(1.0f, 1.0f, 0.3f, 1.0f),
        "Diff: %s%s", isStaged ? "[Staged] " : "", path.c_str());
    ImGui::SameLine(ImGui::GetWindowWidth() - 70);
    if (ImGui::SmallButton("Close")) {
        m_diffOpen = false;
        m_diffLines.clear();
        m_diffFilePath.clear();
        return;
    }
    ImGui::Separator();

    ImGui::BeginChild("##diff_body", ImVec2(0, 0), true, ImGuiWindowFlags_HorizontalScrollbar);

    if (m_diffLines.empty()) {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No changes");
        ImGui::EndChild();
        return;
    }

    ImDrawList* dl = ImGui::GetWindowDrawList();
    float lineH = ImGui::GetTextLineHeight();
    float winW = ImGui::GetWindowWidth();

    for (int i = 0; i < (int)m_diffLines.size(); i++) {
        auto& line = m_diffLines[i];

        ImVec2 lineStart = ImGui::GetCursorScreenPos();

        if (line.type == DiffLine::Added)
            dl->AddRectFilled(lineStart, ImVec2(lineStart.x + winW, lineStart.y + lineH),
                IM_COL32(30, 60, 30, 160));
        else if (line.type == DiffLine::Removed)
            dl->AddRectFilled(lineStart, ImVec2(lineStart.x + winW, lineStart.y + lineH),
                IM_COL32(60, 30, 30, 160));
        else if (line.type == DiffLine::Hunk)
            dl->AddRectFilled(lineStart, ImVec2(lineStart.x + winW, lineStart.y + lineH),
                IM_COL32(25, 40, 60, 100));

        char numBuf[16];
        snprintf(numBuf, sizeof(numBuf), "%d", line.oldLineNo >= 0 ? line.oldLineNo : -1);
        ImGui::SetCursorPosX(4);
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "%5s", numBuf);
        ImGui::SameLine(0, 8);

        ImVec4 c(1, 1, 1, 1);
        if (line.type == DiffLine::Added)       c = ImVec4(0.4f, 0.9f, 0.4f, 1.0f);
        else if (line.type == DiffLine::Removed) c = ImVec4(0.9f, 0.4f, 0.4f, 1.0f);
        else if (line.type == DiffLine::Hunk)    c = ImVec4(0.4f, 0.7f, 1.0f, 1.0f);
        else if (line.type == DiffLine::Header)  c = ImVec4(0.5f, 0.5f, 0.5f, 1.0f);

        ImGui::PushStyleColor(ImGuiCol_Text, c);
        ImGui::TextUnformatted(line.content.c_str());
        ImGui::PopStyleColor();
    }

    ImGui::EndChild();
}

std::vector<WorkspacePanel::DiffLine> WorkspacePanel::ParseDiff(const std::string& content)
{
    std::vector<DiffLine> result;
    std::istringstream stream(content);
    std::string line;
    int oldNo = -1, newNo = -1;

    while (std::getline(stream, line)) {
        DiffLine dl;
        dl.content = line;

        if (line.compare(0, 4, "diff ") == 0) {
            dl.type = DiffLine::Header;
        } else if (line.compare(0, 4, "--- ") == 0 || line.compare(0, 4, "+++ ") == 0) {
            dl.type = DiffLine::Header;
        } else if (line.compare(0, 2, "@@") == 0) {
            dl.type = DiffLine::Hunk;
            int os = 0, ns = 0;
            if (sscanf_s(line.c_str(), "@@ -%d,%*d +%d,%*d @@", &os, &ns) >= 2) {
                oldNo = os; newNo = ns;
            }
        } else if (!line.empty() && line[0] == '+') {
            dl.type = DiffLine::Added;
            dl.oldLineNo = -1;
            dl.newLineNo = newNo >= 0 ? newNo++ : -1;
        } else if (!line.empty() && line[0] == '-') {
            dl.type = DiffLine::Removed;
            dl.oldLineNo = oldNo >= 0 ? oldNo++ : -1;
            dl.newLineNo = -1;
        } else {
            dl.type = DiffLine::Normal;
            dl.oldLineNo = oldNo >= 0 ? oldNo++ : -1;
            dl.newLineNo = newNo >= 0 ? newNo++ : -1;
        }

        result.push_back(std::move(dl));
    }

    return result;
}

// ---- Commit area (fixed buffer, no imgui_stdlib.h) ----

void WorkspacePanel::RenderCommitArea()
{
    ImGui::Separator();

    float w = ImGui::GetContentRegionAvail().x;
    ImGui::SetNextItemWidth(w);
    ImGui::InputTextMultiline("##commit_msg", m_commitBuf, sizeof(m_commitBuf),
        ImVec2(w, 54), ImGuiInputTextFlags_None);

    if (m_repository) {
        auto sig = m_repository->GetSignature();
        ImGui::Text("%s <%s>", sig.name.c_str(), sig.email.c_str());
    }

    ImGui::SameLine();
    ImGui::SetCursorPosX(ImGui::GetWindowWidth() - 100);

    std::string msg(m_commitBuf);
    while (!msg.empty() && (msg.back() == '\n' || msg.back() == ' '))
        msg.pop_back();

    bool canCommit = !msg.empty() &&
        (!m_status.stagedFiles.empty() || !m_status.unstagedFiles.empty());

    if (!canCommit) {
        ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true);
        ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f);
    }

    if (ImGui::Button("Commit", ImVec2(90, 40))) {
        if (m_status.stagedFiles.empty() && !m_status.unstagedFiles.empty())
            m_repository->Stage();
        m_repository->Commit(msg);
        m_commitBuf[0] = '\0';
        Refresh();
    }

    if (!canCommit) {
        ImGui::PopStyleVar();
        ImGui::PopItemFlag();
    }
}
