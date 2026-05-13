#include "log_panel.h"
#include "LoadingSpinner.h"
#include "../gitcore/git_util.h"
#include "../gitcore/git_process.h"
#include <imgui.h>
#include <misc/cpp/imgui_stdlib.h>
#include <ctime>
#include <sstream>
#include <algorithm>
#include <cctype>
#include <chrono>
#include <iomanip>
#include <cstdio>

static const ImU32 GRAPH_LANE_COLORS[] = {
    IM_COL32(100, 200, 100, 220),
    IM_COL32(100, 149, 237, 220),
    IM_COL32(255, 165, 0, 220),
    IM_COL32(255, 100, 100, 220),
    IM_COL32(180, 100, 255, 220),
    IM_COL32(255, 215, 0, 220),
    IM_COL32(0, 200, 200, 220),
    IM_COL32(255, 105, 180, 220),
};
static const int NUM_LANE_COLORS = sizeof(GRAPH_LANE_COLORS) / sizeof(GRAPH_LANE_COLORS[0]);

void LogPanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = std::move(repo);
    if (m_repository)
    {
        m_branchesLoading = true;
        std::thread([this]() {
            std::vector<std::string> branches;
            branches.push_back("All Branches");
            auto r = GitProcess::Execute(
                m_repository->GetPath(), {"branch", "--format=%(refname:short)"});
            if (r.ok && !r.out.empty())
            {
                std::istringstream stream(r.out);
                std::string branch;
                while (std::getline(stream, branch))
                    if (!branch.empty())
                        branches.push_back(branch.front() == '*' ? branch.substr(2) : branch);
            }
            {
                std::lock_guard<std::mutex> lock(m_pendingCommitsMutex);
                m_branches = std::move(branches);
            }
            m_branchesLoading = false;
        }).detach();
        Refresh();
    }
}

void LogPanel::Refresh()
{
    if (!m_repository) return;
    m_commits.clear();
    m_tableRows.clear();
    m_selectedIndex = -1;
    m_hasMore = true;
    m_loading = false;
    m_loadedExtraOnInit = false;
    LoadMoreIfNeeded();
}

void LogPanel::ProcessAsyncResults()
{
    if (!m_commitLoader.running)
    {
        if (m_commitLoader.worker.joinable())
            m_commitLoader.worker.join();

        if (!m_pendingCommits.empty())
        {
            for (auto& c : m_pendingCommits)
                m_commits.push_back(std::move(c));
            m_pendingCommits.clear();
            m_hasMore = m_pendingHasMore;
            m_loading = false;
        }
    }

    if (!m_detailLoader.running)
    {
        if (m_detailLoader.worker.joinable())
            m_detailLoader.worker.join();

        if (!m_detailLoader.hash.empty() && m_detailLoader.hash == m_selectedCommit.hash)
        {
            m_currentCommitDetail = std::move(m_detailLoader.detail);
            m_fileDiffs.clear();
            for (const auto& f : m_detailLoader.modifiedFiles)
                m_fileDiffs.push_back({f, {}, false, 0, 0});
            m_detailLoader.hash.clear();
        }
    }

    if (!m_diffLoader.running)
    {
        if (m_diffLoader.worker.joinable())
            m_diffLoader.worker.join();

        if (!m_diffLoader.diffContent.empty() &&
            m_selectedFileIndex >= 0 && m_selectedFileIndex < (int)m_fileDiffs.size())
        {
            m_fileDiffs[m_selectedFileIndex].diffContent = std::move(m_diffLoader.diffContent);
            m_diffLoader.diffContent.clear();
        }
    }
}

void LogPanel::Render()
{
    ProcessAsyncResults();

    ImGui::BeginChild("##log_panel", ImVec2(0, 0), true);

    RenderFilterBar();
    ImGui::Separator();

    m_contentSplit.Begin();
    RenderCommitTable();
    m_contentSplit.Separate();

    if (m_showDetailPanel && m_selectedIndex >= 0 && m_selectedIndex < (int)m_commits.size())
    {
        m_detailSplit.Begin();

        m_infoFileSplit.Begin();
        RenderCommitHeader();
        m_infoFileSplit.Separate();
        RenderFileList();
        m_infoFileSplit.End();

        m_detailSplit.Separate();

        if (m_selectedFileIndex >= 0 && m_selectedFileIndex < (int)m_fileDiffs.size() &&
            !m_fileDiffs[m_selectedFileIndex].diffContent.empty())
            RenderDiffContent(m_fileDiffs[m_selectedFileIndex].diffContent);
        else if (m_detailLoader.running)
        {
            ImGui::BeginChild("##diff_placeholder");
            LoadingSpinnerWithText("Loading diff...");
            ImGui::EndChild();
        }
        else
        {
            ImGui::BeginChild("##diff_placeholder");
            ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Select a file to view diff");
            ImGui::EndChild();
        }

        m_detailSplit.End();
    }
    else
    {
        ImGui::BeginChild("##detail_placeholder");
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Select a commit to view details");
        ImGui::EndChild();
    }
    m_contentSplit.End();

    ImGui::EndChild();
}

void LogPanel::RenderFilterBar()
{
    ImGui::TextUnformatted("Filter:");
    ImGui::SameLine();
    ImGui::PushItemWidth(150);
    if (ImGui::InputText("##filter", &m_filterText))
        Refresh();
    ImGui::PopItemWidth();

    ImGui::SameLine();
    ImGui::TextUnformatted("Branch:");
    ImGui::SameLine();

    if (m_branchesLoading)
    {
        LoadingSpinner(6.0f, 2.0f);
        ImGui::SameLine();
        std::string preview = m_branches.empty() ? "Loading..." : m_branches[m_selectedBranch];
        ImGui::PushItemWidth(120);
        ImGui::BeginCombo("##branch", preview.c_str(), ImGuiComboFlags_NoPreview);
        ImGui::EndCombo();
        ImGui::PopItemWidth();
    }
    else
    {
        std::string preview = m_branches.empty() ? "HEAD" : m_branches[m_selectedBranch];
        ImGui::PushItemWidth(120);
        if (ImGui::BeginCombo("##branch", preview.c_str()))
        {
            for (int i = 0; i < (int)m_branches.size(); i++)
            {
                if (ImGui::Selectable(m_branches[i].c_str(), m_selectedBranch == i))
                {
                    m_selectedBranch = i;
                    m_logOptions.branch = (i == 0) ? "--all" : m_branches[i];
                    Refresh();
                }
                if (m_selectedBranch == i)
                    ImGui::SetItemDefaultFocus();
            }
            ImGui::EndCombo();
        }
        ImGui::PopItemWidth();
    }
}

void LogPanel::RenderCommitTable()
{
    ImGui::BeginChild("##commit_table", ImVec2(0, 0), false, ImGuiWindowFlags_AlwaysVerticalScrollbar);

    if (m_commits.empty() && !m_loading)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No commits found");
    }

    auto scrollY = ImGui::GetScrollY();
    auto maxScrollY = ImGui::GetScrollMaxY();
    if (!m_loading && m_hasMore)
    {
        if (maxScrollY > 0 && scrollY >= maxScrollY - 10.0f)
            LoadMoreIfNeeded();
        else if (maxScrollY <= 0 && !m_loadedExtraOnInit)
        {
            m_loadedExtraOnInit = true;
            LoadMoreIfNeeded();
        }
    }

    if (ImGui::BeginTable("##commits", 5,
        ImGuiTableFlags_RowBg | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollY,
        ImVec2(0, 0)))
    {
        ImGui::TableSetupColumn("Graph", ImGuiTableColumnFlags_WidthFixed, 60);
        ImGui::TableSetupColumn("Description", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableSetupColumn("Date", ImGuiTableColumnFlags_WidthFixed, 120);
        ImGui::TableSetupColumn("Author", ImGuiTableColumnFlags_WidthFixed, 100);
        ImGui::TableSetupColumn("Commit", ImGuiTableColumnFlags_WidthFixed, 80);
        ImGui::TableHeadersRow();

        std::vector<int> lanes;
        int maxLane = -1;

        for (int i = 0; i < (int)m_commits.size(); i++)
        {
            const auto& commit = m_commits[i];

            if (!m_filterText.empty())
            {
                auto haystack = commit.shortHash + " " + commit.message + " " + commit.author + " " + commit.authorEmail;
                auto it = std::search(haystack.begin(), haystack.end(),
                    m_filterText.begin(), m_filterText.end(),
                    [](unsigned char a, unsigned char b) { return std::tolower(a) == std::tolower(b); });
                if (it == haystack.end()) continue;
            }

            bool isSelected = (i == m_selectedIndex);
            ImGui::TableNextRow();
            ImGui::PushID(i);
            ImGui::TableNextColumn();

            int laneId = 0;
            for (int li = 0; li < (int)lanes.size(); li++)
            {
                if (std::find(commit.parentHashes.begin(), commit.parentHashes.end(),
                    "") != commit.parentHashes.end())
                    break;
            }
            laneId = maxLane + 1;
            for (auto rit = lanes.rbegin(); rit != lanes.rend(); ++rit)
            {
                if (*rit == i - 1) { laneId = (int)(&*rit - &lanes[0]); break; }
            }

            if (laneId > maxLane) maxLane = laneId;
            if (laneId >= (int)lanes.size())
                lanes.resize(laneId + 1, -1);
            lanes[laneId] = i;

            ImDrawList* dl = ImGui::GetWindowDrawList();
            ImVec2 cellPos = ImGui::GetCursorScreenPos();
            float lineHeight = ImGui::GetTextLineHeightWithSpacing() + 4;
            ImVec2 center(cellPos.x + lineHeight * 0.5f, cellPos.y + lineHeight * 0.35f);

            for (int p = 0; p < (int)commit.parentHashes.size(); p++)
            {
                for (int li = 0; li < (int)lanes.size(); li++)
                {
                    if (li != laneId && lanes[li] >= 0 && lanes[li] < i)
                    {
                        ImVec2 p1(center.x + (li - laneId) * lineHeight * 0.6f, center.y);
                        ImVec2 p2(cellPos.x + lineHeight * 0.5f + (li - laneId) * lineHeight * 0.6f,
                            cellPos.y - lineHeight * 0.35f);
                        dl->AddLine(p1, p2, GRAPH_LANE_COLORS[li % NUM_LANE_COLORS], 1.5f);
                    }
                }
            }

            bool isMerge = commit.parentHashes.size() > 1;
            bool isHead = false;
            for (auto& r : commit.refs)
            {
                if (r.find("HEAD") != std::string::npos || r.find("master") != std::string::npos || r.find("main") != std::string::npos)
                    isHead = true;
            }
            DrawGraph(laneId, center, isMerge, isHead);

            ImGui::TableNextColumn();
            for (auto& ref : commit.refs)
            {
                bool isTag = ref.find("tag:") != std::string::npos;
                ImVec4 tagColor = isTag ? ImVec4(0.6f, 0.8f, 0.3f, 1.0f) : ImVec4(0.8f, 0.6f, 0.2f, 1.0f);
                ImGui::PushStyleColor(ImGuiCol_Text, tagColor);
                ImGui::TextUnformatted(ref.c_str());
                ImGui::PopStyleColor();
                ImGui::SameLine();
            }

            bool detailLoading = m_detailLoader.running && m_selectedCommit.hash == commit.hash;
            if (detailLoading)
            {
                ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.5f, 0.5f, 1.0f));
                ImGui::TextUnformatted(commit.message.c_str());
                ImGui::PopStyleColor();
                ImGui::SameLine();
                LoadingSpinner(4.0f, 1.5f);
            }
            else
            {
                if (ImGui::Selectable(commit.message.c_str(), isSelected, ImGuiSelectableFlags_SpanAllColumns))
                {
                    m_selectedIndex = i;
                    m_showDetailPanel = true;
                    m_selectedCommit = commit;
                    StartAsyncDetailLoad();
                    if (OnCommitSelected) OnCommitSelected(commit);
                }
            }
            ImGui::TableNextColumn();
            ImGui::TextUnformatted(FormatRelativeTime(commit.date).c_str());
            ImGui::TableNextColumn();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.5f, 0.5f, 1.0f));
            ImGui::TextUnformatted(commit.author.c_str());
            ImGui::PopStyleColor();
            ImGui::TableNextColumn();
            ImGui::TextUnformatted(commit.shortHash.c_str());
            ImGui::PopID();
        }
        ImGui::EndTable();
    }

    if (m_loading)
    {
        ImGui::Separator();
        ImGui::SetCursorPosX((ImGui::GetWindowWidth() - 60) * 0.5f);
        LoadingSpinnerWithText("Loading commits...", 8.0f);
    }

    ImGui::EndChild();
}

void LogPanel::RenderCommitHeader()
{
    ImGui::BeginChild("##commit_header", ImVec2(0, 0), false);

    if (m_detailLoader.running)
    {
        LoadingSpinnerWithText("Loading commit details...");
        ImGui::EndChild();
        return;
    }

    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
    ImGui::TextUnformatted(m_selectedCommit.shortHash.c_str());
    ImGui::PopStyleColor();
    ImGui::SameLine();
    ImGui::TextUnformatted(m_selectedCommit.message.c_str());

    ImGui::Text("Author: %s <%s>", m_selectedCommit.author.c_str(), m_selectedCommit.authorEmail.c_str());
    ImGui::Text("Date:   %s", m_selectedCommit.date.c_str());

    if (!m_selectedCommit.parentHashes.empty())
    {
        ImGui::Text("Parents:");
        ImGui::SameLine();
        for (auto& ph : m_selectedCommit.parentHashes)
        {
            ImGui::SameLine();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.4f, 0.6f, 1.0f, 1.0f));
            ImGui::TextUnformatted(ph.substr(0, 10).c_str());
            ImGui::PopStyleColor();
            ImGui::SameLine();
        }
    }

    if (!m_selectedCommit.body.empty())
    {
        ImGui::Separator();
        ImGui::TextWrapped("%s", m_selectedCommit.body.c_str());
    }

    char stats[128];
    snprintf(stats, sizeof(stats), "+%d additions, -%d deletions", m_currentCommitDetail.additions, m_currentCommitDetail.deletions);
    ImGui::TextUnformatted(stats);

    ImGui::EndChild();
}

void LogPanel::RenderFileList()
{
    ImGui::BeginChild("##file_list", ImVec2(0, 0), true);

    if (m_detailLoader.running)
    {
        LoadingSpinnerWithText("Loading files...");
        ImGui::EndChild();
        return;
    }

    for (int i = 0; i < (int)m_fileDiffs.size(); i++)
    {
        auto& entry = m_fileDiffs[i];
        bool selected = (i == m_selectedFileIndex);

        ImGui::PushID(i);
        bool isDiffLoading = m_diffLoader.running && m_selectedFileIndex == i;
        if (isDiffLoading)
        {
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.5f, 0.5f, 1.0f));
            ImGui::TextUnformatted(entry.filePath.c_str());
            ImGui::PopStyleColor();
            ImGui::SameLine();
            LoadingSpinner(4.0f, 1.5f);
        }
        else
        {
            if (ImGui::Selectable(entry.filePath.c_str(), selected))
            {
                m_selectedFileIndex = i;
                if (entry.diffContent.empty())
                    StartAsyncDiffLoad(entry.filePath);
            }
        }
        ImGui::PopID();
    }

    if (m_fileDiffs.empty() && !m_detailLoader.running)
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No file changes");

    ImGui::EndChild();
}

void LogPanel::RenderDiffContent(const std::string& diff)
{
    auto lines = ParseDiffLines(diff);

    int numWidth = 40;
    ImGui::PushFont(ImGui::GetIO().Fonts->Fonts.Size > 1
        ? ImGui::GetIO().Fonts->Fonts[1]
        : ImGui::GetIO().Fonts->Fonts[0]);

    ImGui::BeginChild("##diff_full", ImVec2(0, 0), true, ImGuiWindowFlags_HorizontalScrollbar);

    ImDrawList* dl = ImGui::GetWindowDrawList();
    ImVec2 startPos = ImGui::GetCursorScreenPos();
    float lineHeight = ImGui::GetTextLineHeightWithSpacing();
    float textHeight = ImGui::GetTextLineHeight();

    ImGui::SetCursorPosX(numWidth * 2 + 8);

    for (int i = 0; i < (int)lines.size(); i++)
    {
        auto& dlInfo = lines[i];
        ImVec2 lineStart(startPos.x, startPos.y + i * lineHeight);
        ImVec2 lineEnd(startPos.x + ImGui::GetWindowWidth(), lineStart.y + textHeight);

        if (dlInfo.type == DiffLineInfo::Added)
            dl->AddRectFilled(lineStart, lineEnd, IM_COL32(40, 70, 40, 180));
        else if (dlInfo.type == DiffLineInfo::Removed)
            dl->AddRectFilled(lineStart, lineEnd, IM_COL32(70, 40, 40, 180));
        else if (dlInfo.type == DiffLineInfo::Hunk)
            dl->AddRectFilled(lineStart, lineEnd, IM_COL32(30, 50, 80, 120));
    }

    for (int i = 0; i < (int)lines.size(); i++)
    {
        auto& dlInfo = lines[i];

        char oldNo[16] = "-", newNo[16] = "-";
        if (dlInfo.newLineNo >= 0) snprintf(newNo, sizeof(newNo), "%d", dlInfo.newLineNo);
        if (dlInfo.oldLineNo >= 0) snprintf(oldNo, sizeof(oldNo), "%d", dlInfo.oldLineNo);

        ImVec2 pos = ImGui::GetCursorScreenPos();
        ImGui::SetCursorScreenPos(ImVec2(pos.x, pos.y));

        ImGui::TextUnformatted(oldNo);
        ImGui::SameLine(0, 4);
        ImGui::TextUnformatted(newNo);
        ImGui::SameLine(0, 4);

        ImVec4 color(1, 1, 1, 1);
        if (dlInfo.type == DiffLineInfo::Added)
            color = ImVec4(0.3f, 0.8f, 0.3f, 1.0f);
        else if (dlInfo.type == DiffLineInfo::Removed)
            color = ImVec4(0.8f, 0.3f, 0.3f, 1.0f);
        else if (dlInfo.type == DiffLineInfo::Hunk)
            color = ImVec4(0.3f, 0.6f, 1.0f, 1.0f);

        ImGui::PushStyleColor(ImGuiCol_Text, color);
        ImGui::TextUnformatted(dlInfo.content.c_str());
        ImGui::PopStyleColor();
    }

    ImGui::EndChild();
    ImGui::PopFont();
}

void LogPanel::StartAsyncDiffLoad(const std::string& filePath)
{
    if (m_diffLoader.running) return;
    m_diffLoader.running = true;

    std::string repoPath = m_repository->GetPath();
    std::string hash = m_selectedCommit.hash;

    m_diffLoader.worker = std::thread([this, repoPath, hash, filePath]() {
        auto r = GitProcess::Execute(repoPath,
            {"diff", hash + "^!", "--", filePath});

        if (!r.ok)
        {
            m_diffLoader.diffContent = "Error: " + r.err;
        }
        else
        {
            size_t totalLines = 0;
            for (char c : r.out) { if (c == '\n') totalLines++; }

            constexpr int MAX_DIFF_LINES = 5000;
            if (totalLines > MAX_DIFF_LINES)
            {
                size_t cutoff = 0;
                int lines = 0;
                for (size_t i = 0; i < r.out.size(); i++)
                {
                    if (r.out[i] == '\n') { lines++; if (lines >= MAX_DIFF_LINES) break; }
                    cutoff = i;
                }
                m_diffLoader.diffContent = r.out.substr(0, cutoff + 1);
                char buf[128];
                snprintf(buf, sizeof(buf), "\n... Output truncated (%zu lines omitted)", totalLines - MAX_DIFF_LINES);
                m_diffLoader.diffContent += buf;
            }
            else
            {
                m_diffLoader.diffContent = r.out;
            }
        }
        m_diffLoader.running = false;
    });
    m_diffLoader.worker.detach();
}

void LogPanel::StartAsyncDetailLoad()
{
    if (m_detailLoader.running) return;
    m_detailLoader.running = true;
    m_detailLoader.hash = m_selectedCommit.hash;

    std::string repoPath = m_repository->GetPath();
    std::string hash = m_selectedCommit.hash;

    m_detailLoader.worker = std::thread([this, repoPath, hash]() {
        auto r = GitProcess::Execute(repoPath,
            {"show", "--stat", "--format=%H%x00%an%x00%ae%x00%aI%x00%s%x00%B%x00", hash});

        GitCommitDetail detail;
        std::vector<std::string> modifiedFiles;

        if (!r.ok || r.out.empty())
        {
            m_detailLoader.detail = detail;
            m_detailLoader.modifiedFiles = modifiedFiles;
            m_detailLoader.running = false;
            return;
        }

        size_t pos = 0;
        auto extract = [&]() -> std::string {
            if (pos >= r.out.size()) return {};
            auto end = r.out.find('\0', pos);
            if (end == std::string::npos) {
                auto result = r.out.substr(pos);
                pos = r.out.size();
                return result;
            }
            auto result = r.out.substr(pos, end - pos);
            pos = end + 1;
            return result;
        };

        detail.hash = extract();
        detail.shortHash = hash.substr(0, 7);
        detail.author = extract();
        detail.authorEmail = extract();
        detail.date = extract();
        detail.message = extract();
        detail.body = extract();

        std::string statOutput = r.out.substr(pos);

        detail.addedFiles.clear();
        detail.modifiedFiles.clear();
        detail.deletedFiles.clear();
        detail.additions = 0;
        detail.deletions = 0;

        std::istringstream statStream(statOutput);
        std::string line;
        while (std::getline(statStream, line))
        {
            if (line.empty()) continue;

            if (line.find("files changed") != std::string::npos ||
                line.find("insertion") != std::string::npos ||
                line.find("deletion") != std::string::npos)
            {
                int addVal = 0, delVal = 0;
                for (const char* c = line.c_str(); *c; c++)
                {
                    if (*c >= '0' && *c <= '9')
                    {
                        int val = 0;
                        while (*c >= '0' && *c <= '9') { val = val * 10 + (*c - '0'); c++; }
                        if (strstr(c, "insertion")) addVal = val;
                        else if (strstr(c, "deletion")) delVal = val;
                        if (!*c) break;
                    }
                }
                if (addVal) detail.additions = addVal;
                if (delVal) detail.deletions = delVal;
                continue;
            }

            auto pipePos = line.find("|");
            if (pipePos == std::string::npos || pipePos <= 5) continue;

            std::string filename = line.substr(0, pipePos);
            auto end = filename.find_last_not_of(" ");
            if (end != std::string::npos) filename = filename.substr(0, end + 1);
            auto start = filename.find_first_not_of(" ");
            if (start != std::string::npos) filename = filename.substr(start);
            if (filename.empty()) continue;

            modifiedFiles.push_back(filename);
        }

        m_detailLoader.detail = std::move(detail);
        m_detailLoader.modifiedFiles = std::move(modifiedFiles);
        m_detailLoader.running = false;
    });
    m_detailLoader.worker.detach();
}

void LogPanel::StartAsyncCommitLoad()
{
    if (m_commitLoader.running) return;
    m_commitLoader.running = true;
    m_commitLoader.skipCount = (int)m_commits.size();

    std::string repoPath = m_repository->GetPath();
    std::string branchArg = m_logOptions.branch == "--all" ? "--all" : m_logOptions.branch;
    std::string filterAuthor = m_logOptions.author;
    int skip = m_commitLoader.skipCount;

    m_commitLoader.worker = std::thread([this, repoPath, branchArg, skip]() {
        std::vector<std::string> args;
        args.push_back("log");
        args.push_back("--format=%H%x00%h%x00%an%x00%ae%x00%aI%x00%s%x00%D%x00%P%x00");
        args.push_back("-n");
        args.push_back(std::to_string(LOAD_BATCH_SIZE));
        args.push_back("--skip");
        args.push_back(std::to_string(skip));
        args.push_back(branchArg);
        args.push_back("--date-order");

        auto r = GitProcess::Execute(repoPath, args);

        std::vector<GitCommit> parsed;
        bool hasMore = false;

        if (r.ok && !r.out.empty())
        {
            parsed = git::ParseLogOutput(r.out, LOAD_BATCH_SIZE);
            hasMore = ((int)parsed.size() >= LOAD_BATCH_SIZE);
        }

        {
            std::lock_guard<std::mutex> lock(m_pendingCommitsMutex);
            m_pendingCommits = std::move(parsed);
            m_pendingHasMore = hasMore;
        }
        m_commitLoader.running = false;
    });
    m_commitLoader.worker.detach();
}

void LogPanel::LoadMoreIfNeeded()
{
    if (!m_repository || !m_hasMore || m_loading) return;
    m_loading = true;
    StartAsyncCommitLoad();
}

void LogPanel::FetchDiff(FileDiffEntry& entry)
{
    (void)entry;
    // Now handled by StartAsyncDiffLoad
}

void LogPanel::BuildSelectCommitPatch()
{
    // Now handled by StartAsyncDetailLoad
}

std::vector<LogPanel::DiffLineInfo> LogPanel::ParseDiffLines(const std::string& diff)
{
    std::vector<DiffLineInfo> result;
    std::istringstream stream(diff);
    std::string line;
    int currentOldLine = -1;
    int currentNewLine = -1;

    while (std::getline(stream, line))
    {
        DiffLineInfo info;
        info.content = line;

        if (line.compare(0, 4, "--- ") == 0 || line.compare(0, 4, "+++ ") == 0)
        {
            info.type = DiffLineInfo::Header;
            info.oldLineNo = -1;
            info.newLineNo = -1;
        }
        else if (line.compare(0, 3, "@@ ") == 0)
        {
            info.type = DiffLineInfo::Hunk;
            info.oldLineNo = -1;
            info.newLineNo = -1;
            int oldStart = 0, newStart = 0;
            if (sscanf_s(line.c_str(), "@@ -%d,%*d +%d,%*d @@", &oldStart, &newStart) >= 2)
            {
                currentOldLine = oldStart;
                currentNewLine = newStart;
            }
        }
        else if (!line.empty() && line[0] == '+')
        {
            info.type = DiffLineInfo::Added;
            info.oldLineNo = -1;
            info.newLineNo = currentNewLine >= 0 ? currentNewLine++ : -1;
        }
        else if (!line.empty() && line[0] == '-')
        {
            info.type = DiffLineInfo::Removed;
            info.oldLineNo = currentOldLine >= 0 ? currentOldLine++ : -1;
            info.newLineNo = -1;
        }
        else
        {
            info.type = DiffLineInfo::Normal;
            info.oldLineNo = currentOldLine >= 0 ? currentOldLine++ : -1;
            info.newLineNo = currentNewLine >= 0 ? currentNewLine++ : -1;
        }

        result.push_back(std::move(info));
    }

    return result;
}

void LogPanel::DrawGraph(int laneId, const ImVec2& center, bool isMerge, bool isHead)
{
    ImDrawList* dl = ImGui::GetWindowDrawList();
    ImU32 color = GRAPH_LANE_COLORS[laneId % NUM_LANE_COLORS];
    float r = isHead ? 5.0f : 4.0f;

    if (isHead)
    {
        dl->AddCircleFilled(center, r + 2, IM_COL32(255, 255, 255, 150));
        dl->AddCircleFilled(center, r, color);
    }
    else if (isMerge)
    {
        dl->AddCircleFilled(center, r, IM_COL32(255, 255, 255, 200));
        dl->AddCircleFilled(center, r - 2, color);
    }
    else
    {
        dl->AddCircleFilled(center, r, color);
    }

    if (m_selectedIndex >= 0 && m_selectedIndex < (int)m_commits.size())
    {
        dl->AddLine(ImVec2(center.x, center.y - 10), ImVec2(center.x, center.y + 10),
            color, 1.0f);
    }
}

void LogPanel::UpdateGraphLanes(const GitCommit& commit, std::vector<int>& lanes,
    std::vector<CommitGraphLine>& lines, int& maxLane)
{
    (void)commit;
    (void)lanes;
    (void)lines;
    (void)maxLane;
}

std::string LogPanel::FormatRelativeTime(const std::string& isoDate)
{
    if (isoDate.empty()) return {};

    std::tm tm = {};
    std::istringstream ss(isoDate);
    ss >> std::get_time(&tm, "%Y-%m-%dT%H:%M:%S");
    if (ss.fail()) return isoDate;

    auto tp = std::chrono::system_clock::from_time_t(std::mktime(&tm));
    auto now = std::chrono::system_clock::now();
    auto diff = std::chrono::duration_cast<std::chrono::seconds>(now - tp).count();

    if (diff < 0) return "now";
    if (diff < 60) return std::to_string(diff) + "s";
    if (diff < 3600) return std::to_string(diff / 60) + "m";
    if (diff < 86400) return std::to_string(diff / 3600) + "h";
    if (diff < 604800) return std::to_string(diff / 86400) + "d";
    if (diff < 2592000) return std::to_string(diff / 604800) + "w";
    return std::to_string(diff / 2592000) + "mon";
}

void LogPanel::FetchBranches()
{
    if (!m_repository) return;
    m_branches.clear();
    m_branches.push_back("All Branches");

    auto r = GitProcess::Execute(
        m_repository->GetPath(), {"branch", "--format=%(refname:short)"});
    if (r.ok && !r.out.empty())
    {
        std::istringstream stream(r.out);
        std::string branch;
        while (std::getline(stream, branch))
            if (!branch.empty())
                m_branches.push_back(branch.front() == '*' ? branch.substr(2) : branch);
    }
}
