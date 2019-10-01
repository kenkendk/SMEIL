using System;
using System.Collections.Generic;
using System.Linq;

namespace SMEIL.Parser.AST
{
    public class FunctionStatement : Statement
    {
        /// <summary>
        /// The name of the function to invoke
        /// </summary>
        public readonly Identifier Name;

        /// <summary>
        /// The parameters to the function
        /// </summary>
        public readonly ParameterMap[] Parameters;

        /// <summary>
        /// Creates a new function statement
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="name">The name of the function to invoke</param>
        /// <param name="parameters">The parameter map</param>
        public FunctionStatement(ParseToken token, Identifier name, ParameterMap[] parameters)
            : base(token)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        /// <summary>
        /// Returns a string representation of the function statement
        /// </summary>
        /// <returns>A string representation of the function statement</returns>
        public override string ToString()
        {
            return $"{Name.Name}({string.Join(", ", Parameters.Select(x => x?.ToString()))});";
        }

        /// <summary>
        /// Clones this statement and returns a copy of it
        /// </summary>
        /// <returns>A copy of the statement</returns>
        public override Statement Clone()
            => new FunctionStatement(
                SourceToken,
                Name,
                Parameters.ToList().ToArray()
            );

    }
}