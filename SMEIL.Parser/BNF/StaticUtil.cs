using System;

namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// Static helper utility for more concise syntax in BNF definitions
    /// </summary>
    public static class StaticUtil
    {
        /// <summary>
        /// Constructs a new choice
        /// </summary>
        /// <param name="choices">The possible choices</param>
        public static Choice Choice(params BNFItem[] choices) { return new BNF.Choice(choices); }
        /// <summary>
        /// Constructs a new composite item
        /// </summary>
        /// <param name="items">The items in the composite</param>
        public static Composite Composite(params BNFItem[] items) { return new BNF.Composite(items); }
        /// <summary>
        /// Constructs a new custom matcher
        /// </summary>
        /// <param name="matcher">The function used to match</param>
        public static CustomItem CustomItem(Func<string, bool> matcher) { return new BNF.CustomItem(matcher); }
        /// <summary>
        /// Constructs a new literal
        /// </summary>
        /// <param name="value">The literal to match</param>
        public static Literal Literal(string value) { return new BNF.Literal(value); }
        /// <summary>
        /// Constructs a new optional item
        /// </summary>
        /// <param name="item">The item to parse</param>
        public static Optional Optional(BNFItem token) { return new BNF.Optional(token); }
        /// <summary>
        /// Constructs a new matcher for a regular expression
        /// </summary>
        /// <param name="expression">The expression to match</param>
        public static RegEx RegEx(System.Text.RegularExpressions.Regex expression) { return new BNF.RegEx(expression); }
        /// <summary>
        /// Constructs a new matcher for a regular expression
        /// </summary>
        /// <param name="expression">The expression to match</param>
        public static RegEx RegEx(string expression) { return new BNF.RegEx(expression); }
        /// <summary>
        /// The token in the sequence
        /// </summary>
        /// <param name="sequence">The token to repeat</param>
        public static Sequence Sequence(BNFItem sequence) { return new BNF.Sequence(sequence); }
        /// <summary>
        /// Constructs a new mapper item
        /// </summary>
        /// <param name="token">The token being matched</param>
        /// <param name="matcher">The matcher function</param>
        public static Mapper<T> Mapper<T>(BNFItem token, Func<Match, T> matcher) { return new BNF.Mapper<T>(token, matcher); }
    }
}