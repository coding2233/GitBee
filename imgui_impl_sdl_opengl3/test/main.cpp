#if _WIN32
#pragma comment(linker, "/subsystem:\"windows\" /entry:\"mainCRTStartup\"")
#endif

#include <iostream>

#include "imgui_impl_sdl_opengl3.h"
#include "iiso3_export.h"

static TextEditor *text_editor_;


ImGuiContext* OnImGuiInit()
{
   return ImGui::CreateContext();

   text_editor_ = igNewTextEditor();
   text_editor_->SetText("asdasdgfdsfds5116231sdgf234fd56g456fdg416fd");
}

void OnImGuiDraw()
{
  ImGui::ShowDemoWindow();

  ImGui::Begin("Text editor");
  igRenderTextEditor(text_editor_,"aaaa",ImGui::GetContentRegionAvail(),true);
  ImGui::End();
}

int main(int argc,char *args[])
{
    std::cout<< "Hello test main"<<std::endl;   

    // CreateSdlWindow("test windiw",0,0,)
    auto sdl_window= CreateSdlWindow("test window",0,0,0);
    CreateRender(sdl_window,OnImGuiInit,OnImGuiDraw,nullptr);

    return 0;
}
