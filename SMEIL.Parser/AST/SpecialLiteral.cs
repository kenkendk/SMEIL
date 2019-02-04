namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents the special U (undefined) literal
    /// </summary>
    public class SpecialLiteral : Constant
    {
        /// <summary>
        /// Gets the type of the constant
        /// </summary>
        public override ILType Type => ILType.Special;

        /// <summary>
        /// Constructs a new special literal
        /// </summary>
        /// <param name="token">The otken where the item was found</param>
        public SpecialLiteral(ParseToken token)
            : base(token)
        {
        }
    }
}