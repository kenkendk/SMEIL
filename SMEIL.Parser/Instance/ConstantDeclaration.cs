using System;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Reference to a constant value
    /// </summary>
    [DebuggerDisplay("Constant {Name}")]
    public class ConstantReference : IInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => Source.Name?.Name;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.ConstantDeclaration Source;

        /// <summary>
        /// Creates a new variable instnace
        /// </summary>
        /// <param name="source">The source item</param>
        public ConstantReference(AST.ConstantDeclaration source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

    }
}