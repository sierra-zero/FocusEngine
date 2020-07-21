// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Xenko.Assets.Entities.ComponentChecks;
using Xenko.Core.Annotations;
using Xenko.Core.Assets;
using Xenko.Core.Assets.Compiler;
using Xenko.Core.Serialization;
using Xenko.Engine;

namespace Xenko.Assets.Entities
{
    public abstract class EntityHierarchyCompilerBase<T> : AssetCompilerBase where T : EntityHierarchyAssetBase
    {
        public override IEnumerable<Type> GetRuntimeTypes(AssetItem assetItem)
        {
            yield return typeof(Entity);
        }

        protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
        {
            var asset = (T)assetItem.Asset;
            foreach (var entityData in asset.Hierarchy.Parts.Values)
            {
                foreach (var component in entityData.Entity.Components)
                {
                    foreach(var check in componentChecks)
                    {
                        if (check.AppliesTo(component.GetType()))
                            check.Check(component, entityData.Entity, assetItem, targetUrlInStorage, result);
                    }
                }
            }

            result.BuildSteps = new AssetBuildStep(assetItem);
            result.BuildSteps.Add(Create(targetUrlInStorage, asset, assetItem.Package));
        }

        private static List<IEntityComponentCheck> componentChecks = new List<IEntityComponentCheck>
        {
            // TODO: How to make this code pluggable?
            new ModelComponentCheck(),
            new ModelNodeLinkComponentCheck(),
            new RequiredMembersCheck(),
        };

        protected abstract AssetCommand<T> Create(string url, T assetParameters, Package package);
    }
}
