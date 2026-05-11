#include "log_panel.h"
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
        FetchBranches();
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
    LoadMoreIfNeeded();
}

void LogPanel::FetchBranches()
{
    if (!m_repository) return;
    m_branches.clear();
    m_branches.push_back("All Branches");

    auto [ok, output] = GitProcess::Execute(
        m_repository->GetPath(), {"branch", "--format=%(refname:short)"});
    if (ok && !output.empty())
    {
        std::istringstream stream(output);
        std::string branch;
        while (std::getline(stream, branch))
            if (!branch.empty())
                m_branches.push_back(branch.front() == '*' ? branch.substr(2) : branch);
    }
}

void LogPanel::Render()
{
    if (!ImGui::Begin("Commits"))
    {
        ImGui::End();
        return;
    }

    RenderFilterBar();
    ImGui::Separator();

    m_contentSplit.Begin();
    RenderCommitTable();
    m_contentSplit.Separate();

    if (m_showDetailPanel && m_selectedIndex >= 0 && m_selectedIndex < (int)m_commits.size())
        RenderCommitDetail();
    else
    {
        ImGui::BeginChild("##detail_placeholder");
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Select a commit to view details");
        ImGui::EndChild();
    }
    m_contentSplit.End();

    ImGui::End();
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

void LogPanel::RenderCommitTable()
{
    ImGui::BeginChild("##commit_table", ImVec2(0, 0), false, ImGuiWindowFlags_AlwaysVerticalScrollbar);

    auto scrollY = ImGui::GetScrollY();
    auto maxScrollY = ImGui::GetScrollMaxY();
    if (!m_loading && m_hasMore && scrollY > 0 && scrollY >= maxScrollY - 10.0f)
        LoadMoreIfNeeded();

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
            ImGui::TableNextColumn();

            int laneId = 0;
            // Try to find this commit in existing lanes
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

            // Graph rendering
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

            ImGui::SetCursorPosX(ImGui::GetCursorPosX() + lineHeight * 0.5f);

            // Ref tags
            for (auto& ref : commit.refs)
            {
                ImGui::SameLine();
                bool isTag = ref.find("tag:") != std::string::npos;
                ImVec4 tagColor = isTag ? ImVec4(0.6f, 0.8f, 0.3f, 1.0f) : ImVec4(0.8f, 0.6f, 0.2f, 1.0f);
                ImGui::PushStyleColor(ImGuiCol_Text, tagColor);
                ImGui::TextUnformatted(ref.c_str());
                ImGui::PopStyleColor();
                ImGui::SameLine();
            }

            ImGui::TableNextColumn();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
            ImGui::TextUnformatted(commit.shortHash.c_str());
            ImGui::PopStyleColor();
            ImGui::SameLine();
            if (ImGui::Selectable(commit.message.c_str(), isSelected, ImGuiSelectableFlags_SpanAllColumns))
            {
                m_selectedIndex = i;
                m_showDetailPanel = true;
                if (OnCommitSelected) OnCommitSelected(commit);
            }
            ImGui::TableNextColumn();
            ImGui::TextUnformatted(FormatRelativeTime(commit.date).c_str());
            ImGui::TableNextColumn();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.5f, 0.5f, 1.0f));
            ImGui::TextUnformatted(commit.author.c_str());
            ImGui::PopStyleColor();
            ImGui::TableNextColumn();
            ImGui::TextUnformatted(commit.shortHash.c_str());
        }
        ImGui::EndTable();
    }

    if (m_loading)
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Loading...");

    ImGui::EndChild();
}

void LogPanel::RenderCommitDetail()
{
    if (m_selectedIndex < 0 || m_selectedIndex >= (int)m_commits.size()) return;
    const auto& commit = m_commits[m_selectedIndex];

    ImGui::BeginChild("##commit_detail", ImVec2(0, 0), false);

    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
    ImGui::TextUnformatted(commit.shortHash.c_str());
    ImGui::PopStyleColor();
    ImGui::SameLine();
    ImGui::TextUnformatted(commit.message.c_str());

    ImGui::Text("Author: %s <%s>", commit.author.c_str(), commit.authorEmail.c_str());
    ImGui::Text("Date:   %s", commit.date.c_str());

    if (!commit.parentHashes.empty())
    {
        ImGui::Text("Parents:");
        ImGui::SameLine();
        for (auto& ph : commit.parentHashes)
        {
            ImGui::SameLine();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.4f, 0.6f, 1.0f, 1.0f));
            ImGui::TextUnformatted(ph.substr(0, 10).c_str());
            ImGui::PopStyleColor();
            ImGui::SameLine();
        }
    }

    if (!commit.body.empty())
    {
        ImGui::Separator();
        ImGui::TextWrapped("%s", commit.body.c_str());
    }

    ImGui::EndChild();
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
        // vertical line through commit
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

void LogPanel::LoadMoreIfNeeded()
{
    if (!m_repository || !m_hasMore || m_loading) return;

    m_loading = true;

    auto [ok, output] = GitProcess::Execute(
        m_repository->GetPath(),
        {"log",
         "--format=%H%x00%h%x00%an%x00%ae%x00%aI%x00%s%x00%D%x00%P",
         "-n", std::to_string(LOAD_BATCH_SIZE),
         "--skip", std::to_string((int)m_commits.size()),
         m_logOptions.branch == "--all" ? "--all" : m_logOptions.branch.c_str(),
         "--date-order"});

    if (ok && !output.empty())
    {
        auto parsed = git::ParseLogOutput(output, LOAD_BATCH_SIZE);
        m_hasMore = ((int)parsed.size() >= LOAD_BATCH_SIZE);
        for (auto& c : parsed)
            m_commits.push_back(std::move(c));
    }
    else
    {
        m_hasMore = false;
    }

    m_loading = false;
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
