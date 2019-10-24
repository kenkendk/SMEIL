using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Marker class for entities
    /// </summary>
    public abstract class Entity : ParsedItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Entity"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        protected Entity(ParseToken token)
            : base(token)
        {
        }
    }
}
