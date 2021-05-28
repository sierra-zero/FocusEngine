// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;

using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;
using Xenko.Rendering.UI;

namespace Xenko.UI.Renderers
{
    /// <summary>
    /// The UI drawing context.
    /// It provides information about how to render <see cref="UIElement"/>s for drawing.
    /// </summary>
    public class UIRenderingContext
    {
        /// <summary>
        /// The active graphics context.
        /// </summary>
        public GraphicsContext GraphicsContext { get; set; }

        /// <summary>
        /// The current time.
        /// </summary>
        public GameTime Time { get; internal set; }

        /// <summary>
        /// Set to use other values easily
        /// </summary>
        public RenderUIElement RenderObject;

        /// <summary>
        /// The current reference value for the stencil test.
        /// </summary>
        public int StencilTestReferenceValue { get; set; }

        /// <summary>
        /// The value of the depth bias to use for draw call.
        /// </summary>
        public int DepthBias { get; set; }

        public bool ShouldSnapText => RenderObject.SnapText;
        public Vector3 Resolution => RenderObject.Resolution;
        public Matrix WorldMatrix3D => RenderObject.WorldMatrix3D;
        public bool IsFullscreen => RenderObject.IsFullScreen;

        /// <summary>
        /// Gets the view projection matrix of the UI.
        /// </summary>
        public Matrix ViewProjectionMatrix;
    }
}
