using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Represents a process instance
    /// </summary>
    [DebuggerDisplay("Process = {Name}")]
    public class Process : IParameterizedInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        public string Name => Source.Name?.Name?.Name;

        /// <summary>
        /// The item that this was instantiated from
        /// </summary>
        public readonly AST.InstanceDeclaration Source;

        /// <summary>
        /// The process that this instance is from
        /// </summary>
        public readonly AST.Process ProcessDefinition;

        /// <summary>
        /// The parameters that are instantiated for this process
        /// </summary>
        public List<MappedParameter> MappedParameters { get; } = new List<MappedParameter>();

        /// <summary>
        /// The source parameters
        /// </summary>
        public AST.Parameter[] SourceParameters => ProcessDefinition.Parameters;

        /// <summary>
        /// The source name
        /// </summary>
        public string SourceName => ProcessDefinition.Name?.Name;

        /// <summary>
        /// The source item
        /// </summary>
        public AST.ParsedItem SourceItem => ProcessDefinition;

        /// <summary>
        /// The source instantiation element
        /// </summary>
        public AST.InstanceDeclaration DeclarationSource => Source;

        /// <summary>
        /// The types assigned to each expression in this instance
        /// </summary>
        public Dictionary<AST.Expression, AST.DataType> AssignedTypes { get; } = new Dictionary<AST.Expression, AST.DataType>();
        
        /// <summary>
        /// The instances in this process
        /// </summary>
        public readonly List<IInstance> Instances = new List<IInstance>();

        /// <summary>
        /// Constructs a new instance of the bus
        /// </summary>
        /// <param name="source">The process declaration</param>
        /// <param name="process">The resolved process definition</param>
        public Process(AST.InstanceDeclaration source, AST.Process process)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ProcessDefinition = process ?? throw new ArgumentNullException(nameof(process));
        }
    }
}