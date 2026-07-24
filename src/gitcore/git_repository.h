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

    GitSignature GetSignature();

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

    // Worktree management
    std::vector<GitWorktreeInfo> GetWorktrees() const;
    bool AddWorktree(const std::string& path, const std::string& branch,
                     bool detach = false, const std::string& baseCommit = {});
    bool RemoveWorktree(const std::string& path, bool force = false);
    bool PruneWorktrees();

    // Submodule support for worktrees
    bool InitSubmodules(const std::string& worktreePath);

    // Nested repo detection & copy
    std::vector<GitNestedRepoInfo> DetectNestedRepos() const;
    bool CloneNestedRepo(const std::string& srcGitDir, const std::string& dstPath);

    std::string GetLastGitError() const { return m_lastError; }

private:
    std::string m_path;
    std::string m_lastError;

    mutable GitSignature m_signature;
    mutable bool m_signatureLoaded = false;

    static GitResult Git(const std::string& path,
                         const std::vector<std::string>& args);

    GitBranchInfo ParseBranchLine(const std::string& line) const;
};
