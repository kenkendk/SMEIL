using System;
using System.Collections.Generic;
using System.Linq;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents an if statement
    /// </summary>
    public class IfStatement : Statement
    {
        /// <summary>
        /// The primary condition
        /// </summary>
        public readonly Expression Condition;
        /// <summary>
        /// The statements in the the truth part
        /// </summary>
        public readonly Statement[] TrueStatements;
        /// <summary>
        /// Optional else-if items, where Item1 is the condition and Item2 os the statements
        /// </summary>
        public readonly Tuple<Expression, Statement[]>[] ElIfStatements;
        /// <summary>
        /// The statements in the false part
        /// </summary>
        public readonly Statement[] FalseStatements;

        /// <summary>
        /// Constructs a new if statement
        /// </summary>
        /// <param name="token">The token where the statement was found</param>
        /// <param name="condition">The condition expression</param>
        /// <param name="trueStatements">The truth statements</param>
        /// <param name="elifstatements">The optional else-if statements</param>
        /// <param name="falseStatements">The false statements</param>
        public IfStatement(ParseToken token, Expression condition, Statement[] trueStatements, Tuple<Expression, Statement[]>[] elifstatements, Statement[] falseStatements)
            : base(token)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            TrueStatements = trueStatements;
            FalseStatements = falseStatements;
            ElIfStatements = elifstatements;
        }

        /// <summary>
        /// Constructs a new if statement
        /// </summary>
        /// <param name="token">The token where the statement was found</param>
        /// <param name="condition">The condition expression</param>
        /// <param name="trueStatements">The truth statements</param>
        /// <param name="falseStatements">The false statements</param>
        public IfStatement(ParseToken token, Expression condition, Statement[] trueStatements, Statement[] falseStatements)
            : this(token, condition, trueStatements, null, falseStatements)
        {
            
        }

        /// <summary>
        /// Clones this statement and returns a copy of it
        /// </summary>
        /// <returns>A copy of the statement</returns>
        public override Statement Clone()
            => new IfStatement(
                SourceToken,
                Condition.Clone(),
                TrueStatements?.Select(x => x.Clone()).ToArray(),
                ElIfStatements?.Select(x => new Tuple<Expression, Statement[]>(x.Item1.Clone(), x.Item2.Select(y => y.Clone()).ToArray())).ToArray(),
                FalseStatements?.ToArray()
            );


    }
}