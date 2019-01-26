using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a parameter map
    /// </summary>
    public class ParameterMap : ParsedItem
    {
        /// <summary>
        /// The name of the parameter to match
        /// </summary>
        public readonly Identifier Name;
        /// <summary>
        /// The expression for the parameter
        /// </summary>
        public readonly Expression Expression;

        /// <summary>
        /// Creates a new parameter map instance
        /// </summary>
        /// <param name="token">The token used to create the instance</param>
        /// <param name="name">The name of the parameter</param>
        /// <param name="expression">The parameter value</param>
        public ParameterMap(ParseToken token, Identifier name, Expression expression)
            : base(token)
        {
            Name = name;
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
    }
}