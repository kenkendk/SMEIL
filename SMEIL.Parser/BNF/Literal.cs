namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// Represents a BNF literal
    /// </summary>
    public class Literal : BNFItem
    {
        /// <summary>
        /// The literal to match
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// Constructs a new literal
        /// </summary>
        /// <param name="value">The literal to match</param>
        public Literal(string value)
        {
            Value = value;
        }
    }
}