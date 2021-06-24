using System;
using System.Collections.Generic;
using System.Text;
using Silk.NET.OpenXR;
using Xenko.Core.Mathematics;
using Xenko.Games;

namespace Xenko.VirtualReality
{
    class OpenXrTouchController : TouchController
    {
        private string baseHandPath;
        private OpenXRHmd baseHMD;
        private SpaceLocation handLocation;

        // input stuff
        public enum HAND_PATHS
        {
            Hand = 0,
            TriggerValue = 1,
            ThumbstickY = 2,
            ThumbstickX = 3,
            TrackpadX = 4,
            TrackpadY = 5,
            GripValue = 6,
            Button1 = 7, // x on left, a on right (or either index)
            Button2 = 8, // y on left, b on right (or either index)
            Menu = 9,
            System = 10, // may be inaccessible
        }
        public ulong[] hand_paths = new ulong[11];

        public Space myHand;

        public OpenXrTouchController(OpenXRHmd hmd, string baseHandPath)
        {
            this.baseHandPath = baseHandPath;
            baseHMD = hmd;

            handLocation.Type = StructureType.TypeSpaceLocation;

            baseHMD.Xr.StringToPath(baseHMD.Instance, baseHandPath, ref hand_paths[(int)HAND_PATHS.Hand]);
            baseHMD.Xr.StringToPath(baseHMD.Instance, baseHandPath + "/input/trigger/value",
                                    ref hand_paths[(int)HAND_PATHS.TriggerValue]);
        }

        public void SetupPose()
        {
            ActionSpaceCreateInfo action_space_info = new ActionSpaceCreateInfo()
            {
                Type = StructureType.TypeActionSpaceCreateInfo,
                Action = baseHMD.handPoseAction,
                PoseInActionSpace = new Posef(new Quaternionf(0f, 0f, 0f, 1f), new Vector3f(0f, 0f, 0f)),
                SubactionPath = hand_paths[0]
            };

            OpenXRHmd.CheckResult(baseHMD.Xr.CreateActionSpace(baseHMD.globalSession, in action_space_info, ref myHand));
        }

        private Vector3 currentPos;
        public override Vector3 Position => currentPos;

        private Quaternion currentRot;
        public override Quaternion Rotation => currentRot;

        private Vector3 currentVel;
        public override Vector3 LinearVelocity => currentVel;

        private Vector3 currentAngVel;
        public override Vector3 AngularVelocity => currentAngVel;

        public override DeviceState State => (handLocation.LocationFlags & SpaceLocationFlags.SpaceLocationPositionValidBit) != 0 ? DeviceState.Valid : DeviceState.OutOfRange;

        public override bool SwapTouchpadJoystick { get; set; }
        public override float HoldAngleOffset { get; set; }

        public override float Trigger => 0f;

        public override float Grip => 0f;

        public override bool IndexPointing => false;

        public override bool IndexResting => false;

        public override bool ThumbUp => false;

        public override bool ThumbResting => false;

        public override Vector2 ThumbAxis => new Vector2(0f, 0f);

        public override Vector2 ThumbstickAxis => new Vector2(0f, 0f);

        public override string DebugControllerState()
        {
            return "Not Implemented";
        }

        public override Vector2 GetAxis(int index)
        {
            return new Vector2(0f, 0f);
        }

        public override bool IsPressed(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsPressedDown(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsPressReleased(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsTouched(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsTouchedDown(TouchControllerButton button)
        {
            return false;
        }

        public override bool IsTouchReleased(TouchControllerButton button)
        {
            return false;
        }

        public override bool Vibrate(float amount = 1)
        {
            return false;
        }

        public override void Update(GameTime time)
        {
            ActionStatePose hand_pose_state = new ActionStatePose()
            {
                Type = StructureType.TypeActionStatePose,
            };

            ActionStateGetInfo get_info = new ActionStateGetInfo()
            {
                Type = StructureType.TypeActionStateGetInfo,
				Action = baseHMD.handPoseAction,                     
				SubactionPath = hand_paths[0]
            };

            baseHMD.Xr.GetActionStatePose(baseHMD.globalSession, in get_info, ref hand_pose_state);

            baseHMD.Xr.LocateSpace(myHand, baseHMD.globalPlaySpace, baseHMD.globalFrameState.PredictedDisplayTime,
                                   ref handLocation);

            currentPos.X =  handLocation.Pose.Position.X;
            currentPos.Y = -handLocation.Pose.Position.Y;
            currentPos.Z =  handLocation.Pose.Position.Z;

            currentRot = OpenXRHmd.ConvertToFocus(ref handLocation.Pose.Orientation);
        }
    }
}
