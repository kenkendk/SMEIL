using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a name in an expression or statement
    /// </summary>
    [DebuggerDisplay("{AsString}")]
    public class Name : ParsedItem
    {
        /// <summary>
        /// The names of the variable
        /// </summary>
        public readonly Identifier[] Identifier;
        /// <summary>
        /// The optional array index
        /// </summary>
        public readonly ArrayIndex[] Index;

        /// <summary>
        /// Helper method to return name fragments
        /// </summary>
        /// <value>The names and associated index values, if any</value>
        private IEnumerable<string> AsStringParts
        {
            get
            {
                for(var i = 0; i < Identifier.Length; i++)
                {
                    if (Index[i] == null)
                        yield return Identifier[i].Name;
                    else
                        yield return Identifier[i].Name + "[" + Index[i].Index.AsString + "]";
                }
            }
        }

        /// <summary>
        /// Helper property to see the full name in debugging
        /// </summary>
        public string AsString => string.Join(".", AsStringParts);

        /// <summary>
        /// Constructs a new name
        /// </summary>
        /// <param name="source">The source token</param>
        /// <param name="identifier">The identifiers</param>
        /// <param name="index">The array indices</param>
        public Name(ParseToken source, Identifier[] identifier, ArrayIndex[] index)
            : base(source)
        {
            Identifier = identifier;
            Index = index;
        }
    }
}