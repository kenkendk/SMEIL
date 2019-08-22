using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Flags used to mark a process as a special type that can easily
    /// be removed during code generation
    /// </summary>
    public enum ProcessType
    {
        /// <summary>The process is a normal user process</summary>
        Normal,
        /// <summary>The process is an identity process written by the user</summary>
        Identity,
        /// <summary>The process is a dynamically created process for connecting signals</summary>
        Connect,
        /// <summary>The process is a dynamically created process for typecasting signals</summary>
        TypeCast
    }

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
        /// The process type
        /// </summary>
        public readonly ProcessType Type;

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
        /// Map of signal names when being read
        /// </summary>
        public readonly Dictionary<Instance.Signal, string> SignalReadNames = new Dictionary<Instance.Signal, string>();
        /// <summary>
        /// Map of signal names when being written
        /// </summary>
        public readonly Dictionary<Instance.Signal, string> SignalWriteNames = new Dictionary<Instance.Signal, string>();
        /// <summary>
        /// Map of variable names
        /// </summary>
        public readonly Dictionary<Instance.Variable, string> VariableNames = new Dictionary<Instance.Variable, string>();

        /// <summary>
        /// Map of used local token names
        /// </summary>
        public readonly Dictionary<string, int> LocalTokenCounter = new Dictionary<string, int>();

        /// <summary>
        /// Constructs a new instance of the bus
        /// </summary>
        /// <param name="source">The process declaration</param>
        /// <param name="process">The resolved process definition</param>
        /// <param name="type">The process type</param>
        public Process(AST.InstanceDeclaration source, AST.Process process, ProcessType type)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ProcessDefinition = process ?? throw new ArgumentNullException(nameof(process));
            Type = type;
        }
    }
}