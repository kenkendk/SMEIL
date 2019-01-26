using System;
namespace SMEIL.Parser.AST
{    
    public class InstanceDeclaration : NetworkDeclaration
    {
        /// <summary>
        /// The name of this instance
        /// </summary>
        public InstanceName Name;
        /// <summary>
        /// The item being instantiated
        /// </summary>
        public Identifier SourceItem;
        /// <summary>
        /// The parameter maps
        /// </summary>
        public ParameterMap[] Parameters;

        /// <summary>
        /// Creates a new instance declaration
        /// </summary>
        /// <param name="token">The token used to create the instance declaration</param>
        /// <param name="name">The name of this instance</param>
        /// <param name="SourceItem">The item being instantiated</param>
        /// <param name="parameters">The parameters to instantiate</param>
        public InstanceDeclaration(ParseToken token, InstanceName name, Identifier sourceItem, ParameterMap[] parameters)
            : base(token)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            SourceItem = sourceItem ?? throw new ArgumentNullException(nameof(sourceItem));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }
    }
}