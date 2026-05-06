#pragma once

#include <string>
#include <vector>
#include <utility>

class GitProcess {
public:
    static std::pair<bool, std::string> Execute(const std::string& repoPath,
                                                 const std::vector<std::string>& args);

    static std::string GetLastError();

private:
    static std::string m_lastError;
};
