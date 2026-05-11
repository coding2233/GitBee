#include "diff_panel.h"
#include "../gitcore/git_util.h"
#include "../gitcore/git_process.h"
#include <imgui.h>
#include <sstream>
#include <algorithm>
#include <cstring>
#include <cstdio>

static constexpr int MAX_DIFF_LINES = 5000;

DiffPanel::DiffPanel() = default;

void DiffPanel::SetRepository(std::shared_ptr<GitRepository> repo) {
    m_repository = std::move(repo);
}

void DiffPanel::Clear() {
    m_hasCommitDetail = false;
    m_currentCommit = GitCommitDetail{};
    m_fileDiffs.clear();
}

void DiffPanel::ShowCommitDetail(const GitCommit& commit) {
    if (!m_repository) return;

    m_hasCommitDetail = true;
    m_fileDiffs.clear();

    auto [ok, output] = GitProcess::Execute(
        m_repository->GetPath(),
        {"show", "--stat", "--format=%H%x00%an%x00%ae%x00%aI%x00%s%x00%B%x00", commit.hash}
    );

    if (!ok || output.empty()) return;

    size_t pos = 0;
    m_currentCommit.hash = git::ExtractField(output, pos);
    m_currentCommit.shortHash = commit.shortHash;
    m_currentCommit.author = git::ExtractField(output, pos);
    m_currentCommit.authorEmail = git::ExtractField(output, pos);
    m_currentCommit.date = git::ExtractField(output, pos);
    m_currentCommit.message = git::ExtractField(output, pos);
    m_currentCommit.body = git::ExtractField(output, pos);

    std::string statOutput = output.substr(pos);

    m_currentCommit.addedFiles.clear();
    m_currentCommit.modifiedFiles.clear();
    m_currentCommit.deletedFiles.clear();
    m_currentCommit.additions = 0;
    m_currentCommit.deletions = 0;

    std::istringstream statStream(statOutput);
    std::string line;
    while (std::getline(statStream, line)) {
        if (line.empty()) continue;

        if (line.find("files changed") != std::string::npos ||
            line.find("insertion") != std::string::npos ||
            line.find("deletion") != std::string::npos) {
            int addVal = 0, delVal = 0;
            for (const char* c = line.c_str(); *c; c++) {
                if (*c >= '0' && *c <= '9') {
                    int val = 0;
                    while (*c >= '0' && *c <= '9') {
                        val = val * 10 + (*c - '0');
                        c++;
                    }
                    if (strstr(c, "insertion")) addVal = val;
                    else if (strstr(c, "deletion")) delVal = val;
                    if (!*c) break;
                }
            }
            if (addVal) m_currentCommit.additions = addVal;
            if (delVal) m_currentCommit.deletions = delVal;
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

        m_currentCommit.modifiedFiles.push_back(filename);
    }

    for (const auto& f : m_currentCommit.modifiedFiles) {
        m_fileDiffs.push_back({f, {}, false});
    }
}

void DiffPanel::FetchDiff(FileDiffEntry& entry) {
    auto [ok, output] = GitProcess::Execute(
        m_repository->GetPath(),
        {"diff", m_currentCommit.hash + "^!", "--", entry.filePath}
    );

    if (!ok) {
        entry.diffContent = "Error fetching diff for: " + entry.filePath;
        entry.expanded = false;
        return;
    }

    size_t totalLines = 0;
    for (char c : output) {
        if (c == '\n') totalLines++;
    }

    if (totalLines > MAX_DIFF_LINES) {
        size_t cutoff = 0;
        int lines = 0;
        for (size_t i = 0; i < output.size(); i++) {
            if (output[i] == '\n') {
                lines++;
                if (lines >= MAX_DIFF_LINES) break;
            }
            cutoff = i;
        }
        entry.diffContent = output.substr(0, cutoff + 1);
        char buf[128];
        snprintf(buf, sizeof(buf), "\n... Output truncated (%zu lines omitted)",
                 totalLines - MAX_DIFF_LINES);
        entry.diffContent += buf;
    } else {
        entry.diffContent = output;
    }
}

void DiffPanel::Render() {
    if (!ImGui::Begin("Diff View", nullptr, ImGuiWindowFlags_HorizontalScrollbar)) {
        ImGui::End();
        return;
    }

    if (!m_hasCommitDetail) {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f),
            "Select a commit to view changes");
        ImGui::End();
        return;
    }

    RenderCommitHeader();
    ImGui::Separator();
    RenderFileList();

    ImGui::End();
}

void DiffPanel::RenderCommitHeader() {
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
    ImGui::TextUnformatted(("Commit: " + m_currentCommit.shortHash
        + "  " + m_currentCommit.message).c_str());
    ImGui::PopStyleColor();

    ImGui::TextUnformatted(("Author: " + m_currentCommit.author
        + " <" + m_currentCommit.authorEmail + ">").c_str());
    ImGui::TextUnformatted(("Date:   " + m_currentCommit.date).c_str());
}

void DiffPanel::RenderFileList() {
    ImGui::TextUnformatted("Files changed:");
    ImGui::Separator();

    for (auto& entry : m_fileDiffs) {
        int flags = ImGuiTreeNodeFlags_Framed;
        if (entry.expanded) {
            flags |= ImGuiTreeNodeFlags_DefaultOpen;
        }

        if (ImGui::TreeNodeEx(entry.filePath.c_str(), flags)) {
            if (!entry.expanded) {
                entry.expanded = true;
                if (entry.diffContent.empty()) {
                    FetchDiff(entry);
                }
            }

            if (!entry.diffContent.empty()) {
                RenderDiffContent(entry.diffContent);
            }

            ImGui::TreePop();
        } else {
            entry.expanded = false;
        }
    }

    ImGui::Separator();
    char stats[128];
    snprintf(stats, sizeof(stats), "+%d additions, -%d deletions",
        m_currentCommit.additions, m_currentCommit.deletions);
    ImGui::TextUnformatted(stats);
}

void DiffPanel::RenderDiffContent(const std::string& diff) {
    ImGui::PushFont(ImGui::GetIO().Fonts->Fonts.Size > 1
        ? ImGui::GetIO().Fonts->Fonts[1]
        : ImGui::GetIO().Fonts->Fonts[0]);

    ImGui::BeginChild("##diff_text", ImVec2(0, 300), true,
        ImGuiWindowFlags_HorizontalScrollbar);

    std::istringstream stream(diff);
    std::string line;

    while (std::getline(stream, line)) {
        if (line.empty()) {
            ImGui::TextUnformatted("");
            continue;
        }

        if (line.compare(0, 4, "--- ") == 0 || line.compare(0, 4, "+++ ") == 0) {
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.5f, 1.0f, 1.0f));
            ImGui::TextUnformatted(line.c_str());
            ImGui::PopStyleColor();
        } else if (line.compare(0, 3, "@@ ") == 0) {
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.3f, 0.6f, 1.0f, 1.0f));
            ImGui::TextUnformatted(line.c_str());
            ImGui::PopStyleColor();
        } else if (!line.empty() && line[0] == '+') {
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.3f, 0.8f, 0.3f, 1.0f));
            ImGui::TextUnformatted(line.c_str());
            ImGui::PopStyleColor();
        } else if (!line.empty() && line[0] == '-') {
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.3f, 0.3f, 1.0f));
            ImGui::TextUnformatted(line.c_str());
            ImGui::PopStyleColor();
        } else {
            ImGui::TextUnformatted(line.c_str());
        }
    }

    ImGui::EndChild();
    ImGui::PopFont();
}