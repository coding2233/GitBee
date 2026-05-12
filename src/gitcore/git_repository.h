#pragma once

#include <memory>
#include <string>
#include <vector>
#include <functional>
#include "git_types.h"
#include "git_process.h"

class GitRepository {
public:
    explicit GitRepository(const std::string& path);
    ~GitRepository() = default;

    static std::shared_ptr<GitRepository> Open(const std::string& path);

    bool IsValid() const;
    std::string GetRootPath() const;
    std::string GetCurrentBranch() const;
    const std::string& GetPath() const { return m_path; }

    GitSignature GetSignature() const;

    // Log
    std::vector<GitCommit> GetLog(const GitLogOptions& options = GitLogOptions{}) const;
    GitCommitDetail GetCommitDetail(const std::string& hash) const;
    std::vector<GitCommit> GetFileLog(const std::string& filePath, int maxCount = 50) const;
    std::vector<std::string> GetChangedFiles(const std::string& fromHash,
                                              const std::string& toHash) const;

    // Status & Staging
    GitStatus GetStatus() const;
    bool Stage(const std::vector<std::string>& files = {});
    bool Unstage(const std::vector<std::string>& files = {});
    bool Restore(const std::vector<std::string>& files);
    bool Discard(const std::vector<std::string>& files);

    // Commit
    bool Commit(const std::string& message);

    // Branch
    std::vector<GitBranchInfo> GetBranches() const;
    std::vector<GitBranchInfo> GetRemoteBranches() const;
    bool CheckoutBranch(const std::string& name);
    bool CreateBranch(const std::string& name, const std::string& from = {});

    // Tags
    std::vector<GitTagInfo> GetTags() const;

    // Submodules
    std::vector<GitSubmoduleInfo> GetSubmodules() const;

    // Remote operations
    bool Pull();
    bool Push();
    bool Fetch();

    std::string GetLastGitError() const { return m_lastError; }

private:
    std::string m_path;
    std::string m_lastError;

    static GitResult Git(const std::string& path,
                         const std::vector<std::string>& args);

    GitBranchInfo ParseBranchLine(const std::string& line) const;
};
