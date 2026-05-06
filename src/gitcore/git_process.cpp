#include "git_process.h"

#include <cstdio>
#include <cstring>
#include <sstream>

#ifdef _WIN32
#define popen _popen
#define pclose _pclose
#else
#include <sys/wait.h>
#include <unistd.h>
#endif

std::string GitProcess::m_lastError;

static std::string EscapePath(const std::string& path)
{
    std::string escaped = path;
    size_t pos = 0;
    while ((pos = escaped.find('\"', pos)) != std::string::npos) {
        escaped.replace(pos, 1, "\\\"");
        pos += 2;
    }
    return escaped;
}

static std::string BuildCommand(const std::string& repoPath,
                                const std::vector<std::string>& args)
{
    std::string escapedPath = EscapePath(repoPath);
    std::string cmd = "git --git-dir=\"" + escapedPath + "/.git\" --work-tree=\"" + escapedPath + "\"";
    for (const auto& arg : args) {
        cmd += " \"";
        std::string escaped = EscapePath(arg);
        cmd += escaped;
        cmd += "\"";
    }
#ifdef _WIN32
    cmd += " 2>NUL";
#else
    cmd += " 2>/dev/null";
#endif
    return cmd;
}

std::pair<bool, std::string> GitProcess::Execute(const std::string& repoPath,
                                                  const std::vector<std::string>& args)
{
    std::string cmd = BuildCommand(repoPath, args);

    FILE* pipe = popen(cmd.c_str(), "r");

    if (!pipe) {
#ifdef _WIN32
        m_lastError = "Git is not installed or not found in PATH";
#else
        if (errno == ENOENT) {
            m_lastError = "Git is not installed or not found in PATH";
        } else {
            m_lastError = "Failed to execute command: " + std::string(strerror(errno));
        }
#endif
        return {false, {}};
    }

    std::string output;
    char buf[4096];
    while (fgets(buf, sizeof(buf), pipe) != nullptr) {
        output += buf;
    }

    int status = pclose(pipe);

    bool success;
#ifdef _WIN32
    success = (status == 0);
#else
    success = (status != -1 && WIFEXITED(status) && WEXITSTATUS(status) == 0);
#endif

    if (!success) {
        m_lastError = "Git command failed (exit code " + std::to_string(status) + ")";
    } else {
        m_lastError.clear();
    }

    while (!output.empty() && (output.back() == '\n' || output.back() == '\r')) {
        output.pop_back();
    }

    return {success, output};
}

std::string GitProcess::GetLastError()
{
    return m_lastError;
}
