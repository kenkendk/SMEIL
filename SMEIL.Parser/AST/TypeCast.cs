using System;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Implements a type-cast operation, either explicit or implicit
    /// </summary>
    public class TypeCast : ParenthesizedExpression
    {
        /// <summary>
        /// A value indicating if the typecase is explicit (in the source program), or implicit (inserted by the parser)
        /// </summary>
        public readonly bool Explicit;

        /// <summary>
        /// The data type being type cast to
        /// </summary>
        public readonly TypeName TargetName;

        /// <summary>
        /// Constructs a new typecast expression
        /// </summary>
        /// <param name="expression">The expression inside the parenthesis</param>
        /// <param name="targetype">The target type for the typecast</param>
        /// <param name="explicit">A value indicating if the typecast is explicit or implicit</param>
        public TypeCast(Expression expression, TypeName targettype, bool @explicit)
            : this(expression.SourceToken, expression, targettype, @explicit)
        {
        }

        /// <summary>
        /// Constructs a new typecast expression
        /// </summary>
        /// <param name="expression">The expression inside the parenthesis</param>
        /// <param name="targetype">The target type for the typecast</param>
        /// <param name="explicit">A value indicating if the typecast is explicit or implicit</param>
        public TypeCast(Expression expression, DataType targettype, bool @explicit)
            : this(expression.SourceToken, expression, targettype, @explicit)
        {
        }

        /// <summary>
        /// Constructs a new typecast expression
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="expression">The expression inside the parenthesis</param>
        /// <param name="targetype">The target type for the typecast</param>
        /// <param name="explicit">A value indicating if the typecast is explicit or implicit</param>
        public TypeCast(ParseToken token, Expression expression, TypeName targettype, bool @explicit)
            : base(token, expression)
        {
            TargetName = targettype ?? throw new ArgumentNullException(nameof(targettype));
            Explicit = @explicit;
        }

        /// <summary>
        /// Constructs a new typecast expression
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="expression">The expression inside the parenthesis</param>
        /// <param name="targetype">The target type for the typecast</param>
        /// <param name="explicit">A value indicating if the typecast is explicit or implicit</param>
        public TypeCast(ParseToken token, Expression expression, DataType targettype, bool @explicit)
            : base(token, expression)
        {
            TargetName = new TypeName(targettype, null);
            Explicit = @explicit;
        }

        /// <summary>
        /// Clones this expression and returns a copy of it
        /// </summary>
        /// <returns>A copy of the expression</returns>
        public override Expression Clone()
            => new TypeCast(
                SourceToken,                
                Expression.Clone(),
                TargetName,
                Explicit
            );
    }
}