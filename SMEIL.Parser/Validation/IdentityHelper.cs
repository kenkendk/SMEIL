using System;
using System.Linq;
using SMEIL.Parser.AST;
using SMEIL.Parser.Instance;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Class for creating identity processes during parse tree generation
    /// </summary>
    public static class IdentityHelper
    {
        /// <summary>
        /// Creates a fake process that is used to create the connection
        /// </summary>
        /// <param name="state">The validation state to use</param>
        /// <param name="scope">The active scope</param>
        /// <param name="connEntry">The connection entry to create the process for</param>
        /// <returns>A fake process instance</returns>
        public static Instance.Process CreateConnectProcess(Validation.ValidationState state, Validation.ScopeState scope, AST.ConnectEntry connEntry)
        {
            var lhs = state.FindSymbol(connEntry.Source, scope) ?? throw new ParserException($"Could not resolve symbol {connEntry.Source.SourceToken}", connEntry.Source);
            var rhs = state.FindSymbol(connEntry.Target, scope) ?? throw new ParserException($"Could not resolve symbol {connEntry.Target.SourceToken}", connEntry.Target);

            if (lhs is Instance.Signal lhs_signal && rhs is Instance.Signal rhs_signal)
            {
                return IdentityHelper.CreateIdentityProcess(
                    state,
                    scope,
                    connEntry.SourceToken,
                    // Extract the bus name locally
                    new Name(connEntry.Source.SourceToken, connEntry.Source.Identifier.SkipLast(1).ToArray(), null).AsExpression(),
                    new Name(connEntry.Target.SourceToken, connEntry.Target.Identifier.SkipLast(1).ToArray(), null).AsExpression(),
                    // Assign just the named entry
                    new[] { lhs_signal.Source },
                    // Ensure we use the same type to avoid silent type-casting
                    new[] {
                        new BusSignalDeclaration(connEntry.Target.SourceToken,
                            rhs_signal.Source.Name,
                            lhs_signal.Source.Type,
                            null,
                            null,
                            AST.SignalDirection.Normal
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
                return IdentityHelper.CreateIdentityProcess(
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
        /// Creates a fake process that is used to perform a type-cast
        /// </summary>
        /// <param name="state">The state to use</param>
        /// <param name="scope">The scope to use</param>
        /// <param name="sourceToken">The source token to use for reporting errors</param>
        /// <param name="expression">The expression for the item</param>
        /// <param name="mappedItem">The item that is mapped to the parameter</param>
        public static Instance.Process CreateTypeCastProcess(ValidationState state, ScopeState scope, ParseToken sourceToken, AST.Expression input, AST.Expression output, BusSignalDeclaration[] inputshape, BusSignalDeclaration[] outputshape)
        {
            var pt_in = new ParseToken(0, 0, 0, "in");
            var pt_out = new ParseToken(0, 0, 0, "out");

            if (inputshape.Length != outputshape.Length)
                throw new ParserException($"Incorrect mapping of signals while creating an identity process", sourceToken);

            return new Instance.Process(
                new AST.InstanceDeclaration(
                    sourceToken,
                    new InstanceName(sourceToken, new Identifier(new ParseToken(0, 0, 0, "typecast")), null),
                    new Identifier(new ParseToken(0, 0, 0, "typecast")),
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
                    new AST.Identifier(new ParseToken(0, 0, 0, "typecast")),
                    new AST.Parameter[] {
                            new AST.Parameter(
                                input.SourceToken,
                                ParameterDirection.In,
                                new Identifier(pt_in),
                                0,
                                null
                            ),
                            new AST.Parameter(
                                output.SourceToken,
                                ParameterDirection.Out,
                                new Identifier(pt_out),
                                0,
                                null
                            )
                    },
                    new Declaration[0],

                    inputshape.Select(
                        (_, i) =>
                        {
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
        public static Instance.Process CreateIdentityProcess(ValidationState state, ScopeState scope, ParseToken sourceToken, Expression input, Expression output, BusSignalDeclaration[] inputshape, BusSignalDeclaration[] outputshape, Instance.ProcessType type)
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
                    new AST.Parameter[] {
                            new AST.Parameter(
                                input.SourceToken,
                                ParameterDirection.In,
                                new Identifier(pt_in),
                                0,
                                null
                            ),
                            new AST.Parameter(
                                output.SourceToken,
                                ParameterDirection.Out,
                                new Identifier(pt_out),
                                0,
                                null
                            )
                    },
                    new Declaration[0],

                    inputshape.Select(
                        (_, i) =>
                        {
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

                type
            );
        }        
    }
}