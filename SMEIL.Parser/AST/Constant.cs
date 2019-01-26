namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Collecting class for constants
    /// </summary>
    public abstract class Constant : ParsedItem
    {
        /// <summary>
        /// Constructs a new constant
        /// </summary>
        /// <param name="token">The token where the constant was found</param>
        public Constant(ParseToken token)
            : base(token)
        {
        }
    }
}