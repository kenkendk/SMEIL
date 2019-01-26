using System;
using System.Collections.Generic;

namespace SMEIL.Parser.AST
{
    public class AssignmentStatement : Statement
    {
        /// <summary>
        /// The name of the item being assigned to
        /// </summary>
        public readonly Name Name;

        /// <summary>
        /// The value being assigned
        /// </summary>
        public readonly Expression Value;

        /// <summary>
        /// Constructs a new assignment statement
        /// </summary>
        /// <param name="token">The token where the statement was found</param>
        /// <param name="name">The item being assigned</param>
        /// <param name="value">The value being assigned</param>
        public AssignmentStatement(ParseToken token, Name name, Expression value)
            : base(token)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}