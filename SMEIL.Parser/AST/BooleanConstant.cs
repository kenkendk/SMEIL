using System.Collections.Generic;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a boolean constant
    /// </summary>
    [DebuggerDisplay("{Value}")]
    public class BooleanConstant : Constant
    {
        /// <summary>
        /// The value used in this constant
        /// </summary>
        public readonly bool Value;

        /// <summary>
        /// Gets the type of the constant
        /// </summary>
        public override ILType Type => ILType.Bool;

        /// <summary>
        /// Constructs a new boolean constnt
        /// </summary>
        /// <param name="token">The token where the constant was found</param>
        /// <param name="value">The value to extract</param>
        public BooleanConstant(ParseToken token, bool value)
            : base(token)
        {
            Value = value;            
        }
    }
}