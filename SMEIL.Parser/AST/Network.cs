using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a network
    /// </summary>
    public class Network : Entity
    {
        /// <summary>
        /// The network name
        /// </summary>
        public readonly Identifier Name;

        /// <summary>
        /// The process parameters
        /// </summary>
        public readonly Parameter[] Parameters;
        /// <summary>
        /// The network declaration
        /// </summary>
        public readonly NetworkDeclaration[] Declarations;

        /// <summary>
        /// Creates a new parsed network instance
        /// </summary>
        /// <param name="name">The name of the instance</param>
        public Network(ParseToken token, Identifier name, Parameter[] parameters, NetworkDeclaration[] declarations)
            : base(token)
        {        
            Name = name ?? throw new ArgumentNullException(nameof(name)); ;
            Parameters = parameters ?? new Parameter[0];
            Declarations = declarations ?? throw new ArgumentNullException(nameof(declarations));
        }
    }
}