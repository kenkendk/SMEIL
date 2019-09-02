using System;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Reference to an enum field
    /// </summary>
    [DebuggerDisplay("Enum field {ParentType.Name}.{Name} = {Value}")]
    public class EnumFieldReference : IInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => Source.Name?.Name;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.EnumField Source;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly Instance.EnumTypeReference ParentType;

        /// <summary>
        /// The value assigned to this item
        /// </summary>
        public readonly int Value;

        /// <summary>
        /// Creates a new variable instnace
        /// </summary>
        /// <param name="source">The parent enum reference</param>
        /// <param name="field">The field to reference</param>
        /// <param name="value">The value the enum represents</param>
        public EnumFieldReference(Instance.EnumTypeReference source, AST.EnumField field, int value)
        {
            Source = field ?? throw new ArgumentNullException(nameof(field));
            ParentType = source ?? throw new ArgumentNullException(nameof(ParentType));
            Value = value;
        }

    }
}