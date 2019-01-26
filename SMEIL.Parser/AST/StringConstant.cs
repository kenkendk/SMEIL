using System;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a string literal in a program
    /// </summary>
    [DebuggerDisplay("{Value}")]
    public class StringConstant : Constant
    {
        /// <summary>
        /// The string value
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// Constructs a new string constan
        /// </summary>
        /// <param name="token">The token where the string was found</param>
        /// <param name="value">The string that was found</param>
        public StringConstant(ParseToken token, string value)
            : base(token)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}