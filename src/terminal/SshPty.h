#pragma once

#include "PtyAdapter.h"
#include <atomic>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <string>

/// SSH PTY that spawns the system `ssh` command via forkpty.
/// This avoids needing libssh2 at the cost of less control over auth.
class SshPty : public PtyAdapter {
public:
    SshPty();
    ~SshPty() override;

    // PtyAdapter interface
    bool Start(const PtyConfig& cfg) override;
    void Close() override;
    int Write(const char* data, size_t len) override;
    int Read(char* buf, size_t bufsize) override;
    bool Resize(int cols, int rows) override;
    bool IsOpen() const override;
    PtyType Type() const override { return PtyType::Ssh; }

private:
    bool OpenPty(const std::string& sshCommand, int cols, int rows);
    void ClosePty();
    void PumpOutput();

    int m_masterFd = -1;
    int m_pid = -1;

#ifdef _WIN32
    // ConPTY handles
    void* m_hConPty = nullptr;        // HPCON
    void* m_hConPtyInput = nullptr;   // HANDLE for writing to ConPTY
    void* m_hProcess = nullptr;       // HANDLE for child process
#endif

    // Ring buffer (same design as LocalPty)
    static constexpr size_t RING_SIZE = 256 * 1024;  // 256KB
    char* m_ringBuf = nullptr;
    size_t m_ringHead = 0;
    size_t m_ringTail = 0;
    std::mutex m_ringMutex;
    std::condition_variable m_ringCv;

    std::atomic<bool> m_running{false};
    std::thread m_pumpThread;
};
