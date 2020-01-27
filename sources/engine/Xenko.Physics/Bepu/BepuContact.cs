using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Engine;

namespace Xenko.Physics.Bepu
{
    public struct BepuContact
    {
        public BepuPhysicsComponent A, B;
        public Xenko.Core.Mathematics.Vector3 Normal, Offset;
    }
}
