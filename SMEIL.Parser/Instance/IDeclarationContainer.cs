using System.Collections.Generic;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Interface for an instance that has declarations
    /// </summary>
    public interface IDeclarationContainer : IInstance
    {
        /// <summary>
        /// The declarations in this item
        /// </summary>
        IEnumerable<AST.Declaration> Declarations { get; }
    }
}