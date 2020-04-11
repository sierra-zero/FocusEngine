// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
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

        public StaticReference InternalStatic
        {
            get
            {
                _internalStatic.Statics = BepuSimulation.instance.internalSimulation.Statics;
                _internalStatic.Handle = AddedHandle;
                return _internalStatic;
            }
        }

        public override TypedIndex ShapeIndex { get => staticDescription.Collidable.Shape; }

        public BepuStaticColliderComponent() : base ()
        {
            _internalStatic = new StaticReference();
            staticDescription = new StaticDescription();
            staticDescription.Pose.Orientation.W = 1f;
        }

        [DataMember]
        private Xenko.Core.Mathematics.Vector3? usePosition;

        [DataMember]
        private Xenko.Core.Mathematics.Quaternion? useRotation;

        [DataMemberIgnore]
        public override Xenko.Core.Mathematics.Vector3 Position
        {
            get
            {
                return usePosition ?? Entity.Transform.WorldPosition();
            }
            set
            {
                usePosition = value;
            }
        }

        [DataMemberIgnore]
        public override Xenko.Core.Mathematics.Quaternion Rotation
        {
            get
            {
                return useRotation ?? Entity.Transform.WorldRotation();
            }
            set
            {
                useRotation = value;
            }
        }

        [DataMemberIgnore]
        public BepuUtilities.Memory.BufferPool PoolUsedForMesh;

        [DataMember]
        public bool DisposeMeshOnDetach { get; set; } = false;

        /// <summary>
        /// Dispose the mesh right now. Should best be done after confirmed removed from simulation, which DisposeMeshOnDetach boolean can do for you.
        /// </summary>
        /// <returns>true if dispose worked</returns>
        public bool DisposeMesh()
        {
            if (ColliderShape is Mesh m && PoolUsedForMesh != null)
            {
                lock (PoolUsedForMesh)
                {
                    m.Dispose(PoolUsedForMesh);
                }
                ColliderShape = null;
                PoolUsedForMesh = null;
                return true;
            }
            return false;
        }

        internal void preparePose()
        {
            staticDescription.Pose.Position = BepuHelpers.ToBepu(Position);
            staticDescription.Pose.Orientation = BepuHelpers.ToBepu(Rotation);
        }

        [DataMember]
        public override float SpeculativeMargin
        {
            get => base.SpeculativeMargin;
            set
            {
                base.SpeculativeMargin = value;

                if (AddedToScene)
                    InternalStatic.Collidable.SpeculativeMargin = value;
            }
        }

        /// <summary>
        /// Set this to true to add this object to the physics simulation. Will automatically remove itself when the entity. is removed from the scene. Will NOT automatically add the rigidbody
        /// to the scene when the entity is added, though.
        /// </summary>
        [DataMemberIgnore]
        public override bool AddedToScene
        {
            get
            {
                return AddedHandle != -1;
            }
            set
            {
                if (ColliderShape == null)
                    throw new InvalidOperationException(Entity.Name + " has no ColliderShape, can't be added!");

                if (BepuHelpers.SanityCheckShape(ColliderShape) == false)
                    throw new InvalidOperationException(Entity.Name + " has a broken ColliderShape! Check sizes and/or children count.");

                if (value)
                {
                    lock (BepuSimulation.instance.ToBeAdded)
                    {
                        BepuSimulation.instance.ToBeAdded.Add(this);
                        BepuSimulation.instance.ToBeRemoved.Remove(this);
                    }
                }
                else
                {
                    lock (BepuSimulation.instance.ToBeAdded)
                    {
                        BepuSimulation.instance.ToBeAdded.Remove(this);
                        BepuSimulation.instance.ToBeRemoved.Add(this);
                    }
                }
            }
        }
    }
}
