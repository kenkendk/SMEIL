namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// Represents a regular expression token
    /// </summary>
    public class RegEx : BNFItem
    {
        /// <summary>
        /// The regular expression to match
        /// </summary>
        public readonly System.Text.RegularExpressions.Regex Expression;

        /// <summary>
        /// Constructs a new matcher for a regular expression
        /// </summary>
        /// <param name="expression">The expression to match</param>
        public RegEx(string expression)
            : this(new System.Text.RegularExpressions.Regex(expression))
        {
        }

        /// <summary>
        /// Constructs a new matcher for a regular expression
        /// </summary>
        /// <param name="expression">The expression to match</param>
        public RegEx(System.Text.RegularExpressions.Regex expression)
        {
            Expression = expression;
        }
    }
}