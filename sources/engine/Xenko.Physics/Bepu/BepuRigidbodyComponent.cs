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
using System.Runtime.CompilerServices;
using Xenko.Core.Threading;
using System.Threading;
using System.Collections.Concurrent;
using BulletSharp.SoftBody;
using System.Collections;

namespace Xenko.Physics.Bepu
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

        [DataMemberIgnore]
        public BodyHandle myBodyHandle;

        public override int HandleIndex => myBodyHandle.Value;

        /// <summary>
        /// Reference to the body after being added to the scene
        /// </summary>
        public ref BodyReference InternalBody
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                _internalReference.Bodies = BepuSimulation.instance.internalSimulation.Bodies;
                _internalReference.Handle = myBodyHandle;

                return ref _internalReference;
            }
        }

        /// <summary>
        /// Action to be called after simulation, but before transforms are set to new positions. Arguments are this and simulation time.
        /// </summary>
        [DataMemberIgnore]
        public Action<BepuRigidbodyComponent, float> ActionPerSimulationTick;

        private enum ACTION_TYPE
        {
            IsActive,
            CcdMotionThreshold,
            SleepThreshold,
            UpdateInertia,
            SpeculativeMargin,
            ColliderShape,
            ApplyImpulse,
            ApplyImpulseOffset,
            ApplyTorqueImpulse,
            Position,
            Rotation,
            AngularVelocity,
            LinearVelocity
        }

        internal bool[] NeedsAction = new bool[13];
        internal object[] ActionObject = new object[13];

        internal void UpdateAsyncState()
        {
            for (int i=0; i<NeedsAction.Length; i++)
            {
                if (NeedsAction[i])
                {
                    NeedsAction[i] = false;
                    switch ((ACTION_TYPE)i)
                    {
                        case ACTION_TYPE.IsActive:
                            InternalBody.Awake = (bool)ActionObject[i];
                            break;
                        case ACTION_TYPE.AngularVelocity:
                            InternalBody.Velocity.Angular = (System.Numerics.Vector3)ActionObject[i];
                            break;
                        case ACTION_TYPE.ApplyImpulse:
                            lock (ActionObject[i])
                            {
                                InternalBody.ApplyLinearImpulse((System.Numerics.Vector3)ActionObject[i]);
                                ActionObject[i] = System.Numerics.Vector3.Zero;
                            }
                            break;
                        case ACTION_TYPE.ApplyImpulseOffset:
                            lock (impulsePairs)
                            {
                                while (impulsePairs.Count > 0)
                                {
                                    var pair = impulsePairs.Dequeue();
                                    InternalBody.ApplyImpulse(pair[0], pair[1]);
                                }
                            }
                            break;
                        case ACTION_TYPE.ApplyTorqueImpulse:
                            lock (ActionObject[i])
                            {
                                InternalBody.ApplyAngularImpulse((System.Numerics.Vector3)ActionObject[i]);
                                ActionObject[i] = System.Numerics.Vector3.Zero;
                            }
                            break;
                        case ACTION_TYPE.CcdMotionThreshold:
                            InternalBody.Collidable.Continuity = (ContinuousDetectionSettings)ActionObject[i];
                            break;
                        case ACTION_TYPE.ColliderShape:
                            ColliderShapeInternalSwitching();
                            break;
                        case ACTION_TYPE.LinearVelocity:
                            InternalBody.Velocity.Linear = (System.Numerics.Vector3)ActionObject[i];
                            break;
                        case ACTION_TYPE.Position:
                            InternalBody.Pose.Position = (System.Numerics.Vector3)ActionObject[i];
                            break;
                        case ACTION_TYPE.Rotation:
                            InternalBody.Pose.Orientation = (System.Numerics.Quaternion)ActionObject[i];
                            break;
                        case ACTION_TYPE.SleepThreshold:
                            InternalBody.Activity.SleepThreshold = (float)ActionObject[i];
                            break;
                        case ACTION_TYPE.SpeculativeMargin:
                            InternalBody.Collidable.SpeculativeMargin = (float)ActionObject[i];
                            break;
                        case ACTION_TYPE.UpdateInertia:
                            InternalBody.LocalInertia = (BodyInertia)ActionObject[i];
                            break;
                    }
                }
            }
        }

        internal bool wasAwake;

        // are we safe to make changes to rigidbodies (e.g. not simulating)
        internal static volatile bool safeRun;

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

                if (safeRun)
                {
                    InternalBody.Awake = value;
                }
                else
                {
                    ActionObject[(int)ACTION_TYPE.IsActive] = value;
                    NeedsAction[(int)ACTION_TYPE.IsActive] = true;
                }
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
                    if (safeRun)
                    {
                        InternalBody.Collidable.Continuity = bodyDescription.Collidable.Continuity;
                    }
                    else
                    {
                        ActionObject[(int)ACTION_TYPE.CcdMotionThreshold] = bodyDescription.Collidable.Continuity;
                        NeedsAction[(int)ACTION_TYPE.CcdMotionThreshold] = true;
                    }
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
                    _collectCollisions = false;
                    CurrentPhysicalContacts = null;
                    return;
                }

                if (CurrentPhysicalContacts == null || CurrentPhysicalContacts.Length != value)
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
            bodyDescription.Pose.Orientation.W = 1f;
            bodyDescription.LocalInertia.InverseMass = 1f;
            bodyDescription.Activity.MinimumTimestepCountUnderThreshold = 32;
            bodyDescription.Activity.SleepThreshold = 0.01f;
            myBodyHandle.Value = -1;
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
                    if (safeRun)
                    {
                        InternalBody.Activity.SleepThreshold = value;
                    }
                    else
                    {
                        ActionObject[(int)ACTION_TYPE.SleepThreshold] = bodyDescription.Activity.SleepThreshold;
                        NeedsAction[(int)ACTION_TYPE.SleepThreshold] = true;
                    }
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
                if (safeRun)
                {
                    InternalBody.LocalInertia = bodyDescription.LocalInertia;
                }
                else
                {
                    ActionObject[(int)ACTION_TYPE.UpdateInertia] = bodyDescription.LocalInertia;
                    NeedsAction[(int)ACTION_TYPE.UpdateInertia] = true;
                }
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
                    if (safeRun)
                    {
                        InternalBody.Collidable.SpeculativeMargin = value;
                    }
                    else
                    {
                        ActionObject[(int)ACTION_TYPE.SpeculativeMargin] = bodyDescription.Collidable.SpeculativeMargin;
                        NeedsAction[(int)ACTION_TYPE.SpeculativeMargin] = true;
                    }
                }
            }
        }

        internal void ColliderShapeInternalSwitching()
        {
            BepuSimulation bs = BepuSimulation.instance;

            // don't worry about switching if we are to be removed
            if (bs.ToBeRemoved.Contains(this))
                return;

            using (bs.simulationLocker.WriteLock())
            {
                // remove me with the old shape
                bs.internalSimulation.Bodies.Remove(myBodyHandle);
                BepuSimulation.RigidMappings.Remove(myBodyHandle.Value);

                // add me with the new shape
                bodyDescription.Collidable = ColliderShape.GenerateDescription(bs.internalSimulation, SpeculativeMargin);
                myBodyHandle = bs.internalSimulation.Bodies.Add(bodyDescription);
                BepuSimulation.RigidMappings[myBodyHandle.Value] = this;
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

                    if (safeRun)
                    {
                        ColliderShapeInternalSwitching();
                    }
                    else
                    {
                        NeedsAction[(int)ACTION_TYPE.ColliderShape] = true;
                    }
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
                return myBodyHandle.Value != -1; 
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
            {
                if (safeRun)
                {
                    InternalBody.ApplyLinearImpulse(BepuHelpers.ToBepu(impulse));
                }
                else
                {
                    lock (ActionObject[(int)ACTION_TYPE.ApplyImpulse])
                    {
                        ActionObject[(int)ACTION_TYPE.ApplyImpulse] = (System.Numerics.Vector3)ActionObject[(int)ACTION_TYPE.ApplyImpulse] + BepuHelpers.ToBepu(impulse);
                        NeedsAction[(int)ACTION_TYPE.ApplyImpulse] = true;
                    }
                }
            }
        }

        private Queue<System.Numerics.Vector3[]> impulsePairs = new Queue<System.Numerics.Vector3[]>();

        /// <summary>
        /// Applies the impulse.
        /// </summary>
        /// <param name="impulse">The impulse.</param>
        /// <param name="localOffset">The local offset.</param>
        public void ApplyImpulse(Vector3 impulse, Vector3 localOffset)
        {
            if (AddedToScene)
            {
                if (safeRun)
                {
                    InternalBody.ApplyImpulse(BepuHelpers.ToBepu(impulse), BepuHelpers.ToBepu(localOffset));
                }
                else
                {
                    lock (impulsePairs)
                    {
                        impulsePairs.Enqueue(new System.Numerics.Vector3[] { BepuHelpers.ToBepu(impulse), BepuHelpers.ToBepu(localOffset) });
                        NeedsAction[(int)ACTION_TYPE.ApplyImpulseOffset] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Applies the torque impulse.
        /// </summary>
        /// <param name="torque">The torque.</param>
        public void ApplyTorqueImpulse(Vector3 torque)
        {
            if (AddedToScene)
            {
                if (safeRun)
                {
                    InternalBody.ApplyAngularImpulse(BepuHelpers.ToBepu(torque));
                }
                else
                {
                    lock (ActionObject[(int)ACTION_TYPE.ApplyTorqueImpulse])
                    {
                        ActionObject[(int)ACTION_TYPE.ApplyTorqueImpulse] = (System.Numerics.Vector3)ActionObject[(int)ACTION_TYPE.ApplyTorqueImpulse] + BepuHelpers.ToBepu(torque);
                        NeedsAction[(int)ACTION_TYPE.ApplyTorqueImpulse] = true;
                    }
                }
            }
        }

        [DataMemberIgnore]
        public override Vector3 Position
        {
            get
            {
                return BepuHelpers.ToXenko(AddedToScene && PhysicsSystem.IsSimulationThread(Thread.CurrentThread) ? InternalBody.Pose.Position : bodyDescription.Pose.Position);
            }
            set
            {
                if (bodyDescription.Pose.Position == BepuHelpers.ToBepu(value)) return;

                bodyDescription.Pose.Position.X = value.X;
                bodyDescription.Pose.Position.Y = value.Y;
                bodyDescription.Pose.Position.Z = value.Z;

                if (AddedToScene)
                {
                    if (safeRun)
                    {
                        InternalBody.Pose.Position.X = value.X;
                        InternalBody.Pose.Position.Y = value.Y;
                        InternalBody.Pose.Position.Z = value.Z;
                    }
                    else
                    {
                        ActionObject[(int)ACTION_TYPE.Position] = bodyDescription.Pose.Position;
                        NeedsAction[(int)ACTION_TYPE.Position] = true;
                    }
                }
            }
        }

        [DataMemberIgnore]
        public override Xenko.Core.Mathematics.Quaternion Rotation
        {
            get
            {
                return BepuHelpers.ToXenko(AddedToScene && PhysicsSystem.IsSimulationThread(Thread.CurrentThread) ? InternalBody.Pose.Orientation : bodyDescription.Pose.Orientation);
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
                {
                    if (safeRun)
                    {
                        InternalBody.Pose.Orientation.X = value.X;
                        InternalBody.Pose.Orientation.Y = value.Y;
                        InternalBody.Pose.Orientation.Z = value.Z;
                        InternalBody.Pose.Orientation.W = value.W;
                    }
                    else
                    {
                        ActionObject[(int)ACTION_TYPE.Rotation] = bodyDescription.Pose.Orientation;
                        NeedsAction[(int)ACTION_TYPE.Rotation] = true;
                    }
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
                return BepuHelpers.ToXenko(AddedToScene && PhysicsSystem.IsSimulationThread(Thread.CurrentThread) ? InternalBody.Velocity.Angular : bodyDescription.Velocity.Angular);
            }
            set
            {
                if (bodyDescription.Velocity.Angular == BepuHelpers.ToBepu(value)) return;

                bodyDescription.Velocity.Angular.X = value.X;
                bodyDescription.Velocity.Angular.Y = value.Y;
                bodyDescription.Velocity.Angular.Z = value.Z;

                if (AddedToScene)
                {
                    if (safeRun)
                    {
                        InternalBody.Velocity.Angular.X = value.X;
                        InternalBody.Velocity.Angular.Y = value.Y;
                        InternalBody.Velocity.Angular.Z = value.Z;
                    }
                    else
                    {
                        ActionObject[(int)ACTION_TYPE.AngularVelocity] = bodyDescription.Velocity.Angular;
                        NeedsAction[(int)ACTION_TYPE.AngularVelocity] = true;
                    }
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
                return BepuHelpers.ToXenko(AddedToScene && PhysicsSystem.IsSimulationThread(Thread.CurrentThread) ? InternalBody.Velocity.Linear : bodyDescription.Velocity.Linear);
            }
            set
            {
                if (bodyDescription.Velocity.Linear == BepuHelpers.ToBepu(value)) return;

                bodyDescription.Velocity.Linear.X = value.X;
                bodyDescription.Velocity.Linear.Y = value.Y;
                bodyDescription.Velocity.Linear.Z = value.Z;

                if (AddedToScene)
                {
                    if (safeRun)
                    {
                        InternalBody.Velocity.Linear.X = value.X;
                        InternalBody.Velocity.Linear.Y = value.Y;
                        InternalBody.Velocity.Linear.Z = value.Z;
                    }
                    else
                    {
                        ActionObject[(int)ACTION_TYPE.LinearVelocity] = bodyDescription.Velocity.Linear;
                        NeedsAction[(int)ACTION_TYPE.LinearVelocity] = true;
                    }
                }
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
            VelocityLinearChange = BepuHelpers.ToXenko(bv.Linear - bodyDescription.Velocity.Linear);
            VelocityAngularChange = BepuHelpers.ToXenko(bv.Angular - bodyDescription.Velocity.Angular);
            bodyDescription.Velocity = bv;
            bodyDescription.Pose = InternalBody.Pose;
            wasAwake = InternalBody.Awake;
            UpdateAsyncState();
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
