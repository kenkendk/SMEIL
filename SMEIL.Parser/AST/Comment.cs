using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a comment
    /// </summary>
    public class Comment : ParsedItem
    {
        /// <summary>
        /// The comment
        /// </summary>
        public readonly string Text;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Comment"/> class.
        /// </summary>
        /// <param name="token">The source token</param>
        /// <param name="comment">The comment.</param>
        public Comment(ParseToken token, string comment)
            : base(token)
        {
            Text = comment;
        }
    }
}
