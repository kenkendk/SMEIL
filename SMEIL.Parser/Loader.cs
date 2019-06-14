using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace SMEIL.Parser
{
    /// <summary>
    /// Loads a module and all referenced modules
    /// </summary>
    public static class Loader
    {
        /// <summary>
        /// Loads a single module, parsing and constructing the AST for it
        /// </summary>
        /// <param name="file">The file to load</param>
        /// <returns>The module</returns>
        private static AST.Module LoadModule(string file)
        {
            using (var rd = new StreamReader(file))
                return BNFMapper.Parse(Tokenizer.Tokenize(rd));
        }

        /// <summary>
        /// Gets the path of a module, given the referencing module's path
        /// </summary>
        /// <param name="sourcefilepath">The path to the source file requesting the import</param>
        /// <param name="name">The module to load</param>
        /// <returns>The module</returns>
        private static string GetModulePath(string sourcefilepath, AST.ImportName name)
        {
            return
                System.IO.Path.Join(
                    System.IO.Path.GetDirectoryName(sourcefilepath),
                    string.Join(
                        System.IO.Path.DirectorySeparatorChar.ToString(), name.Name.Select(x => x.Name)
                    ) + ".sme"
                );            
        }


        /// <summary>
        /// Loads the module and its imports 
        /// </summary>
        /// <param name="file">The main module</param>
        /// <param name="toplevel">The top-level network or null</param>
        /// <returns>The state for the loaded modules</returns>
        public static Validation.ValidationState LoadModuleAndImports(string file, string toplevel, string[] arguments)
        {
            // Basic setup
            var state = new Validation.ValidationState();
            var rootscope = state.CurrentScope;
            var toResolve = new Stack<AST.Module>();
            state.TopLevel.Module = LoadModule(file);

            // Find the entry network
            var networks = state.TopLevel.Module.Entities.OfType<AST.Network>().ToList();
            if (string.IsNullOrWhiteSpace(toplevel))
            {
                if (networks.Count == 0)
                    throw new ArgumentException("The main module contains no networks?");
                if (networks.Count != 1)
                    throw new ArgumentException($"The main module contains {networks.Count} networks, please specify a network name");
                state.TopLevel.SourceNetwork = networks.First();
            }
            else
            {
                var namednetworks = networks.Where(x => string.Equals(x.Name.Name, toplevel, StringComparison.OrdinalIgnoreCase)).ToList();
                if (networks.Count == 0)
                    throw new ArgumentException($"The main module contains no networks named \"{toplevel}\"");
                if (networks.Count != 1)
                    throw new ArgumentException($"The main module contains {networks.Count} network named \"{toplevel}\"");
                state.TopLevel.SourceNetwork = namednetworks.First();
            }

            // Wire up the top-level network parameters
            var dummyparsetoken = new ParseToken(0, 0, 0, "__commandline__");
            var name = new AST.Identifier(new ParseToken(0, 0, 0, "__main__"));

            state.TopLevel.CommandlineArguments = arguments = arguments ?? new string[0];
            state.Modules[file] = state.TopLevel.Module;
            state.LocalScopes[state.TopLevel.Module] = rootscope;

            // Recursively load and resolve imports
            LoadImports(file, state, state.TopLevel.Module);

            // Register the symbols from the main module in the root scope
            state.RegisterSymbols(state.TopLevel.Module, rootscope);

            // Check that all parameters in the top-level network are explicitly typed
            var untypedparam = state.TopLevel.SourceNetwork.Parameters.FirstOrDefault(x => x.ExplictType == null);
            if (untypedparam != null)
                throw new ParserException("All parameters to the top-level network must have an explict type", untypedparam);

            // Prepare for the parameters to the top-level network
            var pmap = new AST.ParameterMap[state.TopLevel.SourceNetwork.Parameters.Length];
            var externalargumentindex = 0;
            for(var i = 0; i < pmap.Length; i++)
            {
                var p = state.TopLevel.SourceNetwork.Parameters[i];
                var realtype = state.ResolveTypeName(p.ExplictType, rootscope);
                if (realtype == null)
                    throw new ParserException($"Unable to find type: {p.ExplictType.SourceToken.Text}", p);

                if (realtype.IsValue)
                {
                    if (p.Direction == AST.ParameterDirection.Out)
                        throw new ParserException($"A value-type parameter cannot be sent as output: {p.Name}", p);
                    if (externalargumentindex >= state.TopLevel.CommandlineArguments.Length)
                        throw new ParserException($"No value provided for the parameter {p.Name} in the commandline inputs", p);
                    var argtext = state.TopLevel.CommandlineArguments[externalargumentindex++];
                    var literal = ParseAsLiteral(argtext);
                    var littype = new AST.DataType(new ParseToken(0,0,0, argtext), literal.Value.Type, -1);
                    if (!state.CanTypeCast(littype, realtype, rootscope))
                        throw new ParserException($"Parsed {argtext} to {littype} but cannot interpret as {realtype} which is required for parameter {p.Name}", p);
                    
                    pmap[i] = new AST.ParameterMap(p.SourceToken, p.Name, literal);
                    rootscope.SymbolTable.Add(p.Name.Name, literal);
                }
                else if (realtype.IsBus)
                {
                    // Create a new bus as a stand-in for the input or output
                    var newbus = new Instance.Bus(
                        new AST.BusDeclaration(
                            dummyparsetoken,
                            p.Name,
                            realtype
                                .Shape
                                .Signals
                                .Select(x => 
                                    new AST.BusSignalDeclaration(
                                        dummyparsetoken, 
                                        new AST.Identifier(new ParseToken(0, 0, 0, x.Key)), 
                                        x.Value,
                                        null,
                                        null
                                    )
                                ).ToArray()
                        )
                    );

                    newbus.Instances.AddRange(
                        newbus
                            .Source
                            .Signals
                            .Select(x => 
                                new Instance.Signal(newbus, x) 
                                { 
                                    ResolvedType = state.ResolveTypeName(x.Type, rootscope) 
                                } 
                            )
                    );

                    newbus.ResolvedSignalTypes = 
                        newbus
                        .Instances
                        .OfType<Instance.Signal>()
                        .ToDictionary(x => x.Name, x => x.ResolvedType);

                    if (p.Direction == AST.ParameterDirection.Out)
                        state.TopLevel.OutputBusses.Add(newbus);
                    else if (p.Direction == AST.ParameterDirection.In)
                        state.TopLevel.InputBusses.Add(newbus);
                    else
                        throw new ParserException($"Cannot use a top-level bus with direction {p.Direction}", p);

                    pmap[i] = new AST.ParameterMap(p.SourceToken, p.Name, AST.EnumerationExtensions.AsExpression(p.Name));
                    rootscope.SymbolTable.Add(p.Name.Name, newbus);
                    
                    // Register signals
                    using(var sc = state.StartScope(newbus))
                        foreach (var s in newbus.Instances.OfType<Instance.Signal>())
                            sc.SymbolTable.Add(s.Name, s);
                }
                else
                {
                    throw new ParserException($"Unexpected type: {realtype}", p);
                }
            }

            // Check that we have at least one output bus
            if (state.TopLevel.OutputBusses.Count == 0)
                throw new ParserException("The top-level network must have at least one output bus", state.TopLevel.SourceNetwork);
            if (state.TopLevel.CommandlineArguments.Length > externalargumentindex)
                throw new ParserException($"Too many arguments on commandline, expected {externalargumentindex} but got {state.TopLevel.CommandlineArguments.Length}", state.TopLevel.SourceNetwork);

            state.TopLevel.NetworkDeclaration = new AST.InstanceDeclaration(
                dummyparsetoken,
                new AST.InstanceName(dummyparsetoken, name, null),
                name,
                pmap
            );

            state.TopLevel.NetworkInstance = new Instance.Network(
                state.TopLevel.NetworkDeclaration,
                state.TopLevel.SourceNetwork
            );

            return state;
        }

        /// <summary>
        /// Parses a string from the commandline and provides a typed literal
        /// </summary>
        /// <param name="arg">The argument to use</param>
        /// <returns>A parsed literal</returns>
        private static AST.LiteralExpression ParseAsLiteral(string arg)
        {
            var parsetoken = new ParseToken(0, 0, 0, arg);

            if (bool.TryParse(arg, out var bres))
                return new AST.LiteralExpression(parsetoken, new AST.BooleanConstant(parsetoken, bres));
            if (int.TryParse(arg, out var ires))
                return new AST.LiteralExpression(parsetoken, new AST.IntegerConstant(parsetoken, arg));
            if (float.TryParse(arg, out var fres))
            {
                var els = arg.Split(".", 2);
                return new AST.LiteralExpression(parsetoken, new AST.FloatingConstant(parsetoken, els.First(), els.Skip(1).FirstOrDefault() ?? string.Empty));
            }

            return new AST.LiteralExpression(parsetoken, new AST.StringConstant(parsetoken, arg));
        }

        /// <summary>
        /// Recursively loads import statements and registers them in the symbol tables
        /// </summary>
        /// <param name="sourcepath"></param>
        /// <param name="state"></param>
        /// <param name="module"></param>
        private static void LoadImports(string sourcepath, Validation.ValidationState state, AST.Module module)
        {
            var scope = state.CurrentScope;
            foreach (var imp in module.Imports)
            {
                var p = GetModulePath(sourcepath, imp.ModuleName);
                if (!state.Modules.ContainsKey(p))
                {
                    var m = LoadModule(p);
                    using(var sc = state.StartScope(m))
                    {
                        // Recursively load the modules
                        LoadImports(p, state, m);

                        // Register that we have now loaded this module
                        state.Modules.Add(p, m);

                        // Register symbols in this scope
                        state.RegisterSymbols(m, sc);
                    }
                }

                // Inject imported names into the symbol tables
                if (imp.SourceNames == null)
                {
                    // Import the entire module as 
                    scope.SymbolTable.Add(imp.LocalName.Name, state.Modules[p]);
                }
                else
                {
                    // Import only the requested names, but import them without the module name
                    foreach (var n in imp.SourceNames)
                        scope.SymbolTable[n.Name] = state.FindSymbol(n, state.LocalScopes[state.Modules[p]]);
                }
            }
        }
    }
}