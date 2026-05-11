#pragma once

#include <memory>
#include <string>
#include <vector>

#include "git_types.h"

class GitRepository {
public:
    explicit GitRepository(const std::string& path);
    virtual ~GitRepository() = default;

    static std::shared_ptr<GitRepository> Open(const std::string& path);

    bool IsValid() const;

    std::string GetRootPath() const;

    std::string GetCurrentBranch() const;

    const std::string& GetPath() const { return m_path; }

    std::vector<GitCommit> GetLog(const GitLogOptions& options = GitLogOptions{}) const;

    GitCommitDetail GetCommitDetail(const std::string& hash) const;

    std::vector<GitCommit> GetFileLog(const std::string& filePath, int maxCount = 50) const;

    std::vector<std::string> GetChangedFiles(const std::string& fromHash,
                                             const std::string& toHash) const;

    GitStatus GetStatus() const;

private:
    std::string m_path;
};
