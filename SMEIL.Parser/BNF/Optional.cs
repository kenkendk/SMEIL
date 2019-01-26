namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// Represents an optional item
    /// </summary>
    public class Optional : BNFItem
    {
        /// <summary>
        /// The optional item
        /// </summary>
        public readonly BNFItem Item;

        /// <summary>
        /// Constructs a new optional item
        /// </summary>
        /// <param name="item">The item to parse</param>
        public Optional(BNFItem item)
        {
            Item = item;
        }
    }
}