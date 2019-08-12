using System;

namespace SMEIL.Parser.AST
{
    public class ConnectEntry : ParsedItem
    {
        /// <summary>
        /// The source identifier
        /// </summary>
        public readonly Name Source;
        /// <summary>
        /// The target identifier
        /// </summary>
        public readonly Name Target;

        /// <summary>
        /// Creates a new connect entry
        /// </summary>
        /// <param name="item">The source token</param>
        /// <param name="source">The source identifier</param>
        /// <param name="target">The target identifier</param>
        public ConnectEntry(ParseToken token, Name source, Name target) 
            : base(token)
        {
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
            this.Target = target ?? throw new ArgumentNullException(nameof(target));
        }
    }
}