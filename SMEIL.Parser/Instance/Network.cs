using System;
using System.Linq;
using System.Collections.Generic;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Represents an instantiated network
    /// </summary>
    public class Network : IParameterizedInstance, IDeclarationContainer
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => NetworkDefinition.Name?.Name;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.InstanceDeclaration Source;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.Network NetworkDefinition;

        /// <summary>
        /// The parameters in this network
        /// </summary>
        public List<MappedParameter> MappedParameters { get; } = new List<MappedParameter>();

        /// <summary>
        /// The source parameters
        /// </summary>
        public AST.Parameter[] SourceParameters => NetworkDefinition.Parameters;

        /// <summary>
        /// The source name
        /// </summary>
        public string SourceName => NetworkDefinition.Name?.Name;

        /// <summary>
        /// The source item
        /// </summary>
        public AST.ParsedItem SourceItem => NetworkDefinition;

        /// <summary>
        /// The declarations in this item
        /// </summary>
        public IEnumerable<AST.Declaration> Declarations => NetworkDefinition.Declarations;

        /// <summary>
        /// The source instantiation element
        /// </summary>
        public AST.ParameterMap[] ParameterMap => Source.Parameters;

        /// <summary>
        /// The types assigned to each expression in this instance
        /// </summary>
        public Dictionary<AST.Expression, AST.DataType> AssignedTypes { get; } = new Dictionary<AST.Expression, AST.DataType>();

        /// <summary>
        /// The instances in this network
        /// </summary>
        public readonly List<IInstance> Instances = new List<IInstance>();

        /// <summary>
        /// Constructs a new instance of the bus
        /// </summary>
        /// <param name="source">The process declaration</param>
        /// <param name="network">The resolved network definition</param>
        public Network(AST.InstanceDeclaration source, AST.Network network)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            NetworkDefinition = network ?? throw new ArgumentNullException(nameof(network));
        }
    }
}