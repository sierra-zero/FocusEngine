// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using Xenko.Input;

namespace Xenko.VirtualReality
{
    /// <summary>
    /// VR virtual button.
    /// </summary>
    public class VRButtons : VirtualButton
    {
        /// <summary>
        /// Right VR trigger.
        /// </summary>
        public static readonly VirtualButton RightTrigger = new VRButtons("RightTrigger", (int)TouchControllerButton.Trigger | (1 << 16));

        /// <summary>
        /// Right VR X.
        /// </summary>
        public static readonly VirtualButton RightX = new VRButtons("RightX", (int)TouchControllerButton.X | (1 << 16));

        /// <summary>
        /// Right VR Y.
        /// </summary>
        public static readonly VirtualButton RightY = new VRButtons("RightY", (int)TouchControllerButton.Y | (1 << 16));

        /// <summary>
        /// Right VR A.
        /// </summary>
        public static readonly VirtualButton RightA = new VRButtons("RightA", (int)TouchControllerButton.A | (1 << 16));

        /// <summary>
        /// Right VR B.
        /// </summary>
        public static readonly VirtualButton RightB = new VRButtons("RightB", (int)TouchControllerButton.B | (1 << 16));

        /// <summary>
        /// Right VR Grip.
        /// </summary>
        public static readonly VirtualButton RightGrip = new VRButtons("RightGrip", (int)TouchControllerButton.Grip | (1 << 16));

        /// <summary>
        /// Right VR Thumbstick Y axis
        /// </summary>
        public static readonly VirtualButton RightThumbstickY = new VRButtons("RightThumbstickY", (int)TouchControllerButton.Thumbstick | (1 << 16) | (1 << 17), true);

        /// <summary>
        /// Right VR Thumbstick X axis
        /// </summary>
        public static readonly VirtualButton RightThumbstickX = new VRButtons("RightThumbstickX", (int)TouchControllerButton.Thumbstick | (1 << 16), true);

        /// <summary>
        /// Right VR Touchpad Y axis.
        /// </summary>
        public static readonly VirtualButton RightTouchpadY = new VRButtons("RightTouchpadY", (int)TouchControllerButton.Touchpad | (1 << 16) | (1 << 17), true);

        /// <summary>
        /// Right VR Touchpad X axis.
        /// </summary>
        public static readonly VirtualButton RightTouchpadX = new VRButtons("RightTouchpadX", (int)TouchControllerButton.Touchpad | (1 << 16), true);

        /// <summary>
        /// Right VR Menu.
        /// </summary>
        public static readonly VirtualButton RightMenu = new VRButtons("RightMenu", (int)TouchControllerButton.Menu | (1 << 16));

        /// <summary>
        /// Left VR trigger.
        /// </summary>
        public static readonly VirtualButton LeftTrigger = new VRButtons("LeftTrigger", (int)TouchControllerButton.Trigger);

        /// <summary>
        /// Left VR X.
        /// </summary>
        public static readonly VirtualButton LeftX = new VRButtons("LeftX", (int)TouchControllerButton.X);

        /// <summary>
        /// Left VR Y.
        /// </summary>
        public static readonly VirtualButton LeftY = new VRButtons("LeftY", (int)TouchControllerButton.Y);

        /// <summary>
        /// Left VR A.
        /// </summary>
        public static readonly VirtualButton LeftA = new VRButtons("LeftA", (int)TouchControllerButton.A);

        /// <summary>
        /// Left VR B.
        /// </summary>
        public static readonly VirtualButton LeftB = new VRButtons("LeftB", (int)TouchControllerButton.B);

        /// <summary>
        /// Left VR Grip.
        /// </summary>
        public static readonly VirtualButton LeftGrip = new VRButtons("LeftGrip", (int)TouchControllerButton.Grip);

        /// <summary>
        /// Left VR Thumbstick Y axis
        /// </summary>
        public static readonly VirtualButton LeftThumbstickY = new VRButtons("LeftThumbstickY", (int)TouchControllerButton.Thumbstick | (1 << 17), true);

        /// <summary>
        /// Left VR Thumbstick X axis
        /// </summary>
        public static readonly VirtualButton LeftThumbstickX = new VRButtons("LeftThumbstickX", (int)TouchControllerButton.Thumbstick, true);

        /// <summary>
        /// Left VR Touchpad Y axis.
        /// </summary>
        public static readonly VirtualButton LeftTouchpadY = new VRButtons("LeftTouchpadY", (int)TouchControllerButton.Touchpad | (1 << 17), true);

        /// <summary>
        /// Left VR Touchpad X axis.
        /// </summary>
        public static readonly VirtualButton LeftTouchpadX = new VRButtons("LeftTouchpadX", (int)TouchControllerButton.Touchpad, true);

        /// <summary>
        /// Left VR Menu.
        /// </summary>
        public static readonly VirtualButton LeftMenu = new VRButtons("LeftMenu", (int)TouchControllerButton.Menu);

        protected VRButtons(string name, int id, bool isPositiveAndNegative = false)
            : base(name, VirtualButtonType.VR, id, isPositiveAndNegative)
        {
        }

        public override float GetValue(InputManager manager)
        {
            TouchController tc = VRDeviceSystem.GetSystem?.GetController((Index & (1 << 16)) != 0 ? TouchControllerHand.Right : TouchControllerHand.Left);
            if (tc == null) return 0f;
            TouchControllerButton button = (TouchControllerButton)(Index & 0xFF);
            switch (button)
            {
                case TouchControllerButton.Grip:
                    return tc.Grip;
                case TouchControllerButton.Thumbstick:
                    return (Index & (1 << 17)) != 0 ? tc.ThumbstickAxis.Y : tc.ThumbstickAxis.X;
                case TouchControllerButton.Touchpad:
                    return (Index & (1 << 17)) != 0 ? tc.ThumbAxis.Y : tc.ThumbAxis.X;
                case TouchControllerButton.Trigger:
                    return tc.Trigger;
                default:
                    return tc.IsPressed(button) ? 1f : 0f;
            }
        }

        public override bool IsDown(InputManager manager)
        {
            TouchController tc = VRDeviceSystem.GetSystem?.GetController((Index & (1 << 16)) != 0 ? TouchControllerHand.Right : TouchControllerHand.Left);
            if (tc == null) return false;
            return tc.IsPressed((TouchControllerButton)(Index & 0xFF));
        }

        public override bool IsPressed(InputManager manager)
        {
            TouchController tc = VRDeviceSystem.GetSystem?.GetController((Index & (1 << 16)) != 0 ? TouchControllerHand.Right : TouchControllerHand.Left);
            if (tc == null) return false;
            return tc.IsPressedDown((TouchControllerButton)(Index & 0xFF));
        }

        public override bool IsReleased(InputManager manager)
        {
            TouchController tc = VRDeviceSystem.GetSystem?.GetController((Index & (1 << 16)) != 0 ? TouchControllerHand.Right : TouchControllerHand.Left);
            if (tc == null) return false;
            return tc.IsPressReleased((TouchControllerButton)(Index & 0xFF));
        }
    }
}
