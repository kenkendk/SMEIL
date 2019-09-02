using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Reference to a an enum
    /// </summary>
    [DebuggerDisplay("Enum {Name}")]
    public class EnumTypeReference : IInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => Source.Name?.Name;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.EnumDeclaration Source;

        /// <summary>
        /// A map of all the fields in the enum
        /// </summary>
        public readonly Dictionary<string, Instance.EnumFieldReference> Fields = new Dictionary<string, Instance.EnumFieldReference>();

        /// <summary>
        /// The instances (signals) for this bus
        /// </summary>
        public readonly List<Instance.IInstance> Instances = new List<IInstance>();

        /// <summary>
        /// Creates a new variable instnace
        /// </summary>
        /// <param name="source">The source item</param>
        public EnumTypeReference(AST.EnumDeclaration source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

    }
}