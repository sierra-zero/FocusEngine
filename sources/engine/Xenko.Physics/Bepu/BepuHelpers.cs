/*
 * - need to switch between which list index is processing for contact collection
 *   - need to clear list of index we will start populating
 * - throw out contact information without normal or position information...? when count == 0...?
 *
 */

using System;
using System.Collections.Generic;
using System.Text;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Games;
using Xenko.Graphics;
using Xenko.Rendering.Rendering;

namespace Xenko.Physics.Bepu
{
    public class BepuHelpers
    {
        internal static PhysicsSystem physicsSystem;

        internal static void AssureServiceAdded()
        {
            if (physicsSystem == null)
            {
                physicsSystem = ServiceRegistry.instance.GetService<PhysicsSystem>();
                if (physicsSystem == null)
                {
                    physicsSystem = new PhysicsSystem(ServiceRegistry.instance);
                    ServiceRegistry.instance.AddService<IPhysicsSystem>(physicsSystem);
                    var gameSystems = ServiceRegistry.instance.GetSafeServiceAs<IGameSystemCollection>();
                    gameSystems.Add(physicsSystem);
                    ((IReferencable)physicsSystem).AddReference();
                    physicsSystem.Create(null, PhysicsEngineFlags.None, true);
                }
                else if (physicsSystem.HasSimulation<BepuSimulation>() == false)
                {
                    physicsSystem.Create(null, PhysicsEngineFlags.None, true);
                }
            }
        }

        /// <summary>
        /// Goes through the whole scene and adds bepu physics objects to the simulation. Only will add if AllowHelperToAdd is true (which is set to true by default)
        /// and if the body isn't added already.
        /// </summary>
        /// <param name="rootScene"></param>
        public static void AddAllBodiesToSimulation(Scene rootScene)
        {
            foreach (Entity e in rootScene.Entities)
                AddAllBodiesToSimulation(e);
        }

        /// <summary>
        /// Goes through the entity and children and adds bepu physics objects to the simulation. Only will add if AllowHelperToAdd is true (which is set to true by default)
        /// and if the body isn't added already.
        /// </summary>
        /// <param name="rootEntity"></param>
        public static void AddAllBodiesToSimulation(Entity rootEntity)
        {
            BepuPhysicsComponent pc = rootEntity.Get<BepuPhysicsComponent>();
            if (pc?.AllowHelperToAdd ?? false) pc.AddedToScene = true;
            foreach (Entity e in rootEntity.GetChildren())
                AddAllBodiesToSimulation(e);
        }

        public static unsafe bool GenerateMeshShape(Xenko.Rendering.Mesh modelMesh, out BepuPhysics.Collidables.Mesh outMesh, Vector3? scale = null)
        {
            Vector3[] positions;
            int[] indicies;

            if (modelMesh.Draw is StagedMeshDraw)
            {
                StagedMeshDraw smd = modelMesh.Draw as StagedMeshDraw;

                object verts = smd.Verticies;

                if (verts is VertexPositionNormalColor[])
                {
                    VertexPositionNormalColor[] vpnc = verts as VertexPositionNormalColor[];
                    positions = new Vector3[vpnc.Length];
                    for (int k = 0; k < vpnc.Length; k++)
                        positions[k] = vpnc[k].Position;
                }
                else if (verts is VertexPositionNormalTexture[])
                {
                    VertexPositionNormalTexture[] vpnc = verts as VertexPositionNormalTexture[];
                    positions = new Vector3[vpnc.Length];
                    for (int k = 0; k < vpnc.Length; k++)
                        positions[k] = vpnc[k].Position;
                }
                else if (verts is VertexPositionNormalTextureTangent[])
                {
                    VertexPositionNormalTextureTangent[] vpnc = verts as VertexPositionNormalTextureTangent[];
                    positions = new Vector3[vpnc.Length];
                    for (int k = 0; k < vpnc.Length; k++)
                        positions[k] = vpnc[k].Position;
                }
                else
                {
                    outMesh = new Mesh();
                    return false;
                }

                // take care of indicies
                indicies = (int[])(object)smd.Indicies;
            }
            else
            {
                Xenko.Graphics.Buffer buf = modelMesh.Draw?.VertexBuffers[0].Buffer;
                Xenko.Graphics.Buffer ibuf = modelMesh.Draw?.IndexBuffer.Buffer;
                if (buf == null || buf.VertIndexData == null ||
                    ibuf == null || ibuf.VertIndexData == null)
                {
                    outMesh = new Mesh();
                    return false;
                }

                if (ModelBatcher.UnpackRawVertData(buf.VertIndexData, modelMesh.Draw.VertexBuffers[0].Declaration,
                                                   out positions, out Core.Mathematics.Vector3[] normals, out Core.Mathematics.Vector2[] uvs,
                                                   out Color4[] colors, out Vector4[] tangents) == false)
                {
                    outMesh = new Mesh();
                    return false;
                }

                // indicies
                fixed (byte* pdst = ibuf.VertIndexData)
                {
                    if (modelMesh.Draw.IndexBuffer.Is32Bit)
                    {
                        var dst = (uint*)pdst;

                        int numIndices = ibuf.VertIndexData.Length / sizeof(uint);
                        indicies = new int[numIndices];
                        for (var k = 0; k < numIndices; k++)
                        {
                            // Offset indices
                            indicies[k] = (int)dst[k];
                        }
                    }
                    else
                    {
                        var dst = (ushort*)pdst;

                        int numIndices = ibuf.VertIndexData.Length / sizeof(ushort);
                        indicies = new int[numIndices];
                        for (var k = 0; k < numIndices; k++)
                        {
                            // Offset indices
                            indicies[k] = dst[k];
                        }
                    }
                }
            }

            return GenerateMeshShape(positions, indicies, out outMesh, scale);
        }

        public static unsafe bool GenerateMeshShape(Vector3[] positions, int[] indicies, out BepuPhysics.Collidables.Mesh outMesh, Vector3? scale = null)
        {
            // ok, should have what we need to make triangles
            var memory = stackalloc Triangle[indicies.Length];
            BepuUtilities.Memory.Buffer<Triangle> triangles = new BepuUtilities.Memory.Buffer<Triangle>(memory, indicies.Length);

            for (int i = 0; i < indicies.Length; i += 3)
            {
                triangles[i].A = ToBepu(positions[indicies[i]]);
                triangles[i].B = ToBepu(positions[indicies[i+1]]);
                triangles[i].C = ToBepu(positions[indicies[i+2]]);
            }

            outMesh = new Mesh(triangles, new System.Numerics.Vector3(scale?.X ?? 1f, scale?.Y ?? 1f, scale?.Z ?? 1f), BepuSimulation.instance.pBufferPool);

            return true;
        }

        public static unsafe System.Numerics.Vector3 ToBepu(Xenko.Core.Mathematics.Vector3 v)
        {
            return *((System.Numerics.Vector3*)(void*)&v);
        }

        public static unsafe Xenko.Core.Mathematics.Vector3 ToXenko(System.Numerics.Vector3 v)
        {
            return *((Xenko.Core.Mathematics.Vector3*)(void*)&v);
        }

        public static unsafe Xenko.Core.Mathematics.Quaternion ToXenko(BepuUtilities.Quaternion q)
        {
            return *((Xenko.Core.Mathematics.Quaternion*)(void*)&q);
        }

        public static unsafe BepuUtilities.Quaternion ToBepu(Xenko.Core.Mathematics.Quaternion q)
        {
            return *((BepuUtilities.Quaternion*)(void*)&q);
        }
    }
}
