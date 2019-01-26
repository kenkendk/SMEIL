using System;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a floating constant
    /// </summary>
    [DebuggerDisplay("{Value}")]
    public class FloatingConstant : Constant
    {
        /// <summary>
        /// The major part of the number
        /// </summary>
        public readonly IntegerConstant Major;
        /// <summary>
        /// The minor part of the number
        /// </summary>
        public readonly IntegerConstant Minor;

        /// <summary>
        /// Constructs a new floating constant
        /// </summary>
        /// <param name="token">The parser token</param>
        /// <param name="major">The major component</param>
        /// <param name="minor">The minor component</param>
        public FloatingConstant(ParseToken token, string major, string minor)
            : this(token, new AST.IntegerConstant(token, major), new AST.IntegerConstant(token, minor))
        {
        }

        /// <summary>
        /// Constructs a new floating constant
        /// </summary>
        /// <param name="token">The parser token</param>
        /// <param name="major">The major component</param>
        /// <param name="minor">The minor component</param>
        public FloatingConstant(ParseToken token, IntegerConstant major, IntegerConstant minor)
            : base(token)
        {
            Major = major ?? throw new ArgumentNullException(nameof(major));
            Minor = minor ?? throw new ArgumentNullException(nameof(minor));
        }
        
    }
}