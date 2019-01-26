using System;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents an identifier in the source
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class Identifier : ParsedItem
    {
        /// <summary>
        /// Gets the identifier.
        /// </summary>
        public string Name => SourceToken.Text;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Identifier"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        public Identifier(ParseToken token)
            : base(token)
        {
            if (!IsValidIdentifier(Name))
                throw new ArgumentException($"Invalid identifier {Name}", nameof(Name));
        }

        /// <summary>
        /// Evaluates an identifier and returns a value indicating if the identifier is valid
        /// </summary>
        /// <param name="text">The identifier to evaluate</param>
        /// <returns><c>true</c> if the text is a valid identifier, <c>false</c> otherwise</returns>
        public static bool IsValidIdentifier(string text)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, @"\w[\w\d\-_]*");
            return m.Success && m.Length == text.Length;
        }
    }
}
