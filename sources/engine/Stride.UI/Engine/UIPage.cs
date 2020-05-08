// Copyright (c) Stride contributors (https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using Stride.Core;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.UI;

namespace Stride.Engine
{
    /// <summary>
    /// A page containing a UI hierarchy.
    /// </summary>
    [DataContract("UIPage")]
    [ContentSerializer(typeof(DataContentSerializerWithReuse<UIPage>))]
    [ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<UIPage>), Profile = "Content")]
    public sealed class UIPage : ComponentBase
    {
        /// <summary>
        /// Gets or sets the root element of the page.
        /// </summary>
        /// <userdoc>The root element of the page.</userdoc>
        [DataMember]
        public UIElement RootElement { get; set; }

        /// <summary>
        /// Shortcut to gathering a dictionary of UIElements (keyed by name) from page of type T
        /// </summary>
        /// <typeparam name="T">Type of UIElements to gather, can be UIElement too for all</typeparam>
        /// <param name="dictionary">Dictionary to populate</param>
        /// <param name="childrenOnly">Include the root, or just the children?</param>
        public void GatherUIDictionary<T>(ref Dictionary<string, T> dictionary, bool childrenOnly = false) where T : UIElement
        {
            RootElement.GatherUIDictionary<T>(ref dictionary, childrenOnly);
        }

        /// <summary>
        /// Shortcut to gathering a dictionary of UIElements from page of type T
        /// </summary>
        /// <typeparam name="T">Type of UIElements to gather, can be UIElement too for all</typeparam>
        /// <param name="childrenOnly">Include the root, or just the children?</param>
        /// <returns>Populated dictionary, keyed by name of UIElement</returns>
        public Dictionary<string, T> GatherUIDictionary<T>(bool childrenOnly = false) where T : UIElement
        {
            return RootElement.GatherUIDictionary<T>(childrenOnly);
        }
    }
}
