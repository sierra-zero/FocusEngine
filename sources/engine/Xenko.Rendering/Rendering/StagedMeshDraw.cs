using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Xenko.Core;
using Xenko.Graphics;

namespace Xenko.Rendering.Rendering {
    public class StagedMeshDraw : MeshDraw {

        public Action<GraphicsDevice> performStage;

        public uint[] Indicies { get; private set; }
        public object Verticies { get; private set; }

        private StagedMeshDraw() { }
        private static object StagedLock = new object();

        /// <summary>
        /// Gets a MeshDraw that will be prepared when needed with the given index buffer & vertex buffer.
        /// </summary>
        /// <typeparam name="T">Type of vertex buffer used</typeparam>
        /// <param name="indexBuffer">Array of vertex indicies</param>
        /// <param name="vertexBuffer">Vertex buffer</param>
        /// <returns></returns>
        public static StagedMeshDraw MakeStagedMeshDraw<T>(uint[] indexBuffer, T[] vertexBuffer, VertexDeclaration vertexBufferLayout) where T : struct {
            StagedMeshDrawTyped<T> smdt = new StagedMeshDrawTyped<T>();
            smdt.PrimitiveType = PrimitiveType.TriangleList;
            smdt.DrawCount = indexBuffer.Length;
            smdt.Indicies = indexBuffer;
            smdt.Verticies = vertexBuffer;
            smdt.performStage = (GraphicsDevice graphicsDevice) => {
                if (StagedMeshDrawTyped<T>.CachedBuffers.TryGetValue(vertexBuffer, out object[] bufferBindings)) {
                    smdt.VertexBuffers = (VertexBufferBinding[])bufferBindings[0];
                    smdt.IndexBuffer = (IndexBufferBinding)bufferBindings[1];
                } else {
                    Xenko.Graphics.Buffer vbo, ibo;
                    lock (StagedLock) {
                        vbo = Xenko.Graphics.Buffer.Vertex.New<T>(
                            graphicsDevice,
                            (T[])smdt.Verticies,
                            GraphicsResourceUsage.Immutable
                        );
                        ibo = Xenko.Graphics.Buffer.Index.New<uint>(
                            graphicsDevice,
                            smdt.Indicies
                        );
                    }
                    object[] o = new object[2];
                    VertexBufferBinding[] vbb = new[] {
                        new VertexBufferBinding(vbo, vertexBufferLayout, smdt.DrawCount)
                    };
                    IndexBufferBinding ibb = new IndexBufferBinding(ibo, true, smdt.DrawCount);
                    o[0] = vbb;
                    o[1] = ibb;
                    StagedMeshDrawTyped<T>.CachedBuffers.TryAdd((T[])smdt.Verticies, o);
                    smdt.VertexBuffers = vbb;
                    smdt.IndexBuffer = ibb;
                }
            };
            return smdt;
        }

        private class StagedMeshDrawTyped<T> : StagedMeshDraw where T : struct {
            public static ConcurrentDictionary<T[], object[]> CachedBuffers = new ConcurrentDictionary<T[], object[]>();
        }
    }
}
