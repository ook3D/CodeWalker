using System;

namespace ST.Library.UI.NodeEditor
{
    internal static class STNodeFactory
    {
        public static STNode Create(Type nodeType)
        {
            ArgumentNullException.ThrowIfNull(nodeType);
            if (!typeof(STNode).IsAssignableFrom(nodeType))
                throw new ArgumentException("The type must derive from STNode.", nameof(nodeType));

            return Activator.CreateInstance(nodeType) as STNode
                ?? throw new InvalidOperationException($"Unable to create node type '{nodeType.FullName}'.");
        }
    }
}
