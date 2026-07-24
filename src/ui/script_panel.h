#pragma once

#include <memory>
#include <string>
#include <vector>
#include <functional>
#include <atomic>
#include <thread>
#include <mutex>

class GitRepository;

struct ScriptInfo {
    std::string name;       // filename without .sh
    std::string path;       // full path to .sh file
    std::string source;     // "user" or "default" (bundled)
};

class ScriptPanel
{
public:
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

    std::function<void(const std::string& operation, bool success,
                       const std::string& summary, const std::string& detail)> OnOperationLog;

private:
    std::shared_ptr<GitRepository> m_repository;

    // Scripts cache
    bool m_loaded = false;
    std::vector<ScriptInfo> m_scripts;

    // Async execution state
    struct RunningScript {
        std::string name;
        std::atomic<bool> running{false};
        std::string output;
        bool result = false;
        std::thread worker;
    };
    std::unique_ptr<RunningScript> m_running;

    // Open script dir button state
    bool m_openingDir = false;

    void ScanScripts();
    void RunScript(const ScriptInfo& script);
    void ProcessResult();

    static std::string NameFromPath(const std::string& path);
};
