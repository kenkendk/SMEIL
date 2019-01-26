using System;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Represents an instantiated variable
    /// </summary>
    [DebuggerDisplay("Variable = {Name}")]
    public class Variable : IInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => Source.Name?.Name;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.VariableDeclaration Source;

        /// <summary>
        /// Creates a new variable instnace
        /// </summary>
        /// <param name="source">The source item</param>
        public Variable(AST.VariableDeclaration source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }
    }
}