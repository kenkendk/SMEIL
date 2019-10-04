using System;
using System.Collections.Generic;
using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Invocation of a function
    /// </summary>
    public class FunctionInvocation : IParameterizedInstance, IDeclarationContainer
    {
        /// <summary>
        /// The mapped values for the parameters
        /// </summary>
        public List<MappedParameter> MappedParameters { get; } = new List<MappedParameter>();

        /// <summary>
        /// The parameters declared on the function
        /// </summary>
        public AST.Parameter[] SourceParameters => Source.Parameters;

        /// <summary>
        /// The source name
        /// </summary>
        public string SourceName => Statement.Name.AsString;

        /// <summary>
        /// The source item
        /// </summary>
        public AST.ParsedItem SourceItem => Statement;

        /// <summary>
        /// The source instantiation element
        /// </summary>
        public AST.ParameterMap[] ParameterMap => Statement.Parameters;

        /// <summary>
        /// The name of the invoked item
        /// </summary>
        public string Name => Statement.Name.AsString;

        /// <summary>
        /// An unused type assignement dictionary
        /// </summary>
        public Dictionary<Expression, DataType> AssignedTypes { get; } = new Dictionary<Expression, DataType>();

        /// <summary>
        /// The function that is being invoked
        /// </summary>
        public readonly AST.FunctionDefinition Source;

        /// <summary>
        /// The statement performing the invocation
        /// </summary>
        public readonly AST.FunctionStatement Statement;

        /// <summary>
        /// The declarations in this item
        /// </summary>
        public IEnumerable<AST.Declaration> Declarations => Source.Declarations;

        /// <summary>
        /// A copy of the statements inside the function
        /// </summary>
        public AST.Statement[] Statements;

        /// <summary>
        /// The instances in this function
        /// </summary>
        public readonly List<IInstance> Instances = new List<IInstance>();

        /// <summary>
        /// Creates a new function invocation
        /// </summary>
        /// <param name="source">The function to invoke</param>
        public FunctionInvocation(AST.FunctionDefinition source, AST.FunctionStatement statment)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Statement = statment ?? throw new ArgumentNullException(nameof(statment));
            Statements = source.Statements.Select(x => x.Clone()).ToArray();
        }
    }
}