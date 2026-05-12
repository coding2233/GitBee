#include "git_process.h"

#include <cstdio>
#include <cstring>
#include <sstream>
#include <fstream>
#include <cstdlib>

#ifdef _WIN32
#define popen _popen
#define pclose _pclose
#include <io.h>
#include <process.h>
#else
#include <sys/wait.h>
#include <unistd.h>
#endif

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

static std::string GetTempDir()
{
#ifdef _WIN32
    char* tmp = nullptr;
    size_t len = 0;
    if (_dupenv_s(&tmp, &len, "TEMP") == 0 && tmp) {
        std::string result(tmp);
        free(tmp);
        return result;
    }
    return "C:\\Windows\\Temp";
#else
    const char* tmp = std::getenv("TMPDIR");
    if (!tmp) tmp = "/tmp";
    return tmp;
#endif
}

static std::string MakeErrorPath()
{
    static int counter = 0;
    counter++;
#ifdef _WIN32
    return GetTempDir() + "\\gitbee_err_" + std::to_string(_getpid()) + "_" + std::to_string(counter) + ".txt";
#else
    return GetTempDir() + "/gitbee_err_" + std::to_string(getpid()) + "_" + std::to_string(counter) + ".txt";
#endif
}

static std::string BuildCommand(const std::string& repoPath,
                                const std::vector<std::string>& args,
                                const std::string& errPath)
{
    std::string escapedPath = EscapePath(repoPath);
#ifdef _WIN32
    std::string cmd = "git -C \"" + escapedPath + "\"";
#else
    std::string cmd = "LC_ALL=C git -C \"" + escapedPath + "\"";
#endif
    for (const auto& arg : args) {
        cmd += " \"";
        std::string escaped = EscapePath(arg);
        cmd += escaped;
        cmd += "\"";
    }
    cmd += " 2>\"" + errPath + "\"";
    return cmd;
}

GitResult GitProcess::Execute(const std::string& repoPath,
                              const std::vector<std::string>& args)
{
    GitResult result;
    std::string errPath = MakeErrorPath();

    std::string cmd = BuildCommand(repoPath, args, errPath);

    FILE* pipe = popen(cmd.c_str(), "r");

    if (!pipe) {
#ifdef _WIN32
        result.err = "Git is not installed or not found in PATH";
#else
        if (errno == ENOENT) {
            result.err = "Git is not installed or not found in PATH";
        } else {
            result.err = "Failed to execute command: " + std::string(strerror(errno));
        }
#endif
        return result;
    }

    std::string output;
    char buf[4096];
    size_t n;
    while ((n = fread(buf, 1, sizeof(buf), pipe)) > 0) {
        output.append(buf, n);
    }

    int status = pclose(pipe);

#ifdef _WIN32
    result.ok = (status == 0);
#else
    result.ok = (status != -1 && WIFEXITED(status) && WEXITSTATUS(status) == 0);
#endif

    // Read stderr from temp file
    std::ifstream errFile(errPath);
    if (errFile.is_open()) {
        std::stringstream errBuf;
        errBuf << errFile.rdbuf();
        result.err = errBuf.str();
        errFile.close();
    }
    remove(errPath.c_str());

    while (!output.empty() && (output.back() == '\n' || output.back() == '\r')) {
        output.pop_back();
    }
    result.out = std::move(output);

    return result;
}
