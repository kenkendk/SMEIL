using System;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a constant declaration
    /// </summary>
    [DebuggerDisplay("ConstantDeclaration {Name} = {Expression}")]
    public class ConstantDeclaration : NetworkDeclaration, IFunctionDeclaration
    {
        /// <summary>
        /// The constant identifier
        /// </summary>
        public readonly Identifier Name;
        /// <summary>
        /// The constant type
        /// </summary>
        public TypeName DataType;
        /// <summary>
        /// The constant value
        /// </summary>
        public Expression Expression;

        /// <summary>
        /// Constructs a new constant declaration
        /// </summary>
        /// <param name="token">The token to use</param>
        /// <param name="name">The name of the constant</param>
        /// <param name="dataType">The type of the constant</param>
        /// <param name="expression">The value of the constant element</param>
        public ConstantDeclaration(ParseToken token, Identifier name, TypeName dataType, Expression expression)
            : base(token)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
    }
}