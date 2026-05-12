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

void DiffPanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = std::move(repo);
}

void DiffPanel::Clear()
{
    m_hasCommitDetail = false;
    m_currentCommit = GitCommitDetail{};
    m_fileDiffs.clear();
}

void DiffPanel::ShowCommitDetail(const GitCommit& commit)
{
    if (!m_repository) return;

    m_hasCommitDetail = true;
    m_fileDiffs.clear();

    auto [ok, output] = GitProcess::Execute(
        m_repository->GetPath(),
        {"show", "--stat", "--format=%H%x00%an%x00%ae%x00%aI%x00%s%x00%B%x00", commit.hash});

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

    for (const auto& f : m_currentCommit.modifiedFiles)
        m_fileDiffs.push_back({f, {}, false, 0, 0});
}

void DiffPanel::FetchDiff(FileDiffEntry& entry)
{
    auto [ok, output] = GitProcess::Execute(
        m_repository->GetPath(),
        {"diff", m_currentCommit.hash + "^!", "--", entry.filePath});

    if (!ok)
    {
        entry.diffContent = "Error fetching diff for: " + entry.filePath;
        entry.expanded = false;
        return;
    }

    size_t totalLines = 0;
    for (char c : output) { if (c == '\n') totalLines++; }

    if (totalLines > MAX_DIFF_LINES)
    {
        size_t cutoff = 0;
        int lines = 0;
        for (size_t i = 0; i < output.size(); i++)
        {
            if (output[i] == '\n') { lines++; if (lines >= MAX_DIFF_LINES) break; }
            cutoff = i;
        }
        entry.diffContent = output.substr(0, cutoff + 1);
        char buf[128];
        snprintf(buf, sizeof(buf), "\n... Output truncated (%zu lines omitted)", totalLines - MAX_DIFF_LINES);
        entry.diffContent += buf;
    }
    else
    {
        entry.diffContent = output;
    }

    entry.addedLines = 0;
    entry.removedLines = 0;
    std::istringstream stream(entry.diffContent);
    std::string l;
    while (std::getline(stream, l))
    {
        if (l.size() > 0 && l[0] == '+') entry.addedLines++;
        else if (l.size() > 0 && l[0] == '-') entry.removedLines++;
    }
}

void DiffPanel::Render()
{
    ImGui::BeginChild("##diff_panel", ImVec2(0, 0), true, ImGuiWindowFlags_HorizontalScrollbar);

    if (!m_hasCommitDetail)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Select a commit to view changes");
        ImGui::EndChild();
        return;
    }

    m_splitView.Begin();
    RenderFileList();
    m_splitView.Separate();
    RenderCommitHeader();

    // Find expanded file diff to show
    for (auto& entry : m_fileDiffs)
    {
        if (entry.expanded && !entry.diffContent.empty())
        {
            ImGui::Separator();
            RenderDiffContent(entry.diffContent);
            break;
        }
    }
    m_splitView.End();

    ImGui::EndChild();
}

void DiffPanel::RenderCommitHeader()
{
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
    ImGui::TextUnformatted(("Commit: " + m_currentCommit.shortHash + "  " + m_currentCommit.message).c_str());
    ImGui::PopStyleColor();

    ImGui::TextUnformatted(("Author: " + m_currentCommit.author + " <" + m_currentCommit.authorEmail + ">").c_str());
    ImGui::TextUnformatted(("Date:   " + m_currentCommit.date).c_str());

    if (!m_currentCommit.body.empty())
    {
        ImGui::Separator();
        ImGui::TextWrapped("%s", m_currentCommit.body.c_str());
    }

    char stats[128];
    snprintf(stats, sizeof(stats), "+%d additions, -%d deletions", m_currentCommit.additions, m_currentCommit.deletions);
    ImGui::TextUnformatted(stats);
}

void DiffPanel::RenderFileList()
{
    ImGui::TextUnformatted("Files changed:");
    ImGui::Separator();

    ImGui::BeginChild("##file_list", ImVec2(0, 0), false);

    for (auto& entry : m_fileDiffs)
    {
        ImGui::PushID(entry.filePath.c_str());

        bool isOpen = ImGui::TreeNodeEx(entry.filePath.c_str(),
            ImGuiTreeNodeFlags_Framed | (entry.expanded ? ImGuiTreeNodeFlags_DefaultOpen : 0));

        if (ImGui::IsItemClicked() && !entry.expanded)
        {
            entry.expanded = true;
            if (entry.diffContent.empty())
                FetchDiff(entry);
        }
        else if (!isOpen)
        {
            entry.expanded = false;
        }

        if (isOpen)
        {
            if (!entry.diffContent.empty())
            {
                auto lines = ParseDiffLines(entry.diffContent);
                ImGui::BeginChild("##diff_preview", ImVec2(0, 200), true, ImGuiWindowFlags_HorizontalScrollbar);
                int shownLines = 0;
                for (auto& dl : lines)
                {
                    if (shownLines > 60) { ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "..."); break; }
                    if (dl.type == DiffLineInfo::Added)
                    {
                        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.3f, 0.8f, 0.3f, 1.0f));
                        ImGui::TextUnformatted(dl.content.c_str());
                        ImGui::PopStyleColor();
                    }
                    else if (dl.type == DiffLineInfo::Removed)
                    {
                        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.3f, 0.3f, 1.0f));
                        ImGui::TextUnformatted(dl.content.c_str());
                        ImGui::PopStyleColor();
                    }
                    else if (dl.type == DiffLineInfo::Hunk)
                    {
                        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.3f, 0.6f, 1.0f, 1.0f));
                        ImGui::TextUnformatted(dl.content.c_str());
                        ImGui::PopStyleColor();
                    }
                    else
                    {
                        ImGui::TextUnformatted(dl.content.c_str());
                    }
                    shownLines++;
                }
                ImGui::EndChild();
            }
            ImGui::TreePop();
        }

        ImGui::PopID();
    }

    ImGui::EndChild();
}

void DiffPanel::RenderDiffContent(const std::string& diff)
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

        // Line numbers
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

std::vector<DiffPanel::DiffLineInfo> DiffPanel::ParseDiffLines(const std::string& diff)
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
            // Parse @@ -oldStart,count +newStart,count @@
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
