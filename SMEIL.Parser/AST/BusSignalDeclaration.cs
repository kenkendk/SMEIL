using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// The direction of a parameter
    /// </summary>
    public enum SignalDirection
    {
        /// <summary>
        /// The signal follows the parameter direction
        /// </summary>
        Normal,
        /// <summary>
        /// The signal inverses the parameter direction
        /// </summary>
        Inverse,
    }

    /// <summary>
    /// Represents a signal declaration on a bus
    /// </summary>
    [DebuggerDisplay("BusSignalDeclaration = {Name}")]
    public class BusSignalDeclaration : ParsedItem
    {
        /// <summary>
        /// The name of the signal
        /// </summary>
        public readonly Identifier Name;
        /// <summary>
        /// The signal data type
        /// </summary>
        public readonly TypeName Type;
        /// <summary>
        /// The initializer expression, if any
        /// </summary>
        public readonly Expression Initializer;
        /// <summary>
        /// The signal direction
        /// </summary>
        public readonly SignalDirection Direction;

        /// <summary>
        /// Creates a new bus signal declaration
        /// </summary>
        /// <param name="source">The token source</param>
        /// <param name="name">The signal name</param>
        /// <param name="type">The signal type</param>
        /// <param name="initializer">The optional initializer</param>
        /// <param name="direction">The optional signal direction</param>
        public BusSignalDeclaration(ParseToken source, Identifier name, TypeName type, Expression initializer, SignalDirection direction)
            : base(source)
        {
            Name = name;
            Type = type;
            Initializer = initializer;
            Direction = direction;
        }
    }
}