#include "git_repository.h"
#include "git_process.h"

GitRepository::GitRepository(const std::string& path)
    : m_path(path)
{
}

bool GitRepository::IsValid() const
{
    auto [success, output] = GitProcess::Execute(m_path, {"rev-parse", "--git-dir"});
    return success && !output.empty();
}

std::string GitRepository::GetRootPath() const
{
    auto [success, output] = GitProcess::Execute(m_path, {"rev-parse", "--show-toplevel"});
    if (success) {
        return output;
    }
    return {};
}

std::string GitRepository::GetCurrentBranch() const
{
    auto [success, output] = GitProcess::Execute(m_path, {"rev-parse", "--abbrev-ref", "HEAD"});
    if (success) {
        return output;
    }
    return {};
}
