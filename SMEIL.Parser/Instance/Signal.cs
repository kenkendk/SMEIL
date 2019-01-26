using System;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Represents a signal instance
    /// </summary>
    [DebuggerDisplay("Signal = {Name}")]
    public class Signal : IInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => Source.Name?.Name;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.BusSignalDeclaration Source;

        /// <summary>
        /// The parent bus instance
        /// </summary>
        public readonly Instance.Bus ParentBus;
        
        /// <summary>
        /// Constructs a new signal instance
        /// </summary>
        /// <param name="source">The signal used as the source</param>
        public Signal(Instance.Bus parent, AST.BusSignalDeclaration source)
        {
            ParentBus = parent ?? throw new ArgumentNullException(nameof(parent));
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }
    }
}