#include "app/app.h"
#include <cstdio>
#include <cstring>

int main(int argc, char* argv[]) {
    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--help") == 0 || strcmp(argv[i], "-h") == 0) {
            printf("GitBee - Git Repository Viewer\n\n");
            printf("Usage: gitbee [options] [repository-path]\n\n");
            printf("Options:\n");
            printf("  -h, --help       Show this help message\n");
            printf("  -v, --version    Show version information\n\n");
            printf("Arguments:\n");
            printf("  repository-path   Open the specified git repository\n");
            return 0;
        }
        if (strcmp(argv[i], "--version") == 0 || strcmp(argv[i], "-v") == 0) {
            printf("GitBee version 0.1.0\n");
            return 0;
        }
    }

    volt::AppConfig cfg;
    cfg.title = "GitBee - Git Repository Viewer";
    cfg.width = 1280;
    cfg.height = 720;
    cfg.use_topbar = true;

    std::string repoPath;
    for (int i = 1; i < argc; i++) {
        if (argv[i][0] != '-') {
            repoPath = argv[i];
            break;
        }
    }

    GitBeeApp app(cfg);
    if (!repoPath.empty()) {
        app.OpenRepository(repoPath);
    }
    return app.Run();
}
