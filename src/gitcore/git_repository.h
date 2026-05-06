#pragma once

#include <string>

class GitRepository {
public:
    explicit GitRepository(const std::string& path);

    bool IsValid() const;

    std::string GetRootPath() const;

    std::string GetCurrentBranch() const;

    const std::string& GetPath() const { return m_path; }

private:
    std::string m_path;
};
