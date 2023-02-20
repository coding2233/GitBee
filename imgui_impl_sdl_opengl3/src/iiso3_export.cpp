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

const TextEditor::LanguageDefinition& igGetLanguageDefinition(TextEditor* text_editor,std::string lang_def_name, const std::string keywords[],int keywords_length, std::string identifiers[],int identifiers_length, TextEditor::LanguageDefinition::TokenizeCallback tokenize_callback, std::string comment_start, std::string comment_end, std::string sigle_line_comment, bool case_sensitive, bool auto_indentation)
{
	if (lang_def_name.empty())
	{
		return TextEditor::LanguageDefinition::CPlusPlus();
	}

	std::cout << lang_def_name << std::endl;

	if (lang_def_name == "C++")
	{
		return TextEditor::LanguageDefinition::CPlusPlus();
	}
	else if (lang_def_name == "C")
	{
		return TextEditor::LanguageDefinition::C();
	}
	else if (lang_def_name == "Lua")
	{
		return TextEditor::LanguageDefinition::Lua();
	}
	else
	{
		TextEditor::LanguageDefinition langDef;

		for (size_t i = 0; i < keywords_length; i++)
		{
			langDef.mKeywords.insert(keywords[i]);
		}

		for (size_t i = 0; i < identifiers_length; i++)
		{
			TextEditor::Identifier id;
			id.mDeclaration = "Built-in function";
			langDef.mIdentifiers.insert(std::make_pair(identifiers[i], id));
		}


		langDef.mTokenize = tokenize_callback;

		langDef.mCommentStart = comment_start;
		langDef.mCommentEnd = comment_end;
		langDef.mSingleLineComment = sigle_line_comment;
		langDef.mCaseSensitive = case_sensitive;
		langDef.mAutoIndentation = auto_indentation;

		langDef.mName = lang_def_name;

		return langDef;
	}
	
	return TextEditor::LanguageDefinition::CPlusPlus();
	// const LanguageDefinition& aLanguageDef
}


void igSetLanguageDefinition(TextEditor* text_editor, const TextEditor::LanguageDefinition& aLanguageDef)
{
	text_editor->SetLanguageDefinition(aLanguageDef);
}
