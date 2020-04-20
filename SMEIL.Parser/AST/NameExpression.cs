using System;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a name expression
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class NameExpression : Expression
    {
        /// <summary>
        /// The name in this expression
        /// </summary>
        public readonly Name Name;
        
        /// <summary>
        /// Constructs a new name token
        /// </summary>
        /// <param name="token"></param>
        /// <param name="name"></param>
        public NameExpression(ParseToken token, Name name)
            : base(token)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Returns a string representation of the expression, suitable for debugging
        /// </summary>
        public override string AsString => Name?.AsString;

        /// <summary>
        /// Clones this expression and returns a copy of it
        /// </summary>
        /// <returns>A copy of the expression</returns>
        public override Expression Clone()
            => new NameExpression(
                SourceToken,
                Name
            );

    }
}