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
            foreach (var item in state.AllInstances.OfType<Instance.IParameterizedInstance>())
                WireUpParameters(state, item);
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
                var nameindexmap = sourceinstance
                    .SourceParameters
                    .Zip(
                        Enumerable.Range(0, sourceinstance.SourceParameters.Length),
                        (p, i) => new { i, p.Name.Name }
                    )
                    .ToDictionary(x => x.Name, x => x.i);


                foreach (var p in sourceinstance.DeclarationSource.Parameters)
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

                    var value = state.ResolveSymbol(p.Expression, scope);
                    if (value == null)
                        throw new ParserException("Unable to resolve expression", p.Expression);

                    var sourceparam = sourceinstance.SourceParameters[pos];

                    var itemtype = state.InstanceType(value);
                    var parametertype = 
                        sourceparam.ExplictType == null
                        ? itemtype
                        : state.ResolveTypeName(sourceparam.ExplictType, scope);

                    if (parametertype.IsValue && sourceparam.Direction == AST.ParameterDirection.Out)
                        throw new ParserException($"Cannot use a value-type parameter as output: {sourceparam.Name}", sourceparam);

                    // Check argument compatibility
                    if (!state.CanUnifyTypes(itemtype, parametertype, scope))
                        throw new ParserException($"Cannot use {p.Expression} of type {itemtype} as the argument for {sourceparam.Name} of type {parametertype}", sourceparam);
                    
                    // Check that the type we use as input is "larger" than the target
                    var unified = state.UnifiedType(itemtype, parametertype, scope);
                    if (!object.Equals(unified, itemtype))
                        throw new ParserException($"Cannot use {p.Expression} of type {itemtype} as the argument for {sourceparam.Name} of type {parametertype}", sourceparam);

                    map[pos] = new Instance.MappedParameter(p, sourceparam, value, parametertype);

                    // Register the instance in the local symbol table to allow
                    // refering to the instance with the parameter name
                    scope.SymbolTable.Add(map[pos].LocalName, value);
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