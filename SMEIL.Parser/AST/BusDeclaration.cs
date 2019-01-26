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
        /// A flag indicating if the bus is exposed to external code
        /// </summary>
        public bool Exposed;
        /// <summary>
        /// A flag indicating if the bus is unique
        /// </summary>
        public bool Unique;
        /// <summary>
        /// The bus name
        /// </summary>
        public Identifier Name;
        /// <summary>
        /// The signals in the bus
        /// </summary>
        public BusSignalDeclaration[] Signals;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Bus"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        /// <param name="exposed">A flag indicating if the bus is exposed</param>
        /// <param name="name">The bus name.</param>
        /// <param name="signals">The signals in the bus.</param>
        public BusDeclaration(ParseToken token, bool exposed, bool isUnique, Identifier name, BusSignalDeclaration[] signals)
            : base(token)
        {
            Exposed = exposed;
            Unique = isUnique;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Signals = signals ?? throw new ArgumentNullException(nameof(signals));
        }
    }
}
