using System;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Represents a for loop instance
    /// </summary>
    public class ForLoop : IInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => null;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.ForStatement Source;

        /// <summary>
        /// Creates a new variable instnace
        /// </summary>
        /// <param name="source">The source item</param>
        public ForLoop(AST.ForStatement source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }
    }
}