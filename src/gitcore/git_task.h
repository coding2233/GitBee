#pragma once

#include <atomic>
#include <thread>
#include <functional>
#include <string>
#include <vector>
#include "git_process.h"

struct AsyncGitTask {
    std::atomic<bool> running{false};
    GitResult result;
    std::thread worker;

    template<typename F>
    void Start(F&& func) {
        if (running) return;
        running = true;
        worker = std::thread([this, f = std::forward<F>(func)]() {
            f(result);
            running = false;
        });
        worker.detach();
    }

    void StartGit(const std::string& repoPath, std::vector<std::string> args) {
        Start([repoPath, args = std::move(args)](GitResult& res) {
            res = GitProcess::Execute(repoPath, args);
        });
    }

    bool IsRunning() const { return running.load(); }

    void Cancel() {
        // Can't truly cancel a running popen, but we can detach
        if (worker.joinable())
            worker.detach();
        running = false;
    }
};

template<typename T>
struct AsyncData {
    std::atomic<bool> loading{false};
    T data;
    std::mutex mutex;

    template<typename F>
    void Load(F&& func) {
        if (loading) return;
        loading = true;
        std::thread([this, f = std::forward<F>(func)]() {
            T result = f();
            {
                std::lock_guard<std::mutex> lock(mutex);
                data = std::move(result);
            }
            loading = false;
        }).detach();
    }

    bool TryGet(T& out) {
        if (loading) return false;
        std::lock_guard<std::mutex> lock(mutex);
        out = data;
        return true;
    }

    bool IsLoading() const { return loading.load(); }
};
