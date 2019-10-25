using System;
using System.Linq;
using System.Collections.Generic;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// The type of a signal
    /// </summary>
    public struct BusShapeValue
    {
        /// <summary>
        /// The data type of the signal
        /// </summary>
        public TypeName Type;
        /// <summary>
        /// The direction of the signal
        /// </summary>
        public SignalDirection Direction;

        /// <summary>
        /// Creates a new bus shape value
        /// </summary>
        /// <param name="type">The type of the signal</param>
        /// <param name="direction">The direction of the signal</param>
        public BusShapeValue(TypeName type, SignalDirection direction)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Direction = direction;
        }

        /// <summary>
        /// Returns a string representation of the shape value
        /// </summary>
        /// <returns>A string representation of the shape value</returns>
        public override string ToString() {
            return $"{Type}, {Direction}";
        }
    }

    /// <summary>
    /// Represents the inferred shape of a bus
    /// </summary>
    public class BusShape : ParsedItem
    {
        /// <summary>
        /// The list of signals the bus must have for it to be compatible
        /// </summary>
        public readonly IDictionary<string, BusShapeValue> Signals;

        /// <summary>
        /// Builds a shape for a given identifier within the process
        /// </summary>
        /// <param name="source">The source token</param>
        /// <param name="signals">The signals in the bus</param>
        public BusShape(ParseToken source, IEnumerable<AST.BusSignalDeclaration> signals)
            : base(source)
        {
            Signals = signals.ToDictionary(x => x.Name.Name, x => new BusShapeValue(x.Type, x.Direction));
            if (Signals.Count == 0)
                throw new ParserException("Cannot have an empty set of signals in a bus shape", source);            
        }

        /// <summary>
        /// Builds a shape for a given identifier within the process
        /// </summary>
        /// <param name="source">The source token</param>
        /// <param name="contents">The signals in the bus</param>
        public BusShape(ParseToken source, IDictionary<string, BusShapeValue> contents)
            : base(source)
        {
            Signals = new Dictionary<string, BusShapeValue>(contents);
            if (Signals.Count == 0)
                throw new ParserException("Cannot have an empty set of signals in a bus shape", source);
        }

        /// <summary>
        /// Returns a value indicating if <paramref name="a"/> can be assigned to <paramref name="b"/>
        /// </summary>
        /// <param name="a">One shape</param>
        /// <param name="b">Another shape</param>
        /// <param name="throwOnError">A value indicating if an error message is thrown for incompatible bus shapes</param>
        /// <returns><c>true</c> if <paramref name="a"/> can be assigned to <paramref name="b"/>; <c>false</c> otherwise</returns>
        public static bool CanAssignTo(BusShape a, BusShape b, bool throwOnError = false)
        {
            foreach (var signal in b.Signals)
                if (!a.Signals.TryGetValue(signal.Key, out var t) || !object.Equals(t.Type, signal.Value.Type) || !object.Equals(t.Direction, signal.Value.Direction))
                {
                    if (throwOnError)
                    {
                        if (t.Type == null)
                            throw new Exception($"Incompatible bus shapes, missing signal {signal.Key} of type {signal.Value}");
                        else
                            throw new Exception($"Incompatible bus shapes, signal {signal.Key} has type {t} bus should be type {signal.Value}");
                    }

                    return false;
                }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "{" + string.Join(";", Signals.Select(x => $" {x.Key}: {x.Value}")) + " }";
        }

    }
}
