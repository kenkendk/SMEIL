namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents an import name
    /// </summary>
    public class ImportName : ParsedItem
    {
        /// <summary>
        /// The import name
        /// </summary>
        public readonly Identifier[] Name;

        /// <summary>
        /// Creates a new import name
        /// </summary>
        /// <param name="source">The token source</param>
        /// <param name="name">The name</param>
        public ImportName(ParseToken source, Identifier[] name)
            : base(source)
        {
            Name = name;            
        }
    }
}