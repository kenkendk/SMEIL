using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SMEIL.Parser.Instance
{
    [DebuggerDisplay("Module = {Name}")]
    public class Module : IInstance, IDeclarationContainer, IChildContainer
    {
        /// <summary>
        /// The name of the module
        /// </summary>
        public string Name => null;

        /// <summary>
        /// The source item
        /// </summary>
        public AST.ParsedItem SourceItem => ModuleDefinition;

        /// <summary>
        /// The declarations in this item
        /// </summary>
        public IEnumerable<AST.Declaration> Declarations => ModuleDefinition.Declarations;

        /// <summary>
        /// The instances in this process
        /// </summary>
        public List<IInstance> Instances { get; } = new List<IInstance>();

        /// <summary>
        /// The process that this instance is from
        /// </summary>
        public readonly AST.Module ModuleDefinition;

        /// <summary>
        /// Constructs a new module instance
        /// </summary>
        /// <param name="source">The module to instantiate</param>
        public Module(AST.Module source)
        {
            ModuleDefinition = source ?? throw new ArgumentNullException(nameof(source));
        }

    }
}