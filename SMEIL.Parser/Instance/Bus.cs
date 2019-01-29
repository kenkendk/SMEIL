using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Represents an instantiated bus
    /// </summary>
    [DebuggerDisplay("Bus = {Name}")]
    public class Bus : IInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => Source.Name?.Name;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.BusDeclaration Source;

        /// <summary>
        /// The shape of the bus
        /// </summary>
        /// <value></value>
        public readonly AST.BusShape Shape;

        /// <summary>
        /// The resolved type of the variable
        /// </summary>
        public Dictionary<string, AST.DataType> ResolvedSignalTypes { get; set; }

        /// <summary>
        /// The instances (signals) for this bus
        /// </summary>
        public readonly List<Instance.IInstance> Instances = new List<IInstance>();

        /// <summary>
        /// Constructs a new instance of the bus
        /// </summary>
        /// <param name="source">The bus declaration</param>
        public Bus(AST.BusDeclaration source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Shape = new AST.BusShape(source.SourceToken, source.Signals);
        }
    }
}