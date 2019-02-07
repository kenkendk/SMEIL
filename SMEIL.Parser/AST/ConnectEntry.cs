using System;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a connection entry
    /// </summary>
    public class ConnectEntry : ParsedItem
    {
        /// <summary>
        /// The source identifier
        /// </summary>
        public readonly Identifier Source;
        /// <summary>
        /// The target identifier
        /// </summary>
        public readonly Identifier Target;

        /// <summary>
        /// Creates a new connection entry
        /// </summary>
        /// <param name="item">The source token</param>
        /// <param name="source">The source identifier</param>
        /// <param name="target">The target identifier</param>
        public ConnectEntry(ParseToken item, Identifier source, Identifier target)
            : base(item)
        {
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
            this.Target = target ?? throw new ArgumentNullException(nameof(target));
        }
    }
}