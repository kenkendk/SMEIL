namespace SMEIL.Parser.BNF
{
    public class Composite : BNFItem
    {
        /// <summary>
        /// The composition of items
        /// </summary>
        public readonly BNFItem[] Items;

        /// <summary>
        /// Constructs a new composite item
        /// </summary>
        /// <param name="items">The items in the composite</param>
        public Composite(params BNFItem[] items)
        {
            Items = items;
        }
    }
}