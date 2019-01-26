namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a range description
    /// </summary>
    public class Range : ParsedItem
    {
        /// <summary>
        /// The from expression
        /// </summary>
        public readonly Expression From;
        /// <summary>
        /// The to expression
        /// </summary>
        public readonly Expression To;

        /// <summary>
        /// Constructs a new range
        /// </summary>
        /// <param name="source">The token source</param>
        /// <param name="from">The from expression</param>
        /// <param name="to">The to expression</param>
        public Range(ParseToken source, Expression from, Expression to)
            : base(source)
        {
            From = from;
            To = to;
        }
    }
}