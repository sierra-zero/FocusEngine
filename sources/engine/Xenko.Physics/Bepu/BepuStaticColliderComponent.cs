// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using BepuPhysics;
using BepuPhysics.Collidables;
using Xenko.Core;
using Xenko.Engine;

namespace Xenko.Physics.Bepu
{
    [DataContract("BepuStaticColliderComponent")]
    [Display("Bepu Static collider")]
    public sealed class BepuStaticColliderComponent : BepuPhysicsComponent
    {
        public StaticDescription staticDescription;
        private StaticReference _internalStatic;

        public IShape ColliderShape;

        public StaticReference InternalStatic
        {
            get
            {
                _internalStatic.Statics = BepuSimulation.instance.internalSimulation.Statics;
                _internalStatic.Handle = AddedHandle;
                return _internalStatic;
            }
        } 

        public BepuStaticColliderComponent() : base ()
        {
            _internalStatic = new StaticReference();
            staticDescription = new StaticDescription();
        }

        [DataMemberIgnore]
        public override Xenko.Core.Mathematics.Vector3 Position
        {
            get
            {
                return BepuHelpers.ToXenko(InternalStatic.Exists ? InternalStatic.Pose.Position : staticDescription.Pose.Position);
            }
            set
            {
                staticDescription.Pose.Position.X = value.X;
                staticDescription.Pose.Position.Y = value.Y;
                staticDescription.Pose.Position.Z = value.Z;

                if (InternalStatic.Exists)
                {
                    InternalStatic.Pose.Position = staticDescription.Pose.Position;
                }
            }
        }

        [DataMemberIgnore]
        public override Xenko.Core.Mathematics.Quaternion Rotation
        {
            get
            {
                return BepuHelpers.ToXenko(InternalStatic.Exists ? InternalStatic.Pose.Orientation : staticDescription.Pose.Orientation);
            }
            set
            {
                staticDescription.Pose.Orientation.X = value.X;
                staticDescription.Pose.Orientation.Y = value.Y;
                staticDescription.Pose.Orientation.Z = value.Z;
                staticDescription.Pose.Orientation.Z = value.W;

                if (InternalStatic.Exists)
                {
                    InternalStatic.Pose.Orientation = staticDescription.Pose.Orientation;
                }
            }
        }

        [DataMemberIgnore]
        public bool AddedToScene
        {
            get
            {
                return AddedHandle != -1;
            }
            set
            {
                if (AddedToScene == value) return;

                if (value)
                {
                    BepuSimulation.instance.AddCollider(this, (CollisionFilterGroupFlags)CollisionGroup, CanCollideWith);
                    Position = Entity.Transform.WorldPosition();
                    Rotation = Entity.Transform.WorldRotation();
                }
                else
                {
                    BepuSimulation.instance.RemoveCollider(this);
                }
            }
        }
    }
}
