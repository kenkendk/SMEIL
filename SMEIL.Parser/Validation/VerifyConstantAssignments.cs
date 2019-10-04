using System;
using System.Collections.Generic;
using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Validator that checks initial assignments only reference constant values or literals
    /// </summary>
    public class VerifyConstantAssignments : IValidator
    {
        public void Validate(ValidationState state)
        {
            var constParentMap = state.AllInstances
                .OfType<Instance.IDeclarationContainer>()
                .SelectMany(x => 
                    x.Declarations
                        .OfType<AST.ConstantDeclaration>()
                        .Select(y => new {
                            Constant= y,
                            Parent = (Instance.IInstance)x
                        })
                )
                .GroupBy(x => x.Constant)
                .ToDictionary(
                    x => x.Key,
                    x => x.First().Parent
                );

            foreach (var e in state.AllInstances.OfType<Instance.IDeclarationContainer>())
                CheckInitializer(state, e, e.Declarations, constParentMap);
        }

        private void CheckInitializer(ValidationState state, Instance.IInstance parent, IEnumerable<Declaration> declarations, Dictionary<AST.ConstantDeclaration, Instance.IInstance> constParentMap)
        {
            foreach (var item in declarations)
            {
                if (item is AST.ConstantDeclaration cdecl)
                {
                    var dependson = new HashSet<AST.ConstantDeclaration>();
                    var visited = new HashSet<AST.ConstantDeclaration>();
                    CheckInitializer(state, parent, cdecl.Expression, dependson);

                    // Repeat lookup
                    while(dependson.Count != 0)
                    {
                        if (dependson.Contains(cdecl))
                            throw new ParserException($"Cannot have {(visited.Count == 0 ? "self" : "circular")}-refrence in a constant initializer", cdecl);

                        var work = dependson.Where(x => !visited.Contains(x)).ToList();
                        dependson = new HashSet<ConstantDeclaration>();

                        foreach (var nc in work)
                        {
                            CheckInitializer(state, constParentMap[nc], nc.Expression, dependson);
                            visited.Add(nc);
                        }
                    }
                }
                else if (item is AST.VariableDeclaration vdecl)
                {
                    if (vdecl.Initializer != null)
                    {
                        var visited = new HashSet<AST.ConstantDeclaration>();
                        CheckInitializer(state, parent, vdecl.Initializer, visited);
                    }
                }
            }
        }

        private void CheckInitializer(ValidationState state, Instance.IInstance parent, Expression expr, HashSet<AST.ConstantDeclaration> visited)
        {
            if (expr is LiteralExpression)
                return;
            else if (expr is NameExpression ne) 
            {
                var scope = state.LocalScopes[parent];
                var s = state.FindSymbol(ne.Name, scope);
                if (s is Instance.Literal || s is Instance.EnumFieldReference)
                    return;

                if (s is Instance.ConstantReference cref)
                {
                    visited.Add(cref.Source);
                    return;
                }

                throw new ParserException($"Symbol {ne.Name.AsString} resolves to {s?.GetType()} but must be either a constant or a literal", ne);
            }
            else if (expr is TypeCast te)
                CheckInitializer(state, parent, te.Expression, visited);
            else if (expr is UnaryExpression ue)
                CheckInitializer(state, parent, ue.Expression, visited);
            else if (expr is BinaryExpression be)
            {
                CheckInitializer(state, parent, be.Left, visited);
                CheckInitializer(state, parent, be.Right, visited);
            }

        }

    }
}