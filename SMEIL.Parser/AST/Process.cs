using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents an SMEIL process
    /// </summary>
    public class Process : Entity
    {
        /// <summary>
        /// A value describing if the process is clocked
        /// </summary>
        public bool Clocked;
        /// <summary>
        /// The process name
        /// </summary>
        public Identifier Name;
        /// <summary>
        /// The process parameters
        /// </summary>
        public Parameter[] Parameters;
        /// <summary>
        /// The input busses
        /// </summary>
        public Declaration[] Declarations;
        /// <summary>
        /// The statements
        /// </summary>
        public Statement[] Statements;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Process"/> class.
        /// </summary>
        /// <param name="token">The source token</param>
        /// <param name="clocked">If set to <c>true</c>, the process is clocked.</param>
        /// <param name="name">The process name.</param>
        /// <param name="parameters">The parameters</param>
        /// <param name="declarations">The declaration statements.</param>
        /// <param name="statements">The body statements.</param>
        public Process(ParseToken token, bool clocked, Identifier name, Parameter[] parameters, Declaration[] declarations, Statement[] statements)
            : base(token)
        {
            Clocked = clocked;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parameters = parameters ?? new Parameter[0];
            Declarations = declarations ?? throw new ArgumentNullException(nameof(declarations));
            Statements = statements ?? throw new ArgumentNullException(nameof(statements));
        }
    }
}
