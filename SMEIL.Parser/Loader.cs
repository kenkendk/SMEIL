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
            var state = new Validation.ValidationState();
            var rootscope = state.CurrentScope;
            var toResolve = new Stack<AST.Module>();
            state.EntryModule = LoadModule(file);

            var networks = state.EntryModule.Entities.OfType<AST.Network>().ToList();

            if (string.IsNullOrWhiteSpace(toplevel))
            {
                if (networks.Count == 0)
                    throw new ArgumentException("The main module contains no networks?");
                if (networks.Count != 1)
                    throw new ArgumentException($"The main module contains {networks.Count} networks, please specify a network name");
                state.TopLevelNetwork = networks.First();
            }
            else
            {
                var namednetworks = networks.Where(x => string.Equals(x.Name.Name, toplevel, StringComparison.OrdinalIgnoreCase)).ToList();
                if (networks.Count == 0)
                    throw new ArgumentException($"The main module contains no networks named \"{toplevel}\"");
                if (networks.Count != 1)
                    throw new ArgumentException($"The main module contains {networks.Count} network named \"{toplevel}\"");
                state.TopLevelNetwork = namednetworks.First();
            }

            var dummyparsetoken = new ParseToken(0, 0, 0, "__commandline__");
            var name = new AST.Identifier(new ParseToken(0, 0, 0, "__main__"));

            arguments = arguments ?? new string[0];
            if (arguments.Length != state.TopLevelNetwork.Parameters.Length)
                throw new ArgumentException($"The top-level network \"{state.TopLevelNetwork.Name.Name}\" requires {state.TopLevelNetwork.Parameters.Length} parameter(s) and {arguments.Length} were given on the commandline");

            state.TopLevelNetworkDeclaration = new AST.InstanceDeclaration(
                dummyparsetoken, 
                new AST.InstanceName(dummyparsetoken, name, null),
                name,
                state.TopLevelNetwork.Parameters
                    .Zip(
                        arguments
                            .Select(ParseAsLiteral), 
                        (a, b) => new AST.ParameterMap(a.SourceToken, a.Name, b))
                    .ToArray()
            );
            state.Modules[file] = state.EntryModule;
            state.LocalScopes[state.EntryModule] = rootscope;

            // Recursively load and resolve imports
            LoadImports(file, state, state.EntryModule);

            state.RegisterSymbols(state.EntryModule, rootscope);

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