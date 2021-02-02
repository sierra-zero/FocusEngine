// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Rendering;

namespace Xenko.Rendering.UI
{
    /// <summary>
    /// The processor in charge of updating and drawing the entities having UI components.
    /// </summary>
    public class UIRenderProcessor : EntityProcessor<UIComponent, RenderUIElement>, IEntityComponentRenderProcessor
    {
        public VisibilityGroup VisibilityGroup { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UIRenderProcessor"/> class.
        /// </summary>
        public UIRenderProcessor()
            : base(typeof(TransformComponent))
        {
        }
        
        public override void Draw(RenderContext gameTime)
        {
            for (int i=0; i<ComponentDataKeys.Count; i++)
            {
                var uiComponent = ComponentDataKeys[i];
                var renderUIElement = ComponentDataValues[i];
                renderUIElement.Enabled = uiComponent.Enabled;

                if (renderUIElement.Enabled)
                {
                    if (uiComponent.IsFullScreen == false) {
                        renderUIElement.BoundingBox.Center = uiComponent.Entity.Transform.WorldPosition();
                        renderUIElement.WorldMatrix3D.GetScale(out renderUIElement.BoundingBox.Extent);
                        renderUIElement.BoundingBox.Extent.X *= 0.5f * uiComponent.Resolution.X;
                        renderUIElement.BoundingBox.Extent.Y *= 0.5f * uiComponent.Resolution.Y;
                        renderUIElement.BoundingBox.Extent.Z *= 0.5f * uiComponent.Resolution.Z;
                    }
                    else {
                        renderUIElement.BoundingBox.Extent = Vector3.Zero; // always draw this
                    }

                    // Copy values from ECS to render object
                    renderUIElement.Component = uiComponent;
                    renderUIElement.WorldMatrix = uiComponent.Entity.Transform.WorldMatrix;
                    renderUIElement.RenderGroup = uiComponent.RenderGroup;
                    renderUIElement.DistanceSortFudge = uiComponent.IsFullScreen ? -10000f : uiComponent.DistanceSortFudge;
                    renderUIElement.SmallFactorMultiplier = renderUIElement.IsFixedSize || uiComponent.IsFullScreen ? 0f : uiComponent.SmallFactorMultiplier;
                }
            }
        }

        protected override void OnEntityComponentAdding(Entity entity, UIComponent uiComponent, RenderUIElement renderUIElement)
        {
            if (uiComponent.IsFullScreen == false &&
                uiComponent.Page?.RootElement != null &&
                uiComponent.Page.RootElement.previousProvidedMeasureSize.X == -1f)
                uiComponent.Page.RootElement.previousProvidedMeasureSize = uiComponent.Resolution;

            VisibilityGroup.RenderObjects.Add(renderUIElement);
        }

        protected override void OnEntityComponentRemoved(Entity entity, UIComponent uiComponent, RenderUIElement renderUIElement)
        {
            VisibilityGroup.RenderObjects.Remove(renderUIElement);
        }

        protected override RenderUIElement GenerateComponentData(Entity entity, UIComponent component)
        {
            return new RenderUIElement { Source = component };
        }

        protected override bool IsAssociatedDataValid(Entity entity, UIComponent component, RenderUIElement associatedData)
        {
            return associatedData.Source == component;
        }
    }
}
