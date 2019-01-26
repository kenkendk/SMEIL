using System;
using System.Collections.Generic;
using System.Linq;

namespace SMEIL.Parser
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Advances the enumerator and returns the next element, or throws an error if the sequence has no more items
        /// </summary>
        /// <returns>The next element.</returns>
        /// <param name="enumerator">The enumerator to use.</param>
        /// <param name="errormessage">The error message to return.</param>
        /// <typeparam name="T">The enumerator type parameter.</typeparam>
        public static T GetNext<T>(this IEnumerator<T> enumerator, string errormessage)
        {
            var cur = enumerator.Current;
            if (!enumerator.MoveNext())
                throw new Exception(string.Format(errormessage, cur));

            return enumerator.Current;
        }

        /// <summary>
        /// Gets the next element from the sequence and checks that it satisfies the predicate
        /// </summary>
        /// <returns>The next element.</returns>
        /// <param name="enumerator">The enumerator to use.</param>
        /// <param name="predicate">The predicate function to use.</param>
        /// <param name="errormessage">The error message to return.</param>
        /// <typeparam name="T">The enumerator type parameter.</typeparam>
        public static T GetNextAndCheck<T>(this IEnumerator<T> enumerator, Func<T, bool> predicate, string errormessage)
        {
            var next = GetNext(enumerator, errormessage);
            if (!predicate(next))
                throw new Exception(string.Format(errormessage, next));

            return next;
        }

        /// <summary>
        /// Gets the next element from the sequence and checks that it satisfies the predicate
        /// </summary>
        /// <returns>The next element.</returns>
        /// <param name="enumerator">The enumerator to use.</param>
        /// <param name="value">The value to check for.</param>
        public static ParseToken GetNextAndCheck(this IEnumerator<ParseToken> enumerator, string value)
        {
            return enumerator.GetNextAndCheck(x => x.Text == value, $"Expected \"{value}\": " + "{0}");
        }        

        /// <summary>
        /// Checks that the current element satisfies the predicate
        /// </summary>
        /// <returns>The next element.</returns>
        /// <param name="enumerator">The enumerator to use.</param>
        /// <param name="predicate">The predicate function to use.</param>
        /// <param name="errormessage">The error message to return.</param>
        /// <typeparam name="T">The enumerator type parameter.</typeparam>
        public static T CheckCurrent<T>(this IEnumerator<T> enumerator, Func<T, bool> predicate, string errormessage)
        {
            if (!predicate(enumerator.Current))
                throw new Exception(string.Format(errormessage, enumerator.Current));

            return enumerator.Current;
        }

        /// <summary>
        /// Checks that the current element text is equal to the expected
        /// </summary>
        /// <returns>The next element.</returns>
        /// <param name="enumerator">The enumerator to use.</param>
        /// <param name="value">The value to use.</param>
        public static ParseToken CheckCurrent(this IEnumerator<ParseToken> enumerator, string value)
        {
            return enumerator.CheckCurrent(x => x.Text == value, $"Expected \"{value}\": " + "{0}");
        }
    }
}
