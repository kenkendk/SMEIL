using System;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Reference to a constant value
    /// </summary>
    [DebuggerDisplay("Connection {Source} -> {Target}")]
    public class Connection : IInstance
    {
        /// <summary>
        /// The name of the connection entry
        /// </summary>
        public string Name => null;

        /// <summary>
        /// The source instance
        /// </summary>
        public readonly IInstance Source;
        /// <summary>
        /// The target instance
        /// </summary>
        public readonly IInstance Target;

        /// <summary>
        /// The source item
        /// </summary>
        public readonly AST.ParsedItem SourceItem;

        /// <summary>
        /// The source parameters
        /// </summary>
        public readonly AST.ConnectEntry DeclarationSource;

        /// <summary>
        /// Constructs a new connection between two signals
        /// </summary>
        /// <param name="source">The token where the connect was defined</param>
        /// <param name="from">The source signal</param>
        /// <param name="to">The target signal</param>
        public Connection(AST.ConnectEntry source, Signal from, Signal to)
        {
            this.DeclarationSource = source ?? throw new ArgumentNullException(nameof(source));
            this.Source = from ?? throw new ArgumentNullException(nameof(from));
            this.Target = to ?? throw new ArgumentNullException(nameof(to));
            this.SourceItem = source;
        }

        /// <summary>
        /// Constructs a new connection between two busses
        /// </summary>
        /// <param name="source">The token where the connect was defined</param>
        /// <param name="from">The source bus</param>
        /// <param name="to">The target bus</param>
        public Connection(AST.ConnectEntry source, Bus from, Bus to)
        {
            this.DeclarationSource = source ?? throw new ArgumentNullException(nameof(source));
            this.Source = from ?? throw new ArgumentNullException(nameof(from));
            this.Target = to ?? throw new ArgumentNullException(nameof(to));
            this.SourceItem = source;
        }
    }
}