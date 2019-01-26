using System;
using System.Collections.Generic;
using System.Reflection;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// The base class for all AST items
    /// </summary>
    public abstract class ParsedItem
    {
        /// <summary>
        /// The source token.
        /// </summary>
        public readonly ParseToken SourceToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Item"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        protected ParsedItem(ParseToken token)
        {
            SourceToken = token;
        }

        /// <summary>
        /// Returns the immediate children for this item
        /// </summary>
        public virtual IEnumerable<ParsedItem> Children
        {
            get
            {
                foreach(var n in this.GetType().GetFields(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance))
                {
                    if (typeof(ParsedItem).IsAssignableFrom(n.FieldType))
                    {
                        var p = n.GetValue(this) as ParsedItem;
                        if (p != null)
                            yield return p;
                    }
                    else if (n.FieldType.IsArray && typeof(ParsedItem).IsAssignableFrom(n.FieldType.GetElementType()))
                    {
                        var p = n.GetValue(this) as ParsedItem[];
                        if (p != null)
                            foreach (var a in p)
                                if (a != null)
                                    yield return a;
                    }                    
                    // Direct handling of these from the IfStatement and SwitchStatement
                    else if (n.FieldType == typeof(Tuple<Expression, Statement[]>[]))
                    {
                        var p = n.GetValue(this) as Tuple<Expression, Statement[]>[];
                        if (p != null)
                        {
                            foreach (var a in p)
                            {
                                if (a.Item1 != null)
                                    yield return a.Item1;
                                    if (a.Item2 != null)
                                    foreach (var b in a.Item2)
                                        yield return b;
                            }
                        }
                    }
                }
            }
        }
    }
}
