#include "log_panel.h"
#include "../gitcore/git_util.h"
#include <ctime>
#include <sstream>
#include <algorithm>
#include <cctype>
#include <chrono>
#include <iomanip>

void LogPanel::SetRepository(std::shared_ptr<GitRepository> repo) {
    m_repository = std::move(repo);
    if (m_repository) {
        FetchBranches();
        Refresh();
    }
}

void LogPanel::Refresh() {
    if (!m_repository) return;

    m_commits.clear();
    m_selectedIndex = -1;
    m_hasMore = true;
    m_loading = false;

    LoadMoreIfNeeded();
}

void LogPanel::FetchBranches() {
    if (!m_repository) return;

    m_branches.clear();
    m_branches.push_back("All Branches");

    auto [ok, output] = GitProcess::Execute(
        m_repository->GetPath(),
        {"branch", "--format=%(refname:short)"}
    );

    if (ok && !output.empty()) {
        std::istringstream stream(output);
        std::string branch;
        while (std::getline(stream, branch)) {
            if (!branch.empty() && branch.front() != '*') {
                m_branches.push_back(branch);
            } else if (!branch.empty()) {
                m_branches.push_back(branch.substr(2));
            }
        }
    }
}

void LogPanel::Render() {
    if (!ImGui::Begin("Commits")) {
        ImGui::End();
        return;
    }

    RenderFilterBar();
    ImGui::Separator();
    RenderCommitList();

    ImGui::End();
}

void LogPanel::RenderFilterBar() {
    ImGui::TextUnformatted("Filter:");
    ImGui::SameLine();
    ImGui::PushItemWidth(150);
    if (ImGui::InputText("##filter", &m_filterText)) {
        Refresh();
    }
    ImGui::PopItemWidth();

    ImGui::SameLine();
    ImGui::TextUnformatted("Branch:");
    ImGui::SameLine();

    std::string preview = m_branches.empty() ? "HEAD" : m_branches[m_selectedBranch];
    ImGui::PushItemWidth(120);
    if (ImGui::BeginCombo("##branch", preview.c_str())) {
        for (int i = 0; i < (int)m_branches.size(); i++) {
            bool isSelected = (m_selectedBranch == i);
            if (ImGui::Selectable(m_branches[i].c_str(), isSelected)) {
                m_selectedBranch = i;
                m_logOptions.branch = (i == 0) ? "--all" : m_branches[i];
                Refresh();
            }
            if (isSelected) {
                ImGui::SetItemDefaultFocus();
            }
        }
        ImGui::EndCombo();
    }
    ImGui::PopItemWidth();
}

void LogPanel::RenderCommitList() {
    ImGui::BeginChild("##commit_list", ImVec2(0, 0), false, ImGuiWindowFlags_AlwaysVerticalScrollbar);

    auto scrollY = ImGui::GetScrollY();
    auto maxScrollY = ImGui::GetScrollMaxY();

    if (!m_loading && m_hasMore && scrollY > 0 && scrollY >= maxScrollY - 10.0f) {
        LoadMoreIfNeeded();
    }

    for (int i = 0; i < (int)m_commits.size(); i++) {
        const auto& commit = m_commits[i];

        if (!m_filterText.empty()) {
            auto haystack = commit.shortHash + " " + commit.message + " " + commit.author + " " + commit.authorEmail;
            auto it = std::search(
                haystack.begin(), haystack.end(),
                m_filterText.begin(), m_filterText.end(),
                [](unsigned char a, unsigned char b) { return std::tolower(a) == std::tolower(b); }
            );
            if (it == haystack.end()) continue;
        }

        RenderCommitRow(i, commit);
    }

    if (m_loading) {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Loading...");
    }

    ImGui::EndChild();
}

void LogPanel::RenderCommitRow(int index, const GitCommit& commit) {
    ImGui::PushID(index);

    bool isSelected = (index == m_selectedIndex);

    if (isSelected) {
        ImGui::PushStyleColor(ImGuiCol_Header, ImVec4(0.2f, 0.3f, 0.5f, 0.5f));
    } else if (index % 2 == 0) {
        ImGui::PushStyleColor(ImGuiCol_Header, ImVec4(0.08f, 0.08f, 0.08f, 0.3f));
    }

    float rowHeight = ImGui::GetTextLineHeightWithSpacing() + 4;
    ImGui::BeginChild("##row", ImVec2(0, rowHeight), false);

    RenderGraphIndicator(commit);
    ImGui::SameLine();

    if (ImGui::Selectable("##sel", &isSelected, ImGuiSelectableFlags_SpanAllColumns)) {
        m_selectedIndex = index;
        if (OnCommitSelected) {
            OnCommitSelected(commit);
        }
    }

    ImGui::SameLine();
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
    ImGui::TextUnformatted(commit.shortHash.c_str());
    ImGui::PopStyleColor();

    ImGui::SameLine();
    ImGui::TextUnformatted(commit.message.c_str());

    ImGui::SameLine();
    ImGui::Spacing();
    ImGui::SameLine();

    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.5f, 0.5f, 1.0f));
    ImGui::TextUnformatted(commit.author.c_str());
    ImGui::PopStyleColor();

    std::string relativeTime = FormatRelativeTime(commit.date);
    if (!relativeTime.empty()) {
        ImGui::SameLine();
        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.4f, 0.4f, 0.4f, 1.0f));
        ImGui::TextUnformatted(relativeTime.c_str());
        ImGui::PopStyleColor();
    }

    ImGui::EndChild();

    if (isSelected || index % 2 == 0) {
        ImGui::PopStyleColor();
    }

    ImGui::PopID();
}

void LogPanel::RenderGraphIndicator(const GitCommit& commit) {
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    auto pos = ImGui::GetCursorScreenPos();
    float radius = 4.0f;
    ImVec2 center(pos.x + 10, pos.y + ImGui::GetTextLineHeight() / 2);

    bool isMerge = commit.parentHashes.size() > 1;

    if (isMerge) {
        drawList->AddCircleFilled(center, radius, IM_COL32(255, 255, 255, 200));
        drawList->AddCircle(center, radius + 1, IM_COL32(255, 255, 255, 100));
        drawList->AddCircleFilled(center, radius - 2, IM_COL32(100, 149, 237, 255));
    } else {
        drawList->AddCircleFilled(center, radius, IM_COL32(100, 200, 100, 200));
    }

    ImGui::SetCursorPosX(ImGui::GetCursorPosX() + 24);
}

void LogPanel::LoadMoreIfNeeded() {
    if (!m_repository || !m_hasMore || m_loading) return;

    m_loading = true;

    GitLogOptions options = m_logOptions;
    options.maxCount = LOAD_BATCH_SIZE;
    options.skip = (int)m_commits.size();

    auto [ok, output] = GitProcess::Execute(
        m_repository->GetPath(),
        {"log",
         "--format=%H%x00%h%x00%an%x00%ae%x00%aI%x00%s%x00%D%x00%P",
         "-n", std::to_string(LOAD_BATCH_SIZE),
         "--skip", std::to_string(options.skip),
         options.branch == "--all" ? "--all" : options.branch.c_str(),
         "--date-order"}
    );

    if (ok && !output.empty()) {
        auto parsed = git::ParseLogOutput(output, LOAD_BATCH_SIZE);
        m_hasMore = ((int)parsed.size() >= LOAD_BATCH_SIZE);
        for (auto& c : parsed) {
            m_commits.push_back(std::move(c));
        }
    } else {
        m_hasMore = false;
    }

    m_loading = false;
}

std::string LogPanel::FormatRelativeTime(const std::string& isoDate) {
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