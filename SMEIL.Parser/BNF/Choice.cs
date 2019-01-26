namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// Represents a number of choices
    /// </summary>
    public class Choice : BNFItem
    {
        /// <summary>
        /// The choices possible
        /// </summary>
        public readonly BNFItem[] Choices;

        /// <summary>
        /// Constructs a new choice
        /// </summary>
        /// <param name="choices">The possible choices</param>
        public Choice(params BNFItem[] choices)
        {
            Choices = choices;
        }
    }
}