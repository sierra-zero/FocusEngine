// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using Xenko.Core;
using Xenko.Rendering;

namespace Xenko.Engine.Design
{
    /// <summary>
    /// Defines how <see cref="EffectCompilerFactory.CreateEffectCompiler"/> tries to create compiler.
    /// </summary>
    [Flags]
    public enum EffectCompilationMode
    {
        /// <summary>
        /// Effects can't be compiled. <see cref="Shaders.Compiler.NullEffectCompiler"/> will be used.
        /// </summary>
        None = 0,

        /// <summary>
        /// Effects can only be compiled in process (if possible). <see cref="Shaders.Compiler.EffectCompiler"/> will be used.
        /// </summary>
        Local = 1,
    }
}
