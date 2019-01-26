using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// An import statement
    /// </summary>
    public class ImportStatement : ParsedItem
    {
        /// <summary>
        /// The name of the module to import from
        /// </summary>
        public readonly ImportName ModuleName;

        /// <summary>
        /// The optional item to import, <c>null</c> indicates the entire module
        /// </summary>
        public readonly Identifier[] SourceNames;

        /// <summary>
        /// The local name to use for the import
        /// </summary>
        public readonly Identifier LocalName;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Import"/> class.
        /// </summary>
        /// <param name="token">The source token</param>
        /// <param name="localname">The import name.</param>
        public ImportStatement(ParseToken token, ImportName modulename, Identifier[] sourceNames, Identifier localname)
            : base(token)
        {
            ModuleName = modulename;
            SourceNames = sourceNames;
            LocalName = localname;            
        }
    }
}
