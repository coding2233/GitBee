#include "config_panel.h"
#include "../gitcore/git_repository.h"
#include "../gitcore/git_process.h"
#include <imgui.h>
#include <sstream>

void ConfigPanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = std::move(repo);
    m_loaded = false;
}

void ConfigPanel::Refresh()
{
    m_loaded = false;
}

void ConfigPanel::LoadConfig()
{
    m_localConfig.clear();
    m_globalConfig.clear();

    if (!m_repository) return;

    // Local config
    auto r = GitProcess::Execute(m_repository->GetPath(), {"config", "--local", "--list"});
    if (r.ok && !r.out.empty())
    {
        std::istringstream stream(r.out);
        std::string line;
        while (std::getline(stream, line))
        {
            auto eq = line.find('=');
            if (eq != std::string::npos)
                m_localConfig.push_back({line.substr(0, eq), line.substr(eq + 1)});
        }
    }

    // Global config
    auto rg = GitProcess::Execute(m_repository->GetPath(), {"config", "--global", "--list"});
    if (rg.ok && !rg.out.empty())
    {
        std::istringstream stream(rg.out);
        std::string line;
        while (std::getline(stream, line))
        {
            auto eq = line.find('=');
            if (eq != std::string::npos)
                m_globalConfig.push_back({line.substr(0, eq), line.substr(eq + 1)});
        }
    }

    m_loaded = true;
}

void ConfigPanel::Render()
{
    if (!m_repository)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No repository opened");
        return;
    }

    if (!m_loaded)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Loading config...");
        LoadConfig();
        return;
    }

    ImGui::BeginChild("##config_content", ImVec2(0, 0), true);

    ImGui::TextUnformatted("Local Config (project)");
    ImGui::Separator();
    if (m_localConfig.empty())
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No local config");
    else
        RenderTable("##local", m_localConfig);

    ImGui::Spacing();
    ImGui::TextUnformatted("Global Config (user)");
    ImGui::Separator();
    if (m_globalConfig.empty())
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No global config");
    else
        RenderTable("##global", m_globalConfig);

    ImGui::EndChild();
}

void ConfigPanel::RenderTable(const char* title, const std::vector<GitConfigEntry>& entries)
{
    if (ImGui::BeginTable(title, 2,
        ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders,
        ImVec2(0, 0)))
    {
        ImGui::TableSetupColumn("Key", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableSetupColumn("Value", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableHeadersRow();

        for (auto& entry : entries)
        {
            ImGui::TableNextRow();
            ImGui::TableNextColumn();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
            ImGui::TextUnformatted(entry.key.c_str());
            ImGui::PopStyleColor();
            ImGui::TableNextColumn();
            ImGui::TextUnformatted(entry.value.c_str());
        }

        ImGui::EndTable();
    }
}
