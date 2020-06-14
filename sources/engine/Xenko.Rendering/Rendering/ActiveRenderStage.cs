// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Xenko.Rendering
{
    public struct ActiveRenderStage
    {
        public bool Active => EffectSelector != null;
        public bool TemporaryDisable, IsShadowStage;

        private EffectSelector _es;
        public EffectSelector EffectSelector
        {
            get
            {
                if (TemporaryDisable)
                    return null;

                return _es;
            }
            set
            {
                _es = value;
            }
        }

        public ActiveRenderStage(string effectName, bool isShadow = false)
        {
            _es = new EffectSelector(effectName);
            TemporaryDisable = false;
            IsShadowStage = isShadow;
        }
    }
}
