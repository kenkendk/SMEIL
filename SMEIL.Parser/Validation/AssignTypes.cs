using System;
using System.Collections.Generic;
using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Performs type assignment to all instances
    /// </summary>
    public class AssignTypes : IValidator
    {
        /// <summary>
        /// Validates the module
        /// </summary>
        /// <param name="state">The validation state</param>
        public void Validate(ValidationState state)
        {
            // Traverse the state, starting with networks
            foreach (var networkInstance in state.AllInstances.OfType<Instance.Network>())
            {
                // Handle top-level functions
                foreach (var f in networkInstance.Instances.OfType<Instance.FunctionInvocation>())
                    AssignProcessTypes(state, f, f.Statements, f.AssignedTypes);

                // Assign all types in all instantiated processes
                foreach (var instance in networkInstance.Instances.OfType<Instance.Process>())
                {
                    AssignProcessTypes(state, instance, instance.Statements, instance.AssignedTypes); 

                    // Handle process-defined functions
                    foreach (var f in instance.Instances.OfType<Instance.FunctionInvocation>())
                        AssignProcessTypes(state, f, f.Statements, f.AssignedTypes);
                }
            }
        }

        /// <summary>
        /// Performs the type assignment to a process instance
        /// </summary>
        /// <param name="state">The validation state to use</param>
        /// <param name="instance">The process instance to use</param>
        private static void AssignProcessTypes(ValidationState state, Instance.IParameterizedInstance parent, AST.Statement[] statements, Dictionary<Expression, DataType> assignedTypes)
        {
            // Get the scope for the intance
            var scope = state.LocalScopes[parent];

            // We use multiple iterations to assign types
            // The first iteration assigns types to all literal, bus, signal and variable expressions
            foreach (var item in statements.All().OfType<AST.Expression>())
            {
                // Skip duplicate assignments
                if (assignedTypes.ContainsKey(item.Current))
                    continue;

                if (item.Current is AST.LiteralExpression literal)
                {
                    if (literal.Value is AST.BooleanConstant)
                        assignedTypes[literal] = new AST.DataType(literal.SourceToken, ILType.Bool, 1);
                    else if (literal.Value is AST.IntegerConstant)
                        assignedTypes[literal] = new AST.DataType(literal.SourceToken, ILType.SignedInteger, -1);
                    else if (literal.Value is AST.FloatingConstant)
                        assignedTypes[literal] = new AST.DataType(literal.SourceToken, ILType.Float, -1);
                }
                else if (item.Current is AST.NameExpression name)
                {
                    var symbol = state.FindSymbol(name.Name, scope);
                    var dt = FindDataType(state, name, scope);
                    if (dt != null)
                    {
                        assignedTypes[name] = dt;
                        state.RegisterItemUsageDirection(parent, symbol, ItemUsageDirection.Read, item.Current);
                    }
                }
            }

            // Handle variables not used in normal expressions
            foreach (var item in statements.All())            
            {
                if (item.Current is AST.AssignmentStatement assignmentStatement)
                {
                    var symbol = state.FindSymbol(assignmentStatement.Name, scope);
                    if (symbol is Instance.Variable var)
                    {
                        if (var.ResolvedType == null)
                            var.ResolvedType = state.ResolveTypeName(var.Source.Type, scope);
                    }
                    else if (symbol is Instance.Signal sig)
                    {
                        if (sig.ResolvedType == null)
                            sig.ResolvedType = state.ResolveTypeName(sig.Source.Type, scope);
                    }
                    else if (symbol == null)
                        throw new ParserException($"Symbol not found: \"{assignmentStatement.Name.AsString}\"", assignmentStatement.Name.SourceToken);
                    else
                        throw new ParserException($"Can only assign to signal or variable, {assignmentStatement.Name.AsString} is {symbol.GetType().Name}", assignmentStatement.Name.SourceToken);
                }
                else if (item.Current is AST.ForStatement forStatement)
                {
                    var symbol = state.FindSymbol(forStatement.Variable, scope);
                    if (symbol is Instance.Variable var)
                    {
                        if (var.ResolvedType == null)
                            var.ResolvedType = state.ResolveTypeName(var.Source.Type, scope);
                    }
                    else if (symbol == null)
                        throw new ParserException($"Symbol not found: \"{forStatement.Variable.Name}\"", forStatement.Variable.SourceToken);
                    else
                        throw new ParserException($"Can only use variable as the counter in a for loop, {forStatement.Variable.Name} is {symbol.GetType().Name}", forStatement.Variable.SourceToken);
                }
            }

            // We are only concerned with expressions, working from leafs and up
            // At this point all literals, variables, signals, etc. should have a resolved type
            foreach (var item in statements.All(AST.TraverseOrder.DepthFirstPostOrder).OfType<AST.Expression>())
            {
                // Skip duplicate assignments
                if (assignedTypes.ContainsKey(item.Current))
                    continue;

                if (item.Current is AST.UnaryExpression unaryExpression)
                {
                    var sourceType = assignedTypes[unaryExpression.Expression];

                    switch (unaryExpression.Operation.Operation)
                    {
                        case AST.UnaryOperation.UnOp.LogicalNegation:
                            if (!sourceType.IsBoolean)
                                throw new ParserException($"Cannot perform {unaryExpression.Operation.Operation} on {sourceType}", unaryExpression);
                            break;

                        case AST.UnaryOperation.UnOp.Identity:
                        case AST.UnaryOperation.UnOp.Negation:
                            if (!sourceType.IsNumeric)
                                throw new ParserException($"Cannot perform {unaryExpression.Operation.Operation} on {sourceType}", unaryExpression);
                            break;

                        case AST.UnaryOperation.UnOp.BitwiseInvert:
                            if (!sourceType.IsInteger)
                                throw new ParserException($"Cannot perform {unaryExpression.Operation.Operation} on {sourceType}", unaryExpression);
                            break;

                        default:
                            throw new ParserException($"Unsupported unary operation: {unaryExpression.Operation.Operation}", unaryExpression);
                    }

                    // Unary operations do not change the type
                    assignedTypes[item.Current] = sourceType;
                }
                else if (item.Current is AST.BinaryExpression binaryExpression)
                {
                    var leftType = assignedTypes[binaryExpression.Left];
                    var rightType = assignedTypes[binaryExpression.Right];

                    // If we have a numerical operation, verify that the operands are numeric
                    if (binaryExpression.Operation.IsNumericOperation)
                    {
                        if (!leftType.IsNumeric)
                            throw new ParserException($"The operand {binaryExpression.Left} must be numerical to be used with {binaryExpression.Operation.Operation}", binaryExpression.Left);
                        if (!rightType.IsNumeric)
                            throw new ParserException($"The operand {binaryExpression.Right} must be numerical to be used with {binaryExpression.Operation.Operation}", binaryExpression.Right);
                    }

                    // If we have a logical operation, verify that the operands are boolean
                    if (binaryExpression.Operation.IsLogicalOperation)
                    {
                        if (!leftType.IsBoolean)
                            throw new ParserException($"The operand {binaryExpression.Left} must be boolean to be used with {binaryExpression.Operation.Operation}", binaryExpression.Left);
                        if (!rightType.IsBoolean)
                            throw new ParserException($"The operand {binaryExpression.Right} must be boolean to be used with {binaryExpression.Operation.Operation}", binaryExpression.Right);
                    }

                    // If we are doing a compare operation, verify that the types can be compared
                    if (binaryExpression.Operation.IsEqualityOperation)
                    {
                        if (!state.CanEqualityCompare(leftType, rightType, scope))
                            throw new ParserException($"Cannot perform boolean operation {binaryExpression.Operation.Operation} on types {leftType} and {rightType}", binaryExpression);
                    }

                    // Special handling of bitshift, where the type of the shift count does not change they type on the input
                    if (binaryExpression.Operation.Operation == BinOp.ShiftLeft || binaryExpression.Operation.Operation == BinOp.ShiftRight)
                    {
                        if (!leftType.IsInteger)
                            throw new ParserException($"The value being shifted must be an integer type but has type {leftType}", binaryExpression.Left);
                        if (!rightType.IsInteger)
                            throw new ParserException($"The shift operand must be an integer type but has type {rightType}", binaryExpression.Right);
                        assignedTypes[binaryExpression] = leftType;                        
                    }
                    else
                    {
                        // Make sure we can unify the types
                        if (!state.CanUnifyTypes(leftType, rightType, scope))
                            throw new ParserException($"The types types {leftType} and {rightType} cannot be unified for use with the operation {binaryExpression.Operation.Operation}", binaryExpression);

                        // Compute the unified type
                        var unified = state.UnifiedType(leftType, rightType, scope);

                        // If the source operands do not have the unified types, inject an implicit type-cast
                        if (!object.Equals(leftType, unified))
                            assignedTypes[binaryExpression.Left = new AST.TypeCast(binaryExpression.Left, unified, false)] = unified;
                        if (!object.Equals(rightType, unified))
                            assignedTypes[binaryExpression.Right = new AST.TypeCast(binaryExpression.Right, unified, false)] = unified;

                        // Assign the type to this operation
                        switch (binaryExpression.Operation.Operation)
                        {
                            // These operations just use the unified type
                            case BinOp.Add:
                            case BinOp.Subtract:
                            case BinOp.Multiply:
                            case BinOp.Modulo:
                            case BinOp.BitwiseAnd:
                            case BinOp.BitwiseOr:
                            case BinOp.BitwiseXor:
                                assignedTypes[binaryExpression] = unified;
                                break;

                            // These operations return a boolean result
                            case BinOp.Equal:
                            case BinOp.NotEqual:
                            case BinOp.LessThan:
                            case BinOp.LessThanOrEqual:
                            case BinOp.GreaterThan:
                            case BinOp.GreaterThanOrEqual:
                            case BinOp.LogicalAnd:
                            case BinOp.LogicalOr:
                                assignedTypes[binaryExpression] = new AST.DataType(binaryExpression.SourceToken, ILType.Bool, 1);
                                break;

                            default:
                                throw new ParserException($"Unable to handle operation: {binaryExpression.Operation.Operation}", binaryExpression);
                        }
                    }
                }
                else if (item.Current is AST.TypeCast typecastExpression)
                {
                    // Implicit typecasts are made by the parser so we do not validate those
                    if (!typecastExpression.Explicit)
                        continue;

                    var sourceType = assignedTypes[typecastExpression.Expression];
                    var targetType = state.ResolveTypeName(typecastExpression.TargetName, scope);

                    if (!state.CanTypeCast(sourceType, targetType, scope))
                        throw new ParserException($"Cannot cast from {sourceType} to {typecastExpression.TargetName}", typecastExpression);

                    assignedTypes[typecastExpression] = targetType;
                }
                // Carry parenthesis expression types
                else if (item.Current is AST.ParenthesizedExpression parenthesizedExpression)
                {
                    assignedTypes[item.Current] = assignedTypes[parenthesizedExpression.Expression];
                }

            }

            // Then make sure we have assigned all targets
            foreach (var item in statements.All().OfType<AST.Statement>())
            {
                if (item.Current is AST.AssignmentStatement assignmentStatement)
                {
                    var symbol = state.FindSymbol(assignmentStatement.Name, scope);
                    var exprType = assignedTypes[assignmentStatement.Value];
                    DataType targetType;

                    if (symbol is Instance.Variable variableInstance)
                        targetType = state.ResolveTypeName(variableInstance.Source.Type, scope);
                    else if (symbol is Instance.Signal signalInstance)
                        targetType = state.ResolveTypeName(signalInstance.Source.Type, scope);
                    else
                        throw new ParserException($"Assignment must be to a variable or a signal", item.Current);

                    if (!state.CanUnifyTypes(targetType, exprType, scope))
                        throw new ParserException($"Cannot assign \"{assignmentStatement.Value}\" (with type {exprType}) to {assignmentStatement.Name.SourceToken} (with type {targetType})", item.Current);
                    //var unified = state.UnifiedType(targetType, exprType, scope);

                    // Force the right-hand side to be the type we are assigning to
                    if (!object.Equals(exprType, targetType))
                        assignedTypes[assignmentStatement.Value = new AST.TypeCast(assignmentStatement.Value, targetType, false)] = targetType;

                    state.RegisterItemUsageDirection(parent, symbol, ItemUsageDirection.Write, item.Current);
                }
            }
        }

        /// <summary>
        /// Performs a lookup to find the symbol and returns the datatype of the found symbol
        /// </summary>
        /// <param name="state">The current state</param>
        /// <param name="scope">The scope to use</param>
        /// <param name="name">The name to find</param>
        /// <returns>The datatype or <c>null</c></returns>
        private static DataType FindDataType(Validation.ValidationState state, AST.NameExpression name, ScopeState scope)
        {
            var symb = state.ResolveSymbol(name, scope);
            if (symb == null)
                throw new ParserException($"Unable to find instance for name: {name.Name}", name);

            if (symb is Instance.Variable variable)
                return variable.ResolvedType = state.ResolveTypeName(variable.Source.Type, scope);
            else if (symb is Instance.Bus bus)
                return state.ResolveBusSignalTypes(bus, scope);
            else if (symb is Instance.Signal signal)
                return signal.ResolvedType = state.ResolveTypeName(signal.Source.Type, scope);
            else if (symb is Instance.ConstantReference constant)
                return constant.ResolvedType = state.ResolveTypeName(constant.Source.DataType, scope);
            else if (symb is Instance.EnumFieldReference efr)
                return new AST.DataType(name.SourceToken, efr.ParentType.Source);
            else if (symb is Instance.Literal literalInstance)
            {
                if (literalInstance.Source is AST.BooleanConstant)
                    return new AST.DataType(literalInstance.Source.SourceToken, ILType.Bool, 1);
                else if (literalInstance.Source is AST.IntegerConstant)
                    return new AST.DataType(literalInstance.Source.SourceToken, ILType.SignedInteger, -1);
                else if (literalInstance.Source is AST.FloatingConstant)
                    return new AST.DataType(literalInstance.Source.SourceToken, ILType.Float, -1);
            }

            return null;
        }
    }
}
