using System;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Interface for an item that can be a declaration
    /// </summary>
    public abstract class Declaration : ParsedItem
    {
        /// <summary>
        /// Creates a new declaration item
        /// </summary>
        /// <param name="token">The token used to create the instance</param>
        public Declaration(ParseToken token)
            : base(token)
        {        
        }
    }
}