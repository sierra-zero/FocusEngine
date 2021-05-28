// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Rendering.Sprites;
using Xenko.UI;

namespace Xenko.Rendering.UI
{
    public enum UIElementSampler
    {
        [Display("Point (Nearest)")]
        PointClamp,

        [Display("Linear")]
        LinearClamp,

        [Display("Anisotropic")]
        AnisotropicClamp,
    }

    public class RenderUIElement : RenderObject
    {
        public RenderUIElement()
        {
        }

        public Matrix WorldMatrix, WorldMatrix3D;

        // UIComponent values
        public UIComponent Component;

        // stuff to get from the UIComponent
        public UIPage Page => Component.Page;
        public bool IsFullScreen => Component.IsFullScreen;
        public Vector3 Resolution => Component.Resolution;
        public ResolutionStretch ResolutionStretch => Component.ResolutionStretch;
        public bool IsBillboard => Component.IsBillboard;
        public bool SnapText => Component.SnapText;
        public bool IsFixedSize => Component.IsFixedSize;
        public RenderSprite.SpriteDepthMode depthMode => Component.DepthMode;
        public UIElementSampler Sampler => Component.Sampler;

        /// <summary>
        /// Last registered position of teh mouse
        /// </summary>
        public Vector2 LastMousePosition;

        /// <summary>
        /// Last element over which the mouse cursor was registered
        /// </summary>
        public UIElement LastMouseOverElement;

        /// <summary>
        /// Last element which received a touch/click event
        /// </summary>
        public UIElement LastTouchedElement;

        public Vector3 LastIntersectionPoint;

        public Matrix LastRootMatrix;
    }
}
