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

        public ulong[] hand_paths = new ulong[12];

        public Space myHandSpace;
        public Silk.NET.OpenXR.Action myHandAction;

        public OpenXrTouchController(OpenXRHmd hmd, Silk.NET.OpenXR.Action handPoseAction)
        {
            baseHMD = hmd;
            handLocation.Type = StructureType.TypeSpaceLocation;
            myHandAction = handPoseAction;

            ActionSpaceCreateInfo action_space_info = new ActionSpaceCreateInfo()
            {
                Type = StructureType.TypeActionSpaceCreateInfo,
                Action = myHandAction,
                PoseInActionSpace = new Posef(new Quaternionf(0f, 0f, 0f, 1f), new Vector3f(0f, 0f, 0f)),
            };

            OpenXRHmd.CheckResult(baseHMD.Xr.CreateActionSpace(baseHMD.globalSession, in action_space_info, ref myHandSpace));
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
				Action = myHandAction,                 
            };

            baseHMD.Xr.GetActionStatePose(baseHMD.globalSession, in get_info, ref hand_pose_state);

            baseHMD.Xr.LocateSpace(myHandSpace, baseHMD.globalPlaySpace, baseHMD.globalFrameState.PredictedDisplayTime,
                                   ref handLocation);

            currentPos.X = handLocation.Pose.Position.X;
            currentPos.Y = handLocation.Pose.Position.Y;
            currentPos.Z = handLocation.Pose.Position.Z;

            currentRot.X = handLocation.Pose.Orientation.X;
            currentRot.Y = handLocation.Pose.Orientation.Y;
            currentRot.Z = handLocation.Pose.Orientation.Z;
            currentRot.W = handLocation.Pose.Orientation.W;
        }
    }
}
