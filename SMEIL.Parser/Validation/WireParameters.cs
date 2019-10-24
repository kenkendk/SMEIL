using System;
using System.Collections.Generic;
using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Validator module that wires up parameters to their instances
    /// </summary>
    public class WireParameters : IValidator
    {
        /// <summary>
        /// Validates the module
        /// </summary>
        /// <param name="state">The validation state</param>
        public void Validate(ValidationState state)
        {
            // We need to assign the constant and variable types 
            // before we try to match the parameters
            foreach (var item in state.AllInstances)
            {
                if (item is Instance.Module m)
                    ResolveTypes(state, m.Instances, state.LocalScopes[m]);
                else if (item is Instance.Network nv)
                    ResolveTypes(state, nv.Instances, state.LocalScopes[nv]);
                else if (item is Instance.Process pc)
                    ResolveTypes(state, pc.Instances, state.LocalScopes[pc]);
                else if (item is Instance.FunctionInvocation fi)
                    ResolveTypes(state, fi.Instances, state.LocalScopes[fi]);
            }

            // Then wire up the parameters
            foreach (var item in state.AllInstances.OfType<Instance.IParameterizedInstance>())
                WireUpParameters(state, item);
        }

        /// <summary>
        /// The instances 
        /// </summary>
        /// <param name="state">The validation state</param>
        /// <param name="instances">The instances to assign types to</param>
        /// <param name="scope">The scope to use for lookup</param>
        private void ResolveTypes(ValidationState state, IEnumerable<Instance.IInstance> instances, ScopeState scope)
        {
            foreach (var r in instances)
            {
                if (r is Instance.ConstantReference cref)
                {
                    if (cref.ResolvedType == null)
                        cref.ResolvedType = state.ResolveTypeName(cref.Source.DataType, scope);
                }
                else if (r is Instance.Variable v)
                {
                    if (v.ResolvedType == null)
                        v.ResolvedType = state.ResolveTypeName(v.Source.Type, scope);
                }
            }
        }

        /// <summary>
        /// Wires up parameters for a parameterized instance
        /// </summary>
        /// <param name="state">The validation state</param>
        /// <param name="sourceinstance">The instance to wire up</param>
        private void WireUpParameters(ValidationState state, Instance.IParameterizedInstance sourceinstance)
        {
            if (sourceinstance.MappedParameters.Count < sourceinstance.SourceParameters.Length)
            {
                if (sourceinstance.MappedParameters.Count != 0)
                    throw new Exception("Unexpected half-filled parameter list");

                var position = 0;
                var anynamed = false;
                var map = new Instance.MappedParameter[sourceinstance.SourceParameters.Length];
                var scope = state.LocalScopes[sourceinstance];

                // Map for getting the parameter index of a name
                var namelist = sourceinstance
                    .SourceParameters
                    .Zip(
                        Enumerable.Range(0, sourceinstance.SourceParameters.Length),
                        (p, i) => new { i, p.Name.Name }
                    );

                var collisions = namelist
                    .GroupBy(x => x.Name)
                    .Where(x => x.Count() != 1)
                    .FirstOrDefault();

                if (collisions != null)
                    throw new ParserException($"Multiple arguments named {collisions.Key}, positions: {string.Join(", ", collisions.Select(x => x.i.ToString())) }", sourceinstance.SourceParameters[collisions.Last().i].Name);

                var nameindexmap = namelist.ToDictionary(x => x.Name, x => x.i);

                foreach (var p in sourceinstance.ParameterMap)
                {
                    var pos = position;
                    if (p.Name == null)
                    {
                        if (anynamed)
                            throw new ParserException($"Cannot have positional arguments after named arguments", p);
                    }
                    else
                    {
                        anynamed = true;
                        if (!nameindexmap.TryGetValue(p.Name.Name, out pos))
                            throw new ParserException($"No parameter named {p.Name.Name} in {sourceinstance.SourceName}", sourceinstance.SourceItem);
                    }

                    if (map[pos] != null)
                        throw new ParserException($"Double argument for {sourceinstance.SourceParameters[pos].Name.Name} detected", sourceinstance.SourceItem);
                    
                    // Extract the parameter definition
                    var sourceparam = sourceinstance.SourceParameters[pos];

                    Instance.IInstance value;
                    var tc = p.Expression as AST.TypeCast;

                    if (tc != null)
                        value = state.ResolveSymbol(tc.Expression, scope);
                    else
                        value = state.ResolveSymbol(p.Expression, scope);

                    if (value == null)
                        throw new ParserException("Unable to resolve expression", p.Expression.SourceToken);

                    var itemtype = state.InstanceType(value);
                    var parametertype = 
                        sourceparam.ExplictType == null
                        ? itemtype
                        : state.ResolveTypeName(sourceparam.ExplictType, scope);

                    if (parametertype.IsValue && sourceparam.Direction == AST.ParameterDirection.Out)
                        throw new ParserException($"Cannot use a value-type parameter as output: {sourceparam.SourceToken}", sourceparam);

                    // We need to expand both types to intrinsics to remove any type aliases that need lookups
                    var intrinsic_itemtype = state.ResolveToIntrinsics(itemtype, scope);
                    var intrinsic_parametertype = state.ResolveToIntrinsics(parametertype, scope);

                    // If the input is a typecast (and required) we wire it through a process
                    if (tc != null && !state.CanUnifyTypes(intrinsic_itemtype, intrinsic_parametertype, scope))
                    {
                        var typecast_target = state.ResolveToIntrinsics(state.ResolveTypeName(tc.TargetName, scope), scope);
                        var typecast_source = sourceparam.Direction == ParameterDirection.In ? intrinsic_itemtype : intrinsic_parametertype;

                        var sourceSignals = typecast_source
                            .Shape
                            .Signals
                            .Select(x => x.Key)
                            .ToHashSet();

                        var shared_shape =
                            typecast_target
                            .Shape
                            .Signals
                            .Where(x => sourceSignals.Contains(x.Key))
                            .Select(x => new AST.BusSignalDeclaration(
                                p.SourceToken,
                                new AST.Identifier(
                                    new ParseToken(0, 0, 0, x.Key)
                                ),
                                x.Value,
                                null,
                                null
                            ))
                            .ToArray();

                        if (sourceSignals.Count != shared_shape.Length)
                            throw new ParserException($"The typecast is invalid as the names do not match", p.SourceToken);

                        var proc = IdentityHelper.CreateTypeCastProcess(
                            state,
                            scope,
                            p.SourceToken,
                            tc.Expression,
                            new AST.Name(p.SourceToken, new[] {
                                new AST.Identifier(new ParseToken(0, 0, 0, sourceinstance.Name)),
                                sourceparam.Name
                            }, null).AsExpression(),
                            shared_shape,
                            shared_shape
                        );

                        throw new ParserException($"Typecasts inside process instantiations are not currently supported", p.SourceToken);
                        // using (state.StartScope(proc))
                        //     CreateAndRegisterInstance(state, proc);
                        // parentCollection.Add(proc);

                    }

                    // Check argument compatibility
                    if (!state.CanUnifyTypes(intrinsic_itemtype, intrinsic_parametertype, scope))
                        throw new ParserException($"Cannot use {p.Expression.SourceToken} of type {intrinsic_itemtype.ToString()} as the argument for {sourceparam.Name.SourceToken} of type {intrinsic_parametertype}", p.Expression);
                    
                    // Check that the type we use as input is "larger" than the target
                    var unified = state.UnifiedType(intrinsic_itemtype, parametertype, scope);
                    if (!object.Equals(unified, intrinsic_itemtype))
                        throw new ParserException($"Cannot use {p.Expression.SourceToken} of type {intrinsic_itemtype.ToString()} as the argument for {sourceparam.Name.SourceToken} of type {intrinsic_parametertype}", p.Expression);

                    map[pos] = new Instance.MappedParameter(p, sourceparam, value, parametertype);
                    var localname = map[pos].LocalName;

                    // Register the instance in the local symbol table to allow
                    // refering to the instance with the parameter name
                    scope.TryAddSymbol(localname, value, sourceparam.Name);
                    position++;
                }

                if (map.Any(x => x == null))
                    throw new ParserException("Argument missing", sourceinstance.SourceItem);

                sourceinstance.MappedParameters.AddRange(map);

            }
        }

        // /// <summary>
        // /// Wires up parameters for a process
        // /// </summary>
        // /// <param name="state">The validation state</param>
        // /// <param name="process">The process to wire up</param>
        // private void WireUpParameters(ValidationState state, Instance.Process process)
        // {
        //     if (process.MappedParameters.Count < process.Source.Parameters.Length)
        //     {
        //         if (process.MappedParameters.Count != 0)
        //             throw new Exception("Unexpected half-filled parameter list");

        //         if (process.Source.Parameters.Length != process.ProcessDefinition.Parameters.Length)
        //             throw new ParserException($"Too many arguments, got {process.Source.Parameters.Length} expected {process.ProcessDefinition.Parameters.Length}", process.Source);

        //         var position = 0;
        //         var anynamed = false;
        //         var map = new Instance.MappedParameter[process.Source.Parameters.Length];
        //         var symboltable = state.LocalScopes[process];
                
        //         // Map for getting the parameter index of a name
        //         var nameindexmap = process
        //             .ProcessDefinition
        //             .Parameters
        //             .Zip(
        //                 Enumerable.Range(0, process.ProcessDefinition.Parameters.Length), 
        //                 (p, i) => new { i, p.Name.Name }
        //             )
        //             .ToDictionary(x => x.Name, x => x.i);

        //         foreach (var p in process.Source.Parameters)
        //         {
        //             var pos = position;
        //             if (p.Name == null)
        //             {
        //                 if (anynamed)
        //                     throw new ParserException($"Cannot have positional arguments after named arguments", p);
        //             }
        //             else
        //             {
        //                 anynamed = true;
        //                 if (!nameindexmap.TryGetValue(p.Name.Name, out pos))
        //                     throw new ParserException($"No parameter named {p.Name.Name} in {process.ProcessDefinition.Name.Name}", p);
        //             }

        //             if (map[pos] != null)
        //                 throw new ParserException($"Double argument for {process.ProcessDefinition.Parameters[pos].Name.Name} detected", p);

        //             var value = state.ResolveSymbol(p.Expression, symboltable);
        //             if (value == null)
        //                 throw new ParserException("Unable to resolve expression", p.Expression);

        //             map[pos] = new Instance.MappedParameter(p) {
        //                 MappedItem = value
        //             };

        //             // Register the instance in the local symbol table to allow
        //             // refering to the instance with the parameter name
        //             var name = process.ProcessDefinition.Parameters[pos].Name.Name;
        //             symboltable.Add(name, value);

        //             position++;
        //         }

        //         if (map.Any(x => x == null))
        //             throw new ParserException("Argument missing", process.Source);

        //         process.MappedParameters.AddRange(map);
        //     }
        // }



    }
}