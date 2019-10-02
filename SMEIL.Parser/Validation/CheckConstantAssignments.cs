using System;
using System.Linq;
using SMEIL.Parser.AST;
using SMEIL.Parser.Instance;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Validator that checks initial assignments only reference constant values or literals
    /// </summary>
    public class CheckConstantAssignments : IValidator
    {
        public void Validate(ValidationState state)
        {
            foreach (var e in state.AllInstances
                .Where(x => x is Instance.Module 
                    || x is Instance.Network 
                    || x is Instance.Process 
                    || x is Instance.FunctionInvocation))
                {
                    if (e is Instance.Module m)
                        CheckInitializer(state, m, m.ModuleDefinition.Declarations);
                    else if (e is Instance.Network n)
                        CheckInitializer(state, n, n.NetworkDefinition.Declarations);
                    else if (e is Instance.Process p)
                        CheckInitializer(state, p, p.ProcessDefinition.Declarations);
                    else if (e is Instance.FunctionInvocation f)
                        CheckInitializer(state, f, f.Source.Declarations);
                    else
                        throw new InvalidOperationException("Unexpected type");
            }
        }

        private void CheckInitializer(ValidationState state, Instance.IInstance parent, Declaration[] declarations)
        {
            foreach (var item in declarations)
            {
                if (item is AST.ConstantDeclaration cdecl)
                {
                    CheckInitializer(state, parent, cdecl.Expression);
                }
                else if (item is AST.VariableDeclaration vdecl)
                {
                    if (vdecl.Initializer != null)
                        CheckInitializer(state, parent, vdecl.Initializer);
                }
            }
        }

        private void CheckInitializer(ValidationState state, Instance.IInstance parent, Expression expr)
        {
            if (expr is LiteralExpression)
                return;
            else if (expr is NameExpression ne) 
            {
                var scope = state.LocalScopes[parent];
                var s = state.FindSymbol(ne.Name, scope);
                if (s is Instance.Literal || s is Instance.ConstantReference)
                    return;

                throw new ParserException($"Symbol {ne.Name.AsString} resolves to {s?.GetType()} but must be either a constant or a literal", ne);
            }
            else if (expr is TypeCast te)
                CheckInitializer(state, parent, te.Expression);
            else if (expr is UnaryExpression ue)
                CheckInitializer(state, parent, ue.Expression);
            else if (expr is BinaryExpression be)
            {
                CheckInitializer(state, parent, be.Left);
                CheckInitializer(state, parent, be.Right);
            }

        }

    }
}