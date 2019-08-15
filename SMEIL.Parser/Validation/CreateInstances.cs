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
                        var p = CreateConnectProcess(state, scope, connEntry);
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
        /// Creates a fake process that is used to create the connection
        /// </summary>
        /// <param name="state">The validation state to use</param>
        /// <param name="scope">The active scope</param>
        /// <param name="connEntry">The connection entry to create the process for</param>
        /// <returns>A fake process instance</returns>
        private Instance.Process CreateConnectProcess(Validation.ValidationState state, Validation.ScopeState scope, AST.ConnectEntry connEntry)
        {
            var lhs = state.FindSymbol(connEntry.Source, scope) ?? throw new ParserException($"Could not resolve symbol {connEntry.Source.SourceToken}", connEntry.Source);
            var rhs = state.FindSymbol(connEntry.Target, scope) ?? throw new ParserException($"Could not resolve symbol {connEntry.Target.SourceToken}", connEntry.Target);

            if (lhs is Instance.Signal lhs_signal && rhs is Instance.Signal rhs_signal)
            {
                return CreateIdentityProcess(
                    state,
                    scope,
                    connEntry.SourceToken,
                    // Extract the bus name locally
                    new Name(connEntry.Source.SourceToken, connEntry.Source.Identifier.SkipLast(1).ToArray(), null).AsExpression(),
                    new Name(connEntry.Target.SourceToken, connEntry.Target.Identifier.SkipLast(1).ToArray(), null).AsExpression(),
                    // Assign just the named entry
                    new [] { lhs_signal.Source },
                    // Ensure we use the same type to avoid silent type-casting
                    new [] { 
                        new BusSignalDeclaration(connEntry.Target.SourceToken,
                            rhs_signal.Source.Name,
                            lhs_signal.Source.Type,
                            null, 
                            null
                        )
                    },
                    Instance.ProcessType.Connect
                );
            }
            else if (lhs is Instance.Bus lhs_bus && rhs is Instance.Bus rhs_bus)
            {
                // For connect with busses,
                // we only wire up signals with the same name
                var rhs_shape = new BusShape(
                    rhs_bus.Source.SourceToken,
                    rhs_bus.Source.Signals
                );

                // Create a shape with the shared signals
                var shared_shape = 
                    lhs_bus.Source.Signals
                        .Where(x => rhs_shape.Signals.ContainsKey(x.Name.Name))
                        .ToArray();

                // We do not need to do a type check here, 
                // it will be enfored when the types are assigned
                return CreateIdentityProcess(
                    state,
                    scope,
                    connEntry.SourceToken,
                    connEntry.Source.AsExpression(),
                    connEntry.Target.AsExpression(),
                    shared_shape,
                    shared_shape,
                    Instance.ProcessType.Connect
                );
            }
            else
                throw new ParserException($"The item {connEntry.Source.SourceToken} (of type {lhs.GetType()}) cannot be mapped to {connEntry.Target.SourceToken} (of type {rhs.GetType()}), only signal -> signal or bus -> bus mappings are allowed", connEntry.SourceToken);
        }

        /// <summary>
        /// Creates a new identity process
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="scope">The scope to use</param>
        /// <param name="sourceToken">The source token to use for reporting errors</param>
        /// <param name="input">The input expression, must refer to a bus instance</param>
        /// <param name="output">The output expression, must refer to a bus instance</param>
        /// <param name="inputshape">The input shape</param>
        /// <param name="outputshape">The output shape</param>
        /// <param name="type">The the type of identity process to create</param>
        /// <returns>A dynamically instantiated process</returns>
        private Instance.Process CreateIdentityProcess(ValidationState state, ScopeState scope, ParseToken sourceToken, Expression input, Expression output, BusSignalDeclaration[] inputshape, BusSignalDeclaration[] outputshape, Instance.ProcessType type)
        {
            var pt_in = new ParseToken(0, 0, 0, "in");
            var pt_out = new ParseToken(0, 0, 0, "out");

            if (inputshape.Length != outputshape.Length)
                throw new ParserException($"Incorrect mapping of signals while creating an identity process", sourceToken);

            return new Instance.Process(
                new AST.InstanceDeclaration(
                    sourceToken,
                    new InstanceName(sourceToken, new Identifier(new ParseToken(0, 0, 0, "connect")), null),
                    new Identifier(new ParseToken(0, 0, 0, "connect")),
                    new ParameterMap[] {
                            new AST.ParameterMap(
                                input.SourceToken,
                                new Identifier(pt_in),
                                input
                            ),
                            new AST.ParameterMap(
                                output.SourceToken,
                                new Identifier(pt_out),
                                output
                            )
                    }
                ),
                new AST.Process(
                    sourceToken,
                    false,
                    new AST.Identifier(new ParseToken(0, 0, 0, "connect")),
                    new Parameter[] {
                            new Parameter(
                                input.SourceToken,
                                ParameterDirection.In,
                                new Identifier(pt_in),
                                0,
                                null
                            ),
                            new Parameter(
                                output.SourceToken,
                                ParameterDirection.Out,
                                new Identifier(pt_out),
                                0,
                                null
                            )
                    },
                    new Declaration[0],

                    inputshape.Select(
                        (_, i) => {
                            Expression sourceexpr = 
                                new Name(
                                    input.SourceToken,
                                    new Identifier[] {
                                        new Identifier(pt_in),
                                        new Identifier(new ParseToken(0, 0, 0, inputshape[i].Name.Name))
                                    },
                                    new ArrayIndex[0]
                                ).AsExpression();

                            var inputtype = state.ResolveTypeName(inputshape[i].Type, scope);
                            var outputtype = state.ResolveTypeName(outputshape[i].Type, scope);
                            if (!inputtype.Equals(outputtype))
                                sourceexpr = new AST.TypeCast(sourceexpr, outputtype, true);

                            return new AssignmentStatement(
                                sourceToken,
                                new Name(
                                    output.SourceToken,
                                    new Identifier[] {
                                        new Identifier(pt_out),
                                        new Identifier(new ParseToken(0, 0, 0, outputshape[i].Name.Name))
                                    },
                                    new ArrayIndex[0]
                                ),
                                sourceexpr
                            );
                        })
                    .ToArray()
                ),

                Instance.ProcessType.Connect
            );
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