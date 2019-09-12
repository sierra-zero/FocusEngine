using System.Collections.Generic;
using System.Text;
using Xenko.Engine;
using Xenko.Core.Serialization.Contents;
using Xenko.Rendering;
using Xenko.Graphics;
using Xenko.Core.Serialization;
using Xenko.Graphics.Data;
using System;
using Xenko.Extensions;
using Xenko.Core.Mathematics;
using System.Linq;
using System.Threading.Tasks;
using Xenko.Rendering.Materials;
using Xenko.Core;
using System.Runtime.InteropServices;
using Xenko.Rendering.Rendering;

namespace Xenko.Engine
{
    /// <summary>
    /// System for batching entities and models together, to reduce draw calls and entity processing overhead. Works great with static geometry.
    /// </summary>
    public class ModelBatcher
    {
        private struct EntityChunk
        {
            public Entity Entity;
            public Model Model;
            public Matrix Transform;
            public int MaterialIndex;
        }

        /// <summary>
        /// Unpacks a raw buffer of vertex data into proper arrays
        /// </summary>
        /// <returns>Returns true if some data was successful in extraction</returns>
        public static unsafe bool UnpackRawVertData(byte[] data, VertexDeclaration declaration,
                                                    out Vector3[] positions, out Vector3[] normals,
                                                    out Vector2[] uvs, out Color4[] colors)
        {
            positions = null;
            normals = null;
            uvs = null;
            colors = null;
            if (data == null || declaration == null || data.Length <= 0) return false;
            VertexElement[] elements = declaration.VertexElements;
            int totalEntries = data.Length / declaration.VertexStride;
            positions = new Vector3[totalEntries];
            int[] eoffsets = new int[elements.Length];
            for (int i = 1; i < elements.Length; i++) eoffsets[i] = eoffsets[i - 1] + elements[i - 1].Format.SizeInBytes();
            fixed (byte* dp = data)
            {
                for (int offset = 0; offset < data.Length; offset += declaration.VertexStride)
                {
                    int vertindex = offset / declaration.VertexStride;
                    for (int i = 0; i < elements.Length; i++)
                    {
                        VertexElement e = elements[i];
                        switch (e.SemanticName)
                        {
                            case "POSITION":
                                positions[vertindex] = *(Vector3*)&dp[offset + eoffsets[i]];
                                break;
                            case "NORMAL":
                                if (normals == null) normals = new Vector3[totalEntries];
                                normals[vertindex] = *(Vector3*)&dp[offset + eoffsets[i]];
                                break;
                            case "COLOR":
                                if (colors == null) colors = new Color4[totalEntries];
                                colors[vertindex] = *(Color4*)&dp[offset + eoffsets[i]];
                                break;
                            case "TEXCOORD":
                                if (uvs == null) uvs = new Vector2[totalEntries];
                                uvs[vertindex] = *(Vector2*)&dp[offset + eoffsets[i]];
                                break;
                        }
                    }
                }
            }
            return true;
        }

        private static unsafe void ProcessMaterial(List<EntityChunk> chunks, MaterialInstance material, Model prefabModel, HashSet<Entity> unbatched = null)
        {
            //actually create the mesh
            List<VertexPositionNormalTexture> vertsNT = null;
            List<VertexPositionNormalColor> vertsNC = null;
            List<uint> indicies = new List<uint>();
            BoundingBox bb = new BoundingBox(new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                                             new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity));
            uint indexOffset = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                EntityChunk chunk = chunks[i];
                if (unbatched != null && unbatched.Contains(chunk.Entity)) continue; // don't try batching other things in this entity if some failed
                if (chunk.Entity != null) chunk.Entity.Transform.UpdateWorldMatrix();
                for (int j = 0; j < chunk.Model.Meshes.Count; j++)
                {
                    Mesh modelMesh = chunk.Model.Meshes[j];
                    //process only right material
                    if (modelMesh.MaterialIndex == chunk.MaterialIndex)
                    {
                        //vertexes
                        Xenko.Graphics.Buffer buf = modelMesh.Draw?.VertexBuffers[0].Buffer;
                        Xenko.Graphics.Buffer ibuf = modelMesh.Draw?.IndexBuffer.Buffer;
                        if (buf == null || buf.VertIndexData == null ||
                            ibuf == null || ibuf.VertIndexData == null)
                        {
                            if (unbatched != null) unbatched.Add(chunk.Entity);
                            continue;
                        }

                        if (UnpackRawVertData(buf.VertIndexData, modelMesh.Draw.VertexBuffers[0].Declaration,
                                              out Vector3[] positions, out Vector3[] normals, out Vector2[] uvs, out Color4[] colors) == false)
                        {
                            if (unbatched != null) unbatched.Add(chunk.Entity);
                            continue;
                        }

                        // transform verts/norms by matrix
                        Matrix worldMatrix = chunk.Entity == null ? chunk.Transform : chunk.Entity.Transform.WorldMatrix;
                        for (int k = 0; k < positions.Length; k++)
                        {
                            Vector3.Transform(ref positions[k], ref worldMatrix, out positions[k]);

                            // update bounding box?
                            Vector3 pos = positions[k];
                            if (pos.X > bb.Maximum.X) bb.Maximum.X = pos.X;
                            if (pos.Y > bb.Maximum.Y) bb.Maximum.Y = pos.Y;
                            if (pos.Z > bb.Maximum.Z) bb.Maximum.Z = pos.Z;
                            if (pos.X < bb.Minimum.X) bb.Minimum.X = pos.X;
                            if (pos.Y < bb.Minimum.Y) bb.Minimum.Y = pos.Y;
                            if (pos.Z < bb.Minimum.Z) bb.Minimum.Z = pos.Z;

                            if (normals != null) Vector3.TransformNormal(ref normals[k], ref worldMatrix, out normals[k]);
                        }

                        // what kind of structure should we make?
                        if (uvs != null)
                        {
                            if (vertsNT == null) vertsNT = new List<VertexPositionNormalTexture>();
                            for (int k = 0; k < positions.Length; k++)
                            {
                                vertsNT.Add(new VertexPositionNormalTexture
                                {
                                    Position = positions[k],
                                    Normal = normals != null ? normals[k] : Vector3.Zero,
                                    TextureCoordinate = uvs[k]
                                });
                            }
                        }
                        else
                        {
                            if (vertsNC == null) vertsNC = new List<VertexPositionNormalColor>();
                            for (int k = 0; k < positions.Length; k++)
                            {
                                vertsNC.Add(new VertexPositionNormalColor
                                {
                                    Position = positions[k],
                                    Normal = normals != null ? normals[k] : Vector3.Zero,
                                    Color = colors != null ? colors[k] : Color4.White
                                });
                            }
                        }

                        // indicies
                        fixed (byte* pdst = ibuf.VertIndexData)
                        {
                            if (modelMesh.Draw.IndexBuffer.Is32Bit)
                            {
                                var dst = (uint*)pdst;

                                int numIndices = ibuf.VertIndexData.Length / sizeof(uint);
                                for (var k = 0; k < numIndices; k++)
                                {
                                    // Offset indices
                                    indicies.Add(dst[k] + indexOffset);
                                }
                            }
                            else
                            {
                                var dst = (ushort*)pdst;

                                int numIndices = ibuf.VertIndexData.Length / sizeof(ushort);
                                for (var k = 0; k < numIndices; k++)
                                {
                                    // Offset indices
                                    indicies.Add(dst[k] + indexOffset);
                                }
                            }
                        }

                        indexOffset += (uint)positions.Length;
                    }
                }
            }

            if (indicies.Count <= 0) return;

            // make stagedmesh with verts
            uint[] iarray = indicies.ToArray();
            StagedMeshDraw md;
            if (vertsNT != null)
            {
                md = StagedMeshDraw.MakeStagedMeshDraw<VertexPositionNormalTexture>(iarray, vertsNT.ToArray(), VertexPositionNormalTexture.Layout);
            }
            else if (vertsNC != null)
            {
                md = StagedMeshDraw.MakeStagedMeshDraw<VertexPositionNormalColor>(iarray, vertsNC.ToArray(), VertexPositionNormalColor.Layout);
            }
            else return;

            Mesh m = new Mesh
            {
                Draw = md,
                BoundingBox = bb,
                MaterialIndex = prefabModel.Materials.Count
            };

            prefabModel.Add(m);
            prefabModel.Add(material);
        }

        private static void Gather(Entity e, List<Entity> into)
        {
            into.Add(e);
            foreach (Entity ec in e.GetChildren()) Gather(ec, into);
        }

        /// <summary>
        /// Takes an Entity tree and does its best to batch all the children into one entity. Automatically removes batched entities.
        /// </summary>
        /// <param name="root">The root entity to batch from and merge into</param>
        /// <returns>Returns the number of successfully batched and removed entities</returns>
        public static int BatchChildren(Entity root)
        {
            // gather all of the children (and root)
            List<Entity> allEs = new List<Entity>();
            Gather(root, allEs);
            // capture the original transform of the root, then clear it
            // so it isn't included in individual verticies
            Vector3 originalPosition = root.Transform.Position;
            Quaternion originalRotation = root.Transform.Rotation;
            Vector3 originalScale = root.Transform.Scale;
            root.Transform.Position = Vector3.Zero;
            root.Transform.Scale = Vector3.One;
            root.Transform.Rotation = Quaternion.Identity;
            // batch them all together into one model
            Model m = BatchEntities(allEs, out HashSet<Entity> unbatched);
            // set the root to use the new batched model
            root.GetOrCreate<ModelComponent>().Model = m;
            // restore the root transform
            root.Transform.Rotation = originalRotation;
            root.Transform.Scale = originalScale;
            root.Transform.Position = originalPosition;
            // we will want to remove entities from the scene that were batched,
            // so convert allEs into a list of things we want to remove
            foreach (Entity skipped in unbatched) allEs.Remove(skipped);
            // remove now batched entities from the scene, skipping root
            for (int i = 0; i < allEs.Count; i++)
            {
                if (allEs[i] != root) allEs[i].Scene = null;
            }
            // return how many things we were able to batch
            return allEs.Count;
        }

        /// <summary>
        /// Generate a batched model. Copies the model to all positions in listOfTransforms into one batched model.
        /// </summary>
        /// <param name="model">Model to copy around</param>
        /// <param name="listOfTransforms">List of transforms to place the model</param>
        /// <returns>Returns batched model. Null if model coouldn't be made, like if buffers for meshes couldn't be found</returns>
        public static Model GenerateBatch(Model model, List<Matrix> listOfTransforms)
        {
            if (model == null ||
                model.Meshes.Any(x => x.Draw.PrimitiveType != PrimitiveType.TriangleList || x.Draw.VertexBuffers == null || x.Draw.VertexBuffers.Length != 1)) //For now we limit only to TriangleList types and interleaved vertex buffers, also we skip transparent
            {
                return null;
            }

            var materials = new Dictionary<MaterialInstance, List<EntityChunk>>();

            for (var index = 0; index < model.Materials.Count; index++)
            {
                var material = model.Materials[index];

                for (int i = 0; i < listOfTransforms.Count; i++)
                {
                    var chunk = new EntityChunk { Entity = null, Model = model, MaterialIndex = index, Transform = listOfTransforms[i] };

                    if (materials.TryGetValue(material, out var entities))
                    {
                        entities.Add(chunk);
                    }
                    else
                    {
                        materials[material] = new List<EntityChunk> { chunk };
                    }
                }
            }

            Model prefabModel = new Model();

            foreach (var material in materials)
            {
                ProcessMaterial(material.Value, material.Key, prefabModel);
            }

            //handle boundng box/sphere for whole model
            BoundingBox bb = new BoundingBox(new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                                             new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity));
            for (int i = 0; i < prefabModel.Meshes.Count; i++)
            {
                Vector3 max = prefabModel.Meshes[i].BoundingBox.Maximum;
                Vector3 min = prefabModel.Meshes[i].BoundingBox.Minimum;
                // update bounding box?
                if (max.X > bb.Maximum.X) bb.Maximum.X = max.X;
                if (max.Y > bb.Maximum.Y) bb.Maximum.Y = max.Y;
                if (max.Z > bb.Maximum.Z) bb.Maximum.Z = max.Z;
                if (min.X < bb.Minimum.X) bb.Minimum.X = min.X;
                if (min.Y < bb.Minimum.Y) bb.Minimum.Y = min.Y;
                if (min.Z < bb.Minimum.Z) bb.Minimum.Z = min.Z;
            }
            prefabModel.BoundingBox = bb;

            return prefabModel;
        }

        /// <summary>
        /// Returns a model that batches as much as possible from all of the entities in the list. Any entities that couldn't be batched into
        /// the model will be added to unbatched. Entities may not get batched if underlying buffer data couldn't be found to batch with
        /// </summary>
        /// <param name="entityList">List of entities to be batched</param>
        /// <param name="unbatched">List of entities that failed to batch</param>
        /// <returns>Model with meshes merged as much as possible</returns>
        public static Model BatchEntities(List<Entity> entityList, out HashSet<Entity> unbatched)
        {
            var prefabModel = new Model();

            //The objective is to create 1 mesh per material/shadow params
            //1. We group by materials
            //2. Create a mesh per material (might need still more meshes if 16bit indexes or more then 32bit)

            var materials = new Dictionary<MaterialInstance, List<EntityChunk>>();

            foreach (var subEntity in entityList)
            {
                var modelComponent = subEntity.Get<ModelComponent>();

                if (modelComponent?.Model == null || (modelComponent.Skeleton != null && modelComponent.Skeleton.Nodes.Length != 1) || !modelComponent.Enabled)
                    continue;

                var model = modelComponent.Model;

                if (model == null ||
                    model.Meshes.Any(x => x.Draw.PrimitiveType != PrimitiveType.TriangleList || x.Draw.VertexBuffers == null || x.Draw.VertexBuffers.Length != 1)) //For now we limit only to TriangleList types and interleaved vertex buffers, also we skip transparent
                {
                    continue;
                }

                for (var index = 0; index < model.Materials.Count; index++)
                {
                    var material = model.Materials[index];

                    var chunk = new EntityChunk { Entity = subEntity, Model = model, MaterialIndex = index };

                    if (materials.TryGetValue(material, out var entities))
                    {
                        entities.Add(chunk);
                    }
                    else
                    {
                        materials[material] = new List<EntityChunk> { chunk };
                    }
                }
            }

            // keep track of any entities that couldn't be batched
            unbatched = new HashSet<Entity>();

            foreach (var material in materials)
            {
                ProcessMaterial(material.Value, material.Key, prefabModel, unbatched);
            }

            //handle boundng box/sphere for whole model
            BoundingBox bb = new BoundingBox(new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                                             new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity));
            for (int i = 0; i < prefabModel.Meshes.Count; i++)
            {
                Vector3 max = prefabModel.Meshes[i].BoundingBox.Maximum;
                Vector3 min = prefabModel.Meshes[i].BoundingBox.Minimum;
                // update bounding box?
                if (max.X > bb.Maximum.X) bb.Maximum.X = max.X;
                if (max.Y > bb.Maximum.Y) bb.Maximum.Y = max.Y;
                if (max.Z > bb.Maximum.Z) bb.Maximum.Z = max.Z;
                if (min.X < bb.Minimum.X) bb.Minimum.X = min.X;
                if (min.Y < bb.Minimum.Y) bb.Minimum.Y = min.Y;
                if (min.Z < bb.Minimum.Z) bb.Minimum.Z = min.Z;
            }
            prefabModel.BoundingBox = bb;

            return prefabModel;
        }
    }
}
