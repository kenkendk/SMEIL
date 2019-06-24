namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents either an intrinsic type, or an alias
    /// </summary>
    public class TypeName : ParsedItem
    {
        /// <summary>
        /// The intrinsic type, or null if this is an alias
        /// </summary>
        public readonly DataType IntrinsicType;

        /// <summary>
        /// The alias, or null if this is an intrinsic type
        /// </summary>
        public readonly AST.Name Alias;

        /// <summary>
        /// The index expression, if any
        /// </summary>
        public readonly Expression Indexer;

        /// <summary>
        /// Constructs a new typename for an intrinsic type
        /// </summary>
        /// <param name="parent">The data type to name</param>
        /// <param name="indexer">The index expression, if this is an array</param>
        public TypeName(DataType parent, Expression indexer)
            : base(parent.SourceToken)
        {
            IntrinsicType = parent;
            Alias = null;
            Indexer = indexer;
        }

        /// <summary>
        /// Constructs a new typename for an alias
        /// </summary>
        /// <param name="parent">The data type to name</param>
        /// <param name="indexer">The index expression, if this is an array</param>
        public TypeName(AST.Name parent, Expression indexer)
            : base(parent.SourceToken)
        {
            IntrinsicType = null;
            Alias = parent;
            Indexer = indexer;
        }

        /// </inheritdoc />
        public override string ToString()
        {
            return IntrinsicType?.ToString() ?? Alias.AsString;
        }

        /// </inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj == null || IntrinsicType == null || GetType() != obj.GetType())
                return false;

            return object.Equals(IntrinsicType, (obj as TypeName)?.IntrinsicType);
        }

        /// </inheritdoc />
        public override int GetHashCode()
        {
            if (IntrinsicType == null)
                return 1;
            return IntrinsicType.GetHashCode();
        }
    }
}