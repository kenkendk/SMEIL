using System;
using System.Collections.Generic;
using System.Linq;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a switch statement
    /// </summary>
    public class SwitchStatement : Statement
    {
        /// <summary>
        /// The value being switched on
        /// </summary>
        public readonly Expression Value;

        /// <summary>
        /// The cases in the switch statement
        /// </summary>
        public readonly Tuple<Expression, Statement[]>[] Cases;
        
        /// <summary>
        /// Constructs a new switch statement
        /// </summary>
        /// <param name="token">The parsed token</param>
        /// <param name="value">The value to switch on</param>
        /// <param name="cases">The cases in the switch</param>
        public SwitchStatement(ParseToken token, Expression value, Tuple<Expression, Statement[]>[] cases)
            : base(token)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Cases = cases ?? throw new ArgumentNullException(nameof(cases));
            if (Cases.Length == 0)
                throw new ParserException($"A switch statement must have at least one case", token);
        }

        /// <summary>
        /// Clones this statement and returns a copy of it
        /// </summary>
        /// <returns>A copy of the statement</returns>
        public override Statement Clone()
            => new SwitchStatement(
                SourceToken,
                Value.Clone(),
                Cases.Select(x => new Tuple<Expression, Statement[]>(x.Item1.Clone(), x.Item2.Select(y => y.Clone()).ToArray())).ToArray()
            );


    }
}