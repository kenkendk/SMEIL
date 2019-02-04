using System;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents an integer value
    /// </summary>
    [DebuggerDisplay("{Value}")]
    public class IntegerConstant : Constant
    {
        /// <summary>
        /// The integer literal
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// The value as an int64
        /// </summary>
        public long ToInt64 => long.Parse(Value);
        /// <summary>
        /// The value as an int32
        /// </summary>
        public int ToInt32 => int.Parse(Value);
        /// <summary>
        /// Gets the type of the constant
        /// </summary>
        public override ILType Type => ILType.SignedInteger;

        /// <summary>
        /// Constructs a new integer constant
        /// </summary>
        /// <param name="token">The token for the value</param>
        /// <param name="value">The value</param>
        public IntegerConstant(ParseToken token, string value)
            : base(token)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}