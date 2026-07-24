#pragma once

#include <string>
#include <vector>

struct GitResult
{
    bool ok = false;
    std::string out;
    std::string err;
};

class GitProcess {
public:
    static GitResult Execute(const std::string& repoPath,
                             const std::vector<std::string>& args);

    // Run a shell script (.sh) with the repo path as $1.
    // On Windows, uses Git Bash; on Linux/Mac, uses /bin/bash.
    static GitResult ExecuteScript(const std::string& scriptPath,
                                   const std::string& repoPath,
                                   const std::vector<std::string>& extraArgs = {});

    // Find the shell binary to use (git-bash on Windows, /bin/bash elsewhere)
    static std::string GetShellPath();

    // Compute the scripts directory (user scripts + bundled defaults)
    static std::string GetUserScriptsDir();
    static std::string GetDefaultScriptsDir();
};
