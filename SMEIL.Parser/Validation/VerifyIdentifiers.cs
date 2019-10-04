using System;
using System.Collections.Generic;
using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Helper class for ensuring that names do not overwrite keywords
    /// </summary>
    public class VerifyIdentifiers : IValidator
    {
        /// <summary>
        /// Validates the current state
        /// </summary>
        /// <param name="state">The state to validate</param>
        public void Validate(ValidationState state)
        {
            foreach (var item in state.Modules.Values.SelectMany(x => x.All()))
            {
                if (item.Current is AST.Module m)
                    CheckDeclarations(m.Declarations);
                else if (item.Current is AST.Network n)
                    CheckDeclarations(n.Declarations);
                else if (item.Current is AST.Process p)
                    CheckDeclarations(p.Declarations);
                else if (item.Current is AST.FunctionDefinition f)
                    CheckDeclarations(f.Declarations);
                else if (item.Current is AST.TypeDefinition t)
                    CheckIdentifier(t.Name);

            }


        }

        /// <summary>
        /// Checks the declarations and validates the names of the items
        /// </summary>
        /// <param name="declarations">The list of declarations</param>
        private void CheckDeclarations(IEnumerable<AST.Declaration> declarations)
        {
            foreach (var n in declarations)
            {
                if (n is AST.VariableDeclaration varDecl)
                    CheckIdentifier(varDecl.Name);
                else if (n is AST.ConstantDeclaration constDecl)
                    CheckIdentifier(constDecl.Name);
                else if (n is AST.BusDeclaration busDecl)
                {
                    CheckIdentifier(busDecl.Name);
                    foreach (var s in busDecl.Signals)
                        CheckIdentifier(s.Name);
                }
                else if (n is AST.EnumDeclaration enumDecl)
                {
                    CheckIdentifier(enumDecl.Name);
                    foreach (var f in enumDecl.Fields)
                        CheckIdentifier(f.Name);
                }
                else if (n is AST.FunctionDefinition funcDecl)
                    CheckIdentifier(funcDecl.Name);
                else if (n is AST.InstanceDeclaration instDecl)
                    CheckIdentifier(instDecl.Name.Name);
                else if (n is AST.GeneratorDeclaration genDecl)
                    CheckIdentifier(genDecl.Name);
                else if (n is AST.TypeDefinition typeDef)
                    CheckIdentifier(typeDef.Name);
                else if (n is AST.ConnectDeclaration)
                {
                    // No names here
                }
                else 
                    throw new InvalidOperationException($"Unexpected declaration type: {n.GetType()}");
            }
        }

        /// <summary>
        /// Checks if an identifier is a reserved word, and throws an exception if that is the case
        /// </summary>
        /// <param name="identifier">The identifier to check</param>
        private void CheckIdentifier(AST.Identifier identifier)
        {
            if (AST.Identifier.IsReservedKeyword(identifier.Name))
                throw new ParserException($"The word {identifier.Name} is a keyword", identifier);

            if (AST.DataType.IsValidIntrinsicType(identifier.Name))
                throw new ParserException($"The name {identifier.Name} is a a built-in type name", identifier);
        }

    }
}