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
        /// Helper property to see the full name
        /// </summary>
        public string AsString => string.Join(".", Identifier.Select(x => x.Name));

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