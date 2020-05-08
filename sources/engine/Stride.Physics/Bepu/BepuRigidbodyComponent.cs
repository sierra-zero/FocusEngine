// Copyright (c) Stride contributors (https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Collections;
using Stride.Core.Mathematics;
using Stride.Rendering;
using BepuPhysics;
using BepuUtilities;
using Stride.Engine;
using Stride.Physics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using System.Runtime.CompilerServices;
using Stride.Core.Threading;
using System.Threading;
using System.Collections.Concurrent;

namespace Stride.Physics.Bepu
{
    [DataContract("BepuRigidbodyComponent")]
    [Display("Bepu Rigidbody")]
    public sealed class BepuRigidbodyComponent : BepuPhysicsComponent
    {
        /// <summary>
        /// Description of the body to be created when added to the scene
        /// </summary>
        [DataMember]
        public BodyDescription bodyDescription;

        [DataMemberIgnore]
        private BodyReference _internalReference;

        /// <summary>
        /// Reference to the body after being added to the scene
        /// </summary>
        public ref BodyReference InternalBody
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                _internalReference.Bodies = BepuSimulation.instance.internalSimulation.Bodies;
                _internalReference.Handle = AddedHandle;

                return ref _internalReference;
            }
        }

        /// <summary>
        /// Action to be called after simulation, but before transforms are set to new positions. Arguments are this and simulation time.
        /// </summary>
        [DataMemberIgnore]
        public Action<BepuRigidbodyComponent, float> ActionPerSimulationTick;

        internal bool wasAwake;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SafeRun(Action dome)
        {
            if (PhysicsSystem.IsSimulationThread(Thread.CurrentThread))
            {
                dome();
            }
            else
            {
                BepuSimulation.instance.ActionsBeforeSimulationStep.Enqueue((time) =>
                {
                    dome();
                });
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is active (awake).
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is active; otherwise, <c>false</c>.
        /// </value>
        [DataMemberIgnore]
        public bool IsActive
        {
            get
            {
                return AddedToScene && (PhysicsSystem.IsSimulationThread(Thread.CurrentThread) ? InternalBody.Awake : wasAwake);
            }
            set
            {
                if (IsActive == value) return;

                wasAwake = value;
                SafeRun(delegate { InternalBody.Awake = value; });
            }
        }

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
                if (bodyDescription.Collidable.Continuity.SweepConvergenceThreshold == value) return;

                bodyDescription.Collidable.Continuity.SweepConvergenceThreshold = value;
                bodyDescription.Collidable.Continuity.Mode = value > 0 ? ContinuousDetectionMode.Continuous : ContinuousDetectionMode.Discrete;
                bodyDescription.Collidable.Continuity.MinimumSweepTimestep = value > 0 ? 1e-3f : 0f;

                if (AddedToScene)
                {
                    SafeRun(delegate { InternalBody.Collidable.Continuity = bodyDescription.Collidable.Continuity; });
                }
            }
        }

        /// <summary>
        /// If we are collecting collisions, how many to store before we stop storing them? Defaults to 32. Prevents crazy counts when objects are heavily overlapping.
        /// </summary>
        [DataMember]
        public int CollectCollisionMaximumCount
        {
            get
            {
                return CurrentPhysicalContacts == null ? 0 : CurrentPhysicalContacts.Length;
            }
            set
            {
                if (value <= 0)
                {
                    CurrentPhysicalContacts = null;
                    return;
                }

                CurrentPhysicalContacts = new BepuContact[value];
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
                if (_collectCollisions == value) return;

                if (value)
                {
                    if (CurrentPhysicalContacts == null)
                        CurrentPhysicalContacts = new BepuContact[32];
                }
                else
                {
                    CurrentPhysicalContacts = null;
                }

                _collectCollisions = value;
            }
        }

        [DataMemberIgnore]
        private bool _collectCollisions = false;

        internal void resetProcessingContactsList()
        {
            if (CurrentPhysicalContacts == null || IsActive == false) return;

            CurrentPhysicalContactsCount = Math.Min(CurrentPhysicalContacts.Length, processingPhysicalContactCount);
            processingPhysicalContactCount = 0;
        }

        [DataMemberIgnore]
        public BepuContact[] CurrentPhysicalContacts;

        [DataMemberIgnore]
        public int CurrentPhysicalContactsCount;

        [DataMemberIgnore]
        internal int processingPhysicalContactCount;

        private static readonly BodyInertia KinematicInertia = new BodyInertia()
        {
            InverseMass = 0f,
            InverseInertiaTensor = default
        };

        private RigidBodyTypes type = RigidBodyTypes.Dynamic;
        private Vector3 gravity = Vector3.Zero;

        public BepuRigidbodyComponent() : base()
        {
            bodyDescription = new BodyDescription();
            bodyDescription.Pose.Orientation.W = 1f;
            bodyDescription.LocalInertia.InverseMass = 1f;
            bodyDescription.Activity.MinimumTimestepCountUnderThreshold = 32;
            bodyDescription.Activity.SleepThreshold = 0.01f;
        }

        public override TypedIndex ShapeIndex { get => bodyDescription.Collidable.Shape; }

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
                if (bodyDescription.Activity.SleepThreshold == value) return;

                bodyDescription.Activity.SleepThreshold = value;

                if (AddedToScene)
                {
                    SafeRun(delegate { InternalBody.Activity.SleepThreshold = value; });
                }
            }
        }

        /// <summary>
        /// Forcefully cap the velocity of this rigidbody to this magnitude. Can prevent weird issues without the need of continuous collision detection.
        /// </summary>
        [DataMember]
        public float MaximumSpeed = 0f;

        /// <summary>
        /// Prevent this rigidbody from rotating or falling over?
        /// </summary>
        [DataMember]
        public bool RotationLock
        {
            get
            {
                return _rotationLock;
            }
            set
            {
                if (_rotationLock == value) return;

                _rotationLock = value;

                UpdateInertia();
            }
        }

        private bool _rotationLock = false;

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

        private void UpdateInertia(bool skipset = false)
        {
            if (type == RigidBodyTypes.Kinematic)
            {
                bodyDescription.LocalInertia = KinematicInertia;
            }
            else if (ColliderShape is IConvexShape ics && !_rotationLock)
            { 
                ics.ComputeInertia(mass, out bodyDescription.LocalInertia);
            }
            else if (_rotationLock)
            {
                bodyDescription.LocalInertia.InverseMass = 1f / mass;
                bodyDescription.LocalInertia.InverseInertiaTensor = default;
            }
            else if (ColliderShape is BepuPhysics.Collidables.Mesh m)
            {
                m.ComputeInertia(mass, out bodyDescription.LocalInertia);
            }

            if (skipset == false && AddedToScene)
            {
                SafeRun(delegate { InternalBody.LocalInertia = bodyDescription.LocalInertia; });
            }
        }

        [DataMember]
        public override float SpeculativeMargin
        {
            get => bodyDescription.Collidable.SpeculativeMargin;
            set
            {
                if (bodyDescription.Collidable.SpeculativeMargin == value) return;

                bodyDescription.Collidable.SpeculativeMargin = value;

                if (AddedToScene)
                {
                    SafeRun(delegate { InternalBody.Collidable.SpeculativeMargin = value; });
                }
            }
        }

        [DataMemberIgnore]
        public override IShape ColliderShape
        {
            get => base.ColliderShape;
            set
            {
                if (value == null || value == ColliderShape) return;

                if (AddedToScene && ColliderShape != null)
                {
                    base.ColliderShape = value;

                    UpdateInertia(true);

                    SafeRun(delegate {
                        BepuSimulation bs = BepuSimulation.instance;

                        // don't worry about switching if we are to be removed
                        if (bs.ToBeRemoved.Contains(this))
                            return;

                        using (bs.simulationLocker.WriteLock())
                        {
                            // remove me with the old shape
                            bs.internalSimulation.Bodies.Remove(AddedHandle);
                            BepuSimulation.RigidMappings.Remove(AddedHandle);

                            // add me with the new shape
                            bodyDescription.Collidable = ColliderShape.GenerateDescription(bs.internalSimulation, SpeculativeMargin);
                            AddedHandle = bs.internalSimulation.Bodies.Add(bodyDescription);
                            BepuSimulation.RigidMappings[AddedHandle] = this;
                        }
                    });
                }
                else
                {
                    base.ColliderShape = value;

                    UpdateInertia();
                }
            }
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
                if (type == value) return;

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
                        BepuSimulation.instance.ToBeRemoved.Add(this);
                        BepuSimulation.instance.ToBeAdded.Remove(this);
                    }
                }
            }
        }

        /// <summary>
        /// Applies the impulse.
        /// </summary>
        /// <param name="impulse">The impulse.</param>
        public void ApplyImpulse(Vector3 impulse)
        {
            if (AddedToScene)
                SafeRun(delegate { InternalBody.ApplyLinearImpulse(BepuHelpers.ToBepu(impulse)); });
        }

        /// <summary>
        /// Applies the impulse.
        /// </summary>
        /// <param name="impulse">The impulse.</param>
        /// <param name="localOffset">The local offset.</param>
        public void ApplyImpulse(Vector3 impulse, Vector3 localOffset)
        {
            if (AddedToScene)
                SafeRun(delegate { InternalBody.ApplyImpulse(BepuHelpers.ToBepu(impulse), BepuHelpers.ToBepu(localOffset)); });
        }

        /// <summary>
        /// Applies the torque impulse.
        /// </summary>
        /// <param name="torque">The torque.</param>
        public void ApplyTorqueImpulse(Vector3 torque)
        {
            if (AddedToScene)
                SafeRun(delegate { InternalBody.ApplyAngularImpulse(BepuHelpers.ToBepu(torque)); });
        }

        [DataMemberIgnore]
        public override Vector3 Position
        {
            get
            {
                return BepuHelpers.ToStride(AddedToScene && PhysicsSystem.IsSimulationThread(Thread.CurrentThread) ? InternalBody.Pose.Position : bodyDescription.Pose.Position);
            }
            set
            {
                if (bodyDescription.Pose.Position == BepuHelpers.ToBepu(value)) return;

                bodyDescription.Pose.Position.X = value.X;
                bodyDescription.Pose.Position.Y = value.Y;
                bodyDescription.Pose.Position.Z = value.Z;

                if (AddedToScene)
                    SafeRun(delegate { 
                        InternalBody.Pose.Position.X = value.X;
                        InternalBody.Pose.Position.Y = value.Y;
                        InternalBody.Pose.Position.Z = value.Z;
                    });
            }
        }

        [DataMemberIgnore]
        public override Stride.Core.Mathematics.Quaternion Rotation
        {
            get
            {
                return BepuHelpers.ToStride(AddedToScene && PhysicsSystem.IsSimulationThread(Thread.CurrentThread) ? InternalBody.Pose.Orientation : bodyDescription.Pose.Orientation);
            }
            set
            {
                if (bodyDescription.Pose.Orientation.X == value.X &&
                    bodyDescription.Pose.Orientation.Y == value.Y &&
                    bodyDescription.Pose.Orientation.Z == value.Z &&
                    bodyDescription.Pose.Orientation.W == value.W) return;

                bodyDescription.Pose.Orientation.X = value.X;
                bodyDescription.Pose.Orientation.Y = value.Y;
                bodyDescription.Pose.Orientation.Z = value.Z;
                bodyDescription.Pose.Orientation.W = value.W;

                if (AddedToScene)
                    SafeRun(delegate {
                        InternalBody.Pose.Orientation.X = value.X;
                        InternalBody.Pose.Orientation.Y = value.Y;
                        InternalBody.Pose.Orientation.Z = value.Z;
                        InternalBody.Pose.Orientation.W = value.W;
                    });
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
                return BepuHelpers.ToStride(AddedToScene && PhysicsSystem.IsSimulationThread(Thread.CurrentThread) ? InternalBody.Velocity.Angular : bodyDescription.Velocity.Angular);
            }
            set
            {
                if (bodyDescription.Velocity.Angular == BepuHelpers.ToBepu(value)) return;

                bodyDescription.Velocity.Angular.X = value.X;
                bodyDescription.Velocity.Angular.Y = value.Y;
                bodyDescription.Velocity.Angular.Z = value.Z;

                if (AddedToScene)
                    SafeRun(delegate {
                        InternalBody.Velocity.Angular.X = value.X;
                        InternalBody.Velocity.Angular.Y = value.Y;
                        InternalBody.Velocity.Angular.Z = value.Z;
                    });
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
                return BepuHelpers.ToStride(AddedToScene && PhysicsSystem.IsSimulationThread(Thread.CurrentThread) ? InternalBody.Velocity.Linear : bodyDescription.Velocity.Linear);
            }
            set
            {
                if (bodyDescription.Velocity.Linear == BepuHelpers.ToBepu(value)) return;

                bodyDescription.Velocity.Linear.X = value.X;
                bodyDescription.Velocity.Linear.Y = value.Y;
                bodyDescription.Velocity.Linear.Z = value.Z;

                if (AddedToScene)
                    SafeRun(delegate {
                        InternalBody.Velocity.Linear.X = value.X;
                        InternalBody.Velocity.Linear.Y = value.Y;
                        InternalBody.Velocity.Linear.Z = value.Z;
                    });
            }
        }

        /// <summary>
        /// When updating the associated TransformComponent, should we not set rotation?
        /// </summary>
        [DataMember(69)]
        public bool IgnorePhysicsRotation = false;

        [DataMemberIgnore]
        public Vector3? LocalPhysicsOffset = null;

        [DataMemberIgnore]
        public Vector3 VelocityLinearChange { get; private set; }
            
        [DataMemberIgnore]
        public Vector3 VelocityAngularChange { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateCachedPoseAndVelocity()
        {
            ref BodyVelocity bv = ref InternalBody.Velocity;
            VelocityLinearChange = BepuHelpers.ToStride(bv.Linear - bodyDescription.Velocity.Linear);
            VelocityAngularChange = BepuHelpers.ToStride(bv.Angular - bodyDescription.Velocity.Angular);
            bodyDescription.Velocity = bv;
            bodyDescription.Pose = InternalBody.Pose;
            wasAwake = InternalBody.Awake;
        }

        /// <summary>
        /// Updades the graphics transformation from the given physics transformation
        /// </summary>
        /// <param name="physicsTransform"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateTransformationComponent()
        {
            var entity = Entity;

            entity.Transform.Position = Position;
            if (LocalPhysicsOffset.HasValue) entity.Transform.Position += LocalPhysicsOffset.Value;
            if (IgnorePhysicsRotation == false) entity.Transform.Rotation = Rotation;
        }
    }
}
