// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Xenko.Core;
using Xenko.Core.Annotations;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;
using Xenko.Rendering;
using BepuPhysics;
using BepuUtilities;
using Xenko.Engine;
using Xenko.Physics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;

namespace Xenko.Physics.Bepu
{
    [DataContract("BepuRigidbodyComponent")]
    [Display("Bepu Rigidbody")]
    public sealed class BepuRigidbodyComponent : BepuPhysicsComponent
    {
        public BodyDescription bodyDescription;

        private BodyReference _internalReference = new BodyReference();

        public BodyReference InternalBody
        {
            get
            {
                _internalReference.Bodies = BepuSimulation.instance.internalSimulation.Bodies;
                _internalReference.Handle = AddedHandle;
                return _internalReference;
            }
        }

        public Action<BepuRigidbodyComponent, float> ActionPerSimulationTick;

        /// <summary>
        /// Gets a value indicating whether this instance is active (awake).
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is active; otherwise, <c>false</c>.
        /// </value>
        public bool IsActive => InternalBody.Awake;

        [DataMember(67)]
        public float CcdMotionThreshold
        {
            get
            {
                return bodyDescription.Collidable.Continuity.SweepConvergenceThreshold;
            }
            set
            {
                bodyDescription.Collidable.Continuity.SweepConvergenceThreshold = value;

                if (InternalBody.Exists)
                    InternalBody.Collidable.Continuity.SweepConvergenceThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets if this element will store collisions in CurrentPhysicalContacts. Uses less CPU than ProcessCollisions
        /// </summary>
        /// <value>
        /// true, false
        /// </value>
        /// <userdoc>
        /// Stores contact points in a simple CurrentPhysicalContacts list, instead of new/update/ended events. Uses less CPU than ProcessCollisions and is multithreading supported
        /// </userdoc>
        [Display("Simple collision storage")]
        [DataMemberIgnore]
        public bool CollectCollisions
        {
            get
            {
                return _collectCollisions;
            }
            set
            {
                if (value && processingPhysicalContacts == null)
                {
                    processingPhysicalContacts = new List<BepuContact>[2];
                    processingPhysicalContacts[0] = new List<BepuContact>();
                    processingPhysicalContacts[1] = new List<BepuContact>();
                }
                _collectCollisions = value;
            }
        }
        private bool _collectCollisions = false;

        /// <summary>
        /// If we are using ProcessCollisionSlim, this list will maintain all current collisions
        /// </summary>
        [DataMemberIgnore]
        public List<BepuContact> CurrentContacts
        {
            get
            {
                if (processingPhysicalContacts == null) return null;

                return new List<BepuContact>(processingPhysicalContacts[processingPhysicalContactsIndex]);
            }
        }

        internal void swapProcessingContactsList()
        {
            if (processingPhysicalContacts == null || IsActive == false) return;

            processingPhysicalContacts[processingPhysicalContactsIndex].Clear();
            processingPhysicalContactsIndex ^= 1;
        }

        internal List<BepuContact>[] processingPhysicalContacts;
        internal int processingPhysicalContactsIndex;

        private static readonly BodyInertia KinematicInertia = new BodyInertia()
        {
            InverseMass = 0f,
            InverseInertiaTensor = new Symmetric3x3()
            {
                XX = 0f,
                YX = 0f,
                ZX = 0f,
                YY = 0f,
                ZY = 0f,
                ZZ = 0f
            }
        };

        private RigidBodyTypes type = RigidBodyTypes.Dynamic;
        private Vector3 gravity = Vector3.Zero;

        public BepuRigidbodyComponent() : base()
        {
            bodyDescription = new BodyDescription();
            bodyDescription.LocalInertia.InverseMass = 1f;
            bodyDescription.Activity.MinimumTimestepCountUnderThreshold = 32;
            bodyDescription.Activity.SleepThreshold = 0.01f;
        }

        /// <summary>
        /// Attempts to awake the collider.
        /// </summary>
        /// <param name="forceActivation">if set to <c>true</c> [force activation].</param>
        public void Activate()
        {
            BodyReference ib = InternalBody;
            ib.Awake = true;
        }

        /// <summary>
        /// Gets or sets the rolling friction of this element
        /// </summary>
        /// <value>
        /// true, false
        /// </value>
        /// <userdoc>
        /// The rolling friction
        /// </userdoc>
        [DataMember(66)]
        public float RollingFriction => 0f;

        public float SleepThreshold
        {
            get
            {
                return bodyDescription.Activity.SleepThreshold;
            }
            set
            {
                bodyDescription.Activity.SleepThreshold = value;

                if (InternalBody.Exists)
                    InternalBody.Activity.SleepThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the mass of this Rigidbody
        /// </summary>
        /// <value>
        /// true, false
        /// </value>
        /// <userdoc>
        /// Objects with higher mass push objects with lower mass more when they collide. For large differences, use point values; for example, write 0.1 or 10, not 1 or 100000.
        /// </userdoc>
        [DataMember(80)]
        [DataMemberRange(0, 6)]
        public float Mass
        {
            get
            {
                return mass;
            }
            set
            {
                if (mass <= 0.00001f) mass = 0.00001f;

                mass = value;

                UpdateInertia();
            }
        }

        private void UpdateInertia()
        {
            if (type == RigidBodyTypes.Kinematic)
            {
                bodyDescription.LocalInertia = KinematicInertia;
            }
            else if (ColliderShape != null)
            { 
                ColliderShape.ComputeInertia(mass, out bodyDescription.LocalInertia);
            }

            if (InternalBody.Exists)
            {
                InternalBody.LocalInertia = bodyDescription.LocalInertia;
            }
        }

        private IConvexShape _myshape;

        public IConvexShape ColliderShape
        {
            get => _myshape;
            set
            {
                bool wasAddedToScene = AddedToScene;

                AddedToScene = false;

                _myshape = value;
                UpdateInertia();

                AddedToScene = wasAddedToScene;
            }
        }

        public void ReloadColliderShape()
        {
            if (_myshape == null || AddedToScene == false) return;
            ColliderShape = _myshape;
        }

        private float mass = 1f;

        /// <summary>
        /// Gets or sets the linear damping of this rigidbody
        /// </summary>
        /// <value>
        /// true, false
        /// </value>
        /// <userdoc>
        /// The amount of damping for directional forces
        /// </userdoc>
        [DataMember(85)]
        public float LinearDamping = 0f;

        /// <summary>
        /// Gets or sets the angular damping of this rigidbody
        /// </summary>
        /// <value>
        /// true, false
        /// </value>
        /// <userdoc>
        /// The amount of damping for rotational forces
        /// </userdoc>
        [DataMember(90)]
        public float AngularDamping = 0f;

        /// <summary>
        /// Gets or sets if this Rigidbody overrides world gravity
        /// </summary>
        /// <value>
        /// true, false
        /// </value>
        /// <userdoc>
        /// Override gravity with the vector specified in Gravity
        /// </userdoc>
        [DataMember(95)]
        public bool OverrideGravity;

        /// <summary>
        /// Gets or sets the gravity acceleration applied to this RigidBody
        /// </summary>
        /// <value>
        /// A vector representing moment and direction
        /// </value>
        /// <userdoc>
        /// The gravity acceleration applied to this rigidbody
        /// </userdoc>
        [DataMember(100)]
        public Vector3 Gravity;

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public RigidBodyTypes RigidBodyType
        {
            get
            {
                return type;
            }
            set
            {
                type = value;

                UpdateInertia();
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
                    Mass = mass;
                    RigidBodyType = type;
                    BepuSimulation.instance.AddRigidBody(this, (CollisionFilterGroupFlags)CollisionGroup, CanCollideWith);
                    SleepThreshold = bodyDescription.Activity.SleepThreshold;
                    Position = Entity.Transform.WorldPosition();
                    Rotation = Entity.Transform.WorldRotation();
                }
                else
                {
                    if (processingPhysicalContacts != null)
                    {
                        processingPhysicalContacts[0].Clear();
                        processingPhysicalContacts[1].Clear();
                    }

                    BepuSimulation.instance.RemoveRigidBody(this);
                }
            }
        }

        /// <summary>
        /// Applies the impulse.
        /// </summary>
        /// <param name="impulse">The impulse.</param>
        public void ApplyImpulse(Vector3 impulse)
        {
            System.Numerics.Vector3 i = new System.Numerics.Vector3(impulse.X, impulse.Y, impulse.Z);
            InternalBody.ApplyLinearImpulse(i);
        }

        /// <summary>
        /// Applies the impulse.
        /// </summary>
        /// <param name="impulse">The impulse.</param>
        /// <param name="localOffset">The local offset.</param>
        public void ApplyImpulse(Vector3 impulse, Vector3 localOffset)
        {
            System.Numerics.Vector3 i = new System.Numerics.Vector3(impulse.X, impulse.Y, impulse.Z);
            System.Numerics.Vector3 o = new System.Numerics.Vector3(localOffset.X, localOffset.Y, localOffset.Z);
            InternalBody.ApplyImpulse(i, o);
        }

        /// <summary>
        /// Applies the torque impulse.
        /// </summary>
        /// <param name="torque">The torque.</param>
        public void ApplyTorqueImpulse(Vector3 torque)
        {
            System.Numerics.Vector3 i = new System.Numerics.Vector3(torque.X, torque.Y, torque.Z);
            InternalBody.ApplyAngularImpulse(i);
        }

        [DataMemberIgnore]
        public override Vector3 Position
        {
            get
            {
                return BepuHelpers.ToXenko(InternalBody.Exists ? InternalBody.Pose.Position : bodyDescription.Pose.Position);
            }
            set
            {
                bodyDescription.Pose.Position.X = value.X;
                bodyDescription.Pose.Position.Y = value.Y;
                bodyDescription.Pose.Position.Z = value.Z;

                if (InternalBody.Exists)
                {
                    InternalBody.Pose.Position = bodyDescription.Pose.Position;
                }
            }
        }

        [DataMemberIgnore]
        public override Xenko.Core.Mathematics.Quaternion Rotation
        {
            get
            {
                return BepuHelpers.ToXenko(InternalBody.Exists ? InternalBody.Pose.Orientation : bodyDescription.Pose.Orientation);
            }
            set
            {
                bodyDescription.Pose.Orientation.X = value.X;
                bodyDescription.Pose.Orientation.Y = value.Y;
                bodyDescription.Pose.Orientation.Z = value.Z;
                bodyDescription.Pose.Orientation.Z = value.W;

                if (InternalBody.Exists)
                {
                    InternalBody.Pose.Orientation = bodyDescription.Pose.Orientation;
                }
            }
        }

        /// <summary>
        /// Gets or sets the angular velocity.
        /// </summary>
        /// <value>
        /// The angular velocity.
        /// </value>
        [DataMemberIgnore]
        public Vector3 AngularVelocity
        {
            get
            {
                return BepuHelpers.ToXenko(InternalBody.Exists ? InternalBody.Velocity.Angular : bodyDescription.Velocity.Angular);
            }
            set
            {
                bodyDescription.Velocity.Angular.X = value.X;
                bodyDescription.Velocity.Angular.Y = value.Y;
                bodyDescription.Velocity.Angular.Z = value.Z;

                if (InternalBody.Exists)
                {
                    InternalBody.Velocity.Angular = bodyDescription.Velocity.Angular;
                }
            }
        }

        /// <summary>
        /// Gets or sets the linear velocity.
        /// </summary>
        /// <value>
        /// The linear velocity.
        /// </value>
        [DataMemberIgnore]
        public Vector3 LinearVelocity
        {
            get
            {
                return BepuHelpers.ToXenko(InternalBody.Exists ? InternalBody.Velocity.Linear : bodyDescription.Velocity.Linear);
            }
            set
            {
                bodyDescription.Velocity.Linear.X = value.X;
                bodyDescription.Velocity.Linear.Y = value.Y;
                bodyDescription.Velocity.Linear.Z = value.Z;

                if (InternalBody.Exists)
                {
                    InternalBody.Velocity.Linear = bodyDescription.Velocity.Linear;
                }
            }
        }

        /// <summary>
        /// Updades the graphics transformation from the given physics transformation
        /// </summary>
        /// <param name="physicsTransform"></param>
        internal override void UpdateTransformationComponent()
        {
            var entity = Entity;

            entity.Transform.Position = Position;
            if (LocalPhysicsOffset.HasValue) entity.Transform.Position += LocalPhysicsOffset.Value;
            if (IgnorePhysicsRotation == false) entity.Transform.Rotation = Rotation;
        }

    }
}
