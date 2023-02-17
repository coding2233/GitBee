using ImGuiNET;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public unsafe class TextEditor : IDisposable
    {
        private IntPtr _igTextEditor;

        private string nativeText
        {
            get
            {
                byte* igTextPtr = igGetTextEditor(_igTextEditor);
                string igText = Util.StringFromPtr(igTextPtr);
                return igText;
            }
        }

        private string _text;
        public string text
        {
            get
            {
                return _text;
            }
            set
            {

                byte* native_label;
                int label_byteCount = 0;
                if (!string.IsNullOrEmpty(value))
                {
                    label_byteCount = Encoding.UTF8.GetByteCount(value);
                    if (label_byteCount > Util.StackAllocationSizeLimit)
                    {
                        native_label = Util.Allocate(label_byteCount + 1);
                    }
                    else
                    {
                        byte* native_label_stackBytes = stackalloc byte[label_byteCount + 1];
                        native_label = native_label_stackBytes;
                    }
                    int native_label_offset = Util.GetUtf8(value, native_label, label_byteCount);
                    native_label[native_label_offset] = 0;
                    igSetTextEditor(_igTextEditor, native_label);
                }
                else { native_label = null; }

                if (label_byteCount > Util.StackAllocationSizeLimit)
                {
                    Util.Free(native_label);
                }
                _text = nativeText;
            }
        }

        public Vector2 CursorPosition
        {
            get
            {
                Coordinates* igPos = igGetCursorPositionTextEditor(_igTextEditor);
                Vector2 pos = new Vector2(igPos->mLine, igPos->mColumn);
                return pos;
            }
        }
        public int TotalLines => igGetTotalLinesTextEditor(_igTextEditor);
        public bool IsOverwrite => igIsOverwriteTextEditor(_igTextEditor);
        public bool CanUndo => igCanUndoTextEditor(_igTextEditor);

        private bool _isTextChanged = false;
        private bool _lastTextChanged = false;
        public bool IsTextChanged
        {
            get
            {
                var textChanged = igIsTextChangedTextEditor(_igTextEditor);
                if (_lastTextChanged != textChanged)
                {
                    _isTextChanged = false;
                    if (!string.IsNullOrEmpty(_text))
                    {
                        _isTextChanged = !_text.Equals(nativeText);
                    }
                    _lastTextChanged = textChanged;
                }
                return _isTextChanged;
            }
            set
            {
                _isTextChanged = value;
            }
        }


        private bool _ignoreChildWindow;
        public bool ignoreChildWindow
        {
            get
            {
                return _ignoreChildWindow;
            }
            set
            {
                _ignoreChildWindow = value;
                igIgnoreChildTextEditor(_igTextEditor, _ignoreChildWindow);
            }
        }

        private bool _readOnly;
        public bool readOnly
        {
            get
            {
                return _readOnly;
            }
            set
            {
                _readOnly = value;
                igSetReadOnlyTextEditor(_igTextEditor, _readOnly);
            }
        }

        private static HashSet<TextEditor> _allTextEditor = new HashSet<TextEditor>();


        public TextEditor()
        {
            _igTextEditor = igNewTextEditor();
            //igSetPaletteTextEditor(_igTextEditor, 1);
            //readOnly = true;
            igSetShowWhitespacesTextEditor(_igTextEditor, false);
            _allTextEditor.Add(this);
        }

        public void Render(string title, Vector2 size, bool border = false)
        {
            if (_igTextEditor != IntPtr.Zero)
            {
                //    ImGui::Text("%6d/%-6d %6d lines  | %s | %s | %s | %s", cpos.mLine + 1, cpos.mColumn + 1, editor.GetTotalLines(),
                //    editor.IsOverwrite() ? "Ovr" : "Ins",
                //    editor.CanUndo() ? "*" : " ",
                //editor.GetLanguageDefinition().mName.c_str(), fileToEdit);

                //string overWrite = IsOverwrite ? "Ovr" : "Ins";
                //string canUndo = CanUndo ? "*" : " ";
                //ImGui.Text($"{CursorPosition.X}/{-CursorPosition.Y} {TotalLines} lines | {overWrite} | {canUndo}");

                igRenderTextEditor(_igTextEditor, title, size, border);
            }
        }


        public void Dispose()
        {
            _allTextEditor.Remove(this);
            if (_igTextEditor != IntPtr.Zero)
            {
                igDeleteTextEditor(_igTextEditor);
            }
        }


        private byte* ToImguiCharPointer(string value)
        {
            byte* native_label;
            int label_byteCount = 0;
            if (!string.IsNullOrEmpty(value))
            {
                label_byteCount = Encoding.UTF8.GetByteCount(value);
                if (label_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_label = Util.Allocate(label_byteCount + 1);
                }
                else
                {
                    byte* native_label_stackBytes = stackalloc byte[label_byteCount + 1];
                    native_label = native_label_stackBytes;
                }
                int native_label_offset = Util.GetUtf8(value, native_label, label_byteCount);
                native_label[native_label_offset] = 0;
            }
            else { native_label = null; }
            return native_label;
        }

        public struct Coordinates
        {
            public int mLine, mColumn;
        }

        public struct ImVec4
        {
            public float x, y, z, w;
        }

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr igNewTextEditor();

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void igDeleteTextEditor(IntPtr textEditor);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void igRenderTextEditor(IntPtr textEditor, string title, Vector2 size, bool border = false);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void igSetTextEditor(IntPtr textEditor, byte* text);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern byte* igGetTextEditor(IntPtr textEditor);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void igSetPaletteTextEditor(IntPtr textEditor, int style);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void igSetReadOnlyTextEditor(IntPtr textEditor, bool readOnly);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void igSetShowWhitespacesTextEditor(IntPtr textEditor, bool show);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void igIgnoreChildTextEditor(IntPtr textEditor, bool ignoreChild);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Coordinates* igGetCursorPositionTextEditor(IntPtr textEditor);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int igGetTotalLinesTextEditor(IntPtr textEditor);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool igIsOverwriteTextEditor(IntPtr textEditor);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool igCanUndoTextEditor(IntPtr textEditor);

        [DllImport("iiso3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool igIsTextChangedTextEditor(IntPtr textEditor);


    }
}
