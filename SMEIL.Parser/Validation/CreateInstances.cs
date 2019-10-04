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
            // Start the process with the top-level module
            state.TopLevel.ModuleInstance = CreateAndRegisterInstance(state, state.TopLevel.Module);
        }

        /// <summary>
        /// Creates all sub-instances for a module
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="module">The parent module</param>
        /// <returns></returns>
        private Instance.Module CreateAndRegisterInstance(ValidationState state, AST.Module module)
        {
            var modinstance = new Instance.Module(module);
            var parentCollection = modinstance.Instances;

            using (var scope = state.StartScope(module, modinstance))
            {
                // TODO: Create module instances here
                foreach (var imp in module.Imports)
                {
                }

                foreach (var decl in module.Declarations)
                {
                    if (decl is AST.ConstantDeclaration cdecl)
                    {
                        var cref = new Instance.ConstantReference(cdecl);
                        scope.SymbolTable.Add(cref.Name, cref);
                        parentCollection.Add(cref);
                    }
                    else if (decl is AST.EnumDeclaration edecl)
                    {
                        var e = new Instance.EnumTypeReference(edecl);
                        scope.SymbolTable.Add(e.Name, e);
                        using (state.StartScope(e))
                            CreateAndRegisterInstance(state, e);
                        parentCollection.Add(e);
                    }
                    else if (decl is AST.FunctionDefinition fdecl)
                    {
                        scope.SymbolTable.Add(fdecl.Name.Name, fdecl);
                    }
                }

                // The entry-level network is not user-instantiated
                if (module == state.TopLevel.Module)
                    modinstance.Instances.Add(
                        state.TopLevel.NetworkInstance = 
                            CreateAndRegisterInstance(state, state.TopLevel.NetworkDeclaration, state.TopLevel.SourceNetwork)
                    );

            }

            return modinstance;
        }

        /// <summary>
        /// Creates all sub-instances for a network
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="network">The parent network instance</param>
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
                else if (decl is AST.ConstantDeclaration cdecl)
                {
                    var cref = new Instance.ConstantReference(cdecl);
                    scope.SymbolTable.Add(cref.Name, cref);
                    parentCollection.Add(cref);
                    continue; 
                }
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
                        var p = new Instance.Process(instDecl, proc, Instance.ProcessType.Normal);
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
                else if (decl is AST.ConnectDeclaration connDecl)
                {
                    foreach (var connEntry in connDecl.Entries)
                    {
                        var p = IdentityHelper.CreateConnectProcess(state, scope, connEntry);
                        using (state.StartScope(p, connEntry))
                            CreateAndRegisterInstance(state, p);
                        parentCollection.Add(p);
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

            CreateAndRegisterInstancesForDeclarations(state, parent.ProcessDefinition.Declarations, parent.Instances);

            // TODO: The scope should work within the statement tree. We can have nested statements....
            foreach (var loop in parent.Statements.All().OfType<AST.ForStatement>())
            {
                var l = new Instance.ForLoop(loop.Current);
                using (var sc = state.StartScope(l)) {
                    // TODO: Should use the variable instance?
                    sc.SymbolTable.Add(loop.Current.Variable.Name, l);
                }
                parent.Instances.Add(l);
            }

            // Find all function invocations so we can map parameters
            foreach (var func in parent.Statements.All().OfType<AST.FunctionStatement>())
            {
                var fdef = state.FindSymbol(func.Current.Name, scope);
                if (fdef == null)
                    throw new ParserException($"No function named {func.Current.Name.AsString} found", func.Current);
                if (!(fdef is AST.FunctionDefinition ffdef))
                    throw new ParserException($"Expected a function for {func.Current.Name.AsString}, but found {fdef.GetType()}", func.Current);

                var f = new Instance.FunctionInvocation(ffdef, func.Current);
                using (var sc = state.StartScope(f))
                    CreateAndRegisterInstancesForDeclarations(state, ffdef.Declarations, f.Instances);

                parent.Instances.Add(f);
            }
        }

        /// <summary>
        /// Creates all sub-instances from declarations
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="declarations">The declarations to process</param>
        /// <param name="parentInstances">The collection of instances to append to</param>
        private void CreateAndRegisterInstancesForDeclarations(Validation.ValidationState state, IEnumerable<AST.Declaration> declarations, List<Instance.IInstance> parentInstances)
        {
            var scope = state.CurrentScope;

            // Add all variables and locally defined busses
            foreach (var decl in declarations)
            {
                if (decl is EnumDeclaration en)
                {
                    var e = new Instance.EnumTypeReference(en);
                    scope.SymbolTable.Add(e.Name, e);
                    using (state.StartScope(e))
                        CreateAndRegisterInstance(state, e);
                    parentInstances.Add(e);

                }
                else if (decl is FunctionDefinition fdef)
                {
                    scope.SymbolTable.Add(fdef.Name.Name, decl);
                }
                else if (decl is ConstantDeclaration cdecl)
                {
                    var c = new Instance.ConstantReference(cdecl);
                    scope.SymbolTable.Add(c.Name, c);
                    parentInstances.Add(c);
                }
                else if (decl is VariableDeclaration variable)
                {
                    var v = new Instance.Variable(variable);
                    scope.SymbolTable.Add(v.Name, v);
                    parentInstances.Add(v);
                }
                else if (decl is BusDeclaration bus)
                {
                    var b = new Instance.Bus(bus);
                    scope.SymbolTable.Add(b.Name, b);
                    using (state.StartScope(b))
                        CreateAndRegisterInstance(state, b);
                    parentInstances.Add(b);
                }
                else
                    throw new ParserException($"Unable to process {decl.GetType()} inside a process", decl);
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

        /// <summary>
        /// Creates all enum fields for an enum
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="parent">The enum instance</param>
        private void CreateAndRegisterInstance(ValidationState state, Instance.EnumTypeReference parent)
        {
            var scope = state.CurrentScope;

            var cur = 0;
            foreach (var field in parent.Source.Fields)
            {
                var ix = field.Value;
                if (ix < 0)
                    ix = cur;

                var s = new Instance.EnumFieldReference(parent, field, ix);

                parent.Fields.Add(field.Name.Name, s);
                scope.SymbolTable.Add(field.Name.Name, s);
                parent.Instances.Add(s);

                cur = ix + 1;
            }
        }
    }
}