﻿using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// The direction of a parameter
    /// </summary>
    public enum ParameterDirection
    {
        /// <summary>
        /// The parameter is an input
        /// </summary>
        In,
        /// <summary>
        /// The parameter is an output
        /// </summary>
        Out,
        /// <summary>
        /// The parameter is a constant
        /// </summary>
        Const
    }

    /// <summary>
    /// Represents a process parameter
    /// </summary>
    public class Parameter : ParsedItem
    {
        /// <summary>
        /// The name of the parameter
        /// </summary>
        public readonly Identifier Name;
        /// <summary>
        /// The direction of the parameter
        /// </summary>
        public readonly ParameterDirection Direction;
        /// <summary>
        /// The explicit type assigned, or null if using a dynamic type
        /// </summary>
        public readonly TypeName ExplictType;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Parameter"/> class.
        /// </summary>
        /// <param name="token">The token source.</param>
        /// <param name="direction">The parameter direction.</param>
        /// <param name="name">The parameter name.</param>
        /// <param name="index">The optional index</param>
        /// <param name="explicttype">The explict type, if any</param>
        public Parameter(ParseToken token, ParameterDirection direction, Identifier name, TypeName explicittype)
            : base(token)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Direction = direction;
            this.ExplictType = explicittype;
        }
    }
}
