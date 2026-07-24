#pragma once

#include <string>
#include <memory>
#include <functional>

enum class PtyType { Local, Ssh };

struct PtyConfig {
    // General
    int cols = 80;
    int rows = 24;

    // Local PTY
    std::string shellCommand;   // empty = $SHELL or /bin/bash
    std::string workingDir;     // initial cwd (empty = current dir)

    // SSH PTY (used by SshPty in Phase 2)
    std::string host;
    int port = 22;
    std::string username;
    std::string password;           // for password auth (unencrypted in memory)
    std::string privateKeyPath;     // for publickey auth
    std::string passphrase;         // for encrypted key
    bool useAgent = false;
};

/// Abstract PTY interface.
/// LocalPty and SshPty both implement this so TerminalEmulator can be agnostic.
class PtyAdapter {
public:
    virtual ~PtyAdapter() = default;

    /// Open the PTY / SSH connection.
    virtual bool Start(const PtyConfig& cfg) = 0;

    /// Close and clean up.
    virtual void Close() = 0;

    /// Write data to the PTY (user input).
    /// Returns bytes written, or -1 on error.
    virtual int Write(const char* data, size_t len) = 0;

    /// Read available data from the PTY (process output).
    /// Returns bytes read, 0 if no data available, -1 on error/EOF.
    virtual int Read(char* buf, size_t bufsize) = 0;

    /// Resize the PTY dimensions.
    virtual bool Resize(int cols, int rows) = 0;

    /// Check if the PTY is still open/connected.
    virtual bool IsOpen() const = 0;

    /// Get PTY type.
    virtual PtyType Type() const = 0;

    /// Called when the PTY is closed unexpectedly.
    std::function<void()> OnClosed;
};
