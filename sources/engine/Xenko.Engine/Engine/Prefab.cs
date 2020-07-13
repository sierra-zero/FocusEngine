// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;
using Xenko.Core.Serialization;
using Xenko.Core.Serialization.Contents;
using Xenko.Engine.Design;
using Xenko.Rendering;

namespace Xenko.Engine
{
    /// <summary>
    /// A prefab that contains entities.
    /// </summary>
    [DataContract("Prefab")]
    [ContentSerializer(typeof(DataContentSerializerWithReuse<Prefab>))]
    [ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<Prefab>), Profile = "Content")]
    public sealed class Prefab
    {
        /// <summary>
        /// The entities.
        /// </summary>
        public List<Entity> Entities { get; } = new List<Entity>();

        /// <summary>
        /// Instantiates entities from a prefab that can be later added to a <see cref="Scene"/>.
        /// </summary>
        /// <returns>A collection of entities extracted from the prefab</returns>
        public List<Entity> Instantiate()
        {
            if (packed == null)
            {
                var newPrefab = EntityCloner.Clone(this);
                return newPrefab.Entities;
            }
            else
            {
                return new List<Entity>() { EntityCloner.Clone(packed) };
            }
        }

        private Entity packed;

        public Prefab() { }

        /// <summary>
        /// Make Prefab at runtime
        /// </summary>
        /// <param name="e"></param>
        public Prefab(Entity e)
        {
            Entities.Add(e);
            packed = e;
        }

        /// <summary>
        /// Shortcut for making an easy prefab from a model
        /// </summary>
        public Prefab(Model m, string name = null, Vector3? scale = null, Vector3? position = null)
        {
            packed = new Entity(name);
            packed.GetOrCreate<ModelComponent>().Model = m;
            packed.Transform.Scale = scale ?? Vector3.One;
            packed.Transform.Position = position ?? Vector3.Zero;
            Entities.Add(packed);
        }

        /// <summary>
        /// Shortcut for making an easy prefab from a mesh
        /// </summary>
        public Prefab(Mesh m, Material mat = null, string name = null, Vector3? scale = null, Vector3? position = null)
        {
            packed = new Entity(name);
            Model model = new Model();
            model.Add(m);
            model.Add(mat);
            packed.GetOrCreate<ModelComponent>().Model = model;
            packed.Transform.Scale = scale ?? Vector3.One;
            packed.Transform.Position = position ?? Vector3.Zero;
            Entities.Add(packed);
        }

        /// <summary>
        /// Make Prefab at runtime
        /// </summary>
        /// <param name="e"></param>
        public Prefab(List<Entity> e)
        {
            Entities.AddRange(e);
        }

        /// <summary>
        /// Batches this prefab into a single model
        /// </summary>
        public void Optimize()
        {
            ModelBatcher.BatchChildren(PackToEntity());
        }

        /// <summary>
        /// Converts a Prefab into a single Entity that has all entities as children. Makes it easier to use with an EntityPool
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public Entity PackToEntity() {
            if (packed == null) {
                List<Entity> roots = new List<Entity>();
                for (int i = 0; i < Entities.Count; i++) {
                    if (Entities[i].Transform.Parent == null)
                        roots.Add(Entities[i]);
                }
                if (roots.Count == 1) {
                    packed = roots[0];
                } else {
                    packed = new Entity();
                    for (int i = 0; i < roots.Count; i++) {
                        roots[i].Transform.Parent = packed.Transform;
                    }
                }
            }
            return packed;
        }

        /// <summary>
        /// Shortcut to spawning prefab from a pool
        /// </summary>
        public Entity InstantiateFromPool(Vector3? position = null, Quaternion? rotation = null)
        {
            return EntityPool.Spawn(PackToEntity(), position, rotation);
        }
    }
}
