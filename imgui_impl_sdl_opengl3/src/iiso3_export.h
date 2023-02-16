#ifndef __IISO3_EXPORT_H__
#define __IISO3_EXPORT_H__

#include "TextEditor.h"

#if defined _WIN32 || defined __CYGWIN__
#include "windows.h"
#define API __declspec(dllexport)
#elif __GNUC__
#define API  __attribute__((__visibility__("default")))
#else
#define API
#endif

#if defined __cplusplus
#define EXTERN extern "C"
#else
#define EXTERN extern
#endif

#define EXPORT_API EXTERN API



//TextEditor Export

EXPORT_API TextEditor * igNewTextEditor();
EXPORT_API void igDeleteTextEditor(TextEditor * textEditor);
EXPORT_API void igRenderTextEditor(TextEditor * textEditor, const char* aTitle, const ImVec2 aSize = ImVec2(), bool aBorder = false);
EXPORT_API void igSetTextEditor(TextEditor * textEditor, const char* text);
EXPORT_API const char* igGetTextEditor(TextEditor * textEditor);
EXPORT_API void igSetPaletteTextEditor(TextEditor * textEditor, int style);
EXPORT_API void igSetReadOnlyTextEditor(TextEditor * textEditor, bool readOnly);
EXPORT_API void igSetShowWhitespacesTextEditor(TextEditor * textEditor, bool show);
EXPORT_API void igIgnoreChildTextEditor(TextEditor * textEditor, bool ignoreChild);
EXPORT_API TextEditor::Coordinates* igGetCursorPositionTextEditor(TextEditor * textEditor);
EXPORT_API int igGetTotalLinesTextEditor(TextEditor * textEditor);
EXPORT_API bool igIsOverwriteTextEditor(TextEditor * textEditor);
EXPORT_API bool igCanUndoTextEditor(TextEditor * textEditor);
EXPORT_API bool igIsTextChangedTextEditor(TextEditor * textEditor);


#endif