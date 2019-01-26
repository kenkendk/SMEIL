namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a generator declaration
    /// </summary>
    public class GeneratorDeclaration : NetworkDeclaration
    {
        /// <summary>
        /// The name of the generator
        /// </summary>
        public readonly Identifier Name;
        /// <summary>
        /// The source expression
        /// </summary>
        public readonly Expression SourceExpression;
        /// <summary>
        /// The target expression
        /// </summary>
        public readonly Expression TargetExpression;
        /// <summary>
        /// The networks inside the generator
        /// </summary>
        public readonly NetworkDeclaration[] Networks;

        /// <summary>
        /// Creates a new generator declaration
        /// </summary>
        /// <param name="source">The source token</param>
        /// <param name="name">The generator name</param>
        /// <param name="sourceExpression">The source expression</param>
        /// <param name="targetExpression">The target expression</param>
        /// <param name="networks">The generator networks</param>
        public GeneratorDeclaration(ParseToken source, Identifier name, Expression sourceExpression, Expression targetExpression, NetworkDeclaration[] networks)
            : base(source)
        {
            Name = name;
            SourceExpression = sourceExpression;
            TargetExpression = targetExpression;
            Networks = networks;
        }
    }
}