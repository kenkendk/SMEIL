using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// A bus definitions
    /// </summary>
    [DebuggerDisplay("BusDeclaration = {Name}")]
    public class BusDeclaration : NetworkDeclaration
    {
        /// <summary>
        /// The bus name
        /// </summary>
        public Identifier Name;
        /// <summary>
        /// The signals in the bus
        /// </summary>
        public BusSignalDeclaration[] Signals;
        /// <summary>
        /// The name of the type that defines the bus
        /// </summary>
        public TypeName TypeName;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Bus"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        /// <param name="name">The bus name.</param>
        /// <param name="signals">The signals in the bus.</param>
        /// <param name="typename">The typename used to define the bus</param>
        public BusDeclaration(ParseToken token, Identifier name, BusSignalDeclaration[] signals, TypeName typename)
            : base(token)
        {
            // Empty list is the same as no list here
            if (signals != null && signals.Length == 0)
                signals = null;

            if ((signals == null) == (typename == null))
                throw new ArgumentException($"Supply either {nameof(signals)} or {nameof(typename)}, not both");

            Name = name ?? throw new ArgumentNullException(nameof(name));
            Signals = signals;
            TypeName = typename;
            if (TypeName == null && Signals.Length == 0)
                throw new ParserException($"A bus must have at least one signal", token);

        }
    }
}
