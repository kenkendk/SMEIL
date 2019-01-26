using System;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Represents a parameter
    /// </summary>
    public class Parameter : IInstance
    {
        /// <summary>
        /// The source parameter
        /// </summary>
        public readonly AST.Parameter SourceParameter;

        /// <summary>
        /// Gets the name of the parameter
        /// </summary>
        public string Name => SourceParameter.Name.Name;

        /// <summary>
        /// The instance mapped to this parameter
        /// </summary>
        public IInstance MappedItem;

        /// <summary>
        /// Constructs a new parameter instance
        /// </summary>
        /// <param name="parameter">The parameter to map</param>
        public Parameter(AST.Parameter parameter)
        {
            SourceParameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

    }
}