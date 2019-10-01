using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// A variable or constant item
    /// </summary>
    public class VariableDeclaration : Declaration, IFunctionDeclaration
    {
        /// <summary>
        /// The variable name
        /// </summary>
        public readonly Identifier Name;
        /// <summary>
        /// The variable data type
        /// </summary>
        public readonly TypeName Type;
        /// <summary>
        /// The expression initializer
        /// </summary>
        public readonly Expression Initializer;
        /// <summary>
        /// Optional range for the variable
        /// </summary>
        public readonly Range Range;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Variable"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        /// <param name="name">The name of the variable.</param>
        /// <param name="type">The data type.</param>
        /// <param name="initializer">The initializer expression</param>
        public VariableDeclaration(ParseToken token, Identifier name, TypeName type, Expression initializer, Range range)
            : base(token)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Initializer = initializer;
            Range = range;
        }
    }
}
