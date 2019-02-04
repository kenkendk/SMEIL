using System;
using System.Linq;
using System.Collections.Generic;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Wires busses as desired in the network
    /// </summary>
    public class CreateInstances : IValidator
    {
        /// <summary>
        /// Validates the module
        /// </summary>
        /// <param name="state">The validation state</param>
        public void Validate(ValidationState state)
        {
            // Start the process with the network
            state.TopLevel.NetworkInstance = CreateAndRegisterInstance(state, state.TopLevel.NetworkDeclaration, state.TopLevel.SourceNetwork);
        }

        /// <summary>
        /// Creates all sub-instances for a network
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="parent">The parent network instance</param>
        private Instance.Network CreateAndRegisterInstance(ValidationState state, AST.InstanceDeclaration instDecl, AST.Network network)
        {
            // Create the network instance
            var netinstance = new Instance.Network(instDecl, network);

            // We have registered the network by this name already
            //state.SymbolTable.Add(netinstance.Name, netinstance);

            using (state.StartScope(network, netinstance))
                CreateAndRegisterInstances(state, netinstance.NetworkDefinition.Declarations, netinstance.Instances);

            return netinstance;
        }

        /// <summary>
        /// Creates and registers all network declaration instances
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="networkDecls">The network declarations to instantiate</param>
        /// <param name="parentCollection"></param>
        private void CreateAndRegisterInstances(Validation.ValidationState state, IEnumerable<NetworkDeclaration> networkDecls, IList<Instance.IInstance> parentCollection)
        {
            var scope = state.CurrentScope;
            foreach (var decl in networkDecls)
            {
                if (decl is AST.BusDeclaration bus)
                {
                    var b = new Instance.Bus(bus);
                    scope.SymbolTable.Add(b.Name, b);
                    using (state.StartScope(b))
                        CreateAndRegisterInstance(state, b);
                    parentCollection.Add(b);
                }
                else if (decl is AST.ConstantDeclaration)
                    continue; // We just refer to the constant, no need for an instance
                else if (decl is AST.GeneratorDeclaration genDecl)
                {
                    var startSymbol = state.ResolveToInteger(genDecl.SourceExpression, scope);
                    var finishSymbol = state.ResolveToInteger(genDecl.TargetExpression, scope);

                    // TODO: Need to fix array support for this to work correctly?
                    for(var i = startSymbol; i < finishSymbol; i++)
                        CreateAndRegisterInstances(state, genDecl.Networks, parentCollection);
                }
                else if (decl is AST.InstanceDeclaration instDecl)
                {
                    var sym = state.FindSymbol(instDecl.SourceItem, scope);
                    if (sym != null && sym is AST.Network netw)
                    {
                        // Recursively create network
                        // TODO: Guard against infinite recursion
                        parentCollection.Add(CreateAndRegisterInstance(state, instDecl, netw));
                    }
                    else if (sym != null && sym is AST.Process proc)
                    {
                        var p = new Instance.Process(instDecl, proc);
                        if (instDecl.Name.Name != null)
                            scope.SymbolTable.Add(instDecl.Name.Name.Name, p);

                        using(state.StartScope(p, instDecl))
                            CreateAndRegisterInstance(state, p);
                        parentCollection.Add(p);
                    }
                    else
                    {
                        if (sym == null)
                            throw new ParserException($"No item found with the name {instDecl.SourceItem}", decl);
                        else
                            throw new ParserException($"The item {instDecl.SourceItem} is not a process, but a {sym.GetType().Name}", decl);
                    }
                }
                else
                    throw new ParserException($"Attempted to create an instance of type: {decl.GetType()}", decl);
            }
        }


        /// <summary>
        /// Creates all sub-instances for a process
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="parent">The parent process</param>
        private void CreateAndRegisterInstance(Validation.ValidationState state, Instance.Process parent)
        {
            var scope = state.CurrentScope;

            // Then add all variables and locally defined busses
            foreach (var decl in parent.ProcessDefinition.Declarations)
            {
                if (decl is EnumDeclaration)
                    continue; // No instance needed
                else if (decl is FunctionDeclaration)
                    continue; // No instance needed
                else if (decl is ConstantDeclaration)
                    continue; // No instance needed
                else if (decl is VariableDeclaration variable)
                {
                    var v = new Instance.Variable(variable);
                    scope.SymbolTable.Add(v.Name, v);
                    parent.Instances.Add(v);
                }
                else if (decl is BusDeclaration bus)
                {
                    var b = new Instance.Bus(bus);
                    scope.SymbolTable.Add(b.Name, b);
                    using(state.StartScope(b))
                        CreateAndRegisterInstance(state, b);
                    parent.Instances.Add(b);
                }
                else
                    throw new ParserException($"Unable to process {decl.GetType()} inside a process", decl);

            }

            // TODO: The scope should work within the statement tree. We can have nested statements....
            foreach (var loop in parent.ProcessDefinition.Statements.All().OfType<AST.ForStatement>())
            {
                var l = new Instance.ForLoop(loop.Current);
                using (var sc = state.StartScope(l)) {
                    // TODO: Should use the variable instance?
                    sc.SymbolTable.Add(loop.Current.Variable.Name, l);
                }
                parent.Instances.Add(l);
            }
        }

        /// <summary>
        /// Creates instances for all signals of a bus
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="parent">The bus to create the signal instances for</param>
        private void CreateAndRegisterInstance(Validation.ValidationState state, Instance.Bus parent)
        {
            var scope = state.CurrentScope;

            foreach (var signal in parent.Source.Signals)
            {
                var s = new Instance.Signal(parent, signal);
                scope.SymbolTable.Add(s.Name, s);
                parent.Instances.Add(s);

            }
        }

    }
}