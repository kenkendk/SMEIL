using System;
using System.Linq;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a function definition
    /// </summary>
    public class FunctionDefinition : Declaration
    {
        /// <summary>
        /// The name of the function
        /// </summary>
        public readonly Identifier Name;
        /// <summary>
        /// The parameters for the function
        /// </summary>
        public readonly Parameter[] Parameters;
        /// <summary>
        /// The declarations on the function
        /// </summary>
        public readonly Declaration[] Declarations;
        /// <summary>
        /// The statements inside the function
        /// </summary>
        public readonly Statement[] Statements;

        /// <summary>
        /// Parses a function definition
        /// </summary>
        /// <param name="token">The source token</param>
        /// <param name="name">The name of the function</param>
        /// <param name="parameters">The function parameters</param>
        /// <param name="statements">The statements in the function</param>
        public FunctionDefinition(ParseToken token, Identifier name, Parameter[] parameters, Declaration[] declarations, Statement[] statements)
            : base(token)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Statements = statements ?? throw new ArgumentNullException(nameof(statements));
            Declarations = declarations ?? throw new ArgumentNullException(nameof(declarations));
        }
    }
}