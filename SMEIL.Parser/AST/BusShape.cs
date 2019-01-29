using System;
using System.Linq;
using System.Collections.Generic;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents the inferred shape of a bus
    /// </summary>
    public class BusShape : ParsedItem
    {
        /// <summary>
        /// The list of signals the bus must have for it to be compatible
        /// </summary>
        public readonly IDictionary<string, TypeName> Signals;

        /// <summary>
        /// Builds a shape for a given identifier within the process
        /// </summary>
        /// <param name="source">The source token</param>
        /// <param name="signals">The signals in the bus</param>
        public BusShape(ParseToken source, IEnumerable<AST.BusSignalDeclaration> signals)
            : base(source)
        {
            Signals = signals.ToDictionary(x => x.Name.Name, x => x.Type);
        }

        /// <summary>
        /// Builds a shape for a given identifier within the process
        /// </summary>
        /// <param name="source">The source token</param>
        /// <param name="contents">The signals in the bus</param>
        public BusShape(ParseToken source, IDictionary<string, TypeName> contents)
            : base(source)
        {
            Signals = new Dictionary<string, TypeName>(contents);
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
                if (!a.Signals.TryGetValue(signal.Key, out var t) || !object.Equals(t, signal.Value))
                {
                    if (throwOnError)
                    {
                        if (t == null)
                            throw new Exception($"Incompatible bus shapes, missing signal {signal.Key} of type {signal.Value}");
                        else
                            throw new Exception($"Incompatible bus shapes, signal {signal.Key} has type {t} bus should be type {signal.Value}");
                    }

                    return false;
                }

            return true;
        }

    }
}
