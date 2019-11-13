using System;
using System.Collections.Generic;
using System.Linq;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a type definition
    /// </summary>
    public class TypeDefinition : Declaration
    {
        /// <summary>
        /// The name of the type alias
        /// </summary>
        public readonly Identifier Name;

        /// <summary>
        /// The alias, or null if this is not an alias
        /// </summary>
        public readonly TypeName Alias;

        /// <summary>
        /// The shape this type defines, or null if this is an alias
        /// </summary>
        public readonly BusShape Shape;

        /// <summary>
        /// A lookup table with initializers
        /// </summary>
        public readonly Dictionary<string, AST.Expression> Initializers;

        /// <summary>
        /// Constructs a new type definition for an alias
        /// </summary>
        /// <param name="source">The source parse token</param>
        /// <param name="name">The name of this type definition</param>
        /// <param name="alias">The alias to use</param>
        public TypeDefinition(ParseToken source, Identifier name, TypeName alias)
            : base(source)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Alias = alias ?? throw new ArgumentNullException(nameof(alias));
            if (string.IsNullOrWhiteSpace(Name.Name))
                throw new ParserException($"The name of a type definition cannot be anonymous", source);
        }

        /// <summary>
        /// Constructs a new type definition for a bus shape
        /// </summary>
        /// <param name="source">The source parse token</param>
        /// <param name="name">The name of this type definition</param>
        /// <param name="alias">The shape to use</param>
        public TypeDefinition(ParseToken source, Identifier name, IEnumerable<BusSignalDeclaration> signals)
            : base(source)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(Name.Name))
                throw new ParserException($"The name of a type definition cannot be anonymous", source);

            if (signals == null)
                throw new ArgumentNullException(nameof(signals));

            Shape = new BusShape(source, signals);
            
            Initializers = signals
                .ToDictionary(
                    x => x.Name.Name,
                    x => x.Initializer
                );
        }        
    }
}