using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Interface for an item that can be a network declaration
    /// </summary>
    public abstract class NetworkDeclaration : Declaration
    {
        /// <summary>
        /// Creates a new network declaration
        /// </summary>
        /// <param name="token">The token used to create the instance</param>
         public NetworkDeclaration(ParseToken token)
            : base(token)
         {
         
         }
    }
}