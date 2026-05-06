#include "app/app.h"

int main() {
    volt::AppConfig cfg;
    cfg.title = "GitBee - Git Repository Viewer";
    cfg.width = 1280;
    cfg.height = 720;
    cfg.use_topbar = true;

    GitBeeApp app(cfg);
    return app.Run();
}
