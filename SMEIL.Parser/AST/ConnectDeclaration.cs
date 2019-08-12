using System;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a connect declaration
    /// </summary>
    public class ConnectDeclaration : NetworkDeclaration
    {
        /// <summary>
        /// The connect entries
        /// </summary>
        public readonly ConnectEntry[] Entries;

        /// <summary>
        /// Creates a new connect declaration
        /// </summary>
        /// <param name="item">The source token</param>
        /// <param name="source">The source identifier</param>
        /// <param name="target">The target identifier</param>
        public ConnectDeclaration(ParseToken item, ConnectEntry[] entries)
            : base(item)
        {
            this.Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            if (this.Entries.Length == 0)
                throw new ArgumentException("Cannot have an empty connect declaration", nameof(entries));
        }
    }
}