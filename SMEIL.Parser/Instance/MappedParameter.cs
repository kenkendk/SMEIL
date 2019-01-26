using System;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Represents a mapped parameter
    /// </summary>
    public class MappedParameter
    {
        /// <summary>
        /// The source parameter
        /// </summary>
        public readonly AST.ParameterMap SourceParameter;

        /// <summary>
        /// The matched parameter
        /// </summary>
        public readonly AST.Parameter MatchedParameter;

        /// <summary>
        /// The instance mapped to this parameter
        /// </summary>
        public readonly IInstance MappedItem;

        /// <summary>
        /// The name of the matched parameter
        /// </summary>
        public string LocalName => MatchedParameter.Name.Name;

        /// <summary>
        /// Constructs a new parameter instance
        /// </summary>
        /// <param name="parameter">The parameter to map</param>
        /// <param name="matchedParameter">The parameter that was matched</param>
        /// <param name="mappedItem">The item that this entry is mapped to</param>
        public MappedParameter(AST.ParameterMap parameter, AST.Parameter matchedParameter, IInstance mappeditem)
        {
            SourceParameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            MatchedParameter = matchedParameter ?? throw new ArgumentNullException(nameof(matchedParameter));
            MappedItem = mappeditem ?? throw new ArgumentNullException(nameof(mappeditem));
        }                
    }
}