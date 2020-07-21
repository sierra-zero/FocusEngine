using System;
using Xenko.Core.Assets;
using Xenko.Core.Assets.Compiler;
using Xenko.Engine;

namespace Xenko.Assets.Entities.ComponentChecks
{
    /// <summary>
    /// Checks the validity of a <see cref="ModelNodeLinkComponent"/>.
    /// </summary>
    public class ModelNodeLinkComponentCheck : IEntityComponentCheck
    {
        /// <inheritdoc/>
        public bool AppliesTo(Type componentType)
        {
            return componentType == typeof(ModelNodeLinkComponent);
        }

        /// <inheritdoc/>
        public void Check(EntityComponent component, Entity entity, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
        {
            var nodeLinkComponent = component as ModelNodeLinkComponent;
            nodeLinkComponent.ValidityCheck();
            if (!nodeLinkComponent.IsValid)
            {
                result.Warning($"The Model Node Link between {entity.Name} and {nodeLinkComponent.Target?.Entity.Name} is invalid.");
                nodeLinkComponent.Target = null;
            }
        }
    }
}
