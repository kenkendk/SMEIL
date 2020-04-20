using System;

namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// Represents a mapping from a BNF subtree to an object instance
    /// </summary>
    public class Mapper<T> : BNFItem
    {
        /// <summary>
        /// The sub token to use
        /// </summary>
        public BNFItem Token;

        /// <summary>
        /// The matcher to use
        /// </summary>
        public Func<Match, T> Matcher;

        /// <summary>
        /// Constructs a new mapper item
        /// </summary>
        /// <param name="token">The token being matched</param>
        /// <param name="matcher">The matcher function</param>
        public Mapper(BNFItem token, Func<Match, T> matcher)
        {
            Matcher = matcher;
            Token = token;
        }

        /// <summary>
        /// Invokes the matcher function
        /// </summary>
        /// <param name="m">The match instance</param>
        /// <returns>The mapped instance</returns>
        public T InvokeMatcher(Match m)
        {
            return Matcher(m);
        }
    }
}