using System;

namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// A token with custom matcher code
    /// </summary>
    public class CustomItem : BNFItem
    {
        /// <summary>
        /// The matcher method
        /// </summary>
        public readonly Func<string, bool> Matcher;

        /// <summary>
        /// Constructs a new custom matcher
        /// </summary>
        /// <param name="matcher">The function used to match</param>
        public CustomItem(Func<string, bool> matcher)
        {
            Matcher = matcher;
        }
    }
}