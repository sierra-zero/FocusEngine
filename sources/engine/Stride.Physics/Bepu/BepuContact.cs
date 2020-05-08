using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Xenko.Engine;

namespace Xenko.Physics.Bepu
{
    public struct BepuContact
    {
        public BepuPhysicsComponent A, B;
        public Xenko.Core.Mathematics.Vector3 Normal, Offset;

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
