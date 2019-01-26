using System;
using System.Collections.Generic;

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
        }
    }
}