// Copyright (c) Stride contributors (https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;

namespace Stride.Input {

    public class CharConverter {

        private static Dictionary<Keys, char> keyToChar = new Dictionary<Keys, char>() {
            {Keys.Tilde        , '`'},
            {Keys.D1              , '1'},
            {Keys.D2              , '2'},
            {Keys.D3              , '3'},
            {Keys.D4              , '4'},
            {Keys.D5              , '5'},
            {Keys.D6              , '6'},
            {Keys.D7              , '7'},
            {Keys.D8              , '8'},
            {Keys.D9              , '9'},
            {Keys.D0              , '0'},
            {Keys.NumPad1         , '1'},
            {Keys.NumPad2         , '2'},
            {Keys.NumPad3         , '3'},
            {Keys.NumPad4         , '4'},
            {Keys.NumPad5         , '5'},
            {Keys.NumPad6         , '6'},
            {Keys.NumPad7         , '7'},
            {Keys.NumPad8         , '8'},
            {Keys.NumPad9         , '9'},
            {Keys.NumPad0         , '0'},
            {Keys.Divide          , '/'},
            {Keys.Multiply        , '*'},
            {Keys.Subtract        , '-'},
            {Keys.Add             , '+'},
            {Keys.A               , 'a'},
            {Keys.B               , 'b'},
            {Keys.C               , 'c'},
            {Keys.D               , 'd'},
            {Keys.E               , 'e'},
            {Keys.F               , 'f'},
            {Keys.G               , 'g'},
            {Keys.H               , 'h'},
            {Keys.I               , 'i'},
            {Keys.J               , 'j'},
            {Keys.K               , 'k'},
            {Keys.L               , 'l'},
            {Keys.M               , 'm'},
            {Keys.N               , 'n'},
            {Keys.O               , 'o'},
            {Keys.P               , 'p'},
            {Keys.Q               , 'q'},
            {Keys.R               , 'r'},
            {Keys.S               , 's'},
            {Keys.T               , 't'},
            {Keys.U               , 'u'},
            {Keys.V               , 'v'},
            {Keys.W               , 'w'},
            {Keys.X               , 'x'},
            {Keys.Y               , 'y'},
            {Keys.Z               , 'z'},
            {Keys.Tab             , '\t'},
            {Keys.Minus        , '-'},
            {Keys.Equals         , '='},
            {Keys.Plus        , '+' },
            {Keys.OpenBrackets , '['},
            {Keys.CloseBrackets, ']'},
            {Keys.Pipe         , '\\'},
            {Keys.Backslash    , '\\' },
            {Keys.Semicolon    , ';'},
            {Keys.Quotes       , '\''},
            {Keys.Comma        , ','},
            {Keys.Period       , '.'},
            {Keys.Question     , '/'},
            {Keys.ForwardSlash , '/'},
            {Keys.Space           , ' '},
            {Keys.Decimal         , '.'},
            {Keys.Return          , '\n'},
            {Keys.Colon           , ';' },
            {Keys.Back            , '\b'}
        };

        private static Dictionary<Keys, char> keyToCharShift = new Dictionary<Keys, char>() {
            {Keys.Tilde        , '~'},
            {Keys.D1              , '!'},
            {Keys.D2              , '@'},
            {Keys.D3              , '#'},
            {Keys.D4              , '$'},
            {Keys.D5              , '%'},
            {Keys.D6              , '^'},
            {Keys.D7              , '&'},
            {Keys.D8              , '*'},
            {Keys.D9              , '('},
            {Keys.D0              , ')'},
            {Keys.Divide          , '/'},
            {Keys.Multiply        , '*'},
            {Keys.Subtract        , '-'},
            {Keys.Add             , '+'},
            {Keys.A               , 'A'},
            {Keys.B               , 'B'},
            {Keys.C               , 'C'},
            {Keys.D               , 'D'},
            {Keys.E               , 'E'},
            {Keys.F               , 'F'},
            {Keys.G               , 'G'},
            {Keys.H               , 'H'},
            {Keys.I               , 'I'},
            {Keys.J               , 'J'},
            {Keys.K               , 'K'},
            {Keys.L               , 'L'},
            {Keys.M               , 'M'},
            {Keys.N               , 'N'},
            {Keys.O               , 'O'},
            {Keys.P               , 'P'},
            {Keys.Q               , 'Q'},
            {Keys.R               , 'R'},
            {Keys.S               , 'S'},
            {Keys.T               , 'T'},
            {Keys.U               , 'U'},
            {Keys.V               , 'V'},
            {Keys.W               , 'W'},
            {Keys.X               , 'X'},
            {Keys.Y               , 'Y'},
            {Keys.Z               , 'Z'},
            {Keys.Minus        , '_'},
            {Keys.Plus         , '+'},
            {Keys.Equals         , '+'},
            {Keys.OpenBrackets , '{'},
            {Keys.CloseBrackets, '}'},
            {Keys.Pipe         , '|'},
            {Keys.Backslash    , '|' },
            {Keys.Semicolon    , ':'},
            {Keys.Quotes       , '\"'},
            {Keys.Comma        , '<'},
            {Keys.Period       , '>'},
            {Keys.Question     , '?'},
            {Keys.ForwardSlash , '?' },
            {Keys.Colon        , ':' },
            {Keys.Space           , ' '},
            {Keys.Return          , '\n'},
            {Keys.Back            , '\b'}
        };

        public static char Get(Keys key, bool shift = false) {
            if (!shift) {
                if (keyToChar.TryGetValue(key, out char val)) return val;
            } else if (keyToCharShift.TryGetValue(key, out char val)) return val;
            return '\0';
        }
    }

    /// <summary>
    /// Enumeration for keys.
    /// </summary>
    public enum Keys
    {
        /// <summary>
        /// The 'none' key.
        /// </summary>
        None = 0,

        /// <summary>
        /// The 'cancel' key.
        /// </summary>
        Cancel = 1,

        /// <summary>
        /// The 'back' key.
        /// </summary>
        Back = 2,

        /// <summary>
        /// The 'tab' key.
        /// </summary>
        Tab = 3,

        /// <summary>
        /// The 'linefeed' key.
        /// </summary>
        LineFeed = 4,

        /// <summary>
        /// The 'clear' key.
        /// </summary>
        Clear = 5,

        /// <summary>
        /// The 'enter' key.
        /// </summary>
        Enter = 6,

        /// <summary>
        /// The 'return' key.
        /// </summary>
        Return = 6,

        /// <summary>
        /// The 'pause' key.
        /// </summary>
        Pause = 7,

        /// <summary>
        /// The 'capital' key.
        /// </summary>
        Capital = 8,

        /// <summary>
        /// The 'capslock' key.
        /// </summary>
        CapsLock = 8,

        /// <summary>
        /// The 'hangulmode' key.
        /// </summary>
        HangulMode = 9,

        /// <summary>
        /// The 'kanamode' key.
        /// </summary>
        KanaMode = 9,

        /// <summary>
        /// The 'junjamode' key.
        /// </summary>
        JunjaMode = 10,

        /// <summary>
        /// The 'finalmode' key.
        /// </summary>
        FinalMode = 11,

        /// <summary>
        /// The 'hanjamode' key.
        /// </summary>
        HanjaMode = 12,

        /// <summary>
        /// The 'kanjimode' key.
        /// </summary>
        KanjiMode = 12,

        /// <summary>
        /// The 'escape' key.
        /// </summary>
        Escape = 13,

        /// <summary>
        /// The 'imeconvert' key.
        /// </summary>
        ImeConvert = 14,

        /// <summary>
        /// The 'imenonconvert' key.
        /// </summary>
        ImeNonConvert = 15,

        /// <summary>
        /// The 'imeaccept' key.
        /// </summary>
        ImeAccept = 16,

        /// <summary>
        /// The 'imemodechange' key.
        /// </summary>
        ImeModeChange = 17,

        /// <summary>
        /// The 'space' key.
        /// </summary>
        Space = 18,

        /// <summary>
        /// The 'pageup' key.
        /// </summary>
        PageUp = 19,

        /// <summary>
        /// The 'prior' key.
        /// </summary>
        Prior = 19,

        /// <summary>
        /// The 'next' key.
        /// </summary>
        Next = 20,

        /// <summary>
        /// The 'pagedown' key.
        /// </summary>
        PageDown = 20,

        /// <summary>
        /// The 'end' key.
        /// </summary>
        End = 21,

        /// <summary>
        /// The 'home' key.
        /// </summary>
        Home = 22,

        /// <summary>
        /// The 'left' key.
        /// </summary>
        Left = 23,

        /// <summary>
        /// The 'up' key.
        /// </summary>
        Up = 24,

        /// <summary>
        /// The 'right' key.
        /// </summary>
        Right = 25,

        /// <summary>
        /// The 'down' key.
        /// </summary>
        Down = 26,

        /// <summary>
        /// The 'select' key.
        /// </summary>
        Select = 27,

        /// <summary>
        /// The 'print' key.
        /// </summary>
        Print = 28,

        /// <summary>
        /// The 'execute' key.
        /// </summary>
        Execute = 29,

        /// <summary>
        /// The 'printscreen' key.
        /// </summary>
        PrintScreen = 30,

        /// <summary>
        /// The 'snapshot' key.
        /// </summary>
        Snapshot = 30,

        /// <summary>
        /// The 'insert' key.
        /// </summary>
        Insert = 31,

        /// <summary>
        /// The 'delete' key.
        /// </summary>
        Delete = 32,

        /// <summary>
        /// The 'help' key.
        /// </summary>
        Help = 33,

        /// <summary>
        /// The 'd0' key.
        /// </summary>
        D0 = 34,

        /// <summary>
        /// The 'd1' key.
        /// </summary>
        D1 = 35,

        /// <summary>
        /// The 'd2' key.
        /// </summary>
        D2 = 36,

        /// <summary>
        /// The 'd3' key.
        /// </summary>
        D3 = 37,

        /// <summary>
        /// The 'd4' key.
        /// </summary>
        D4 = 38,

        /// <summary>
        /// The 'd5' key.
        /// </summary>
        D5 = 39,

        /// <summary>
        /// The 'd6' key.
        /// </summary>
        D6 = 40,

        /// <summary>
        /// The 'd7' key.
        /// </summary>
        D7 = 41,

        /// <summary>
        /// The 'd8' key.
        /// </summary>
        D8 = 42,

        /// <summary>
        /// The 'd9' key.
        /// </summary>
        D9 = 43,

        /// <summary>
        /// The 'a' key.
        /// </summary>
        A = 44,

        /// <summary>
        /// The 'b' key.
        /// </summary>
        B = 45,

        /// <summary>
        /// The 'c' key.
        /// </summary>
        C = 46,

        /// <summary>
        /// The 'd' key.
        /// </summary>
        D = 47,

        /// <summary>
        /// The 'e' key.
        /// </summary>
        E = 48,

        /// <summary>
        /// The 'f' key.
        /// </summary>
        F = 49,

        /// <summary>
        /// The 'g' key.
        /// </summary>
        G = 50,

        /// <summary>
        /// The 'h' key.
        /// </summary>
        H = 51,

        /// <summary>
        /// The 'i' key.
        /// </summary>
        I = 52,

        /// <summary>
        /// The 'j' key.
        /// </summary>
        J = 53,

        /// <summary>
        /// The 'k' key.
        /// </summary>
        K = 54,

        /// <summary>
        /// The 'l' key.
        /// </summary>
        L = 55,

        /// <summary>
        /// The 'm' key.
        /// </summary>
        M = 56,

        /// <summary>
        /// The 'n' key.
        /// </summary>
        N = 57,

        /// <summary>
        /// The 'o' key.
        /// </summary>
        O = 58,

        /// <summary>
        /// The 'p' key.
        /// </summary>
        P = 59,

        /// <summary>
        /// The 'q' key.
        /// </summary>
        Q = 60,

        /// <summary>
        /// The 'r' key.
        /// </summary>
        R = 61,

        /// <summary>
        /// The 's' key.
        /// </summary>
        S = 62,

        /// <summary>
        /// The 't' key.
        /// </summary>
        T = 63,

        /// <summary>
        /// The 'u' key.
        /// </summary>
        U = 64,

        /// <summary>
        /// The 'v' key.
        /// </summary>
        V = 65,

        /// <summary>
        /// The 'w' key.
        /// </summary>
        W = 66,

        /// <summary>
        /// The 'x' key.
        /// </summary>
        X = 67,

        /// <summary>
        /// The 'y' key.
        /// </summary>
        Y = 68,

        /// <summary>
        /// The 'z' key.
        /// </summary>
        Z = 69,

        /// <summary>
        /// The 'leftwin' key.
        /// </summary>
        LeftWin = 70,

        /// <summary>
        /// The 'rightwin' key.
        /// </summary>
        RightWin = 71,

        /// <summary>
        /// The 'apps' key.
        /// </summary>
        Apps = 72,

        /// <summary>
        /// The 'sleep' key.
        /// </summary>
        Sleep = 73,

        /// <summary>
        /// The 'numpad0' key.
        /// </summary>
        NumPad0 = 74,

        /// <summary>
        /// The 'numpad1' key.
        /// </summary>
        NumPad1 = 75,

        /// <summary>
        /// The 'numpad2' key.
        /// </summary>
        NumPad2 = 76,

        /// <summary>
        /// The 'numpad3' key.
        /// </summary>
        NumPad3 = 77,

        /// <summary>
        /// The 'numpad4' key.
        /// </summary>
        NumPad4 = 78,

        /// <summary>
        /// The 'numpad5' key.
        /// </summary>
        NumPad5 = 79,

        /// <summary>
        /// The 'numpad6' key.
        /// </summary>
        NumPad6 = 80,

        /// <summary>
        /// The 'numpad7' key.
        /// </summary>
        NumPad7 = 81,

        /// <summary>
        /// The 'numpad8' key.
        /// </summary>
        NumPad8 = 82,

        /// <summary>
        /// The 'numpad9' key.
        /// </summary>
        NumPad9 = 83,

        /// <summary>
        /// The 'multiply' key.
        /// </summary>
        Multiply = 84,

        /// <summary>
        /// The 'add' key.
        /// </summary>
        Add = 85,

        /// <summary>
        /// The 'separator' key.
        /// </summary>
        Separator = 86,

        /// <summary>
        /// The 'subtract' key.
        /// </summary>
        Subtract = 87,

        /// <summary>
        /// The 'decimal' key.
        /// </summary>
        Decimal = 88,

        /// <summary>
        /// The 'divide' key.
        /// </summary>
        Divide = 89,

        /// <summary>
        /// The 'f1' key.
        /// </summary>
        F1 = 90,

        /// <summary>
        /// The 'f2' key.
        /// </summary>
        F2 = 91,

        /// <summary>
        /// The 'f3' key.
        /// </summary>
        F3 = 92,

        /// <summary>
        /// The 'f4' key.
        /// </summary>
        F4 = 93,

        /// <summary>
        /// The 'f5' key.
        /// </summary>
        F5 = 94,

        /// <summary>
        /// The 'f6' key.
        /// </summary>
        F6 = 95,

        /// <summary>
        /// The 'f7' key.
        /// </summary>
        F7 = 96,

        /// <summary>
        /// The 'f8' key.
        /// </summary>
        F8 = 97,

        /// <summary>
        /// The 'f9' key.
        /// </summary>
        F9 = 98,

        /// <summary>
        /// The 'f10' key.
        /// </summary>
        F10 = 99,

        /// <summary>
        /// The 'f11' key.
        /// </summary>
        F11 = 100,

        /// <summary>
        /// The 'f12' key.
        /// </summary>
        F12 = 101,

        /// <summary>
        /// The 'f13' key.
        /// </summary>
        F13 = 102,

        /// <summary>
        /// The 'f14' key.
        /// </summary>
        F14 = 103,

        /// <summary>
        /// The 'f15' key.
        /// </summary>
        F15 = 104,

        /// <summary>
        /// The 'f16' key.
        /// </summary>
        F16 = 105,

        /// <summary>
        /// The 'f17' key.
        /// </summary>
        F17 = 106,

        /// <summary>
        /// The 'f18' key.
        /// </summary>
        F18 = 107,

        /// <summary>
        /// The 'f19' key.
        /// </summary>
        F19 = 108,

        /// <summary>
        /// The 'f20' key.
        /// </summary>
        F20 = 109,

        /// <summary>
        /// The 'f21' key.
        /// </summary>
        F21 = 110,

        /// <summary>
        /// The 'f22' key.
        /// </summary>
        F22 = 111,

        /// <summary>
        /// The 'f23' key.
        /// </summary>
        F23 = 112,

        /// <summary>
        /// The 'f24' key.
        /// </summary>
        F24 = 113,

        /// <summary>
        /// The 'numlock' key.
        /// </summary>
        NumLock = 114,

        /// <summary>
        /// The 'scroll' key.
        /// </summary>
        Scroll = 115,

        /// <summary>
        /// The 'leftshift' key.
        /// </summary>
        LeftShift = 116,

        /// <summary>
        /// The 'rightshift' key.
        /// </summary>
        RightShift = 117,

        /// <summary>
        /// The 'leftctrl' key.
        /// </summary>
        LeftCtrl = 118,

        /// <summary>
        /// The 'rightctrl' key.
        /// </summary>
        RightCtrl = 119,

        /// <summary>
        /// The 'leftalt' key.
        /// </summary>
        LeftAlt = 120,

        /// <summary>
        /// The 'rightalt' key.
        /// </summary>
        RightAlt = 121,

        /// <summary>
        /// The 'browserback' key.
        /// </summary>
        BrowserBack = 122,

        /// <summary>
        /// The 'browserforward' key.
        /// </summary>
        BrowserForward = 123,

        /// <summary>
        /// The 'browserrefresh' key.
        /// </summary>
        BrowserRefresh = 124,

        /// <summary>
        /// The 'browserstop' key.
        /// </summary>
        BrowserStop = 125,

        /// <summary>
        /// The 'browsersearch' key.
        /// </summary>
        BrowserSearch = 126,

        /// <summary>
        /// The 'browserfavorites' key.
        /// </summary>
        BrowserFavorites = 127,

        /// <summary>
        /// The 'browserhome' key.
        /// </summary>
        BrowserHome = 128,

        /// <summary>
        /// The 'volumemute' key.
        /// </summary>
        VolumeMute = 129,

        /// <summary>
        /// The 'volumedown' key.
        /// </summary>
        VolumeDown = 130,

        /// <summary>
        /// The 'volumeup' key.
        /// </summary>
        VolumeUp = 131,

        /// <summary>
        /// The 'medianexttrack' key.
        /// </summary>
        MediaNextTrack = 132,

        /// <summary>
        /// The 'mediaprevioustrack' key.
        /// </summary>
        MediaPreviousTrack = 133,

        /// <summary>
        /// The 'mediastop' key.
        /// </summary>
        MediaStop = 134,

        /// <summary>
        /// The 'mediaplaypause' key.
        /// </summary>
        MediaPlayPause = 135,

        /// <summary>
        /// The 'launchmail' key.
        /// </summary>
        LaunchMail = 136,

        /// <summary>
        /// The 'selectmedia' key.
        /// </summary>
        SelectMedia = 137,

        /// <summary>
        /// The 'launchapplication1' key.
        /// </summary>
        LaunchApplication1 = 138,

        /// <summary>
        /// The 'launchapplication2' key.
        /// </summary>
        LaunchApplication2 = 139,

        /// <summary>
        /// The 'oemsemicolon' key.
        /// </summary>
        Semicolon = 140,

        /// <summary>
        /// The 'oemplus' key.
        /// </summary>
        Plus = 141,

        /// <summary>
        /// The 'oemcomma' key.
        /// </summary>
        Comma = 142,

        /// <summary>
        /// The 'oemminus' key.
        /// </summary>
        Minus = 143,

        /// <summary>
        /// The 'oemperiod' key.
        /// </summary>
        Period = 144,

        /// <summary>
        /// The 'oemquestion' key.
        /// </summary>
        Question = 145,

        /// <summary>
        /// The 'oemtilde' key.
        /// </summary>
        Tilde = 146,

        /// <summary>
        /// The 'oemopenbrackets' key.
        /// </summary>
        OpenBrackets = 149,

        /// <summary>
        /// The 'oempipe' key.
        /// </summary>
        Pipe = 150,

        /// <summary>
        /// The 'oemclosebrackets' key.
        /// </summary>
        CloseBrackets = 151,

        /// <summary>
        /// The 'oemquotes' key.
        /// </summary>
        Quotes = 152,

        /// <summary>
        /// The 'oem8' key.
        /// </summary>
        Oem8 = 153,

        /// <summary>
        /// The 'oembackslash' key.
        /// </summary>
        Backslash = 154,

        /// <summary>
        /// The 'attn' key.
        /// </summary>
        Attn = 163,

        /// <summary>
        /// The 'crsel' key.
        /// </summary>
        CrSel = 164,

        /// <summary>
        /// The 'exsel' key.
        /// </summary>
        ExSel = 165,

        /// <summary>
        /// The 'eraseeof' key.
        /// </summary>
        EraseEof = 166,

        /// <summary>
        /// The 'play' key.
        /// </summary>
        Play = 167,

        /// <summary>
        /// The 'zoom' key.
        /// </summary>
        Zoom = 168,

        /// <summary>
        /// The 'noname' key.
        /// </summary>
        NoName = 169,

        /// <summary>
        /// The 'pa1' key.
        /// </summary>
        Pa1 = 170,

        /// <summary>
        /// The 'oemclear' key.
        /// </summary>
        OemClear = 171,

        /// <summary>
        /// The 'numpad enter' key.
        /// </summary>
        NumPadEnter = 180,

        /// <summary>
        /// The 'numpad decimal' key.
        /// </summary>
        NumPadDecimal = 181,

        /// <summary>
        /// The 'equals' key
        /// </summary>
        Equals = 182,

        /// <summary>
        /// The ':' key
        /// </summary>
        Colon = 183,

        /// <summary>
        /// The '/' key
        /// </summary>
        ForwardSlash = 184,

        /// <summary>
        /// The ' key
        /// </summary>
        SingleQuote = 185,

        /// <summary>
        /// The ` key
        /// </summary>
        BackQuote = 186
    }
}