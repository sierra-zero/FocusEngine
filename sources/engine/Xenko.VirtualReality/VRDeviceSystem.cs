// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;

namespace Xenko.VirtualReality
{
    public class VRDeviceSystem : GameSystemBase
    {
        /// <summary>
        /// An active instance of the VRDeviceSystem
        /// </summary>
        public static VRDeviceSystem GetSystem { get; private set; }

        /// <summary>
        /// Is VR currently active and initialized? May take some frames to start returning true.
        /// </summary>
        public static bool VRActive
        {
            get
            {
                return GetSystem != null && GetSystem.Enabled && GetSystem.Device != null;
            }
        }

        /// <summary>
        /// Swap hands at a low level? Easy way to have right hand act like the left hand and vice versa.
        /// </summary>
        public bool GetControllerSwapped;

        /// <summary>
        /// Which VR button to activate UI? Defaults to trigger.
        /// </summary>
        public static TouchControllerButton UIActivationButton = TouchControllerButton.Trigger;

        /// <summary>
        /// Shortcut to getting VR hands
        /// </summary>
        /// <param name="hand">Which hand?</param>
        /// <returns>TouchController object, otherwise null</returns>
        public TouchController GetController(TouchControllerHand hand)
        {
            if (Device == null) return null;

            switch(hand)
            {
                case TouchControllerHand.Left:
                    return GetControllerSwapped ? Device.RightHand : Device.LeftHand;
                case TouchControllerHand.Right:
                    return GetControllerSwapped ? Device.LeftHand : Device.RightHand;
                default:
                    return null;
            }
        }

        private static bool physicalDeviceInUse;

        public VRDeviceSystem(IServiceRegistry registry) : base(registry)
        {
            GetSystem = this;

            EnabledChanged += OnEnabledChanged;

            DrawOrder = -100;
            UpdateOrder = -100;
        }

        public VRApi[] PreferredApis;

        public Dictionary<VRApi, float> PreferredScalings;

        public VRDevice Device { get; private set; }

        public bool RequireMirror;

        public bool PreviousUseCustomProjectionMatrix;

        public bool PreviousUseCustomViewMatrix;

        public Matrix PreviousCameraProjection;

        public float ResolutionScale;

        private void OnEnabledChanged(object sender, EventArgs eventArgs)
        {
            if (Enabled && Device == null)
            {
                if (PreferredApis == null)
                {
                    return;
                }

                double refreshRate = 90.0;

                if (physicalDeviceInUse)
                {
                    Device = null;
                    goto postswitch;
                }

                foreach (var hmdApi in PreferredApis)
                {
                    switch (hmdApi)
                    {
                        case VRApi.Dummy:
                        {
                            Device = new DummyDevice(Services);
                            break;
                        }
                        /*case VRApi.Oculus:
                        {
#if XENKO_GRAPHICS_API_DIRECT3D11
                            Device = new OculusOvrHmd();
                                
#endif
                            break;
                        }*/
                        case VRApi.OpenVR:
                        {
#if XENKO_GRAPHICS_API_VULKAN || XENKO_GRAPHICS_API_DIRECT3D11
                            Device = new OpenVRHmd(Game);
#endif
                            break;
                        
/*                        case VRApi.WindowsMixedReality:
                        {
#if XENKO_GRAPHICS_API_DIRECT3D11 && XENKO_PLATFORM_UWP
                            if (Windows.Graphics.Holographic.HolographicSpace.IsAvailable && GraphicsDevice.Presenter is WindowsMixedRealityGraphicsPresenter)
                            {
                                Device = new WindowsMixedRealityHmd();
                            }
#endif
                            break;*/
                        }
                        //case VRApi.Fove:
                        //{
                        //#if XENKO_GRAPHICS_API_DIRECT3D11
                        //    Device = new FoveHmd();
                        //#endif
                        //break;
                        //}
                        //case VRApi.Google:
                        //{
                        //#if XENKO_PLATFORM_IOS || XENKO_PLATFORM_ANDROID
                        //    VRDevice = new GoogleVrHmd();
                        //#endif
                        //    break;
                        //}
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (Device != null)
                    {
                        Device.Game = Game;

                        if (Device != null && !Device.CanInitialize)
                        {
                            Device.Dispose();
                            Device = null;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

postswitch:

                var deviceManager = (GraphicsDeviceManager)Services.GetService<IGraphicsDeviceManager>();
                if (Device != null)
                {
                    Game.TreatNotFocusedLikeMinimized = false;
                    Game.DrawEvenMinimized = true;
                    Game.IsFixedTimeStep = false;
                    deviceManager.SynchronizeWithVerticalRetrace = false;

                    Device.RenderFrameScaling = PreferredScalings[Device.VRApi];
                    Device.Enable(GraphicsDevice, deviceManager, RequireMirror);
                    Device.SetTrackingSpace(TrackingSpace.Standing);
                    physicalDeviceInUse = true;

                    // default values
                    Game.TargetElapsedTime = Utilities.FromSecondsPrecise(1.0 / refreshRate);
                    Game.WindowMinimumUpdateRate.MinimumElapsedTime = Game.TargetElapsedTime;
                    Game.MinimizedMinimumUpdateRate.MinimumElapsedTime = Game.TargetElapsedTime;

#if XENKO_GRAPHICS_API_VULKAN || XENKO_GRAPHICS_API_DIRECT3D11
                    if (Device is OpenVRHmd)
                    {
                        // WaitGetPoses should throttle our application, so don't do it elsewhere
                        //refreshRate = ((OpenVRHmd)Device).RefreshRate();
                        Game.TargetElapsedTime = TimeSpan.Zero; //Utilities.FromSecondsPrecise(1.0 / refreshRate);
                        Game.WindowMinimumUpdateRate.MinimumElapsedTime = TimeSpan.Zero;
                        Game.MinimizedMinimumUpdateRate.MinimumElapsedTime = TimeSpan.Zero;
                    }
#endif
                }
                else
                {
                    //fallback to dummy device
                    Device = new DummyDevice(Services)
                    {
                        Game = Game,
                        RenderFrameScaling = 1.0f,
                    };
                    Device.Enable(GraphicsDevice, deviceManager, RequireMirror);
                }

                // init virtual buttons for use with VR input
                Xenko.Input.VirtualButton.RegisterExternalVirtualButtonType(typeof(Xenko.VirtualReality.VRButtons));
            }
        }

        public override void Update(GameTime gameTime)
        {
            Device?.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            Device?.Draw(gameTime);
        }

        protected override void Destroy()
        {
            if (Device != null)
            {
                if (!(Device is DummyDevice))
                {
                    physicalDeviceInUse = false;
                }

                Device.Dispose();
                Device = null;
            }
        }
    }
}
