using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a single enum value
    /// </summary>
    public class EnumField : ParsedItem
    {
        /// <summary>
        /// The name of the enum
        /// </summary>
        public readonly Identifier Name;

        /// <summary>
        /// The enum's numeric value
        /// </summary>
        public readonly int Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.EnumField"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        /// <param name="name">The enum name.</param>
        /// <param name="value">The enum value.</param>
        public EnumField(ParseToken token, Identifier name, int value)
            : base(token)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
        }
    }
}
