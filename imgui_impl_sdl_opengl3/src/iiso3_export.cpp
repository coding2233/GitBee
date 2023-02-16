#include "iiso3_export.h"

TextEditor* igNewTextEditor()
{
    auto text_editor = new TextEditor();
    return text_editor;
}

void igDeleteTextEditor(TextEditor* text_editor)
{
    delete text_editor;
}

void igRenderTextEditor(TextEditor* text_editor, const char* aTitle, const ImVec2 aSize, bool aBorder)
{
    text_editor->Render(aTitle, aSize, aBorder);
}

void igSetTextEditor(TextEditor* text_editor, const char* text)
{
    text_editor->SetText(text);
}

const char* igGetTextEditor(TextEditor* text_editor)
{
    return text_editor->GetText().c_str();
}

void igSetPaletteTextEditor(TextEditor* text_editor, int style)
{
    switch (style)
    {
    case 0:
        text_editor->SetPalette(TextEditor::GetLightPalette());
        break;
    case 1:
        text_editor->SetPalette(TextEditor::GetDarkPalette());
        break;
    case 2:
        text_editor->SetPalette(TextEditor::GetRetroBluePalette());
        break;
    default:
        text_editor->SetPalette(TextEditor::GetDarkPalette());
        break;
    }
}

void igSetReadOnlyTextEditor(TextEditor* text_editor, bool readOnly)
{
    text_editor->SetReadOnly(readOnly);
}

void igSetShowWhitespacesTextEditor(TextEditor* text_editor, bool show)
{
    text_editor->SetShowWhitespaces(show);
}


void igIgnoreChildTextEditor(TextEditor* text_editor, bool ignore)
{
    text_editor->SetImGuiChildIgnored(ignore);
}

TextEditor::Coordinates* igGetCursorPositionTextEditor(TextEditor* text_editor)
{
    auto cursorPos = text_editor->GetCursorPosition();
    // int position[2];
    // position[0]=cursorPos.mLine;
    // position[1]=cursorPos.mColumn;
    // return position;
    return &cursorPos;
}
int igGetTotalLinesTextEditor(TextEditor* text_editor)
{
    return text_editor->GetTotalLines();
}
bool igIsOverwriteTextEditor(TextEditor* text_editor)
{
    return text_editor->IsOverwrite();
}
bool igCanUndoTextEditor(TextEditor* text_editor)
{
    return text_editor->CanUndo();
}
bool igIsTextChangedTextEditor(TextEditor* text_editor)
{
    return text_editor->IsTextChanged();
}

