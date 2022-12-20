
#include "imgui_impl_sdl_opengl3.h"


int Create(const char* title,IMGUI_INIT_CALLBACK imgui_init_cb,IMGUI_DRAW_CALLBACK imgui_draw_cb)
{
char *glsl_version_;
  
    // Decide GL+GLSL versions
#ifdef __APPLE__
    // GL 3.2 Core + GLSL 150
    glsl_version_ = "#version 150";
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_FLAGS, SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG); // Always required on Mac
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, 3);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, 2);
#else
    // GL 3.0 + GLSL 130
    glsl_version_ = "#version 130";
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_FLAGS, 0);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, 3);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, 0);
#endif

    SDL_GL_SetAttribute(SDL_GL_DOUBLEBUFFER, 1);
    SDL_GL_SetAttribute(SDL_GL_DEPTH_SIZE, 24);
    SDL_GL_SetAttribute(SDL_GL_STENCIL_SIZE, 8);

    if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_EVENTS | SDL_INIT_GAMECONTROLLER) != 0) 
    {
        std::cout<<"Error initializing sdl: "<<SDL_GetError()<<std::endl;
        return -1;
    }
    SDL_EnableScreenSaver();
    SDL_EventState(SDL_DROPFILE, SDL_ENABLE);
    // atexit(SDL_Quit);

#ifdef SDL_HINT_VIDEO_X11_NET_WM_BYPASS_COMPOSITOR /* Available since 2.0.8 */
  SDL_SetHint(SDL_HINT_VIDEO_X11_NET_WM_BYPASS_COMPOSITOR, "0");
#endif
#if SDL_VERSION_ATLEAST(2, 0, 5)
  SDL_SetHint(SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");
#endif
 #if SDL_VERSION_ATLEAST(2, 0, 18)
   SDL_SetHint(SDL_HINT_IME_SHOW_UI, "1");
 #endif
#if SDL_VERSION_ATLEAST(2, 0, 22)
  SDL_SetHint(SDL_HINT_IME_SUPPORT_EXTENDED_TEXT, "1");
#endif

#if SDL_VERSION_ATLEAST(2, 0, 8)
  /* This hint tells SDL to respect borderless window as a normal window.
  ** For example, the window will sit right on top of the taskbar instead
  ** of obscuring it. */
  SDL_SetHint("SDL_BORDERLESS_WINDOWED_STYLE", "1");
#endif
#if SDL_VERSION_ATLEAST(2, 0, 12)
  /* This hint tells SDL to allow the user to resize a borderless windoow.
  ** It also enables aero-snap on Windows apparently. */
  SDL_SetHint("SDL_BORDERLESS_RESIZABLE_STYLE", "1");
#endif
#if SDL_VERSION_ATLEAST(2, 0, 9)
  SDL_SetHint("SDL_MOUSE_DOUBLE_CLICK_RADIUS", "4");
#endif

    SDL_DisplayMode dm;
    SDL_GetCurrentDisplayMode(0, &dm);

    auto window = SDL_CreateWindow(
        title, SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, dm.w * 0.8, dm.h * 0.8,
        SDL_WINDOW_RESIZABLE | SDL_WINDOW_ALLOW_HIGHDPI | SDL_WINDOW_OPENGL);
    // init_window_icon();
    if (!window) {
        fprintf(stderr, "Error creating lite-xl window: %s", SDL_GetError());
        // exit(1);
        return -1;
    }

    auto gl_context = SDL_GL_CreateContext(window);
    SDL_GL_MakeCurrent(window, gl_context);
    // SDL_GL_SetSwapInterval(1); // Enable vsync

    //Check if Glew OpenGL loader is correct
    int err = glewInit();
    if (err!=GLEW_OK)
    {
        std::cout<<glewGetErrorString(err)<<std::endl;
        fprintf(stderr, "Failed to initialize OpenGL loader!\n");
        return -1;
    }

    // Setup Dear ImGui context
    // IMGUI_CHECKVERSION();
    // ImGui::CreateContext();
    auto imgui_context = imgui_init_cb();
    if (!imgui_context)
    {
        imgui_context = ImGui::CreateContext();
    }
    ImGui::SetCurrentContext(imgui_context);

    
  //  // Setup Platform/Renderer backends
    ImGui_ImplSDL2_InitForOpenGL(window, gl_context);
    ImGui_ImplOpenGL3_Init(glsl_version_);

   ImVec4 clear_color_ = ImVec4(0.45f, 0.55f, 0.60f, 1.00f);
   bool is_done_ = false;
//    bool show_demo=true;
     while (!is_done_)
     {
        // Poll and handle events (inputs, window resize, etc.)
        // You can read the io.WantCaptureMouse, io.WantCaptureKeyboard flags to tell if dear imgui wants to use your inputs.
        // - When io.WantCaptureMouse is true, do not dispatch mouse input data to your main application.
        // - When io.WantCaptureKeyboard is true, do not dispatch keyboard input data to your main application.
        // Generally you may always pass all inputs to dear imgui, and hide them from your application based on those two flags.
        SDL_Event event;
        while (SDL_PollEvent(&event))
        {
            ImGui_ImplSDL2_ProcessEvent(&event);
            if (event.type == SDL_QUIT)
                is_done_ = true;
            if (event.type == SDL_WINDOWEVENT && event.window.event == SDL_WINDOWEVENT_CLOSE && event.window.windowID == SDL_GetWindowID(window))
                is_done_ = true;

            // Start the Dear ImGui frame
            ImGui_ImplOpenGL3_NewFrame();
            ImGui_ImplSDL2_NewFrame(window);

            ImGui::NewFrame();
            imgui_draw_cb();
            ImGui::Render();
            // ImGui::ShowDemoWindow(&show_demo);
            // ImGui::Render();
            
            // int window_width=0;
            // int window_height=0;
            // SDL_GetWindowSize(window,&window_width,&window_height);
            // glViewport(0, 0, window_width, window_height);
            ImGuiIO &io = ImGui::GetIO();
            glViewport(0, 0, (int)io.DisplaySize.x, (int)io.DisplaySize.y);
            glClearColor(clear_color_.x, clear_color_.y, clear_color_.z, clear_color_.w);
            glClear(GL_COLOR_BUFFER_BIT);

            // if(imgui_draw_render_cb)
            // {
            //     //RenderDrawData
            //     imgui_draw_render_cb();
            // }
            ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());

            SDL_GL_SwapWindow(window);

        }
     }
    
}

// void RenderDrawData(struct ImDrawData* draw_data)
// {
//     ImGui_ImplOpenGL3_RenderDrawData(draw_data);
// }


 struct lua_State* CreateLuaState()
 {
     lua_State * lua_state = luaL_newstate(); 
     luaL_openlibs(lua_state);
     //luaJIT_setmode(lua_state,-1,LUAJIT_MODE_ON);
     std::cout << "CreateLuaState:" << lua_state << std::endl;
     return lua_state;
 }

// void CallLuaScript(struct lua_State* lua_state,const char* lua_script)
// {
//     int ret = luaL_loadfile(lua_state,lua_script); 
//     std::cout << "CallLuaScript luaL_loadfile:" << lua_script << ret << std::endl;
//     ret = lua_pcall(lua_state,0,0,0);
// }

// void CloseLuaState(struct lua_State* lua_state)
// {
//     std::cout << "CloseLuaState" << std::endl;
//     lua_close(lua_state); 
// }

// void CallLuaFunction(struct lua_State* lua_state, const char* function_name)
// {
//     //获取lua中的showinfo函数
//     lua_getglobal(lua_state, function_name);
//     //cpp 调用无参数的lua函数，无返回值
//     //lua_pcall(global_state_, 1, 0, 0);
//     lua_pcall(lua_state,0,0,0);
// }