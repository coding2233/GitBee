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

// const TextEditor::LanguageDefinition& igGetLanguageDefinition(std::string lang_def_name, std::string keywords[],int keywords_length, std::string identifiers[],int identifiers_length, TextTokenizeCallback text_tokenize_callback, std::string comment_start, std::string comment_end, std::string sigle_line_comment, bool case_sensitive, bool auto_indentation)
// {
// 	if (lang_def_name.empty())
// 	{
// 		return TextEditor::LanguageDefinition::CPlusPlus();
// 	}

// 	std::cout <<"[igGetLanguageDefinition]  ==> lang_def_name: "<< lang_def_name << std::endl;

// 	if (lang_def_name == "C++")
// 	{
// 		return TextEditor::LanguageDefinition::CPlusPlus();
// 	}
// 	else if (lang_def_name == "C")
// 	{
// 		return TextEditor::LanguageDefinition::C();
// 	}
// 	else if (lang_def_name == "Lua")
// 	{
// 		return TextEditor::LanguageDefinition::Lua();
// 	}
// 	else
// 	{
// 		TextEditor::LanguageDefinition langDef;

// 		for (size_t i = 0; i < keywords_length; i++)
// 		{
// 			langDef.mKeywords.insert(keywords[i]);
// 			std::cout <<"[igGetLanguageDefinition]  ==> keywords: "<< keywords[i] <<"  i:"<< i << std::endl;
// 		}

// 		for (size_t i = 0; i < identifiers_length; i++)
// 		{
// 			TextEditor::Identifier id;
// 			id.mDeclaration = "Built-in function";
// 			langDef.mIdentifiers.insert(std::make_pair(identifiers[i], id));
// 			std::cout <<"[igGetLanguageDefinition]  ==> keywords: "<< identifiers[i] <<"  i:"<< i << std::endl;
// 		}

// 		std::cout <<"[igGetLanguageDefinition]  ==> text_tokenize_callback: "<< text_tokenize_callback << std::endl;

// 		static TextTokenizeCallback& text_tokenize_callback_ = text_tokenize_callback;
// 		static TextTokenize text_tokenize_;

// 		langDef.mTokenize = [](const char * in_begin, const char * in_end, const char *& out_begin, const char *& out_end, TextEditor::PaletteIndex & paletteIndex) -> bool
// 		{
// 			paletteIndex = TextEditor::PaletteIndex::Max;

// 			while (in_begin < in_end && isascii(*in_begin) && isblank(*in_begin))
// 				in_begin++;

// 			if (in_begin == in_end)
// 			{
// 				out_begin = in_end;
// 				out_end = in_end;
// 				paletteIndex = TextEditor::PaletteIndex::Default;
// 			}
// 			else
// 			{
// 				bool result = false;

// 				if (text_tokenize_callback_)
// 				{
// 					text_tokenize_.result=false;
// 					text_tokenize_.begin = in_begin;
// 					text_tokenize_.end = in_end;
// 					text_tokenize_.paletteIndex = (int)paletteIndex;
// 					TextTokenize* text_tokenize_result = text_tokenize_callback_(&text_tokenize_);
// 					result = text_tokenize_result->result;
// 					//调用成功
// 					if (result)
// 					{
// 						out_begin = text_tokenize_result->begin;
// 						out_end = text_tokenize_result->end;
// 						paletteIndex = (TextEditor::PaletteIndex)text_tokenize_result->paletteIndex;
// 					}
					
// 				}

// 				// if(!result)
// 				// {
// 				// 	out_begin = in_end;
// 				// 	out_end = in_end;
// 				// 	paletteIndex = TextEditor::PaletteIndex::Default;
// 				// }
				
// 			}
// 			// else if (TokenizeCStyleString(in_begin, in_end, out_begin, out_end))
// 			// 	paletteIndex = PaletteIndex::String;
// 			// else if (TokenizeCStyleCharacterLiteral(in_begin, in_end, out_begin, out_end))
// 			// 	paletteIndex = PaletteIndex::CharLiteral;
// 			// else if (TokenizeCStyleIdentifier(in_begin, in_end, out_begin, out_end))
// 			// 	paletteIndex = PaletteIndex::Identifier;
// 			// else if (TokenizeCStyleNumber(in_begin, in_end, out_begin, out_end))
// 			// 	paletteIndex = PaletteIndex::Number;
// 			// else if (TokenizeCStylePunctuation(in_begin, in_end, out_begin, out_end))
// 			// 	paletteIndex = PaletteIndex::Punctuation;

// 			return paletteIndex != TextEditor::PaletteIndex::Max;
// 		};

// 		langDef.mCommentStart = comment_start;
// 		langDef.mCommentEnd = comment_end;
// 		langDef.mSingleLineComment = sigle_line_comment;
// 		langDef.mCaseSensitive = case_sensitive;
// 		langDef.mAutoIndentation = auto_indentation;

// 		langDef.mName = lang_def_name;

// 		return langDef;
// 	}
	
// 	return TextEditor::LanguageDefinition::CPlusPlus();
// 	// const LanguageDefinition& aLanguageDef
// }


void igSetLanguageDefinition(TextEditor* text_editor, const char* lang_def_name)
{
	if (strcmp(lang_def_name,"C++") == 0)
	{
		text_editor->SetLanguageDefinition(TextEditor::LanguageDefinition::CPlusPlus());
	}
	else if (strcmp(lang_def_name,"C")==0)
	{
		text_editor->SetLanguageDefinition(TextEditor::LanguageDefinition::C());
	}
	else if (strcmp(lang_def_name,"Lua")==0)
	{
		text_editor->SetLanguageDefinition(TextEditor::LanguageDefinition::Lua());
	}
	else
	{
		text_editor->SetLanguageDefinition(GetLanguageDefinitionEx(lang_def_name));	
	}

	
	std::cout<<"igSetLanguageDefinition: "<<lang_def_name <<" text_editor->GetLanguageDefinition: "<< text_editor->GetLanguageDefinition().mName <<std::endl;
}
