#include "home_view.h"
#include <imgui.h>
#include <algorithm>
#include <fstream>
#include <sstream>
#include <filesystem>

static const char* APP_VERSION = "v0.2.0";
static const char* APP_DESC = "A Lightweight Git Interface Management Tool";

static void DefaultLinkCallback(ImGui::MarkdownLinkCallbackData data)
{
    if (data.isImage) return;
    std::string url(data.link, data.linkLength);
    std::string cmd = "start " + url;
    system(cmd.c_str());
}

void HomeView::LinkCallback(ImGui::MarkdownLinkCallbackData data)
{
    DefaultLinkCallback(data);
}

void HomeView::SaveRecents(const std::string& filePath)
{
    std::ofstream file(filePath);
    if (!file.is_open()) return;
    file << "[\n";
    for (size_t i = 0; i < m_recentRepos.size(); i++)
    {
        file << "  {\"path\":\"";
        for (char c : m_recentRepos[i].path)
        {
            if (c == '\\') file << "\\\\";
            else if (c == '"') file << "\\\"";
            else file << c;
        }
        file << "\",\"name\":\"";
        for (char c : m_recentRepos[i].name)
        {
            if (c == '\\') file << "\\\\";
            else if (c == '"') file << "\\\"";
            else file << c;
        }
        file << "\"}";
        if (i < m_recentRepos.size() - 1) file << ",";
        file << "\n";
    }
    file << "]\n";
}

void HomeView::LoadRecents(const std::string& filePath)
{
    m_recentRepos.clear();
    std::ifstream file(filePath);
    if (!file.is_open()) return;

    std::stringstream buf;
    buf << file.rdbuf();
    std::string content = buf.str();

    size_t pos = 0;
    while ((pos = content.find("\"path\":\"", pos)) != std::string::npos)
    {
        pos += 8;
        std::string path;
        while (pos < content.size() && content[pos] != '"')
        {
            if (content[pos] == '\\' && pos + 1 < content.size())
            {
                if (content[pos + 1] == '\\') path += '\\';
                else if (content[pos + 1] == '"') path += '"';
                else path += content[pos];
                pos += 2;
            }
            else
            {
                path += content[pos];
                pos++;
            }
        }

        auto namePos = content.find("\"name\":\"", pos);
        if (namePos == std::string::npos) break;
        namePos += 8;
        std::string name;
        while (namePos < content.size() && content[namePos] != '"')
        {
            if (content[namePos] == '\\' && namePos + 1 < content.size())
            {
                if (content[namePos + 1] == '\\') name += '\\';
                else if (content[namePos + 1] == '"') name += '"';
                else name += content[namePos];
                namePos += 2;
            }
            else
            {
                name += content[namePos];
                namePos++;
            }
        }

        if (!path.empty())
            m_recentRepos.push_back({path, name.empty() ? path : name});
    }

    if (!m_recentRepos.empty())
        m_selectedRepoIndex = 0;
}

void HomeView::AddRecent(const std::string& path)
{
    std::error_code ec;
    auto normalized = std::filesystem::weakly_canonical(path, ec);
    std::string normPath = normalized.string();
    if (ec) normPath = path;

    auto slash = normPath.find_last_of("\\/");
    std::string name = (slash != std::string::npos) ? normPath.substr(slash + 1) : normPath;
    m_recentRepos.erase(
        std::remove_if(m_recentRepos.begin(), m_recentRepos.end(),
            [&normPath](const RecentRepo& r) { return r.path == normPath; }),
        m_recentRepos.end());
    m_recentRepos.insert(m_recentRepos.begin(), {normPath, name});
    if (m_recentRepos.size() > 50) m_recentRepos.pop_back();
    m_selectedRepoIndex = 0;
    LoadReadme(m_recentRepos[0].path);
}

void HomeView::LoadReadme(const std::string& repoPath)
{
    m_readmeContent.clear();

    std::string readmePath;
    auto repoDir = std::filesystem::path(repoPath).parent_path();
    auto candidates = { repoPath + "/../README.md", repoPath + "/../Readme.md",
                        repoPath + "/../readme.md", repoPath + "/../docs/README.md" };

    for (auto& c : candidates)
    {
        auto p = std::filesystem::path(c).lexically_normal();
        std::error_code ec;
        if (std::filesystem::exists(p, ec))
        {
            readmePath = p.string();
            break;
        }
    }

    if (readmePath.empty())
    {
        // Try repo root directly
        auto dir = std::filesystem::path(repoPath);
        auto dirCandidates = { dir / "README.md", dir / "Readme.md",
                               dir / "readme.md", dir / "docs/README.md" };
        for (auto& c : dirCandidates)
        {
            std::error_code ec;
            if (std::filesystem::exists(c, ec))
            {
                readmePath = c.string();
                break;
            }
        }
    }

    if (readmePath.empty()) return;

    std::ifstream file(readmePath);
    if (!file.is_open()) return;
    std::stringstream buf;
    buf << file.rdbuf();
    m_readmeContent = buf.str();
}

void HomeView::Render()
{
    auto avail = ImGui::GetContentRegionAvail();

    if (m_recentRepos.empty() || m_selectedRepoIndex < 0)
    {
        // Show centered welcome card
        float cardW = 500;
        float cardH = std::min(avail.y - 20, 400.0f);
        ImGui::SetCursorPos(ImVec2((avail.x - cardW) * 0.5f, (avail.y - cardH) * 0.3f));

        ImGui::BeginChild("##home_card", ImVec2(cardW, cardH), true, ImGuiWindowFlags_NoScrollbar);

        ImGui::Spacing();
        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
        ImGui::PushFont(nullptr);
        ImVec2 titleSize = ImGui::CalcTextSize("GitBee");
        ImGui::SetCursorPosX((cardW - titleSize.x) * 0.5f);
        ImGui::TextUnformatted("GitBee");
        ImGui::PopFont();
        ImGui::PopStyleColor();

        ImVec2 descSize = ImGui::CalcTextSize(APP_DESC);
        ImGui::SetCursorPosX((cardW - descSize.x) * 0.5f);
        ImGui::TextColored(ImVec4(0.6f, 0.6f, 0.6f, 1.0f), "%s", APP_DESC);

        ImVec2 verSize = ImGui::CalcTextSize(APP_VERSION);
        ImGui::SetCursorPosX((cardW - verSize.x) * 0.5f);
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "%s", APP_VERSION);

        ImGui::Spacing();
        ImGui::Spacing();
        ImGui::Separator();
        ImGui::Spacing();

        ImGui::SetCursorPosX((cardW - 220) * 0.5f);
        if (ImGui::Button("Open Repository...", ImVec2(220, 32)))
        {
            if (OnOpenRepository) OnOpenRepository();
        }

        ImGui::Spacing();
        ImGui::SetCursorPosX((cardW - 220) * 0.5f);
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.15f, 0.15f, 0.4f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.2f, 0.2f, 0.5f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.25f, 0.25f, 0.55f, 1.0f));
        if (ImGui::Button("Scan for Repositories...", ImVec2(220, 32)))
        {
            if (OnScanFolder) OnScanFolder();
        }
        ImGui::PopStyleColor(3);

        ImGui::Spacing();
        ImGui::SetCursorPosX((cardW - 300) * 0.5f);
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
            "Or drag & drop a folder onto this window");

        ImGui::EndChild();
        return;
    }

    // Main layout: left panel (repos) + right panel (readme)
    m_splitView.Begin();
    RenderLeftPanel();
    m_splitView.Separate();
    RenderRightPanel();
    m_splitView.End();
}

void HomeView::RenderLeftPanel()
{
    ImGui::BeginChild("##home_repo_list", ImVec2(0, 0), true);

    ImGui::TextUnformatted("Repositories");
    ImGui::Separator();

    for (int i = 0; i < (int)m_recentRepos.size(); i++)
    {
        auto& repo = m_recentRepos[i];
        bool exists = std::filesystem::exists(repo.path);

        ImGui::PushID(i);

        ImVec4 textColor = exists ? ImVec4(1, 1, 1, 1) : ImVec4(0.4f, 0.4f, 0.4f, 1);
        ImGui::PushStyleColor(ImGuiCol_Text, textColor);

        if (!exists)
            ImGui::BeginDisabled();

        if (ImGui::Selectable(repo.name.c_str(), i == m_selectedRepoIndex))
        {
            m_selectedRepoIndex = i;
            LoadReadme(repo.path);
        }

        if (!exists)
        {
            ImGui::EndDisabled();
            ImGui::SameLine();
            if (ImGui::SmallButton("X"))
            {
                m_recentRepos.erase(m_recentRepos.begin() + i);
                if (m_selectedRepoIndex >= (int)m_recentRepos.size())
                    m_selectedRepoIndex = (int)m_recentRepos.size() - 1;
                ImGui::PopStyleColor();
                ImGui::PopID();
                break;
            }
        }

        if (ImGui::IsItemHovered() && exists)
            ImGui::SetTooltip("%s", repo.path.c_str());

        ImGui::PopStyleColor();
        ImGui::PopID();
    }

    ImGui::EndChild();
}

void HomeView::RenderRightPanel()
{
    if (m_selectedRepoIndex < 0 || m_selectedRepoIndex >= (int)m_recentRepos.size())
        return;

    auto& repo = m_recentRepos[m_selectedRepoIndex];

    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
    ImGui::TextUnformatted(repo.name.c_str());
    ImGui::PopStyleColor();

    ImGui::SameLine();
    if (ImGui::SmallButton("Open"))
    {
        if (OnOpenRecent) OnOpenRecent(repo.path);
    }

    ImGui::SameLine();
    if (ImGui::SmallButton("Remove"))
    {
        m_recentRepos.erase(m_recentRepos.begin() + m_selectedRepoIndex);
        if (m_selectedRepoIndex >= (int)m_recentRepos.size())
            m_selectedRepoIndex = (int)m_recentRepos.size() - 1;
        return;
    }

    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "%s", repo.path.c_str());

    ImGui::Separator();

    if (!m_readmeContent.empty())
    {
        ImGui::BeginChild("##readme_view", ImVec2(0, 0), false);

        ImGui::MarkdownConfig mdConfig;
        mdConfig.linkCallback = LinkCallback;
        mdConfig.headingFormats[0] = { ImGui::GetFont(), true };
        mdConfig.headingFormats[1] = { ImGui::GetFont(), true };
        mdConfig.headingFormats[2] = { ImGui::GetFont(), false };

        ImGui::Markdown(m_readmeContent.c_str(), m_readmeContent.size(), mdConfig);

        ImGui::EndChild();
    }
    else
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No README.md");
    }
}
