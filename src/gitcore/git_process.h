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
};
