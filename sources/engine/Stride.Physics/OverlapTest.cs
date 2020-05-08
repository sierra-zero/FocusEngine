using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Stride.Physics
{
    public class OverlapTest
    {
        public struct OverlapContactPoint
        {
            public PhysicsComponent ContactComponent;
            public float Distance;
            public Stride.Core.Mathematics.Vector3 Position, Normal;
        }

        [ThreadStatic]
        private static BulletSharp.PairCachingGhostObject ghostObject;

        internal static Simulation mySimulation;

        [ThreadStatic]
        private static OverlapCallback internalResults;

        [ThreadStatic]
        public static HashSet<object> NativeOverlappingObjects;

        public static HashSet<OverlapContactPoint> ContactTestResults
        {
            get
            {
                return internalResults.Contacts;
            }
        }

        class OverlapCallback : BulletSharp.ContactResultCallback
        {
            public readonly HashSet<OverlapContactPoint> Contacts = new HashSet<OverlapContactPoint>();

            public override float AddSingleResult(BulletSharp.ManifoldPoint contact, BulletSharp.CollisionObjectWrapper obj0, int partId0, int index0, BulletSharp.CollisionObjectWrapper obj1, int partId1, int index1)
            {
                var component0 = obj0.CollisionObject.UserObject as PhysicsComponent;
                var component1 = obj1.CollisionObject.UserObject as PhysicsComponent;

                Contacts.Add(new OverlapContactPoint
                {
                    ContactComponent = component0 ?? component1,
                    Distance = contact.m_distance1,
                    Normal = contact.m_normalWorldOnB,
                    Position = contact.m_positionWorldOnB,
                });

                return 0f;
            }

            public void Remove(OverlapContactPoint contact) => Contacts.Remove(contact);
            public bool Contains(OverlapContactPoint contact) => Contacts.Contains(contact);
            public void Clear() => Contacts.Clear();
            public HashSet<OverlapContactPoint>.Enumerator GetEnumerator() => Contacts.GetEnumerator();
        }

        /// <summary>
        /// Counts how many objects are overlapping with a given ColliderShape. If trackObjects are true, you can
        /// use OverlappedWith to check if another PhysicsComponent was part of the overlapping objects
        /// </summary>
        /// <param name="shape">ColliderShape to check for overlaps with</param>
        /// <param name="position">Position to move the ColliderShape, in addition to its LocalOffset</param>
        /// <param name="myGroup">What collision group is the ColliderShape in?</param>
        /// <param name="overlapsWith">What collision groups does the ColliderShape overlap with?</param>
        /// <param name="contactTest">If true, contact test overlapping objects. See ContactResults for output. Defaults to false</param>
        /// <param name="stopAfterFirstContact">If contact testing, should we stop contact testing after our first contact was found?</param>
        /// <returns>Number of overlapping objects</returns>
        public static int PerformOverlapTest(ColliderShape shape, Stride.Core.Mathematics.Vector3? position = null,
                                             CollisionFilterGroups myGroup = CollisionFilterGroups.DefaultFilter,
                                             CollisionFilterGroupFlags overlapsWith = CollisionFilterGroupFlags.AllFilter,
                                             bool contactTest = false, bool stopAfterFirstContact = false)
        {
            // doesn't support multithreading
            if (mySimulation.simulationLocker != null)
                throw new InvalidOperationException("Overlap testing not supported with multithreaded physics");

            if (ghostObject == null)
            {
                ghostObject = new BulletSharp.PairCachingGhostObject
                {
                    ContactProcessingThreshold = 1e18f,
                    UserObject = shape
                };
                ghostObject.CollisionFlags |= BulletSharp.CollisionFlags.NoContactResponse;

                internalResults = new OverlapCallback();

                NativeOverlappingObjects = new HashSet<object>();
            }

            ghostObject.CollisionShape = shape.InternalShape;
            ghostObject.WorldTransform = Matrix.Transformation(shape.Scaling, shape.LocalRotation, position.HasValue ? position.Value + shape.LocalOffset : shape.LocalOffset);

            mySimulation.collisionWorld.AddCollisionObject(ghostObject, (BulletSharp.CollisionFilterGroups)myGroup, (BulletSharp.CollisionFilterGroups)overlapsWith);

            int overlapCount = ghostObject.NumOverlappingObjects;

            NativeOverlappingObjects.Clear();
            for (int i = 0; i < overlapCount; i++)
                NativeOverlappingObjects.Add(ghostObject.OverlappingPairs[i]);

            if (contactTest)
            {
                internalResults.Clear();
                internalResults.CollisionFilterGroup = (int)myGroup;
                internalResults.CollisionFilterMask = (int)overlapsWith;

                foreach (object nativeobj in NativeOverlappingObjects)
                {
                    mySimulation.collisionWorld.ContactPairTest(ghostObject, (BulletSharp.CollisionObject)nativeobj, internalResults);
                    if (stopAfterFirstContact && internalResults.Contacts.Count > 0) break;
                }
            }

            mySimulation.collisionWorld.RemoveCollisionObject(ghostObject);

            return overlapCount;
        }
    }
}
