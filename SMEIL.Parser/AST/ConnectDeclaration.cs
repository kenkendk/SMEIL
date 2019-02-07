using System;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a connect declaration
    /// </summary>
    public class ConnectDeclaration : ParsedItem
    {
        /// <summary>
        /// The entries in the declaration
        /// </summary>
        public readonly ConnectEntry[] Entries;

        /// <summary>
        /// Creates a new connect declaration
        /// </summary>
        /// <param name="item">The parsed item</param>
        /// <param name="entries">The connection entries</param>
        public ConnectDeclaration(ParseToken item, ConnectEntry[] entries)
            : base(item)
        {
            this.Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }
    }
}