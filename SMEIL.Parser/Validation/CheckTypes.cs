using System;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Module that checks that the assigned types are compatible
    /// </summary>
    public class CheckTypes : IValidator
    {
        /// <summary>
        /// Validates the module
        /// </summary>
        /// <param name="state">The validation state</param>
        public void Validate(ValidationState state)
        {
            // TODO: Remove this file? We do the checks during type assignment anyway...

            // We use the instances to define new scopes for names
            // foreach (var instanceDecl in state.Modules.Values.All().OfType<AST.InstanceDeclaration>())
            // {
            //     var symbolTable = state.FindSymbolTable(instanceDecl);
            //     symbolTable.TryGetValue(instanceDecl.Current.Name.Name.Name, out var instanceobj);
            //     if (instanceobj == null)
            //         throw new ArgumentException($"Failed to find element with name {instanceDecl.Current.Name.Name.Name}");

            //     if (!(instanceobj is Instance.Process instance))
            //         throw new ArgumentException($"The item with name {instanceDecl.Current.Name.Name.Name} should have type{nameof(Instance.Signal)} but has ");


            //     // Check operations for valid types
            //     foreach (var item in instanceDecl.Current.All().OfType<AST.Expression>())
            //     {
            //         if (item.Current is AST.UnaryExpression unaryExpression)
            //         {

            //         }
            //         else if (item.Current is AST.BinaryExpression binaryExpression)
            //         {

            //         }
            //     }

            // }
        }
    }
}