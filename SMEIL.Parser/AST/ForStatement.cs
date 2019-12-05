using System;
using System.Linq;

namespace SMEIL.Parser.AST
{   
    /// <summary>
    /// Represents a for statement
    /// </summary>
    public class ForStatement : Statement
    {
        /// <summary>
        /// The loop variable
        /// </summary>
        public readonly VariableDeclaration Variable;

        /// <summary>
        /// The expression for the loop start
        /// </summary>
        public readonly Expression FromExpression;

        /// <summary>
        /// The expression where the loop terminates
        /// </summary>
        public readonly Expression ToExpression;

        /// <summary>
        /// The statements in the loop body
        /// </summary>
        public readonly Statement[] Statements;

        /// <summary>
        /// Constructs a new for statement
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="variable">The loop variable</param>
        /// <param name="fromExpression">The from expression</param>
        /// <param name="toExpression">The to expression</param>
        /// <param name="statements">The statements in the loop body</param>
        public ForStatement(ParseToken token, Identifier variable, Expression fromExpression, Expression toExpression, Statement[] statements)
            : this(
                token, 
                new AST.VariableDeclaration(
                    token, 
                    variable, 
                    new AST.TypeName(new AST.DataType(fromExpression.SourceToken, ILType.SignedInteger, -1), null), 
                    fromExpression
                ), 
                fromExpression, toExpression, statements)
        {
        }

        /// <summary>
        /// Constructs a new for statement
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="variable">The loop variable</param>
        /// <param name="fromExpression">The from expression</param>
        /// <param name="toExpression">The to expression</param>
        /// <param name="statements">The statements in the loop body</param>
        public ForStatement(ParseToken token, VariableDeclaration variable, Expression fromExpression, Expression toExpression, Statement[] statements)
            : base(token)
        {
            Variable = variable ?? throw new ArgumentNullException(nameof(variable));
            FromExpression = fromExpression ?? throw new ArgumentNullException(nameof(fromExpression));
            ToExpression = toExpression ?? throw new ArgumentNullException(nameof(toExpression));
            Statements = statements ?? throw new ArgumentNullException(nameof(statements));
        }

        /// <summary>
        /// Clones this statement and returns a copy of it
        /// </summary>
        /// <returns>A copy of the statement</returns>
        public override Statement Clone() 
            => new ForStatement(
                SourceToken,
                Variable,
                FromExpression.Clone(),
                ToExpression.Clone(),
                Statements.Select(x => x.Clone()).ToArray()       
            );
    }
}