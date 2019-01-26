namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents an instance name
    /// </summary>
    public class InstanceName : ParsedItem
    {
        /// <summary>
        /// The name of the instance, null for anonymous
        /// </summary>
        public readonly Identifier Name;

        /// <summary>
        /// The indexed expression, can be null
        /// </summary>
        public readonly Expression IndexExpression;

        /// <summary>
        /// Constructs a new instance name
        /// </summary>
        /// <param name="token">The token for the parsed instance</param>
        /// <param name="name">The instance name, or null for anonymous</param>
        /// <param name="index">The index expression, if any</param>
        public InstanceName(ParseToken token, Identifier name, Expression index)
            : base(token)
        {
            Name = name;
            IndexExpression = index;
        }
    }
}