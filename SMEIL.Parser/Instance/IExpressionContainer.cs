using System.Collections.Generic;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Interface for instances that can contain expressions
    /// </summary>
    public interface IExpressionContainer
    {
        /// <summary>
        /// The type lookup table to use for this instance
        /// </summary>
        /// <value></value>
        Dictionary<AST.Expression, AST.DataType> AssignedTypes { get; }

    }
}