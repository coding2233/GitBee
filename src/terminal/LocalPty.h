#pragma once

#include "PtyAdapter.h"
#include <atomic>
#include <thread>
#include <mutex>
#include <condition_variable>

/// Local pseudo-terminal backed by forkpty (Linux/macOS) or ConPTY (Windows).
class LocalPty : public PtyAdapter {
public:
    LocalPty();
    ~LocalPty() override;

    // PtyAdapter interface
    bool Start(const PtyConfig& cfg) override;
    void Close() override;
    int Write(const char* data, size_t len) override;
    int Read(char* buf, size_t bufsize) override;
    bool Resize(int cols, int rows) override;
    bool IsOpen() const override;
    PtyType Type() const override { return PtyType::Local; }

private:
    bool OpenPty(int cols, int rows);
    void ClosePty();
    void PumpOutput();  // thread: read from PTY fd into ring buffer

    int m_masterFd = -1;
    int m_pid = -1;

#ifdef _WIN32
    // ConPTY handles
    void* m_hConPty = nullptr;        // HPCON
    void* m_hConPtyInput = nullptr;   // HANDLE for writing to ConPTY
    void* m_hProcess = nullptr;       // HANDLE for child process
#endif

    // Ring buffer for PTY output (producer: PumpOutput thread, consumer: Read)
    static constexpr size_t RING_SIZE = 256 * 1024;  // 256KB
    char* m_ringBuf = nullptr;
    size_t m_ringHead = 0;  // write position
    size_t m_ringTail = 0;  // read position
    std::mutex m_ringMutex;
    std::condition_variable m_ringCv;

    std::atomic<bool> m_running{false};
    std::thread m_pumpThread;

    PtyConfig m_config;
};
