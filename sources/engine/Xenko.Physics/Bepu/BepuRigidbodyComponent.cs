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
        /// <summary>
        /// Description of the body to be created when added to the scene
        /// </summary>
        public BodyDescription bodyDescription;

        private BodyReference _internalReference = new BodyReference();

        /// <summary>
        /// Reference to the body after being added to the scene
        /// </summary>
        public BodyReference InternalBody
        {
            get
            {
                _internalReference.Bodies = BepuSimulation.instance.internalSimulation.Bodies;
                _internalReference.Handle = AddedHandle;
                return _internalReference;
            }
        }

        /// <summary>
        /// Action to be called after simulation, but before transforms are set to new positions. Arguments are this and simulation time.
        /// </summary>
        public Action<BepuRigidbodyComponent, float> ActionPerSimulationTick;

        /// <summary>
        /// Gets a value indicating whether this instance is active (awake).
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is active; otherwise, <c>false</c>.
        /// </value>
        public bool IsActive => InternalBody.Awake;

        /// <summary>
        /// Use continuous collision detection? Set greater than 0 to use, 0 or less to disable
        /// </summary>
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
                bodyDescription.Collidable.Continuity.Mode = value > 0 ? ContinuousDetectionMode.Continuous : ContinuousDetectionMode.Discrete;
                bodyDescription.Collidable.Continuity.MinimumSweepTimestep = value > 0 ? 1e-3f : 0f;

                if (InternalBody.Exists)
                    InternalBody.Collidable.Continuity = bodyDescription.Collidable.Continuity;
            }
        }

        /// <summary>
        /// If we are collecting collisions, how many to store before we stop storing them? Defaults to 32. Prevents crazy counts when objects are heavily overlapping.
        /// </summary>
        [DataMember]
        public int CollectCollisionMaximumCount = 32;
        
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
                if (_collectCollisions == value) return;

                if (value)
                {
                    if (processingPhysicalContacts == null)
                    {
                        processingPhysicalContacts = new List<BepuContact>[2];
                        processingPhysicalContacts[0] = new List<BepuContact>();
                        processingPhysicalContacts[1] = new List<BepuContact>();
                        _currentContacts = new List<BepuContact>();
                    }
                }
                else if (processingPhysicalContacts != null)
                {
                    _currentContacts.Clear();
                    _currentContacts = null;
                    processingPhysicalContacts[0].Clear();
                    processingPhysicalContacts[1].Clear();
                    processingPhysicalContacts = null;
                }

                _collectCollisions = value;
            }
        }
        private bool _collectCollisions = false;
        private List<BepuContact> _currentContacts;

        /// <summary>
        /// If we are using ProcessCollisionSlim, this list will maintain all current collisions
        /// </summary>
        [DataMemberIgnore]
        public List<BepuContact> CurrentContacts
        {
            get
            {
                if (_currentContacts == null) return null;

                _currentContacts.Clear();

                List<BepuContact> getFrom = processingPhysicalContacts[processingPhysicalContactsIndex^1];

                for (int i = 0; i < getFrom.Count; i++)
                    _currentContacts.Add(getFrom[i]);

                return _currentContacts;
            }
        }

        internal void swapProcessingContactsList()
        {
            if (processingPhysicalContacts == null || IsActive == false) return;

            processingPhysicalContactsIndex ^= 1;
            processingPhysicalContacts[processingPhysicalContactsIndex].Clear();
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
        /// How slow does this need to be to sleep? Set less than 0 to never sleep
        /// </summary>
        [DataMember]
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

        /// <summary>
        /// If you made a modification to the existing ColliderShape (like changed its radius), try reloading it via this function.
        /// </summary>
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
            InternalBody.ApplyLinearImpulse(BepuHelpers.ToBepu(impulse));
        }

        /// <summary>
        /// Applies the impulse.
        /// </summary>
        /// <param name="impulse">The impulse.</param>
        /// <param name="localOffset">The local offset.</param>
        public void ApplyImpulse(Vector3 impulse, Vector3 localOffset)
        {
            InternalBody.ApplyImpulse(BepuHelpers.ToBepu(impulse), BepuHelpers.ToBepu(localOffset));
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
                bodyDescription.Pose.Orientation.W = value.W;

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
