namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a function declaration
    /// </summary>
    public class FunctionDeclaration : Declaration
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
        /// The statements inside the function
        /// </summary>
        public readonly Statement[] Statements;

        /// <summary>
        /// Parses a function declaration
        /// </summary>
        /// <param name="token">The source token</param>
        /// <param name="name">The name of the function</param>
        /// <param name="parameters">The function parameters</param>
        /// <param name="statements">The statements in the function</param>
        public FunctionDeclaration(ParseToken token, Identifier name, Parameter[] parameters, Statement[] statements)
            : base(token)
        {
            Name = name;
            Parameters = parameters;
            Statements = statements;
        }
    }
}