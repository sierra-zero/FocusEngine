// Copyright (c) Xenko contributors (https://xenko.com)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using Xenko.Core.Assets.Editor.Settings;
using Xenko.Core.Settings;
using Xenko.Core.Translation;

namespace Xenko.Core.Presentation.Themes
{
    public static class ThemesSettings
    {
        // Categories
        public static readonly string Themes = Tr._p("Settings", "Themes");

        static ThemesSettings()
        {
            ThemeName = new SettingsKey<ThemeType>("Themes/ThemeName", EditorSettings.SettingsContainer, ThemeType.ExpressionDark)
            {
                DisplayName = $"{Themes}/{Tr._p("Settings", "Theme Name")}"
            };
        }

        public static SettingsKey<ThemeType> ThemeName { get; }

        public static void Initialize()
        {
            ThemeController.CurrentTheme = ThemeName.GetValue();
        }
    }
}
