#include "script_panel.h"
#include "LoadingSpinner.h"
#include "../gitcore/git_process.h"
#include "../gitcore/git_repository.h"
#include "../dbg_log.h"
#include <imgui.h>
#include <filesystem>
#include <fstream>
#include <algorithm>

#ifndef _WIN32
#include <sys/stat.h>
#endif

std::string ScriptPanel::NameFromPath(const std::string& path)
{
    std::string name = std::filesystem::path(path).stem().string();
    return name;
}

void ScriptPanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = std::move(repo);
    m_loaded = false;
    m_scripts.clear();
    Refresh();
}

void ScriptPanel::Refresh()
{
    m_loaded = false;
    m_scripts.clear();
}

void ScriptPanel::ScanScripts()
{
    m_scripts.clear();

    // Ensure user scripts dir exists and seed a sample if empty
    std::string userDir = GitProcess::GetUserScriptsDir();
    if (!userDir.empty()) {
        std::error_code ec;
        std::filesystem::create_directories(userDir, ec);
        if (!ec && std::filesystem::exists(userDir, ec)) {
            // Seed a sample script on first use
            bool hasAny = false;
            for (auto& entry : std::filesystem::directory_iterator(userDir)) {
                if (entry.path().extension() == ".sh") { hasAny = true; break; }
            }
            if (!hasAny) {
                std::string samplePath = userDir + "/example-status.sh";
                std::ofstream sample(samplePath);
                if (sample.is_open()) {
                    sample << "#!/usr/bin/env bash\n";
                    sample << "# GitBee Quick Script\n";
                    sample <<
                        "#!/usr/bin/env bash\n"
                        "# GitBee Quick Script\n"
                        "# The repo path is passed as $1\n"
                        "# You can add any .sh file here to extend GitBee.\n"
                        "\n"
                        "REPO_PATH=\"$1\"\n"
                        "echo \"=== Git Status for: $REPO_PATH ===\"\n"
                        "cd \"$REPO_PATH\" || exit 1\n"
                        "git status --short\n"
                        "echo \"\"\n"
                        "echo \"=== Last 5 commits ===\"\n"
                        "git log --oneline -5\n";
                    sample.close();
                    // Make executable on Linux/Mac
#ifndef _WIN32
                    chmod(samplePath.c_str(), 0755);
#endif
                    LOG_INFO("Created sample script: %s", samplePath.c_str());
                }
            }
            for (auto& entry : std::filesystem::directory_iterator(userDir)) {
                if (entry.path().extension() == ".sh") {
                    m_scripts.push_back({
                        NameFromPath(entry.path().string()),
                        entry.path().string(),
                        "user"
                    });
                }
            }
        }
    }

    // Then bundled defaults (lower priority — user scripts override by name)
    std::string defaultDir = GitProcess::GetDefaultScriptsDir();
    if (!defaultDir.empty() && defaultDir != userDir) {
        std::error_code ec;
        if (std::filesystem::exists(defaultDir, ec)) {
            for (auto& entry : std::filesystem::directory_iterator(defaultDir)) {
                if (entry.path().extension() == ".sh") {
                    std::string name = NameFromPath(entry.path().string());
                    // Skip if user already has a script with same name
                    bool dup = false;
                    for (auto& s : m_scripts) {
                        if (s.name == name) { dup = true; break; }
                    }
                    if (!dup) {
                        m_scripts.push_back({
                            name,
                            entry.path().string(),
                            "default"
                        });
                    }
                }
            }
        }
    }

    // Sort by name
    std::sort(m_scripts.begin(), m_scripts.end(),
        [](const ScriptInfo& a, const ScriptInfo& b) {
            if (a.source != b.source)
                return a.source < b.source;  // "default" before "user"
            return a.name < b.name;
        });

    m_loaded = true;
    LOG_DEBUG("ScriptPanel: found %zu scripts (%zu user, %zu default)",
              m_scripts.size(),
              std::count_if(m_scripts.begin(), m_scripts.end(),
                  [](auto& s) { return s.source == "user"; }),
              std::count_if(m_scripts.begin(), m_scripts.end(),
                  [](auto& s) { return s.source == "default"; }));
}

void ScriptPanel::RunScript(const ScriptInfo& script)
{
    if (!m_repository || m_running) return;

    auto rs = std::make_unique<RunningScript>();
    rs->name = script.name;
    rs->running = true;
    rs->result = false;

    std::string scriptPath = script.path;
    std::string repoPath = m_repository->GetPath();
    auto* raw = rs.get();

    raw->worker = std::thread([raw, scriptPath, repoPath, name = script.name]() {
        LOG_INFO("ScriptPanel: running '%s' on repo %s", name.c_str(), repoPath.c_str());
        auto r = GitProcess::ExecuteScript(scriptPath, repoPath);
        raw->output = r.out;
        raw->result = r.ok;
        raw->running = false;
    });
    raw->worker.detach();

    m_running = std::move(rs);
}

void ScriptPanel::ProcessResult()
{
    if (!m_running || m_running->running) return;

    if (m_running->worker.joinable())
        m_running->worker.join();

    if (OnOperationLog) {
        OnOperationLog("script: " + m_running->name, m_running->result,
            m_running->result ? ("Script '" + m_running->name + "' completed")
                              : ("Script '" + m_running->name + "' failed"),
            m_running->output);
    }

    m_running.reset();
}

void ScriptPanel::Render()
{
    ProcessResult();

    if (!m_repository) {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No repository opened");
        return;
    }

    if (!m_loaded)
        ScanScripts();

    ImGui::BeginChild("##script_panel", ImVec2(0, 0), true);

    // Header
    ImGui::TextColored(ImVec4(0.8f, 0.6f, 0.2f, 1.0f), "Quick Scripts");

    // Running indicator
    if (m_running && m_running->running) {
        LoadingSpinner(6.0f, 2.0f);
        ImGui::SameLine();
        ImGui::TextColored(ImVec4(0.5f, 0.7f, 1.0f, 1.0f),
            "Running: %s...", m_running->name.c_str());
    }

    // Toolbar
    ImGui::SameLine(ImGui::GetWindowWidth() - 100);
    if (ImGui::SmallButton("Refresh"))
        Refresh();
    ImGui::SameLine();
    if (ImGui::SmallButton("+ Add")) {
        // Open the user scripts directory in file manager
        std::string dir = GitProcess::GetUserScriptsDir();
        if (!dir.empty()) {
            std::error_code ec;
            std::filesystem::create_directories(dir, ec);
            if (!ec) {
#ifdef _WIN32
                std::string cmd = "explorer \"" + dir + "\"";
#elif defined(__APPLE__)
                std::string cmd = "open \"" + dir + "\"";
#else
                std::string cmd = "xdg-open \"" + dir + "\"";
#endif
                std::thread([cmd]() { system(cmd.c_str()); }).detach();
            }
        }
    }
    if (ImGui::IsItemHovered())
        ImGui::SetTooltip("Open scripts folder — place .sh files here");

    ImGui::Separator();

    // Script list
    if (m_scripts.empty()) {
        ImGui::Dummy(ImVec2(0, 8));
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
            "  No scripts found.");
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
            "  Click '+ Add' to open the scripts folder");
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
            "  and place .sh files there.");
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
            "  (The repo path is passed as $1 to each script.)");
        ImGui::EndChild();
        return;
    }

    for (auto& script : m_scripts) {
        ImGui::PushID(script.path.c_str());

        // Source badge
        if (script.source == "default") {
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.4f, 0.4f, 0.4f, 1.0f));
            ImGui::Text("[D]");
            ImGui::PopStyleColor();
        } else {
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.4f, 0.8f, 0.4f, 1.0f));
            ImGui::Text("[U]");
            ImGui::PopStyleColor();
        }
        ImGui::SameLine();

        // Script name
        ImGui::TextUnformatted(script.name.c_str());
        ImGui::SameLine(ImGui::GetWindowWidth() - 60);

        // Run button
        bool disabled = (m_running && m_running->running);
        if (disabled) {
            ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true);
            ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f);
        }

        if (ImGui::SmallButton("Run")) {
            RunScript(script);
        }

        if (disabled) {
            ImGui::PopStyleVar();
            ImGui::PopItemFlag();
        }

        if (ImGui::IsItemHovered())
            ImGui::SetTooltip("Run in repo: %s", m_repository->GetPath().c_str());

        ImGui::PopID();
    }

    ImGui::EndChild();
}
