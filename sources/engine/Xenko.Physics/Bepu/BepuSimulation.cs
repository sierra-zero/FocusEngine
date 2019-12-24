// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Diagnostics;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Engine.Design;
using Xenko.Games;
using Xenko.Physics.Engine;
using Xenko.Rendering;

namespace Xenko.Physics.Bepu
{
    public class BepuSimulation : IDisposable
    {
        private const CollisionFilterGroups DefaultGroup = CollisionFilterGroups.DefaultFilter;
        private const CollisionFilterGroupFlags DefaultFlags = CollisionFilterGroupFlags.AllFilter;

        public BepuPhysics.Simulation internalSimulation;

        internal ConcurrentQueue<BepuPhysicsComponent> ToBeAdded = new ConcurrentQueue<BepuPhysicsComponent>(), ToBeRemoved = new ConcurrentQueue<BepuPhysicsComponent>();

        private static BepuSimulation _instance;
        public static BepuSimulation instance
        {
            get
            {
                if (_instance == null) BepuHelpers.AssureBepuSystemCreated();
                return _instance;
            }
            private set
            {
                _instance = value;
            }
        }

        private PoseIntegratorCallbacks poseCallbacks;
        internal BufferPool pBufferPool;
        internal int clearRequested;
        private BepuSimpleThreadDispatcher threadDispatcher = new BepuSimpleThreadDispatcher(Environment.ProcessorCount);

#if DEBUG
        private static readonly Logger Log = GlobalLogger.GetLogger(typeof(Simulation).FullName);
#endif

        /// <summary>
        /// Totally disable the simulation if set to true
        /// </summary>
        public static bool DisableSimulation = false;

        internal static Dictionary<int, BepuStaticColliderComponent> StaticMappings = new Dictionary<int, BepuStaticColliderComponent>();
        internal static Dictionary<int, BepuRigidbodyComponent> RigidMappings = new Dictionary<int, BepuRigidbodyComponent>();

        internal List<BepuRigidbodyComponent> AllRigidbodies = new List<BepuRigidbodyComponent>();

        /// <summary>
        /// Clears out the whole simulation of bodies, optionally clears all related buffers (like meshes) too
        /// </summary>
        /// <param name="clearBuffers">Clear out all buffers, like mesh shapes? Defaults to true</param>
        /// <param name="forceRightNow">Clear everything right now. Might cause crashes if simulation is happening at the same time!</param>
        public void Clear(bool clearBuffers = true, bool forceRightNow = false)
        {
            if (forceRightNow)
            {
                clearRequested = 0;

                internalSimulation.Clear();

                if (clearBuffers) pBufferPool.Clear();

                for (int i = 0; i < AllRigidbodies.Count; i++)
                    AllRigidbodies[i].AddedHandle = -1;
                foreach (BepuStaticColliderComponent sc in StaticMappings.Values)
                    sc.AddedHandle = -1;

                StaticMappings.Clear();
                RigidMappings.Clear();
                AllRigidbodies.Clear();

                return;
            }

            clearRequested = clearBuffers ? 2 : 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static BepuRigidbodyComponent getRigidFromIndex(int index)
        {
            return RigidMappings[instance.internalSimulation.Bodies.ActiveSet.IndexToHandle[index]];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BepuPhysicsComponent getFromHandle(int handle, CollidableMobility mobility)
        {
            return mobility == CollidableMobility.Static ? (BepuPhysicsComponent)StaticMappings[handle] : (BepuPhysicsComponent)RigidMappings[handle];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BepuPhysicsComponent getFromReference(CollidableReference cref)
        {
            return cref.Mobility == CollidableMobility.Static ? (BepuPhysicsComponent)StaticMappings[cref.Handle] : (BepuPhysicsComponent)RigidMappings[cref.Handle];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool CanCollide(int source, CollidableMobility sourcemob, int target, CollidableMobility targetmob)
        {
            return ((uint)getFromHandle(source, sourcemob).CollisionGroup & (uint)getFromHandle(target, targetmob).CanCollideWith) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool CanCollide(CollidableReference a, CollidableReference b)
        {
            return ((uint)getFromReference(a).CollisionGroup & (uint)getFromReference(b).CanCollideWith) != 0;
        }

        unsafe struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
        {
            /// <summary>
            /// Performs any required initialization logic after the Simulation instance has been constructed.
            /// </summary>
            /// <param name="simulation">Simulation that owns these callbacks.</param>
            public void Initialize(BepuPhysics.Simulation simulation)
            {
                //Often, the callbacks type is created before the simulation instance is fully constructed, so the simulation will call this function when it's ready.
                //Any logic which depends on the simulation existing can be put here.
            }

            /// <summary>
            /// Chooses whether to allow contact generation to proceed for two overlapping collidables.
            /// </summary>
            /// <param name="workerIndex">Index of the worker that identified the overlap.</param>
            /// <param name="a">Reference to the first collidable in the pair.</param>
            /// <param name="b">Reference to the second collidable in the pair.</param>
            /// <returns>True if collision detection should proceed, false otherwise.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b)
            {
                //Before creating a narrow phase pair, the broad phase asks this callback whether to bother with a given pair of objects.
                //This can be used to implement arbitrary forms of collision filtering. See the RagdollDemo or NewtDemo for examples.
                //Here, we'll make sure at least one of the two bodies is dynamic.
                //The engine won't generate static-static pairs, but it will generate kinematic-kinematic pairs.
                //That's useful if you're trying to make some sort of sensor/trigger object, but since kinematic-kinematic pairs
                //can't generate constraints (both bodies have infinite inertia), simple simulations can just ignore such pairs.
                return (a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic) && CanCollide(a, b);
            }

            /// <summary>
            /// Chooses whether to allow contact generation to proceed for the children of two overlapping collidables in a compound-including pair.
            /// </summary>
            /// <param name="pair">Parent pair of the two child collidables.</param>
            /// <param name="childIndexA">Index of the child of collidable A in the pair. If collidable A is not compound, then this is always 0.</param>
            /// <param name="childIndexB">Index of the child of collidable B in the pair. If collidable B is not compound, then this is always 0.</param>
            /// <returns>True if collision detection should proceed, false otherwise.</returns>
            /// <remarks>This is called for each sub-overlap in a collidable pair involving compound collidables. If neither collidable in a pair is compound, this will not be called.
            /// For compound-including pairs, if the earlier call to AllowContactGeneration returns false for owning pair, this will not be called. Note that it is possible
            /// for this function to be called twice for the same subpair if the pair has continuous collision detection enabled; 
            /// the CCD sweep test that runs before the contact generation test also asks before performing child pair tests.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
            {
                //This is similar to the top level broad phase callback above. It's called by the narrow phase before generating
                //subpairs between children in parent shapes. 
                //This only gets called in pairs that involve at least one shape type that can contain multiple children, like a Compound.
                return CanCollide(pair.A, pair.B);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RecordContact<TManifold>(BepuPhysicsComponent A, BepuPhysicsComponent B, TManifold manifold) where TManifold : struct, IContactManifold<TManifold>
            {
                // sanity checking
                if (A == null || B == null || manifold.Count == 0) return;

                BepuRigidbodyComponent ar = (A as BepuRigidbodyComponent);
                BepuRigidbodyComponent br = (B as BepuRigidbodyComponent);
                bool Acollect = ar == null ? false : ar.CollectCollisions && ar.CollectCollisionMaximumCount > ar.processingPhysicalContacts[ar.processingPhysicalContactsIndex].Count;
                bool Bcollect = br == null ? false : br.CollectCollisions && br.CollectCollisionMaximumCount > br.processingPhysicalContacts[br.processingPhysicalContactsIndex].Count;
                // do we want to store this collision?
                if (Acollect || Bcollect)
                {
                    BepuContact bc = new BepuContact()
                    {
                        A = A,
                        B = B,
                        Normal = BepuHelpers.ToXenko(manifold.SimpleGetNormal()),
                        Position = B.Position - BepuHelpers.ToXenko(manifold.SimpleGetOffset())
                    };
                    if (Acollect) ar.processingPhysicalContacts[ar.processingPhysicalContactsIndex].Add(bc);
                    if (Bcollect) br.processingPhysicalContacts[br.processingPhysicalContactsIndex].Add(bc);
                }
            }

            /// <summary>
            /// Provides a notification that a manifold has been created for a pair. Offers an opportunity to change the manifold's details. 
            /// </summary>
            /// <param name="workerIndex">Index of the worker thread that created this manifold.</param>
            /// <param name="pair">Pair of collidables that the manifold was detected between.</param>
            /// <param name="manifold">Set of contacts detected between the collidables.</param>
            /// <param name="pairMaterial">Material properties of the manifold.</param>
            /// <returns>True if a constraint should be created for the manifold, false otherwise.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            {
                BepuPhysicsComponent a = getFromReference(pair.A);
                BepuPhysicsComponent b = getFromReference(pair.B);
                pairMaterial.FrictionCoefficient = a.FrictionCoefficient * b.FrictionCoefficient;
                pairMaterial.MaximumRecoveryVelocity = a.MaximumRecoveryVelocity * b.MaximumRecoveryVelocity;
                pairMaterial.SpringSettings = a.SpringSettings;
                if (((uint)a.CanCollideWith & (uint)b.CollisionGroup) != 0)
                {
                    RecordContact(a, b, manifold);
                    return !a.GhostBody && !b.GhostBody;
                }
                return false;
            }

            /// <summary>
            /// Provides a notification that a manifold has been created between the children of two collidables in a compound-including pair.
            /// Offers an opportunity to change the manifold's details. 
            /// </summary>
            /// <param name="workerIndex">Index of the worker thread that created this manifold.</param>
            /// <param name="pair">Pair of collidables that the manifold was detected between.</param>
            /// <param name="childIndexA">Index of the child of collidable A in the pair. If collidable A is not compound, then this is always 0.</param>
            /// <param name="childIndexB">Index of the child of collidable B in the pair. If collidable B is not compound, then this is always 0.</param>
            /// <param name="manifold">Set of contacts detected between the collidables.</param>
            /// <returns>True if this manifold should be considered for constraint generation, false otherwise.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
            {
                BepuPhysicsComponent A = getFromReference(pair.A);
                BepuPhysicsComponent B = getFromReference(pair.B);
                if (((uint)A.CanCollideWith & (uint)B.CollisionGroup) != 0)
                {
                    RecordContact(A, B, manifold);
                    return !A.GhostBody && !B.GhostBody;
                }
                return false;
            }

            /// <summary>
            /// Releases any resources held by the callbacks. Called by the owning narrow phase when it is being disposed.
            /// </summary>
            public void Dispose()
            {
            }
        }

        //Note that the engine does not require any particular form of gravity- it, like all the contact callbacks, is managed by a callback.
        public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
        {
            System.Numerics.Vector3 gravityDt;
            public System.Numerics.Vector3 Gravity;
            float indt;

            /// <summary>
            /// Gets how the pose integrator should handle angular velocity integration.
            /// </summary>
            public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving; //Don't care about fidelity in this demo!

            public PoseIntegratorCallbacks(System.Numerics.Vector3 gravity) : this()
            {
                Gravity = gravity;
            }

            /// <summary>
            /// Called prior to integrating the simulation's active bodies. When used with a substepping timestepper, this could be called multiple times per frame with different time step values.
            /// </summary>
            /// <param name="dt">Current time step duration.</param>
            public void PrepareForIntegration(float dt)
            {
                //No reason to recalculate gravity * dt for every body; just cache it ahead of time.
                gravityDt = Gravity * dt;
                indt = dt;
            }

            /// <summary>
            /// Callback called for each active body within the simulation during body integration.
            /// </summary>
            /// <param name="bodyIndex">Index of the body being visited.</param>
            /// <param name="pose">Body's current pose.</param>
            /// <param name="localInertia">Body's current local inertia.</param>
            /// <param name="workerIndex">Index of the worker thread processing this body.</param>
            /// <param name="velocity">Reference to the body's current velocity to integrate.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IntegrateVelocity(int bodyIndex, in RigidPose pose, in BodyInertia localInertia, int workerIndex, ref BodyVelocity velocity)
            {
                //Note that we avoid accelerating kinematics. Kinematics are any body with an inverse mass of zero (so a mass of ~infinity). No force can move them.
                if (localInertia.InverseMass > 0)
                {
                    BepuRigidbodyComponent rb = BepuSimulation.getRigidFromIndex(bodyIndex) as BepuRigidbodyComponent;

                    velocity.Linear += rb.OverrideGravity ? BepuHelpers.ToBepu(rb.Gravity) * indt : gravityDt;

                    // damping?
                    if (rb.LinearDamping > 0f)
                        velocity.Linear -= velocity.Linear * indt * rb.LinearDamping;

                    if (rb.AngularDamping > 0f)
                        velocity.Angular -= velocity.Angular * indt * rb.AngularDamping;
                }
            }
        }

        /// <summary>
        /// Initializes the Physics engine using the specified flags.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="configuration"></param>
        /// <exception cref="System.NotImplementedException">SoftBody processing is not yet available</exception>
        internal BepuSimulation(PhysicsSettings configuration)
        {
            Game GameService = ServiceRegistry.instance.GetService<IGame>() as Game;
            GameService.SceneSystem.SceneInstance.EntityAdded += EntityAdded;
            GameService.SceneSystem.SceneInstance.EntityRemoved += EntityRemoved;
            GameService.SceneSystem.SceneInstance.ComponentChanged += ComponentChanged;

            MaxSubSteps = configuration.MaxSubSteps;
            FixedTimeStep = configuration.FixedTimeStep;

            pBufferPool = new BufferPool();
            poseCallbacks = new PoseIntegratorCallbacks(new System.Numerics.Vector3(0f, -9.81f, 0f));
            internalSimulation = BepuPhysics.Simulation.Create(pBufferPool, new NarrowPhaseCallbacks(), poseCallbacks);
            instance = this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EntityAdded(object sender, Entity e)
        {
            foreach (BepuPhysicsComponent bpc in e.GetAll<BepuPhysicsComponent>())
                if (bpc.AutomaticAdd) bpc.AddedToScene = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EntityRemoved(object sender, Entity e)
        {
            foreach (BepuPhysicsComponent bpc in e.GetAll<BepuPhysicsComponent>())
                bpc.AddedToScene = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ComponentChanged(object sender, EntityComponentEventArgs e)
        {
            if (e.PreviousComponent is BepuPhysicsComponent rem) rem.AddedToScene = false;
            if (e.NewComponent is BepuPhysicsComponent add && add.AutomaticAdd) add.AddedToScene = true;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Clear(true, true);
        }

        /// <summary>
        /// Free up memory used to store triangles for a mesh collision shape
        /// </summary>
        /// <param name="m"></param>
        public void DisposeMesh(BepuPhysics.Collidables.Mesh m)
        {
            m.Dispose(pBufferPool);
        }

        public RenderGroup ColliderShapesRenderGroup { get; set; } = RenderGroup.Group0;

        internal void ProcessAdds()
        {
            while (ToBeAdded.Count > 0)
            {
                if (ToBeAdded.TryDequeue(out var component))
                {
                    if (component.AddedHandle > -1) continue; // already added
                    if (component is BepuStaticColliderComponent scc)
                    {
                        scc.staticDescription.Collidable = scc.ColliderShape.GenerateDescription(internalSimulation);
                        scc.AddedHandle = internalSimulation.Statics.Add(scc.staticDescription);
                        StaticMappings[scc.AddedHandle] = scc;
                    }
                    else if (component is BepuRigidbodyComponent rigidBody)
                    {
                        rigidBody.ColliderShape.ComputeInertia(rigidBody.Mass, out rigidBody.bodyDescription.LocalInertia);
                        rigidBody.bodyDescription.Collidable = rigidBody.ColliderShape.GenerateDescription(internalSimulation);
                        AllRigidbodies.Add(rigidBody);
                        rigidBody.AddedHandle = internalSimulation.Bodies.Add(rigidBody.bodyDescription);
                        RigidMappings[rigidBody.AddedHandle] = rigidBody;
                        rigidBody.SleepThreshold = rigidBody.bodyDescription.Activity.SleepThreshold;
                    }
                    component.Position = component.Entity.Transform.WorldPosition();
                    component.Rotation = component.Entity.Transform.WorldRotation();
                }   
            }
        }

        internal void ProcessRemovals()
        {
            while (ToBeRemoved.Count > 0)
            {
                if (ToBeRemoved.TryDequeue(out var component))
                {
                    int addedIndex = component.AddedHandle;
                    if (addedIndex == -1) continue; // already removed
                    if (component is BepuStaticColliderComponent scc)
                    {
                        scc.AddedHandle = -1;
                        internalSimulation.Statics.Remove(addedIndex);
                        StaticMappings.Remove(addedIndex);
                    } 
                    else if (component is BepuRigidbodyComponent rigidBody)
                    {
                        rigidBody.AddedHandle = -1;
                        internalSimulation.Bodies.Remove(addedIndex);
                        RigidMappings.Remove(addedIndex);
                        AllRigidbodies.Remove(rigidBody);

                        if (rigidBody.processingPhysicalContacts != null)
                        {
                            rigidBody.processingPhysicalContacts[0].Clear();
                            rigidBody.processingPhysicalContacts[1].Clear();
                        }
                    }
                }
            }
        }

        struct RayHitClosestHandler : IRayHitHandler
        {
            public CollisionFilterGroupFlags findGroups;
            public float furthestHitSoFar, startLength;
            public BepuHitResult HitCollidable;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable)
            {
                return ((uint)getFromReference(collidable).CollisionGroup & (uint)findGroups) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable, int childIndex)
            {
                return ((uint)getFromReference(collidable).CollisionGroup & (uint)findGroups) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnRayHit(in RayData ray, ref float maximumT, float t, in System.Numerics.Vector3 normal, CollidableReference collidable, int childIndex)
            {
                if (t < furthestHitSoFar)
                {
                    //Cache the earliest impact.
                    furthestHitSoFar = t;
                    HitCollidable.HitFraction = t / startLength;
                    HitCollidable.Normal.X = normal.X;
                    HitCollidable.Normal.Y = normal.Y;
                    HitCollidable.Normal.Z = normal.Z;
                    HitCollidable.Point.X = ray.Origin.X + ray.Direction.X * t;
                    HitCollidable.Point.Y = ray.Origin.Y + ray.Direction.Y * t;
                    HitCollidable.Point.Z = ray.Origin.Z + ray.Direction.Z * t;
                    HitCollidable.Collider = getFromReference(collidable);
                    HitCollidable.Succeeded = true;
                }

                //We are only interested in the earliest hit. This callback is executing within the traversal, so modifying maximumT informs the traversal
                //that it can skip any AABBs which are more distant than the new maximumT.
                if (t < maximumT)
                    maximumT = t;
            }
        }

        struct RayHitAllHandler : IRayHitHandler
        {
            public CollisionFilterGroupFlags hitGroups;
            public float startLength;
            public List<BepuHitResult> HitCollidables;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable)
            {
                return ((uint)getFromReference(collidable).CollisionGroup & (uint)hitGroups) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable, int childIndex)
            {
                return ((uint)getFromReference(collidable).CollisionGroup & (uint)hitGroups) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnRayHit(in RayData ray, ref float maximumT, float t, in System.Numerics.Vector3 normal, CollidableReference collidable, int childIndex)
            {
                BepuHitResult HitCollidable = new BepuHitResult();
                HitCollidable.HitFraction = t / startLength;
                HitCollidable.Normal.X = normal.X;
                HitCollidable.Normal.Y = normal.Y;
                HitCollidable.Normal.Z = normal.Z;
                HitCollidable.Point.X = ray.Origin.X + ray.Direction.X * t;
                HitCollidable.Point.Y = ray.Origin.Y + ray.Direction.Y * t;
                HitCollidable.Point.Z = ray.Origin.Z + ray.Direction.Z * t;
                HitCollidable.Collider = getFromReference(collidable);
                HitCollidable.Succeeded = true;
                HitCollidables.Add(HitCollidable);
            }
        }

        /// <summary>
        /// Raycasts and returns the closest hit
        /// </summary>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="filterGroup">The collision group of this raycast</param>
        /// <param name="hitGroups">The collision group that this raycast can collide with</param>
        /// <returns>The list with hit results.</returns>
        public BepuHitResult Raycast(Vector3 from, Vector3 to, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            Vector3 diff = to - from;
            float length = diff.Length();
            float inv = 1.0f / length;
            diff.X *= inv;
            diff.Y *= inv;
            diff.Z *= inv;
            return Raycast(from, diff, length, hitGroups);
        }

        /// <summary>
        /// Raycasts and returns the closest hit
        /// </summary>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="filterGroup">The collision group of this raycast</param>
        /// <param name="hitGroups">The collision group that this raycast can collide with</param>
        /// <returns>The list with hit results.</returns>
        public BepuHitResult Raycast(Vector3 from, Vector3 direction, float length, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            RayHitClosestHandler rhch = new RayHitClosestHandler()
            {
                findGroups = hitGroups,
                startLength = length,
                furthestHitSoFar = float.MaxValue
            };
            internalSimulation.RayCast(new System.Numerics.Vector3(from.X, from.Y, from.Z), new System.Numerics.Vector3(direction.X, direction.Y, direction.Z), length, ref rhch);
            return rhch.HitCollidable;
        }

        /// <summary>
        /// Raycasts penetrating any shape the ray encounters.
        /// Filtering by CollisionGroup
        /// </summary>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="resultsOutput">The list to fill with results.</param>
        /// <param name="filterGroup">The collision group of this raycast</param>
        /// <param name="hitGroups">The collision group that this raycast can collide with</param>
        public void RaycastPenetrating(Vector3 from, Vector3 to, List<BepuHitResult> resultsOutput, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            Vector3 diff = to - from;
            float length = diff.Length();
            float inv = 1.0f / length;
            diff.X *= inv;
            diff.Y *= inv;
            diff.Z *= inv;
            RaycastPenetrating(from, diff, length, resultsOutput, hitGroups);
        }

        /// <summary>
        /// Raycasts penetrating any shape the ray encounters.
        /// Filtering by CollisionGroup
        /// </summary>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="resultsOutput">The list to fill with results.</param>
        /// <param name="filterGroup">The collision group of this raycast</param>
        /// <param name="hitGroups">The collision group that this raycast can collide with</param>
        public void RaycastPenetrating(Vector3 from, Vector3 direction, float length, List<BepuHitResult> resultsOutput, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            RayHitAllHandler rhch = new RayHitAllHandler()
            {
                hitGroups = hitGroups,
                HitCollidables = resultsOutput,
                startLength = length
            };
            internalSimulation.RayCast(new System.Numerics.Vector3(from.X, from.Y, from.Z), new System.Numerics.Vector3(direction.X, direction.Y, direction.Z), length, ref rhch);
        }

        struct SweepTestFirst : ISweepHitHandler
        {
            public CollisionFilterGroupFlags hitGroups;
            public BepuHitResult result;
            public float furthestHitSoFar, startLength;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable)
            {
                return ((uint)getFromReference(collidable).CollisionGroup & (uint)hitGroups) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable, int child)
            {
                return ((uint)getFromReference(collidable).CollisionGroup & (uint)hitGroups) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnHit(ref float maximumT, float t, in System.Numerics.Vector3 hitLocation, in System.Numerics.Vector3 hitNormal, CollidableReference collidable)
            {
                if (t < furthestHitSoFar)
                {
                    furthestHitSoFar = t;
                    result.Succeeded = true;
                    result.Collider = getFromReference(collidable);
                    result.Normal.X = hitNormal.X;
                    result.Normal.Y = hitNormal.Y;
                    result.Normal.Z = hitNormal.Z;
                    result.Point.X = hitLocation.X;
                    result.Point.Y = hitLocation.Y;
                    result.Point.Z = hitLocation.Z;
                    result.HitFraction = t / startLength;
                }

                //Changing the maximum T value prevents the traversal from visiting any leaf nodes more distant than that later in the traversal.
                //It is effectively an optimization that you can use if you only care about the time of first impact.
                if (t < maximumT)
                    maximumT = t;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
            {
                result.Succeeded = true;
                result.Collider = getFromReference(collidable);
                maximumT = 0;
                furthestHitSoFar = 0;
            }
        }

        struct SweepTestAll : ISweepHitHandler
        {
            public CollisionFilterGroupFlags hitGroups;
            public List<BepuHitResult> results;
            public float startLength;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable)
            {
                return ((uint)getFromReference(collidable).CollisionGroup & (uint)hitGroups) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable, int child)
            {
                return ((uint)getFromReference(collidable).CollisionGroup & (uint)hitGroups) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnHit(ref float maximumT, float t, in System.Numerics.Vector3 hitLocation, in System.Numerics.Vector3 hitNormal, CollidableReference collidable)
            {
                BepuHitResult result = new BepuHitResult();
                result.Succeeded = true;
                result.Collider = getFromReference(collidable);
                result.Normal.X = hitNormal.X;
                result.Normal.Y = hitNormal.Y;
                result.Normal.Z = hitNormal.Z;
                result.Point.X = hitLocation.X;
                result.Point.Y = hitLocation.Y;
                result.Point.Z = hitLocation.Z;
                result.HitFraction = t / startLength;
                results.Add(result);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
            {
                BepuHitResult result = new BepuHitResult();
                result.Succeeded = true;
                result.Collider = getFromReference(collidable);
                results.Add(result);
            }
        }

        /// <summary>
        /// Performs a test to see if a collider shape collides with anything, and if so, returns the closest hit to the center of the shape.
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <param name="position">Where to test.</param>
        /// <param name="filterGroup">Group of the shape.</param>
        /// <param name="hitGroups">What the shape should collide with</param>
        /// <returns>PhysicsComponent of thing hitting shapetest</returns>
        public BepuPhysicsComponent ShapeTest(IConvexShape shape, Vector3 position, Xenko.Core.Mathematics.Quaternion rotation, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            SweepTestFirst sshh = new SweepTestFirst()
            {
                hitGroups = hitGroups,
                startLength = 0f,
                furthestHitSoFar = float.MaxValue
            };
            RigidPose rp = new RigidPose();
            rp.Position.X = position.X;
            rp.Position.Y = position.Y;
            rp.Position.Z = position.Z;
            rp.Orientation.X = rotation.X;
            rp.Orientation.Y = rotation.Y;
            rp.Orientation.Z = rotation.Z;
            rp.Orientation.W = rotation.W;
            internalSimulation.Sweep(shape, rp, new BodyVelocity(), 0f, pBufferPool, ref sshh);
            return sshh.result.Collider;
        }

        /// <summary>
        /// Performs a test to see if a collider shape collides with anything, and if so, returns all results in a list
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <param name="position">Where to test.</param>
        /// <param name="resultsOutput">Where to store results</param>
        /// <param name="filterGroup">Group of the shape.</param>
        /// <param name="filterFlags">What the shape should collide with</param>
        /// <returns></returns>
        public void ShapeTestPenetrating(IConvexShape shape, Vector3 position, Xenko.Core.Mathematics.Quaternion rotation, List<BepuHitResult> output, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            SweepTestAll sshh = new SweepTestAll()
            {
                hitGroups = hitGroups,
                startLength = 0f,
                results = output
            };
            RigidPose rp = new RigidPose();
            rp.Position.X = position.X;
            rp.Position.Y = position.Y;
            rp.Position.Z = position.Z;
            rp.Orientation.X = rotation.X;
            rp.Orientation.Y = rotation.Y;
            rp.Orientation.Z = rotation.Z;
            rp.Orientation.W = rotation.W;
            internalSimulation.Sweep(shape, rp, new BodyVelocity(), 0f, pBufferPool, ref sshh);
        }

        /// <summary>
        /// Performs a sweep test using a collider shape and returns the closest hit
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="filterGroup">The collision group of this shape sweep</param>
        /// <param name="filterFlags">The collision group that this shape sweep can collide with</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">This kind of shape cannot be used for a ShapeSweep.</exception>
        public BepuHitResult ShapeSweep(IConvexShape shape, Vector3 position, Xenko.Core.Mathematics.Quaternion rotation, Vector3 endpoint, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            Vector3 diff = endpoint - position;
            float length = diff.Length();
            float inv = 1.0f / length;
            diff.X *= inv;
            diff.Y *= inv;
            diff.Z *= inv;
            return ShapeSweep(shape, position, rotation, diff, length, hitGroups);
        }

        /// <summary>
        /// Performs a sweep test using a collider shape and returns the closest hit
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="filterGroup">The collision group of this shape sweep</param>
        /// <param name="filterFlags">The collision group that this shape sweep can collide with</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">This kind of shape cannot be used for a ShapeSweep.</exception>
        public BepuHitResult ShapeSweep(IConvexShape shape, Vector3 position, Xenko.Core.Mathematics.Quaternion rotation, Vector3 direction, float length, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            SweepTestFirst sshh = new SweepTestFirst()
            {
                hitGroups = hitGroups,
                startLength = length,
                furthestHitSoFar = float.MaxValue
            };
            RigidPose rp = new RigidPose();
            rp.Position.X = position.X;
            rp.Position.Y = position.Y;
            rp.Position.Z = position.Z;
            rp.Orientation.X = rotation.X;
            rp.Orientation.Y = rotation.Y;
            rp.Orientation.Z = rotation.Z;
            rp.Orientation.W = rotation.W;
            internalSimulation.Sweep(shape, rp, new BodyVelocity(new System.Numerics.Vector3(direction.X, direction.Y, direction.Z)), length, pBufferPool, ref sshh);
            return sshh.result;
        }

        /// <summary>
        /// Performs a sweep test using a collider shape and never stops until "to"
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="resultsOutput">The list to fill with results.</param>
        /// <param name="filterGroup">The collision group of this shape sweep</param>
        /// <param name="filterFlags">The collision group that this shape sweep can collide with</param>
        /// <exception cref="System.Exception">This kind of shape cannot be used for a ShapeSweep.</exception>
        public void ShapeSweepPenetrating(IConvexShape shape, Vector3 position, Xenko.Core.Mathematics.Quaternion rotation, Vector3 endpoint, List<BepuHitResult> output, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            Vector3 diff = endpoint - position;
            float length = diff.Length();
            float inv = 1.0f / length;
            diff.X *= inv;
            diff.Y *= inv;
            diff.Z *= inv;
            ShapeSweepPenetrating(shape, position, rotation, diff, length, output, hitGroups);
        }

        /// <summary>
        /// Performs a sweep test using a collider shape and never stops until "to"
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="resultsOutput">The list to fill with results.</param>
        /// <param name="filterGroup">The collision group of this shape sweep</param>
        /// <param name="filterFlags">The collision group that this shape sweep can collide with</param>
        /// <exception cref="System.Exception">This kind of shape cannot be used for a ShapeSweep.</exception>
        public void ShapeSweepPenetrating(IConvexShape shape, Vector3 position, Xenko.Core.Mathematics.Quaternion rotation, Vector3 direction, float length, List<BepuHitResult> output, CollisionFilterGroupFlags hitGroups = DefaultFlags)
        {
            SweepTestAll sshh = new SweepTestAll()
            {
                hitGroups = hitGroups,
                startLength = length,
                results = output
            };
            RigidPose rp = new RigidPose();
            rp.Position.X = position.X;
            rp.Position.Y = position.Y;
            rp.Position.Z = position.Z;
            rp.Orientation.X = rotation.X;
            rp.Orientation.Y = rotation.Y;
            rp.Orientation.Z = rotation.Z;
            rp.Orientation.W = rotation.W;
            internalSimulation.Sweep(shape, rp, new BodyVelocity(new System.Numerics.Vector3(direction.X, direction.Y, direction.Z)), length, pBufferPool, ref sshh);
        }

        /// <summary>
        /// Gets or sets the gravity.
        /// </summary>
        /// <value>
        /// The gravity.
        /// </value>
        /// <exception cref="System.Exception">
        /// Cannot perform this action when the physics engine is set to CollisionsOnly
        /// or
        /// Cannot perform this action when the physics engine is set to CollisionsOnly
        /// </exception>
        public Vector3 Gravity
        {
            get
            {
                return new Vector3(poseCallbacks.Gravity.X, poseCallbacks.Gravity.Y, poseCallbacks.Gravity.Z);
            }
            set
            {
                poseCallbacks.Gravity.X = value.X;
                poseCallbacks.Gravity.Y = value.Y;
                poseCallbacks.Gravity.Z = value.Z;
            }
        }

        /// <summary>
        /// The maximum number of steps that the Simulation is allowed to take each tick.
        /// If the engine is running slow (large deltaTime), then you must increase the number of maxSubSteps to compensate for this, otherwise your simulation is “losing” time.
        /// It's important that frame DeltaTime is always less than MaxSubSteps*FixedTimeStep, otherwise you are losing time.
        /// </summary>
        public int MaxSubSteps { get; set; }

        /// <summary>
        /// By decreasing the size of fixedTimeStep, you are increasing the “resolution” of the simulation.
        /// Default is 1.0f / 60.0f or 60fps
        /// </summary>
        public float FixedTimeStep { get; set; }

        public class SimulationArgs : EventArgs
        {
            public float DeltaTime;
        }

        /// <summary>
        /// Called right before the physics simulation.
        /// This event might not be fired by the main thread.
        /// </summary>
        public event EventHandler<SimulationArgs> SimulationBegin;

        protected virtual void OnSimulationBegin(SimulationArgs e)
        {
            var handler = SimulationBegin;
            handler?.Invoke(this, e);
        }

        internal int UpdatedRigidbodies;

        private readonly SimulationArgs simulationArgs = new SimulationArgs();

        internal ProfilingState SimulationProfiler;

        internal void Simulate(float deltaTime)
        {
            if (internalSimulation == null || DisableSimulation) return;

            OnSimulationBegin(simulationArgs);

            internalSimulation.Timestep(deltaTime, threadDispatcher);

            OnSimulationEnd(simulationArgs);
        }

        /// <summary>
        /// Called right after the physics simulation.
        /// This event might not be fired by the main thread.
        /// </summary>
        public event EventHandler<SimulationArgs> SimulationEnd;

        /// <summary>
        /// Called right before processing a tick of the physics simulation.
        /// </summary>
        public event EventHandler<float> PreSimulationTick;

        protected virtual void OnSimulationEnd(SimulationArgs e)
        {
            var handler = SimulationEnd;
            handler?.Invoke(this, e);
        }

        protected virtual void OnPreSimulationTick(float timeStep)
        {
            var handler = PreSimulationTick;
            handler?.Invoke(this, timeStep);
        }
    }
}
