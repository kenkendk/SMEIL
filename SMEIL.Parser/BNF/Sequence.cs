namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// Represents a sequence of items
    /// </summary>
    public class Sequence : BNFItem
    {
        /// <summary>
        /// The item in the sequence
        /// </summary>
        public readonly BNFItem Items;

        /// <summary>
        /// The token in the sequence
        /// </summary>
        /// <param name="sequence">The token to repeat</param>
        public Sequence(BNFItem sequence)
        {
            Items = sequence;
        }
    }
}