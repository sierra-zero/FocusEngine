// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if XENKO_GRAPHICS_API_VULKAN || XENKO_GRAPHICS_API_DIRECT3D11

using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;

namespace Xenko.VirtualReality
{
    public class OpenVRHmd : VRDevice
    {
        private RectangleF leftView = new RectangleF(0.0f, 0.0f, 0.5f, 1.0f);
        private RectangleF rightView = new RectangleF(0.5f, 0.0f, 1.0f, 1.0f);
        private DeviceState state;
        private OpenVRTouchController leftHandController;
        private OpenVRTouchController rightHandController;
        private OpenVRTrackedDevice[] trackedDevices;
        private bool needsMirror;
        private Matrix currentHead;
        private Vector3 currentHeadPos;
        private Vector3 currentHeadLinearVelocity;
        private Vector3 currentHeadAngularVelocity;
        private Quaternion currentHeadRot;
        private GameBase mainGame;
        private int HMDindex;
        private ulong poseCount;

        public override bool CanInitialize => OpenVR.InitDone || OpenVR.Init();

        public override ulong PoseCount
        {
            get
            {
                return poseCount;
            }
        }

        public OpenVRHmd(GameBase game)
        {
            mainGame = game;
            VRApi = VRApi.OpenVR;
        }

        public override void Enable(GraphicsDevice device, GraphicsDeviceManager graphicsDeviceManager, bool requireMirror)
        {
            ActualRenderFrameSize = OptimalRenderFrameSize;

            needsMirror = requireMirror;

            leftHandController = new OpenVRTouchController(TouchControllerHand.Left);
            rightHandController = new OpenVRTouchController(TouchControllerHand.Right);
            leftHandController.HostDevice = this;
            rightHandController.HostDevice = this;

            trackedDevices = new OpenVRTrackedDevice[Valve.VR.OpenVR.k_unMaxTrackedDeviceCount];
            for (int i = 0; i < trackedDevices.Length; i++) {
                trackedDevices[i] = new OpenVRTrackedDevice(i);
                if (trackedDevices[i].Class == DeviceClass.HMD) {
                    HMDindex = i;
                }
            }

#if XENKO_GRAPHICS_API_VULKAN
            OpenVR.InitVulkan(mainGame);
#endif
        }

        public override void Draw(GameTime gameTime)
        {
            OpenVR.UpdatePoses();
            state = OpenVR.GetHeadPose(out currentHead, out currentHeadLinearVelocity, out currentHeadAngularVelocity);
            Vector3 scale;
            currentHead.Decompose(out scale, out currentHeadRot, out currentHeadPos);
            poseCount++;
        }

        public override void Update(GameTime gameTime)
        {
            LeftHand.Update(gameTime);
            RightHand.Update(gameTime);
            foreach (var tracker in trackedDevices)
                tracker.Update(gameTime);
        }

        public override void ReadEyeParameters(Eyes eye, float near, float far, ref Vector3 cameraPosition, ref Matrix cameraRotation, bool ignoreHeadRotation, bool ignoreHeadPosition, out Matrix view, out Matrix projection)
        {
            Matrix eyeMat, rot;
            Vector3 pos, scale;

            OpenVR.GetEyeToHead(eye == Eyes.Left ? 0 : 1, out eyeMat);
            OpenVR.GetProjection(eye == Eyes.Left ? 0 : 1, near, far, out projection);

            var adjustedHeadMatrix = currentHead;
            if (ignoreHeadPosition)
            {
                adjustedHeadMatrix.TranslationVector = Vector3.Zero;
            }
            if (ignoreHeadRotation)
            {
                // keep the scale just in case
                adjustedHeadMatrix.Row1 = new Vector4(adjustedHeadMatrix.Row1.Length(), 0, 0, 0);
                adjustedHeadMatrix.Row2 = new Vector4(0, adjustedHeadMatrix.Row2.Length(), 0, 0);
                adjustedHeadMatrix.Row3 = new Vector4(0, 0, adjustedHeadMatrix.Row3.Length(), 0);
            }

            eyeMat = eyeMat * adjustedHeadMatrix * Matrix.Scaling(BodyScaling) * cameraRotation * Matrix.Translation(cameraPosition);
            eyeMat.Decompose(out scale, out rot, out pos);
            var finalUp = Vector3.TransformCoordinate(new Vector3(0, 1, 0), rot);
            var finalForward = Vector3.TransformCoordinate(new Vector3(0, 0, -1), rot);
            view = Matrix.LookAtRH(pos, pos + finalForward, finalUp);
        }

        public override void Commit(CommandList commandList, Texture renderFrame)
        {
            OpenVR.Submit(0, renderFrame, ref leftView);
            OpenVR.Submit(1, renderFrame, ref rightView);
        }
        public override void Recenter()
        {
            OpenVR.Recenter();
        }

        public override void SetTrackingSpace(TrackingSpace space)
        {
            OpenVR.SetTrackingSpace((Valve.VR.ETrackingUniverseOrigin)space);
        }

        public override DeviceState State => state;

        public override Vector3 HeadPosition => currentHeadPos;

        public override Quaternion HeadRotation => currentHeadRot;

        public override Vector3 HeadLinearVelocity => currentHeadLinearVelocity;

        public override Vector3 HeadAngularVelocity => currentHeadAngularVelocity;

        public override TouchController LeftHand => leftHandController;

        public override TouchController RightHand => rightHandController;

        public override TrackedItem[] TrackedItems => trackedDevices;

        public override Texture MirrorTexture { get; protected set; }

        public override float RenderFrameScaling { get; set; } = 1.4f;

        public override Size2 ActualRenderFrameSize { get; protected set; }

        public override Size2 OptimalRenderFrameSize {
            get {
                uint width = 0, height = 0;
                Valve.VR.OpenVR.System.GetRecommendedRenderTargetSize(ref width, ref height);
                width = (uint)(width * RenderFrameScaling);
                width += width % 2;
                height = (uint)(height * RenderFrameScaling);
                height += height % 2;
                return new Size2((int)width, (int)height);
            }
        }

        public float RefreshRate() {
            Valve.VR.ETrackedPropertyError err = default;
            return Valve.VR.OpenVR.System.GetFloatTrackedDeviceProperty((uint)HMDindex, Valve.VR.ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref err);
        }

        public override void Dispose()
        {
            OpenVR.Shutdown();
        }
    }
}

#endif
