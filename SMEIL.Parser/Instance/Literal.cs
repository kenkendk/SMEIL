using System;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Reference to a constant value
    /// </summary>
    [DebuggerDisplay("Literal: {Source}")]
    public class Literal : IInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => null;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.Constant Source;

        /// <summary>
        /// Creates a new variable instnace
        /// </summary>
        /// <param name="source">The source item</param>
        public Literal(AST.Constant source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

    }
}