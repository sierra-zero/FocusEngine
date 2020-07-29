// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Xenko.Core;
using Xenko.Core.Serialization;
using Xenko.Core.Serialization.Contents;
using Xenko.Engine.Design;
using Xenko.UI;

namespace Xenko.Engine
{
    [DataContract("UIlibrary")]
    [ContentSerializer(typeof(DataContentSerializerWithReuse<UILibrary>))]
    [ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<UILibrary>), Profile = "Content")]
    public class UILibrary : ComponentBase
    {
        public UILibrary()
        {
            UIElements = new Dictionary<string, UIElement>();
        }

        /// <summary>
        /// Gets the UI elements.
        /// </summary>
        public Dictionary<string, UIElement> UIElements { get; }

        /// <summary>
        /// Instantiates a copy of the element of the library identified by <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="TElement">The type of the element.</typeparam>
        /// <param name="library">The library.</param>
        /// <param name="name">The name of the element in the library.</param>
        /// <returns></returns>
        public TElement InstantiateElement<TElement>(string name)
            where TElement : UIElement
        {
            UIElement source;
            if (UIElements.TryGetValue(name, out source))
            {
                return UICloner.Clone(source) as TElement;
            }
            return null;
        }

        /// <summary>
        /// Instantiates an element without knowing its type explicitly
        /// </summary>
        public UIElement InstantiateElement(string name)
        {
            UIElement source;
            if (UIElements.TryGetValue(name, out source))
            {
                return UICloner.Clone(source);
            }
            return null;
        }

        /// <summary>
        /// Instantiates this library by cloning the first root element
        /// </summary>
        public UIElement InstantiateFirstRoot()
        {
            foreach (UIElement uie in UIElements.Values)
            {
                if (uie.Parent == null)
                    return UICloner.Clone(uie);
            }
            return null;
        }

        /// <summary>
        /// Instantiates this library by cloning all root elements
        /// </summary>
        public List<UIElement> InstantiateAllRoots()
        {
            List<UIElement> elements = new List<UIElement>();
            foreach (UIElement uie in UIElements.Values)
            {
                if (uie.Parent == null)
                    elements.Add(UICloner.Clone(uie));
            }
            return elements;
        }

        /// <summary>
        /// Instantiates this library by cloning the first element of a given type
        /// </summary>
        public TElement InstantiateFirst<TElement>()
            where TElement : UIElement
        {
            foreach (UIElement uie in UIElements.Values)
            {
                if (uie is TElement)
                    return UICloner.Clone(uie) as TElement;
            }
            return null;
        }
    }
}
