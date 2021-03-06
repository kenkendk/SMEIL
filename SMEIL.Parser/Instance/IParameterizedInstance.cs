using System.Collections.Generic;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Interface for instances that can be parameterized
    /// </summary>
    public interface IParameterizedInstance : IInstance
    {
        /// <summary>
        /// The parameters that are instantiated for this item
        /// </summary>
        List<MappedParameter> MappedParameters { get; }

        /// <summary>
        /// The source parameters
        /// </summary>
        AST.Parameter[] SourceParameters { get; }

        /// <summary>
        /// The name of the source item
        /// </summary>
        string SourceName { get; }

        /// <summary>
        /// The source item
        /// </summary>
        AST.ParsedItem SourceItem { get; }

        /// <summary>
        /// The source parameters
        /// </summary>
        AST.ParameterMap[] ParameterMap { get; }

    }
}