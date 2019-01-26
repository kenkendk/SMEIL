using System;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a name expression
    /// </summary>
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
    }
}