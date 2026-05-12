#include "home_view.h"
#include <imgui.h>
#include <algorithm>
#include <fstream>
#include <sstream>

static const char* APP_VERSION = "v0.2.0";
static const char* APP_DESC = "A Lightweight Git Interface Management Tool";

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
}

void HomeView::AddRecent(const std::string& path)
{
    auto slash = path.find_last_of("\\/");
    std::string name = (slash != std::string::npos) ? path.substr(slash + 1) : path;
    m_recentRepos.erase(
        std::remove_if(m_recentRepos.begin(), m_recentRepos.end(),
            [&path](const RecentRepo& r) { return r.path == path; }),
        m_recentRepos.end());
    m_recentRepos.insert(m_recentRepos.begin(), {path, name});
    if (m_recentRepos.size() > 10) m_recentRepos.pop_back();
}

void HomeView::Render()
{
    auto avail = ImGui::GetContentRegionAvail();

    // Centered content in the upper portion
    float cardW = 500;
    float cardH = std::min(avail.y - 20, 400.0f);
    ImGui::SetCursorPos(ImVec2((avail.x - cardW) * 0.5f, (avail.y - cardH) * 0.3f));

    ImGui::BeginChild("##home_card", ImVec2(cardW, cardH), true, ImGuiWindowFlags_NoScrollbar);

    // App title area
    ImGui::Spacing();
    ImGui::SetCursorPosX((cardW - 80) * 0.5f);
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

    // Quick actions
    ImGui::SetCursorPosX((cardW - 220) * 0.5f);

    if (ImGui::Button("Open Repository...", ImVec2(220, 32)))
    {
        if (OnOpenRepository) OnOpenRepository();
    }

    ImGui::Spacing();
    ImGui::SetCursorPosX((cardW - 220) * 0.5f);
    ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.15f, 0.4f, 0.15f, 1.0f));
    ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.2f, 0.5f, 0.2f, 1.0f));
    ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.25f, 0.55f, 0.25f, 1.0f));
    if (ImGui::Button("Clone Repository...", ImVec2(220, 32)))
    {
        // Clone not yet implemented
    }
    ImGui::PopStyleColor(3);

    ImGui::Spacing();
    ImGui::SetCursorPosX((cardW - 300) * 0.5f);
    ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
        "Or drag & drop a folder onto this window");

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    // Recent repositories
    ImGui::TextUnformatted("Recent Repositories:");
    ImGui::Spacing();

    if (m_recentRepos.empty())
    {
        ImGui::Indent(16);
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No recent repositories");
        ImGui::Unindent(16);
    }
    else
    {
        ImGui::BeginChild("##recent_list", ImVec2(0, std::min((int)m_recentRepos.size() * 24 + 4, 200)), true);
        for (auto& repo : m_recentRepos)
        {
            ImGui::PushID(repo.path.c_str());
            if (ImGui::Selectable(repo.name.c_str(), false, ImGuiSelectableFlags_AllowDoubleClick))
            {
                if (ImGui::IsMouseDoubleClicked(0))
                {
                    if (OnOpenRecent) OnOpenRecent(repo.path);
                }
            }
            if (ImGui::IsItemHovered())
                ImGui::SetTooltip("%s", repo.path.c_str());
            ImGui::PopID();
        }
        ImGui::EndChild();
    }

    ImGui::EndChild();

    // Bottom hint
    ImVec2 hintSize = ImGui::CalcTextSize("Use File > Open Repository or Ctrl+O to get started");
    ImGui::SetCursorPos(ImVec2((avail.x - hintSize.x) * 0.5f, avail.y - 25));
    ImGui::TextColored(ImVec4(0.3f, 0.3f, 0.3f, 1.0f),
        "Use File > Open Repository or Ctrl+O to get started");
}
