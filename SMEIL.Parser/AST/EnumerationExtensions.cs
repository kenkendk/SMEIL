using System.Linq;
using System.Collections.Generic;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// The order items can be traversed in
    /// </summary>
    public enum TraverseOrder
    {
        /// <summary>Visits items in depth-first pre-order</summary>
        DepthFirstPreOrder,

        /// <summary>Visits items in depth-first post-order</summary>
        DepthFirstPostOrder,

        // Only makes sense for binary trees
        //DepthFirstInOrder,

        // Not currently used
        //BreadthFirst
    }

    /// <summary>
    /// Helper structure for visiting all items
    /// </summary>
    public class VisitedItem
    {
        /// <summary>
        /// The current item
        /// </summary>
        public ParsedItem Current { get; set; }
        /// <summary>
        /// The stack of parents
        /// </summary>
        public List<ParsedItem> Parents { get; protected set; } = new List<ParsedItem>();
    }

    /// <summary>
    /// Returns a strongly typed version of a visited item
    /// </summary>
    /// <typeparam name="T">The type tp use</typeparam>
    public class TypedVisitedItem<T> : VisitedItem
        where T : ParsedItem
    {
        /// <summary>
        /// Returns the current item in a strongly type way
        /// </summary>
        public new T Current => (T)base.Current;

        /// <summary>
        /// Constructs a new strongly type visited item
        /// </summary>
        /// <param name="parent">The item to copy</param>
        public TypedVisitedItem(VisitedItem parent)
        {
            base.Current = parent.Current;
            base.Parents = parent.Parents;
        }
    }

    /// <summary>
    /// Helper methods for enumerating parsed items
    /// </summary>
    public static class EnumerationExtensions
    {
        /// <summary>
        /// Visits all items in the given order
        /// </summary>
        /// <param name="item">The item to visit</param>
        /// <param name="order">The order to visit in</param>
        /// <returns>All items, including the start item, in the given order</returns>
        public static IEnumerable<VisitedItem> All(this ParsedItem item, TraverseOrder order = TraverseOrder.DepthFirstPreOrder)
        {
            return All(item, new VisitedItem(), order);
        }

        /// <summary>
        /// Visits all items in depth first pre-order
        /// </summary>
        /// <param name="item"></param>
        /// <param name="order">The order to visit in</param>
        /// <returns>All items, including the start item, in the given order</returns>
        public static IEnumerable<VisitedItem> All(this IEnumerable<ParsedItem> item, TraverseOrder order = TraverseOrder.DepthFirstPreOrder)
        {
            foreach (var s in item)
                foreach (var n in All(s, order))
                    yield return n;
        }

        /// <summary>
        /// Visits all items in depth first pre-order
        /// </summary>
        /// <param name="item">The item to type</param>
        /// <returns>All items, including the start item, in the given order</returns>
        public static IEnumerable<TypedVisitedItem<T>> OfType<T>(this IEnumerable<VisitedItem> item)   
            where T : ParsedItem
        {
            return item.Where(x => x.Current is T).Select(x => new TypedVisitedItem<T>(x));
        }


        /// <summary>
        /// Visits all items in the given order
        /// </summary>
        /// <param name="item">The item to visit</param>
        /// <param name="start">The state to use</param>
        /// <returns>All items, including the start item, in the given order</returns>
        private static IEnumerable<VisitedItem> All(ParsedItem item, VisitedItem start, TraverseOrder order)
        {
            start = start ?? new VisitedItem();

            if (order == TraverseOrder.DepthFirstPostOrder)
            {
                start.Parents.Add(item);

                foreach (var n in item.Children)
                    foreach (var c in All(n, start, order))
                        yield return c;

                start.Parents.RemoveAt(start.Parents.Count - 1);

                start.Current = item;
                yield return start;

            }
            else //if (order == TraverseOrder.DepthFirstPreOrder)
            {
                start.Current = item;
                yield return start;
                
                start.Parents.Add(item);

                foreach (var n in item.Children)
                    foreach (var c in All(n, start, order))
                        yield return c;

                start.Parents.RemoveAt(start.Parents.Count - 1);
            }
        }

        /// <summary>
        /// Returns the current identifier as a name
        /// </summary>
        /// <param name="item">The identifier to map as a name</param>
        /// <returns>A name instance of the identifier</returns>
        public static AST.Name AsName(this AST.Identifier item)
        {
            return new AST.Name(item.SourceToken, new [] { item }, null);
        }

        /// <summary>
        /// Creates an expression for a name
        /// </summary>
        /// <param name="item">The item to create the expression for</param>
        /// <returns>An expression that represents the name</returns>
        public static Expression AsExpression(this AST.Name item)
        {
            return new AST.NameExpression(item.SourceToken, item);
        }

        /// <summary>
        /// Creates an expression for an identifier
        /// </summary>
        /// <param name="item">The item to create the expression for</param>
        /// <returns>An expression that represents the item</returns>
        public static Expression AsExpression(this AST.Identifier item)
        {
            return new AST.NameExpression(item.SourceToken, new AST.Name(item.SourceToken, new[] { item }, null));
        }
    }
}