using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using static Xenko.Animations.Interpolator;
using static Xenko.Physics.Simulation;

namespace Xenko.Physics
{
    public class OverlapTest
    {
        public struct OverlapContactPoint
        {
            public PhysicsComponent ContactComponent;
            public float Distance;
            public Xenko.Core.Mathematics.Vector3 Position, Normal;
        }

        [ThreadStatic]
        private static BulletSharp.PairCachingGhostObject ghostObject;

        internal static Simulation mySimulation;

        [ThreadStatic]
        private static readonly OverlapCallback internalResults = new OverlapCallback();

        [ThreadStatic]
        public static readonly HashSet<object> NativeOverlappingObjects = new HashSet<object>();

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
        /// <returns>Number of overlapping objects</returns>
        public static int PerformOverlapTest(ColliderShape shape, Xenko.Core.Mathematics.Vector3? position = null,
                                             CollisionFilterGroups myGroup = CollisionFilterGroups.DefaultFilter,
                                             CollisionFilterGroupFlags overlapsWith = CollisionFilterGroupFlags.AllFilter,
                                             bool contactTest = false)
        {
            if (ghostObject == null)
            {
                ghostObject = new BulletSharp.PairCachingGhostObject
                {
                    ContactProcessingThreshold = 1e18f,
                    UserObject = shape
                };
                ghostObject.CollisionFlags |= BulletSharp.CollisionFlags.NoContactResponse;
            }

            ghostObject.CollisionShape = shape.InternalShape;
            ghostObject.WorldTransform = Matrix.Transformation(shape.Scaling, shape.LocalRotation, position.HasValue ? position.Value + shape.LocalOffset : shape.LocalOffset);

            mySimulation.collisionWorld.AddCollisionObject(ghostObject, (BulletSharp.CollisionFilterGroups)myGroup, (BulletSharp.CollisionFilterGroups)overlapsWith);

            int overlapCount = ghostObject.NumOverlappingObjects;

            if (contactTest)
            {
                internalResults.Clear();

                if (overlapCount > 0)
                {
                    internalResults.CollisionFilterGroup = (int)myGroup;
                    internalResults.CollisionFilterMask = (int)overlapsWith;
                    mySimulation.collisionWorld.ContactTest(ghostObject, internalResults);
                }
            }

            NativeOverlappingObjects.Clear();
            for (int i = 0; i < overlapCount; i++)
                NativeOverlappingObjects.Add(ghostObject.OverlappingPairs[i]);

            mySimulation.collisionWorld.RemoveCollisionObject(ghostObject);

            return overlapCount;
        }
    }
}
