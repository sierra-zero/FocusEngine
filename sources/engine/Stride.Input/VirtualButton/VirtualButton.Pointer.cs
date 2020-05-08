// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using Xenko.Core.Collections;

namespace Xenko.Input
{
    /// <summary>
    /// Describes a virtual button (a key from a keyboard, a mouse button, an axis of a joystick...etc.).
    /// </summary>
    public partial class VirtualButton
    {
        /// <summary>
        /// Mouse virtual button.
        /// </summary>
        public class Pointer : VirtualButton
        {
            private Pointer(string name, int id, bool isPositiveAndNegative)
                : this(name, id, -1, isPositiveAndNegative)
            {
            }

            private Pointer(Pointer parent, int pointerId)
                : this(parent.ShortName, parent.Id, pointerId, parent.IsPositiveAndNegative)
            {
            }

            protected Pointer(string name, int id, int pointerId, bool isPositiveAndNegative)
                : base(name, VirtualButtonType.Pointer, id, isPositiveAndNegative)
            {
                PointerId = pointerId;
            }

            /// <summary>
            /// The pad index.
            /// </summary>
            public readonly int PointerId;

            /// <summary>
            /// Return a pointer button for the given point Id.
            /// </summary>
            /// <param name="pointerId">the Id of the pointer</param>
            /// <returns>An pointer button for the given pointer Id.</returns>
            public Pointer WithId(int pointerId)
            {
                return new Pointer(this, pointerId);
            }

            /// <summary>
            /// The current state of pointers.
            /// </summary>
            public static readonly VirtualButton State = new Pointer("State", 0, false);

            /// <summary>
            /// The X component of the Pointer <see cref="PointerPoint.Position"/>.
            /// </summary>
            public static readonly VirtualButton PositionX = new Pointer("PositionX", 1, true);

            /// <summary>
            /// The Y component of the Pointer <see cref="PointerPoint.Position"/>.
            /// </summary>
            public static readonly VirtualButton PositionY = new Pointer("PositionY", 2, true);

            /// <summary>
            /// The X component of the Pointer <see cref="PointerPoint.Delta"/>.
            /// </summary>
            public static readonly VirtualButton DeltaX = new Pointer("DeltaX", 3, true);

            /// <summary>
            /// The Y component of the Pointer <see cref="PointerPoint.Delta"/>.
            /// </summary>
            public static readonly VirtualButton DeltaY = new Pointer("DeltaY", 4, true);

            protected override string BuildButtonName()
            {
                return PointerId < 0 ? base.BuildButtonName() : Type.ToString() + PointerId + "." + ShortName;
            }

            public override float GetValue()
            {
                int index = (Id & TypeIdMask);
                switch (index)
                {
                    case 0:
                        return IsDown() ? 1f : 0f;
                    case 1:
                        return FromFirstMatchingEvent(GetPositionX);
                    case 2:
                        return FromFirstMatchingEvent(GetPositionY);
                    case 3:
                        return FromFirstMatchingEvent(GetDeltaX);
                    case 4:
                        return FromFirstMatchingEvent(GetDeltaY);
                }

                return 0.0f;
            }

            public override bool IsDown()
            {
                return Index == 0 ? AnyPointerInState(GetDownPointers) : false;
            }

            public override bool IsPressed()
            {
                return Index == 0 ? AnyPointerInState(GetPressedPointers) : false;
            }

            public override bool IsReleased()
            {
                return Index == 0 ? AnyPointerInState(GetReleasedPointers) : false;
            }

            private float FromFirstMatchingEvent(Func<PointerEvent, float> valueGetter)
            {
                foreach (var pointerEvent in InputManager.instance.PointerEvents)
                {
                    if (PointerId < 0 || pointerEvent.PointerId == PointerId)
                        return valueGetter(pointerEvent);
                }
                return 0f;
            }

            private bool AnyPointerInState(Func<IPointerDevice, IReadOnlySet<PointerPoint>> stateGetter)
            {
                foreach (var pointerDevice in InputManager.instance.Pointers)
                {
                    foreach (var pointerPoint in stateGetter(pointerDevice))
                    {
                        if (PointerId < 0 || pointerPoint.Id == PointerId)
                            return true;
                    }
                }
                return false;
            }

            private IReadOnlySet<PointerPoint> GetDownPointers(IPointerDevice device)
            {
                return device.DownPointers;
            }

            private IReadOnlySet<PointerPoint> GetPressedPointers(IPointerDevice device)
            {
                return device.DownPointers;
            }

            private IReadOnlySet<PointerPoint> GetReleasedPointers(IPointerDevice device)
            {
                return device.DownPointers;
            }

            private float GetPositionX(PointerEvent pointerEvent)
            {
                return pointerEvent.Position.X;
            }

            private float GetPositionY(PointerEvent pointerEvent)
            {
                return pointerEvent.Position.Y;
            }

            private float GetDeltaX(PointerEvent pointerEvent)
            {
                return pointerEvent.DeltaPosition.X;
            }

            private float GetDeltaY(PointerEvent pointerEvent)
            {
                return pointerEvent.DeltaPosition.Y;
            }
        }
    }
}
