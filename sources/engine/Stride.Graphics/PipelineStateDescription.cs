// Copyright (c) Stride contributors (https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Shaders;

namespace Stride.Graphics
{
    public class PipelineStateDescription : IEquatable<PipelineStateDescription>
    {
        // Root Signature
        public RootSignature RootSignature;

        // Effect/Shader
        public EffectBytecode EffectBytecode;

        // Rendering States
        public BlendStateDescription BlendState;
        public uint SampleMask = 0xFFFFFFFF;
        public RasterizerStateDescription RasterizerState;
        public DepthStencilStateDescription DepthStencilState;

        // Input layout
        public InputElementDescription[] InputElements;

        public PrimitiveType PrimitiveType;

        public RenderOutputDescription Output;

        public unsafe PipelineStateDescription Clone()
        {
            InputElementDescription[] inputElements;
            if (InputElements != null)
            {
                inputElements = new InputElementDescription[InputElements.Length];
                for (int i = 0; i < inputElements.Length; ++i)
                    inputElements[i] = InputElements[i];
            }
            else
            {
                inputElements = null;
            }

            return new PipelineStateDescription
            {
                RootSignature = RootSignature,
                EffectBytecode = EffectBytecode,
                BlendState = BlendState,
                SampleMask = SampleMask,
                RasterizerState = RasterizerState,
                DepthStencilState = DepthStencilState,

                InputElements = inputElements,

                PrimitiveType = PrimitiveType,

                Output = Output,
            };
        }

        public void SetDefaults()
        {
            BlendState.SetDefaults();
            RasterizerState.SetDefault();
            DepthStencilState.SetDefault();
        }

        public bool Equals(PipelineStateDescription other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (!(RootSignature == other.RootSignature
                && EffectBytecode == other.EffectBytecode
                && BlendState.Equals(other.BlendState)
                && SampleMask == other.SampleMask
                && RasterizerState.Equals(other.RasterizerState)
                && DepthStencilState.Equals(other.DepthStencilState)
                && PrimitiveType == other.PrimitiveType
                && Output == other.Output))
                return false;

            recheck: if (InputElements != null && other.InputElements != null)
            {
                if (InputElements.Length != other.InputElements.Length) return false;
                try
                {
                    for (int i = 0; i < InputElements.Length && i < other.InputElements.Length; i++)
                    {
                        if (!InputElements[i].Equals(other.InputElements[i]))
                            return false;
                    }
                } catch(Exception e)
                {
                    // input elements changed during processing, which should be extremely rare
                    // but we don't want the engine to die, so lets just recheck
                    goto recheck;
                }
            }
            else if ((InputElements != null) != (other.InputElements != null))
                return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PipelineStateDescription)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RootSignature != null ? RootSignature.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (EffectBytecode != null ? EffectBytecode.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ BlendState.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)SampleMask;
                hashCode = (hashCode * 397) ^ RasterizerState.GetHashCode();
                hashCode = (hashCode * 397) ^ DepthStencilState.GetHashCode();
                if (InputElements != null)
                {
                    for (int i=0; i<InputElements.Length; i++)
                        hashCode = (hashCode * 397) ^ InputElements[i].GetHashCode();
                }

                hashCode = (hashCode * 397) ^ (int)PrimitiveType;
                hashCode = (hashCode * 397) ^ Output.GetHashCode();
                return hashCode;
            }
        }

        public long GetLongHashCode()
        {
            unchecked
            {
                long hashCode = RootSignature != null ? RootSignature.GetHashCode() : 271;
                hashCode = (hashCode * 397) ^ (EffectBytecode != null ? EffectBytecode.GetHashCode() : 541);
                hashCode = (hashCode * 503) ^ BlendState.GetHashCode();
                hashCode = (hashCode * 641) ^ (long)SampleMask;
                hashCode = (hashCode * 773) ^ RasterizerState.GetHashCode();
                hashCode = (hashCode * 997) ^ DepthStencilState.GetHashCode();
                if (InputElements != null)
                {
                    for (int i = 0; i < InputElements.Length; i++)
                        hashCode = (hashCode * 127) ^ InputElements[i].GetHashCode();
                }

                hashCode = (hashCode * 1021) ^ (long)PrimitiveType;
                hashCode = (hashCode * 1213) ^ Output.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(PipelineStateDescription left, PipelineStateDescription right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PipelineStateDescription left, PipelineStateDescription right)
        {
            return !Equals(left, right);
        }
    }
}
