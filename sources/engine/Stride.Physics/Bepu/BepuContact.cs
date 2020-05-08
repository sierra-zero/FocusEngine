using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Stride.Engine;

namespace Stride.Physics.Bepu
{
    public struct BepuContact
    {
        public BepuPhysicsComponent A, B;
        public Stride.Core.Mathematics.Vector3 Normal, Offset;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Swap()
        {
            Normal.X = -Normal.X;
            Normal.Y = -Normal.Y;
            Normal.Z = -Normal.Z;
            Offset = B.Position - (A.Position + Offset);
            var C = A;
            A = B;
            B = C;
        }
    }
}
