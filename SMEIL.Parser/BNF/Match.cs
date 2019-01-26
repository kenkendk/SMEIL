using System;
using System.Linq;
using System.Collections.Generic;

namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// Represents a match in the BNF
    /// </summary>
    public class Match
    {
        /// <summary>
        /// The token that matched
        /// </summary>
        public readonly BNFItem Token;

        /// <summary>
        /// The entry that matched
        /// </summary>
        public readonly ParseToken Item;

        /// <summary>
        /// If this item has submatches, this structure contains them
        /// </summary>
        public readonly Match[] SubMatches;

        /// <summary>
        /// A flag indicating if the item matched
        /// </summary>
        public readonly bool Matched;

        /// <summary>
        /// Constructs a new match
        /// </summary>
        /// <param name="token">The token that matched</param>
        /// <param name="item">The item that matched</param>
        /// <param name="subMatches">Submatches</param>
        public Match(BNFItem token, ParseToken item, Match[] subMatches, bool matched)
        {
            Token = token;
            Item = item;
            SubMatches = subMatches;
            Matched = matched;
        }

        /// <summary>
        /// Returns all items with a mapper returning an item deriving from <typeparamref name="T" />
        /// </summary>
        /// <typeparam name="T">The base type to look for</typeparam>
        /// <returns>A list of items that derives from <typeparamref name="T" /></returns>
        public IEnumerable<T> InvokeDerivedMappers<T>()
        {
            return Flat
                .Where(n => n != this)
                .Where(
                    n => 
                        n.Token.GetType().IsConstructedGenericType 
                        &&
                        n.Token.GetType().GetGenericTypeDefinition() == typeof(BNF.Mapper<>)
                        &&
                        typeof(T).IsAssignableFrom(n.Token.GetType().GetGenericArguments().First())
                )
                .Select(
                    n => (T)n.Token
                        .GetType()
                        .GetMethod(nameof(BNF.Mapper<T>.InvokeMatcher))
                        .Invoke(n.Token, new object[] { n })
                );
        }

        /// <summary>
        /// Gets the first mapper that returns a type derived from the instance
        /// </summary>
        /// <param name="instance">The instance to find</param>
        /// <typeparam name="T">The mapped type</typeparam>
        /// <returns>The item returned by the mapper</returns>
        public T FirstDerivedMapper<T>()
        {
            return InvokeDerivedMappers<T>().First();
        }

        /// <summary>
        /// Gets the first mapper that has the given instance
        /// </summary>
        /// <param name="instance">The instance to find</param>
        /// <typeparam name="T">The mapped type</typeparam>
        /// <returns>The item returned by the mapper</returns>
        public T FirstMapper<T>(BNF.Mapper<T> instance)
        {
            return InvokeMappers(instance).First();
        }

        /// <summary>
        /// Gets the first mapper that has the given instance
        /// </summary>
        /// <param name="instance">The instance to find</param>
        /// <typeparam name="T">The mapped type</typeparam>
        /// <returns>The item returned by the mapper</returns>
        public T FirstOrDefaultMapper<T>(BNF.Mapper<T> instance)
        {
            return InvokeMappers(instance).FirstOrDefault();
        }

        /// <summary>
        /// Invokes all mappers of the given type and returns the result
        /// </summary>
        /// <param name="instance">The instance to match, or <c>null</c> to match any</param>
        /// <typeparam name="T">The mapper type to match</typeparam>
        /// <returns>A sequence of the invoked mappers</returns>
        public IEnumerable<T> InvokeMappers<T>(BNF.Mapper<T> instance = null)
        {
            return GetMappers(instance).Select(x => x.Item1);
        }

        /// <summary>
        /// Invokes all mappers of the given type and returns the result
        /// and the BNF match for each item
        /// </summary>
        /// <param name="instance">The instance to match, or <c>null</c> to match any</param>
        /// <typeparam name="T">The mapper type to match</typeparam>
        /// <returns>A sequence of the invoked mappers and the match tokens</returns>
        public IEnumerable<Tuple<T, Match>> GetMappers<T>(BNF.Mapper<T> instance = null)
        {
            return Flat
                .Where(n => n.Token is BNF.Mapper<T>)
                .Where(n => instance == null || n.Token == instance)
                .Select(n => 
                    new Tuple<T, Match>(
                        ((BNF.Mapper<T>)n.Token).Matcher(n),
                        n
                    )
                );
        }

        /// <summary>
        /// Gets the first match for the given instance and throws an exception if not found
        /// </summary>
        /// <param name="instance">The instance to look for</param>
        /// <returns>The first match</returns>
        public BNF.Match First(BNF.BNFItem instance)
        {
            return Flat.First(x => x.Token == instance);
        }

        /// <summary>
        /// Gets the first match for the given instance, or null
        /// </summary>
        /// <param name="instance">The instance to look for</param>
        /// <returns>The first match or null</returns>
        public BNF.Match FirstOrDefault(BNF.BNFItem instance)
        {
            return Flat.FirstOrDefault(x => x.Token == instance);
        }

        /// <summary>
        /// Gets all items and sub-items from this item
        /// </summary>
        public IEnumerable<Match> Flat
        {
            get
            {
                yield return this;

                foreach (var m in SubMatches ?? new Match[0])
                    foreach (var n in m.Flat)
                        yield return n;                    
            }
        }
    }
}