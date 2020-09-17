using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Core.Storage;
using Xenko.Rendering.Compositing;
using Xenko.Rendering.Materials;
using Xenko.Rendering.Materials.ComputeColors;
using Xenko.Shaders;

namespace Xenko.Rendering.Rendering.Materials
{
    [DataContract("MaterialFogFeature")]
    [Display("GlobalFog")]
    public class GlobalFog : MaterialFeature, IMaterialFogFeature
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FogData
        {
            public Vector4 FogColor;
            public float FogStart;
        }

        internal static bool usingGlobalFog = false;

        private static FogData GlobalFogParameters;

        public static void SetGlobalFog(Color3? color = null, float? density = null, float? fogstart = null)
        {
            if (color.HasValue)
            {
                GlobalFogParameters.FogColor.X = color.Value.R;
                GlobalFogParameters.FogColor.Y = color.Value.G;
                GlobalFogParameters.FogColor.Z = color.Value.B;
            }

            if (density.HasValue)
            {
                GlobalFogParameters.FogColor.W = density.Value;
            }

            if (fogstart.HasValue)
            {
                GlobalFogParameters.FogStart = fogstart.Value;
            }

            usingGlobalFog = true;
        }

        public static void GetGlobalFog(out Color3 color, out float density, out float fogstart)
        {
            color = ((Color4)GlobalFogParameters.FogColor).ToColor3();
            density = GlobalFogParameters.FogColor.W;
            fogstart = GlobalFogParameters.FogStart;
        }

        [DataMember]
        public Color3 FogColor
        {
            get => ((Color4)GlobalFogParameters.FogColor).ToColor3();
            set
            {
                GlobalFogParameters.FogColor.X = value.R;
                GlobalFogParameters.FogColor.Y = value.G;
                GlobalFogParameters.FogColor.Z = value.B;
                usingGlobalFog = true;
            }
        }
        
        [DataMember]
        public float FogDensity
        {
            get => GlobalFogParameters.FogColor.W;
            set
            {
                GlobalFogParameters.FogColor.W = value;
                usingGlobalFog = true;
            }
        }

        [DataMember]
        public float FogStart
        {
            get => GlobalFogParameters.FogStart;
            set
            {
                GlobalFogParameters.FogStart = value;
                usingGlobalFog = true;
            }
        }

        public bool Equals(IMaterialShadingModelFeature other)
        {
            return other is GlobalFog;
        }

        internal static unsafe void PrepareFogConstantBuffer(RenderContext context)
        {
            foreach (var renderFeature in context.RenderSystem.RenderFeatures)
            {
                if (!(renderFeature is RootEffectRenderFeature))
                    continue;

                var renderView = context.RenderView;
                var logicalKey = ((RootEffectRenderFeature)renderFeature).CreateViewLogicalGroup("GlobalFog");
                var viewFeature = renderView.Features[renderFeature.Index];

                foreach (var viewLayout in viewFeature.Layouts)
                {
                    var resourceGroup = viewLayout.Entries[renderView.Index].Resources;

                    var logicalGroup = viewLayout.GetLogicalGroup(logicalKey);
                    if (logicalGroup.Hash == ObjectId.Empty)
                        continue;

                    var mappedCB = (FogData*)(resourceGroup.ConstantBuffer.Data + logicalGroup.ConstantBufferOffset);
                    mappedCB->FogColor = GlobalFogParameters.FogColor;
                    mappedCB->FogStart = GlobalFogParameters.FogStart;
                }
            }
        }

        public override void GenerateShader(MaterialGeneratorContext context)
        {
            usingGlobalFog = true;

            var mixin = new ShaderMixinSource();
            mixin.Mixins.Add(new ShaderClassSource("FogFeature"));

            var shaderBuilder = context.AddShading(this);
            shaderBuilder.ShaderSources.Add(new ShaderClassSource("FogFeature"));
        }
    }
}
