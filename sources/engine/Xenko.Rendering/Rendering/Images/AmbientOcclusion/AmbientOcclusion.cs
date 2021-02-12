// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
using Xenko.Core;
using Xenko.Core.Annotations;
using Xenko.Core.Mathematics;
using Xenko.Graphics;

namespace Xenko.Rendering.Images
{
    /// <summary>
    /// Applies an ambient occlusion effect to a scene. Ambient occlusion is a technique which fakes occlusion for objects close to other opaque objects.
    /// It takes as input a color-buffer where the scene was rendered, with its associated depth-buffer.
    /// You also need to provide the camera configuration you used when rendering the scene.
    /// </summary>
    [DataContract("AmbientOcclusion")]
    public class AmbientOcclusion : ImageEffect
    {
        private ImageEffectShader aoRawImageEffect;
        private ImageEffectShader blur;
        private float[] offsetsWeights;

        private ImageEffectShader aoApplyImageEffect;

        public AmbientOcclusion()
        {
            //Enabled = false;

            NumberOfSamples = 13;
            ParamProjScale = 0.5f;
            ParamIntensity = 0.2f;
            ParamBias = 0.01f;
            ParamRadius = 1f;
            Blur = true;
            BlurScale = 1.85f;
            EdgeSharpness = 3f;
            ResolutionScale = 1f;
        }

        /// <userdoc>
        /// The number of pixels sampled to determine how occluded a point is. Higher values reduce noise, but affect performance.
        /// Use with "Blur count to find a balance between results and performance.
        /// </userdoc>
        [DataMember(10)]
        [DefaultValue(13)]
        [DataMemberRange(1, 50, 1, 5, 0)]
        [Display("Samples")]
        public int NumberOfSamples { get; set; } = 13;

        /// <userdoc>
        /// Scales the sample radius. In most cases, 1 (no scaling) produces the most accurate result.
        /// </userdoc>
        [DataMember(20)]
        [DefaultValue(0.5f)]
        [Display("Projection scale")]
        public float ParamProjScale { get; set; } = 0.5f;

        /// <userdoc>
        /// Scales the sample radius. In most cases, 1 (no scaling) produces the most accurate result.
        /// </userdoc>
        [DataMember(25)]
        [DefaultValue(300f)]
        [Display("Fade Distance")]
        public float ParamDistance { get; set; } = 300f;

        /// <userdoc>
        /// The strength of the darkening effect in occluded areas
        /// </userdoc>
        [DataMember(30)]
        [DefaultValue(0.2f)]
        [Display("Intensity")]
        public float ParamIntensity { get; set; } = 0.2f;

        /// <userdoc>
        /// The angle at which Xenko considers an area of geometry an occluder. At high values, only narrow joins and crevices are considered occluders.
        /// </userdoc>
        [DataMember(40)]
        [DefaultValue(0.01f)]
        [Display("Sample bias")]
        public float ParamBias { get; set; } = 0.01f;

        /// <userdoc>
        /// Use with "projection scale" to control the radius of the occlusion effect
        /// </userdoc>
        [DataMember(50)]
        [DefaultValue(1f)]
        [Display("Sample radius")]
        public float ParamRadius { get; set; } = 1f;

        /// <userdoc>
        /// The number of times the ambient occlusion image is blurred. Higher numbers reduce noise, but can produce artifacts.
        /// </userdoc>
        [DataMember(70)]
        [DefaultValue(true)]
        [Display("Apply Blur")]
        public bool Blur { get; set; } = true;

        /// <userdoc>
        /// The blur radius in pixels
        /// </userdoc>
        [DataMember(74)]
        [DefaultValue(1.85f)]
        [Display("Blur radius")]
        public float BlurScale { get; set; } = 1.85f;

        /// <userdoc>
        /// How much the blur respects the depth differences of occluded areas. Lower numbers create more blur, but might blur unwanted areas (ie beyond occluded areas).
        /// </userdoc>
        [DataMember(78)]
        [DefaultValue(3f)]
        [Display("Edge sharpness")]
        public float EdgeSharpness { get; set; } = 3f;

        /// <userdoc>
        /// The resolution the ambient occlusion is calculated at. The result is upscaled to the game resolution.
        /// Larger sizes produce better results but use more memory and affect performance.
        /// </userdoc>
        [DataMember(100)]
        [DefaultValue(1f)]
        [Display("Scale Resolution")]
        public float ResolutionScale {
            get => _resolutionScale;
            set
            {
                if (value > 1f)
                    _resolutionScale = 1f;
                else if (value < 0.01f)
                    _resolutionScale = 0.01f;
                else
                    _resolutionScale = value;
            }
        }

        private float _resolutionScale = 1f;

        protected override void InitializeCore()
        {
            base.InitializeCore();

            aoApplyImageEffect = ToLoadAndUnload(new ImageEffectShader("ApplyAmbientOcclusionShader"));

            aoRawImageEffect = ToLoadAndUnload(new ImageEffectShader("AmbientOcclusionRawAOEffect"));
            aoRawImageEffect.Initialize(Context);

            blur = ToLoadAndUnload(new ImageEffectShader("AmbientOcclusionBlurEffect"));
            blur.Initialize(Context);
        }

        protected override void Destroy()
        {
            base.Destroy();
        }

        /// <summary>
        /// Provides a color buffer and a depth buffer to apply the depth-of-field to.
        /// </summary>
        /// <param name="colorBuffer">A color buffer to process.</param>
        /// <param name="depthBuffer">The depth buffer corresponding to the color buffer provided.</param>
        public void SetColorDepthInput(Texture colorBuffer, Texture depthBuffer)
        {
            SetInput(0, colorBuffer);
            SetInput(1, depthBuffer);
        }

        protected override void DrawCore(RenderDrawContext context)
        {
            var originalColorBuffer = GetSafeInput(0);
            var originalDepthBuffer = GetSafeInput(1);

            var outputTexture = GetSafeOutput(0);

            var renderView = context.RenderContext.RenderView;

            //---------------------------------
            // Ambient Occlusion
            //---------------------------------

            aoRawImageEffect.Parameters.Set(AmbientOcclusionRawAOKeys.Count, NumberOfSamples > 0 ? NumberOfSamples : 9);

            // Set Near/Far pre-calculated factors to speed up the linear depth reconstruction
            aoRawImageEffect.Parameters.Set(CameraKeys.ZProjection, CameraKeys.ZProjectionACalculate(renderView.NearClipPlane, renderView.FarClipPlane));

            Vector4 screenSize = new Vector4(originalColorBuffer.Width, originalColorBuffer.Height, 0, 0);
            screenSize.Z = screenSize.X / screenSize.Y;

            // Projection infor used to reconstruct the View space position from linear depth
            var p00 = renderView.Projection.M11;
            var p11 = renderView.Projection.M22;
            var p02 = renderView.Projection.M13;
            var p12 = renderView.Projection.M23;
            Vector4 projInfo = new Vector4(-2.0f / (screenSize.X * p00), -2.0f / (screenSize.Y * p11), (1.0f - p02) / p00, (1.0f + p12) / p11);
            aoRawImageEffect.Parameters.Set(AmbientOcclusionRawAOShaderKeys.ProjInfo, projInfo);

            //**********************************
            // User parameters
            aoRawImageEffect.Parameters.Set(AmbientOcclusionRawAOShaderKeys.ScreenInfo, screenSize);
            aoRawImageEffect.Parameters.Set(AmbientOcclusionRawAOShaderKeys.ParamProjScale, ParamProjScale);
            aoRawImageEffect.Parameters.Set(AmbientOcclusionRawAOShaderKeys.ParamDistance, ParamDistance);
            aoRawImageEffect.Parameters.Set(AmbientOcclusionRawAOShaderKeys.ParamIntensity, ParamIntensity);
            aoRawImageEffect.Parameters.Set(AmbientOcclusionRawAOShaderKeys.ParamBias, ParamBias);
            aoRawImageEffect.Parameters.Set(AmbientOcclusionRawAOShaderKeys.ParamRadius, ParamRadius);
            aoRawImageEffect.Parameters.Set(AmbientOcclusionRawAOShaderKeys.ParamRadiusSquared, ParamRadius * ParamRadius);

            var tempWidth = (int)((float)originalColorBuffer.Width * ResolutionScale);
            var tempHeight = (int)((float)originalColorBuffer.Height * ResolutionScale);
            var aoTexture1 = NewScopedRenderTarget2D(tempWidth, tempHeight, PixelFormat.R8_UNorm, 1);

            aoRawImageEffect.SetInput(0, originalDepthBuffer);
            aoRawImageEffect.SetOutput(aoTexture1);
            aoRawImageEffect.Draw(context, "AmbientOcclusionRawAO");

            if (Blur)
            {
                var aoTexture2 = NewScopedRenderTarget2D(tempWidth, tempHeight, PixelFormat.R8_UNorm, 1);

                if (offsetsWeights == null)
                {
                    offsetsWeights = new[]
                    {
                        0.153170f, 0.144893f, 0.122649f, 0.092902f, 0.062970f, // stddev = 2.0
                    };
                }

                // Set Near/Far pre-calculated factors to speed up the linear depth reconstruction
                var zProj = CameraKeys.ZProjectionACalculate(renderView.NearClipPlane, renderView.FarClipPlane);
                blur.Parameters.Set(CameraKeys.ZProjection, ref zProj);

                blur.Parameters.Set(AmbientOcclusionBlurKeys.Count, offsetsWeights.Length);
                blur.Parameters.Set(AmbientOcclusionBlurKeys.BlurScale, BlurScale * 0.01f);
                blur.Parameters.Set(AmbientOcclusionBlurKeys.EdgeSharpness, EdgeSharpness);
                blur.EffectInstance.UpdateEffect(context.GraphicsDevice);

                blur.Parameters.Set(AmbientOcclusionBlurShaderKeys.Weights, offsetsWeights);
                blur.Parameters.Set(AmbientOcclusionBlurShaderKeys.BlurDistance, ParamDistance);

                blur.SetInput(0, aoTexture1);
                blur.SetInput(1, originalDepthBuffer);
                blur.SetInput(2, originalColorBuffer);
                blur.SetOutput(aoTexture2);
                blur.Draw(context);

                aoApplyImageEffect.SetInput(1, aoTexture2);
            }
            else
            {
                aoApplyImageEffect.SetInput(1, aoTexture1);
            }

            aoApplyImageEffect.SetInput(0, originalColorBuffer);
            aoApplyImageEffect.SetOutput(outputTexture);
            aoApplyImageEffect.Draw(context, "AmbientOcclusionApply");
        }
    }
}
