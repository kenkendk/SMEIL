using System;
using System.Linq;
using System.Collections.Generic;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents the inferred shape of a bus
    /// </summary>
    public class BusShape
    {
        /// <summary>
        /// The list of signals the bus must have for it to be compatible
        /// </summary>
        public readonly IDictionary<string, DataType> Signals;

        /// <summary>
        /// Builds a shape for a given identifier within the process
        /// </summary>
        /// <param name="signals">The signals in the bus</param>
        public BusShape(IDictionary<string, DataType> signals = null)
        {
            Signals = new Dictionary<string, DataType>(signals ?? new Dictionary<string, DataType>());
        }

        /// <summary>
        /// Creates a new shape for the given bus
        /// </summary>
        /// <param name="bus">The declaration to build the shape from</param>
        public BusShape(AST.BusDeclaration bus)
        {
            Signals = bus.Signals.ToDictionary(
                x => x.Name.Name,
                x => x.Type
            );
        }

        // /// <summary>
        // /// Analyses a process and returns the bus names and shapes
        // /// </summary>
        // /// <param name="process">The process to analyse</param>
        // /// <returns>A list of identifiers and their required shapes</returns>
        // public static Tuple<Parameter, BusShape>[] FindShapes(Process process)
        // {
        //     // Make a lookup table for the bus name
        //     var lookup = process            
        //         .Parameters
        //         .Zip(
        //             Enumerable.Range(0, process.Parameters.Length), 
        //             (a,b) => new { Index = b, Item = a, Shape = new BusShape() }
        //         )
        //         .ToDictionary(x => x.Item.Name.Name);

        //     foreach(var s in process.Statements.All())
        //     {
        //         if (s.Current is Name name && name.Identifier.Length == 2)
        //         {
        //             if (lookup.TryGetValue(name.Identifier.First().Name, out var el))
        //             {
        //                 // TODO: Figure out the signal type
        //                 xxx
        //                 el.Shape.Signals[name.Identifier.Last().Name] = null;
        //             }
        //         }
        //     }

        //     return lookup
        //         .Values
        //         .OrderBy(x => x.Index)
        //         .Select(x => new Tuple<Parameter, BusShape>(x.Item, x.Shape))
        //         .ToArray();
        // }

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
                if (!a.Signals.TryGetValue(signal.Key, out var t) || t != signal.Value)
                {
                    if (throwOnError)
                        throw new Exception($"Incompatible bus shapes, missing signal {signal.Key} of type {signal.Value}");

                    return false;
                }

            return true;
        }

    }
}
