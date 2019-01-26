using System;

namespace SMEIL.Parser
{
    /// <summary>
    /// An exception with a source code location
    /// </summary>
    public class ParserException : Exception
    {
        /// <summary>
        /// The place in the source code where the problem was found
        /// </summary>
        public readonly ParseToken Location;

        /// <summary>
        /// The optional item that was found
        /// </summary>
        public readonly AST.ParsedItem Item;

        /// <summary>
        /// Constructs a new parserexception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="source">The location of the problem</param>
        public ParserException(string message, ParseToken source)
            : base(message)
        {
            Location = source;
        }

        /// <summary>
        /// Constructs a new parserexception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="source">The item with the problem</param>
        public ParserException(string message, AST.ParsedItem source)
            : this(message, source.SourceToken)
        {
            Item = source;
        }
    }
}
