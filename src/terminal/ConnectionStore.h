#pragma once

#include <string>
#include <vector>
#include <functional>

/// Represents a saved SSH connection profile.
struct SshConnection {
    std::string id;                 // UUID
    std::string name;               // display name
    std::string host;
    int port = 22;
    std::string username;

    enum AuthMethod {
        Agent = 0,                  // use ssh-agent
        PublicKey,                  // use key file
        Password,                   // prompt for password / keyboard-interactive
        Default                     // use ~/.ssh/config
    };
    AuthMethod authMethod = Default;

    std::string privateKeyPath;     // e.g., ~/.ssh/id_ed25519

    std::string group;              // optional grouping
    int order = 0;                  // sort order
    std::string notes;              // arbitrary notes

    // New fields
    std::string startupCommand;     // optional command to run after connect
    std::string jumpHost;           // optional SSH proxy/jump host
    bool keepAlive = true;          // TCP keepalive
    int keepAliveInterval = 60;     // keepalive interval in seconds

    bool valid() const {
        return !host.empty() && port > 0 && port <= 65535;
    }
};

/// Persists and manages SSH connection profiles as JSON.
class ConnectionStore {
public:
    ConnectionStore();
    ~ConnectionStore();

    /// Load connections from the default file path (~/.config/GitBee/connections.json).
    void Load();

    /// Save connections to the default file path.
    void Save() const;

    /// Get all connections.
    const std::vector<SshConnection>& GetAll() const { return m_connections; }

    /// Add or update a connection. If id is empty, generates a new one.
    void SaveConnection(const SshConnection& conn);

    /// Remove a connection by id.
    void RemoveConnection(const std::string& id);

    /// Find a connection by id.
    const SshConnection* FindById(const std::string& id) const;

    /// Get unique group names from all connections.
    std::vector<std::string> GetGroups() const;

    /// Get connections filtered by group.
    std::vector<const SshConnection*> GetByGroup(const std::string& group) const;

    /// Get connections that have no group.
    std::vector<const SshConnection*> GetUngrouped() const;

    /// Build an SSH command string from a connection profile.
    static std::string BuildSshCommand(const SshConnection& conn);

    /// Full path to the connections.json file.
    static std::string GetFilePath();

private:
    std::vector<SshConnection> m_connections;
    std::string m_filePath;

    static std::string GenerateId();
};
