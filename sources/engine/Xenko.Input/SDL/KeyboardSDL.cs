// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if XENKO_UI_SDL
using System;
using System.Collections.Generic;
using System.Text;
using SDL2;
using Xenko.Graphics.SDL;

namespace Xenko.Input
{
    internal class KeyboardSDL : KeyboardDeviceBase, ITextInputDevice, IDisposable
    {
        private readonly Window window;
        private readonly List<TextInputEvent> textEvents = new List<TextInputEvent>();

        public KeyboardSDL(InputSourceSDL source, Window window)
        {
            Source = source;
            this.window = window;
            this.window.KeyDownActions += OnKeyEvent;
            this.window.KeyUpActions += OnKeyEvent;
            this.window.TextInputActions += OnTextInputActions;
            this.window.TextEditingActions += OnTextEditingActions;
        }
        
        public void Dispose()
        {
            window.KeyDownActions -= OnKeyEvent;
            window.KeyUpActions -= OnKeyEvent;
        }

        public override string Name => "SDL Keyboard";

        public override Guid Id => new Guid("a25469ad-804e-4713-82da-347c6b187323");

        public override IInputSource Source { get; }

        public override void Update(List<InputEvent> inputEvents)
        {
            base.Update(inputEvents);

            inputEvents.AddRange(textEvents);
            textEvents.Clear();
        }

        public void EnabledTextInput()
        {
            SDL.SDL_StartTextInput();
        }

        public void DisableTextInput()
        {
            SDL.SDL_StopTextInput();
        }

        private void OnKeyEvent(SDL.SDL_KeyboardEvent e)
        {
            // Try to map to a xenko key
            Keys key = ConvertSDLKey(e.keysym.sym);
            if (key != Keys.None)
            {
                if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
                    HandleKeyDown(key);
                else
                    HandleKeyUp(key);
            }
        }

        private unsafe void OnTextEditingActions(SDL.SDL_TextEditingEvent e)
        {
            var textInputEvent = InputEventPool<TextInputEvent>.GetOrCreate(this);
            textInputEvent.Text = SDLBufferToString(e.text);
            textInputEvent.Type = TextInputEventType.Composition;
            textInputEvent.CompositionStart = e.start;
            textInputEvent.CompositionLength = e.length;
            textEvents.Add(textInputEvent);
        }

        private unsafe void OnTextInputActions(SDL.SDL_TextInputEvent e)
        {
            var textInputEvent = InputEventPool<TextInputEvent>.GetOrCreate(this);
            textInputEvent.Text = SDLBufferToString(e.text);
            textInputEvent.Type = TextInputEventType.Input;
            textEvents.Add(textInputEvent);
        }
        
        private unsafe string SDLBufferToString(byte* text, int size = 32)
        {
            byte[] sourceBytes = new byte[size];
            int length = 0;

            for (int i = 0; i < size; i++)
            {
                if (text[i] == 0)
                    break;

                sourceBytes[i] = text[i];
                length++;
            }

            return Encoding.UTF8.GetString(sourceBytes, 0, length);
        }

        /// <summary>
        /// Converts an SDL key to a Xenko key
        /// </summary>
        private static Keys ConvertSDLKey(SDL.SDL_Keycode k) {
            switch (k) {
                default:
                case SDL.SDL_Keycode.SDLK_UNKNOWN:
                    return Keys.None;
                case SDL.SDL_Keycode.SDLK_BACKQUOTE:
                    return Keys.BackQuote;
                case SDL.SDL_Keycode.SDLK_QUOTE:
                    return Keys.SingleQuote;
                case SDL.SDL_Keycode.SDLK_QUOTEDBL:
                    return Keys.Quotes;
                case SDL.SDL_Keycode.SDLK_LEFTBRACKET:
                    return Keys.OpenBrackets;
                case SDL.SDL_Keycode.SDLK_RIGHTBRACKET:
                    return Keys.CloseBrackets;
                case SDL.SDL_Keycode.SDLK_KP_PERIOD:
                    return Keys.Period;
                case SDL.SDL_Keycode.SDLK_PERIOD:
                    return Keys.Period;
                case SDL.SDL_Keycode.SDLK_SLASH:
                    return Keys.ForwardSlash;
                case SDL.SDL_Keycode.SDLK_BACKSLASH:
                    return Keys.Backslash;
                case SDL.SDL_Keycode.SDLK_EQUALS:
                    return Keys.Equals;
                case SDL.SDL_Keycode.SDLK_SEMICOLON:
                    return Keys.Semicolon;
                case SDL.SDL_Keycode.SDLK_COMMA:
                    return Keys.Comma;
                case SDL.SDL_Keycode.SDLK_KP_COMMA:
                    return Keys.Comma;
                case SDL.SDL_Keycode.SDLK_KP_EQUALS:
                    return Keys.Equals;
                case SDL.SDL_Keycode.SDLK_CANCEL:
                    return Keys.Cancel;
                case SDL.SDL_Keycode.SDLK_BACKSPACE:
                    return Keys.Back;
                case SDL.SDL_Keycode.SDLK_TAB:
                    return Keys.Tab;
                case SDL.SDL_Keycode.SDLK_KP_TAB:
                    return Keys.Tab;
                case SDL.SDL_Keycode.SDLK_CLEAR:
                    return Keys.Clear;
                case SDL.SDL_Keycode.SDLK_CLEARAGAIN:
                    return Keys.Clear;
                case SDL.SDL_Keycode.SDLK_KP_CLEAR:
                    return Keys.Clear;
                case SDL.SDL_Keycode.SDLK_KP_CLEARENTRY:
                    return Keys.Clear;
                case SDL.SDL_Keycode.SDLK_KP_ENTER:
                    return Keys.Enter;
                case SDL.SDL_Keycode.SDLK_RETURN:
                    return Keys.Return;
                case SDL.SDL_Keycode.SDLK_RETURN2:
                    return Keys.Return;
                case SDL.SDL_Keycode.SDLK_PAUSE:
                    return Keys.Pause;
                case SDL.SDL_Keycode.SDLK_CAPSLOCK:
                    return Keys.Capital;
                case SDL.SDL_Keycode.SDLK_ESCAPE:
                    return Keys.Escape;
                case SDL.SDL_Keycode.SDLK_SPACE:
                    return Keys.Space;
                case SDL.SDL_Keycode.SDLK_KP_SPACE:
                    return Keys.Space;
                case SDL.SDL_Keycode.SDLK_PAGEUP:
                    return Keys.PageUp;
                case SDL.SDL_Keycode.SDLK_PRIOR:
                    return Keys.Prior;
                case SDL.SDL_Keycode.SDLK_PAGEDOWN:
                    return Keys.PageDown;
                case SDL.SDL_Keycode.SDLK_END:
                    return Keys.End;
                case SDL.SDL_Keycode.SDLK_HOME:
                    return Keys.Home;
                case SDL.SDL_Keycode.SDLK_AC_HOME:
                    return Keys.Home;
                case SDL.SDL_Keycode.SDLK_LEFT:
                    return Keys.Left;
                case SDL.SDL_Keycode.SDLK_UP:
                    return Keys.Up;
                case SDL.SDL_Keycode.SDLK_RIGHT:
                    return Keys.Right;
                case SDL.SDL_Keycode.SDLK_DOWN:
                    return Keys.Down;
                case SDL.SDL_Keycode.SDLK_SELECT:
                    return Keys.Select;
                case SDL.SDL_Keycode.SDLK_EXECUTE:
                    return Keys.Execute;
                case SDL.SDL_Keycode.SDLK_PRINTSCREEN:
                    return Keys.PrintScreen;
                case SDL.SDL_Keycode.SDLK_INSERT:
                    return Keys.Insert;
                case SDL.SDL_Keycode.SDLK_DELETE:
                    return Keys.Delete;
                case SDL.SDL_Keycode.SDLK_HELP:
                    return Keys.Help;
                case SDL.SDL_Keycode.SDLK_0:
                    return Keys.D0;
                case SDL.SDL_Keycode.SDLK_1:
                    return Keys.D1;
                case SDL.SDL_Keycode.SDLK_2:
                    return Keys.D2;
                case SDL.SDL_Keycode.SDLK_3:
                    return Keys.D3;
                case SDL.SDL_Keycode.SDLK_4:
                    return Keys.D4;
                case SDL.SDL_Keycode.SDLK_5:
                    return Keys.D5;
                case SDL.SDL_Keycode.SDLK_6:
                    return Keys.D6;
                case SDL.SDL_Keycode.SDLK_7:
                    return Keys.D7;
                case SDL.SDL_Keycode.SDLK_8:
                    return Keys.D8;
                case SDL.SDL_Keycode.SDLK_9:
                    return Keys.D9;
                case SDL.SDL_Keycode.SDLK_a:
                    return Keys.A;
                case SDL.SDL_Keycode.SDLK_b:
                    return Keys.B;
                case SDL.SDL_Keycode.SDLK_c:
                    return Keys.C;
                case SDL.SDL_Keycode.SDLK_d:
                    return Keys.D;
                case SDL.SDL_Keycode.SDLK_e:
                    return Keys.E;
                case SDL.SDL_Keycode.SDLK_f:
                    return Keys.F;
                case SDL.SDL_Keycode.SDLK_g:
                    return Keys.G;
                case SDL.SDL_Keycode.SDLK_h:
                    return Keys.H;
                case SDL.SDL_Keycode.SDLK_i:
                    return Keys.I;
                case SDL.SDL_Keycode.SDLK_j:
                    return Keys.J;
                case SDL.SDL_Keycode.SDLK_k:
                    return Keys.K;
                case SDL.SDL_Keycode.SDLK_l:
                    return Keys.L;
                case SDL.SDL_Keycode.SDLK_m:
                    return Keys.M;
                case SDL.SDL_Keycode.SDLK_n:
                    return Keys.N;
                case SDL.SDL_Keycode.SDLK_o:
                    return Keys.O;
                case SDL.SDL_Keycode.SDLK_p:
                    return Keys.P;
                case SDL.SDL_Keycode.SDLK_q:
                    return Keys.Q;
                case SDL.SDL_Keycode.SDLK_r:
                    return Keys.R;
                case SDL.SDL_Keycode.SDLK_s:
                    return Keys.S;
                case SDL.SDL_Keycode.SDLK_t:
                    return Keys.T;
                case SDL.SDL_Keycode.SDLK_u:
                    return Keys.U;
                case SDL.SDL_Keycode.SDLK_v:
                    return Keys.V;
                case SDL.SDL_Keycode.SDLK_w:
                    return Keys.W;
                case SDL.SDL_Keycode.SDLK_x:
                    return Keys.X;
                case SDL.SDL_Keycode.SDLK_y:
                    return Keys.Y;
                case SDL.SDL_Keycode.SDLK_z:
                    return Keys.Z;
                case SDL.SDL_Keycode.SDLK_LGUI:
                    return Keys.LeftWin;
                case SDL.SDL_Keycode.SDLK_RGUI:
                    return Keys.RightWin;
                case SDL.SDL_Keycode.SDLK_APPLICATION:
                    return Keys.Apps;
                case SDL.SDL_Keycode.SDLK_SLEEP:
                    return Keys.Sleep;
                case SDL.SDL_Keycode.SDLK_KP_0:
                    return Keys.NumPad0;
                case SDL.SDL_Keycode.SDLK_KP_1:
                    return Keys.NumPad1;
                case SDL.SDL_Keycode.SDLK_KP_2:
                    return Keys.NumPad2;
                case SDL.SDL_Keycode.SDLK_KP_3:
                    return Keys.NumPad3;
                case SDL.SDL_Keycode.SDLK_KP_4:
                    return Keys.NumPad4;
                case SDL.SDL_Keycode.SDLK_KP_5:
                    return Keys.NumPad5;
                case SDL.SDL_Keycode.SDLK_KP_6:
                    return Keys.NumPad6;
                case SDL.SDL_Keycode.SDLK_KP_7:
                    return Keys.NumPad7;
                case SDL.SDL_Keycode.SDLK_KP_8:
                    return Keys.NumPad8;
                case SDL.SDL_Keycode.SDLK_KP_9:
                    return Keys.NumPad9;
                case SDL.SDL_Keycode.SDLK_KP_MULTIPLY:
                    return Keys.Multiply;
                case SDL.SDL_Keycode.SDLK_PLUS:
                    return Keys.Equals;
                case SDL.SDL_Keycode.SDLK_KP_PLUS:
                    return Keys.Add;
                case SDL.SDL_Keycode.SDLK_SEPARATOR:
                    return Keys.Separator;
                case SDL.SDL_Keycode.SDLK_MINUS:
                    return Keys.Minus;
                case SDL.SDL_Keycode.SDLK_KP_MINUS:
                    return Keys.Subtract;
                case SDL.SDL_Keycode.SDLK_DECIMALSEPARATOR:
                    return Keys.Decimal;
                case SDL.SDL_Keycode.SDLK_KP_DECIMAL:
                    return Keys.Decimal;
                case SDL.SDL_Keycode.SDLK_KP_DIVIDE:
                    return Keys.Divide;
                case SDL.SDL_Keycode.SDLK_F1:
                    return Keys.F1;
                case SDL.SDL_Keycode.SDLK_F2:
                    return Keys.F2;
                case SDL.SDL_Keycode.SDLK_F3:
                    return Keys.F3;
                case SDL.SDL_Keycode.SDLK_F4:
                    return Keys.F4;
                case SDL.SDL_Keycode.SDLK_F5:
                    return Keys.F5;
                case SDL.SDL_Keycode.SDLK_F6:
                    return Keys.F6;
                case SDL.SDL_Keycode.SDLK_F7:
                    return Keys.F7;
                case SDL.SDL_Keycode.SDLK_F8:
                    return Keys.F8;
                case SDL.SDL_Keycode.SDLK_F9:
                    return Keys.F9;
                case SDL.SDL_Keycode.SDLK_F10:
                    return Keys.F10;
                case SDL.SDL_Keycode.SDLK_F11:
                    return Keys.F11;
                case SDL.SDL_Keycode.SDLK_F12:
                    return Keys.F12;
                case SDL.SDL_Keycode.SDLK_F13:
                    return Keys.F13;
                case SDL.SDL_Keycode.SDLK_F14:
                    return Keys.F14;
                case SDL.SDL_Keycode.SDLK_F15:
                    return Keys.F15;
                case SDL.SDL_Keycode.SDLK_F16:
                    return Keys.F16;
                case SDL.SDL_Keycode.SDLK_F17:
                    return Keys.F17;
                case SDL.SDL_Keycode.SDLK_F18:
                    return Keys.F18;
                case SDL.SDL_Keycode.SDLK_F19:
                    return Keys.F19;
                case SDL.SDL_Keycode.SDLK_F20:
                    return Keys.F20;
                case SDL.SDL_Keycode.SDLK_F21:
                    return Keys.F21;
                case SDL.SDL_Keycode.SDLK_F22:
                    return Keys.F22;
                case SDL.SDL_Keycode.SDLK_F23:
                    return Keys.F23;
                case SDL.SDL_Keycode.SDLK_F24:
                    return Keys.F24;
                case SDL.SDL_Keycode.SDLK_NUMLOCKCLEAR:
                    return Keys.NumLock;
                case SDL.SDL_Keycode.SDLK_SCROLLLOCK:
                    return Keys.Scroll;
                case SDL.SDL_Keycode.SDLK_LSHIFT:
                    return Keys.LeftShift;
                case SDL.SDL_Keycode.SDLK_RSHIFT:
                    return Keys.RightShift;
                case SDL.SDL_Keycode.SDLK_LCTRL:
                    return Keys.LeftCtrl;
                case SDL.SDL_Keycode.SDLK_RCTRL:
                    return Keys.RightCtrl;
                case SDL.SDL_Keycode.SDLK_LALT:
                    return Keys.LeftAlt;
                case SDL.SDL_Keycode.SDLK_RALT:
                    return Keys.RightAlt;
                case SDL.SDL_Keycode.SDLK_AC_BACK:
                    return Keys.BrowserBack;
                case SDL.SDL_Keycode.SDLK_AC_FORWARD:
                    return Keys.BrowserForward;
                case SDL.SDL_Keycode.SDLK_AC_REFRESH:
                    return Keys.BrowserRefresh;
                case SDL.SDL_Keycode.SDLK_AC_STOP:
                    return Keys.BrowserStop;
                case SDL.SDL_Keycode.SDLK_AC_SEARCH:
                    return Keys.BrowserSearch;
                case SDL.SDL_Keycode.SDLK_AC_BOOKMARKS:
                    return Keys.BrowserFavorites;
                case SDL.SDL_Keycode.SDLK_AUDIOMUTE:
                    return Keys.VolumeMute;
                case SDL.SDL_Keycode.SDLK_VOLUMEDOWN:
                    return Keys.VolumeDown;
                case SDL.SDL_Keycode.SDLK_VOLUMEUP:
                    return Keys.VolumeUp;
                case SDL.SDL_Keycode.SDLK_AUDIONEXT:
                    return Keys.MediaNextTrack;
                case SDL.SDL_Keycode.SDLK_AUDIOPREV:
                    return Keys.MediaPreviousTrack;
                case SDL.SDL_Keycode.SDLK_AUDIOSTOP:
                    return Keys.MediaStop;
                case SDL.SDL_Keycode.SDLK_AUDIOPLAY:
                    return Keys.MediaPlayPause;
                case SDL.SDL_Keycode.SDLK_MAIL:
                    return Keys.LaunchMail;
                case SDL.SDL_Keycode.SDLK_MEDIASELECT:
                    return Keys.SelectMedia;
                case SDL.SDL_Keycode.SDLK_CRSEL:
                    return Keys.CrSel;
                case SDL.SDL_Keycode.SDLK_EXSEL:
                    return Keys.ExSel;
            }
        }
    }
}
#endif
