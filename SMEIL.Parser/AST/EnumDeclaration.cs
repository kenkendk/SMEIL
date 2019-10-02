using System;
namespace SMEIL.Parser.AST
{
    public class EnumDeclaration : Declaration, IFunctionDeclaration
    {
        /// <summary>
        /// The name of the enum
        /// </summary>
        public readonly Identifier Name;
        /// <summary>
        /// The enum values
        /// </summary>
        public readonly EnumField[] Fields;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.EnumDefinition"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        /// <param name="name">The enum name.</param>
        /// <param name="fields">The enum fields.</param>
        public EnumDeclaration(ParseToken token, Identifier name, EnumField[] fields)
            : base(token)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
            if (Fields.Length == 0)
                throw new ParserException($"Enumerations must have at least one field", token);
        }
    }
}
