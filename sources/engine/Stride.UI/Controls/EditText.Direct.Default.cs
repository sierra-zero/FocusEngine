// Copyright (c) Stride contributors (https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if STRIDE_PLATFORM_WINDOWS_DESKTOP || STRIDE_PLATFORM_UNIX

namespace Stride.UI.Controls
{
    public partial class EditText
    {
        private static void InitializeStaticImpl()
        {
        }

        private void InitializeImpl()
        {
        }

        private int GetLineCountImpl()
        {
            if (Font == null)
                return 1;

            int cnt = text.Length > 0 ? 1 : 0;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') cnt++;

            return cnt;
        }

        private void OnMaxLinesChangedImpl()
        {
        }

        private void OnMinLinesChangedImpl()
        {
        }

        private void UpdateTextToEditImpl()
        {
        }

        private void UpdateInputTypeImpl()
        {
        }

        private void UpdateSelectionFromEditImpl()
        {
        }

        private void UpdateSelectionToEditImpl()
        {
        }

        private void OnTouchUpImpl(TouchEventArgs args)
        {
        }
    }
}

#endif
