using System;
using System.Linq;
using System.Collections.Generic;
using SMEIL.Parser.AST;
using System.Text.RegularExpressions;

namespace SMEIL.Parser.Codegen.VHDL
{
    /// <summary>
    /// The allowed directions for a bus
    /// </summary>
    public enum BusDirection
    {
        /// <summary>The bus is only for input</summary>
        Input,
        /// <summary>The bus is only for output</summary>
        Output,
        /// <summary>The bus is only for both input and output</summary>
        Both
    }

    /// <summary>
    /// Class for generating VHDL code from the AST
    /// </summary>
    public class VHDLGenerator
    {
        /// <summary>
        /// The render configuration
        /// </summary>
        public readonly RenderConfig Config;

        /// <summary>
        /// The validation state used to build the output
        /// </summary>
        public readonly Validation.ValidationState ValidationState;

        /// <summary>
        /// The simulation forming the basis of the network
        /// </summary>
        //public readonly Simulation Simulation;

        /// <summary>
        /// The name of the file where a CSV trace is stored
        /// </summary>
        public string CSVTracename;
        /// <summary>
        /// The number of ticks to run the simulation for
        /// </summary>
        public int Ticks = 100;

        /// <summary>
        /// Sequence of custom VHDL files to include in the compilation
        /// </summary>
        public IEnumerable<string> CustomFiles;

        /// <summary>
        /// The assigned bus names
        /// </summary>
        public readonly Dictionary<Instance.Bus, string> BusNames;

        /// <summary>
        /// The assigned process names
        /// </summary>
        public readonly Dictionary<Instance.Process, string> ProcessNames;

        /// <summary>
        /// The assigned enum names
        /// </summary>
        public readonly Dictionary<AST.EnumField, string> EnumFieldNames;

        /// <summary>
        /// The list of all enums
        /// </summary>
        public readonly List<AST.EnumDeclaration> AllEnums;

        /// <summary>
        /// The list of all busses
        /// </summary>
        public readonly List<Instance.Bus> AllBusses;

        /// <summary>
        /// The list of all processes
        /// </summary>
        public readonly List<Instance.Process> AllProcesses;

        /// <summary>
        /// The list of all rendered processes
        /// </summary>
        public readonly Instance.Process[] AllRenderedProcesses;

        /// <summary>
        /// The name scopes for all processes
        /// </summary>
        public readonly Dictionary<Instance.IInstance, NameScopeHelper> NameScopes = new Dictionary<Instance.IInstance, NameScopeHelper>();

        /// <summary>
        /// The list of globally registered names, the key is the instance or declaration
        /// </summary>
        public readonly Dictionary<object, string> GlobalNames = new Dictionary<object, string>();

        /// <summary>
        /// A lookup table used to avoid name clashes and give name clashed items numbered name variants
        /// </summary>
        public readonly Dictionary<string, int> GlobalTokenCounter = new Dictionary<string, int>();

        /// <summary>
        /// The state passed to each render step
        /// </summary>
        public class RenderState
        {
            /// <summary>
            /// The current identationlevel
            /// </summary>
            /// <value></value>
            public int Indentation { get; set; }

            /// <summary>
            /// Increases the indentation
            /// </summary>
            public void IncreaseIdent() => Indentation += 4;
            /// <summary>
            /// Decreases the indentation
            /// </summary>
            public void DecreaseIdent() => Indentation -= 4;

            /// <summary>
            /// Returns the current indentation string
            /// </summary>
            public string Indent => new string(' ', Indentation);

            /// <summary>
            /// The list of active scopes
            /// </summary>
            public readonly List<Instance.IInstance> ActiveScopes = new List<Instance.IInstance>();

            /// <summary>
            /// Creates a disposable indenter
            /// </summary>
            /// <returns>The disposable indenter</returns>
            public IDisposable Indenter()
            {
                IncreaseIdent();
                return new Disposer(DecreaseIdent);
            }

            /// <summary>
            /// Starts a new scope
            /// </summary>
            /// <param name="item">The item to activate</param>
            /// <returns>A disposer that clears the current scope</returns>
            public IDisposable StartScope(Instance.IInstance item)
            {
                ActiveScopes.Add(item ?? throw new ArgumentNullException(nameof(item)));
                return new Disposer(() => ActiveScopes.RemoveAt(ActiveScopes.Count - 1));
            }

            /// <summary>
            /// Helper class for using the disposable patter with a callback
            /// </summary>
            private class Disposer : IDisposable
            {
                /// <summary>
                /// The method to call on disposal
                /// </summary>
                private Action m_callback;

                /// <summary>
                /// Creates a new disposer
                /// </summary>
                /// <param name="callback">The method to call on dispose</param>
                public Disposer(Action callback)
                {
                    m_callback = callback;
                }

                /// <summary>
                /// Calls the dispose method
                /// </summary>
                public void Dispose()
                {
                    var m = System.Threading.Interlocked.Exchange(ref m_callback, null);
                    m?.Invoke();
                }
            }
        }

        /// <summary>
        /// Creates a new VHDL generator
        /// </summary>
        /// <param name="validationstate">The validation state to use</param>
        /// <param name="config">The configuration to use</param>
        public VHDLGenerator(Validation.ValidationState validationstate, RenderConfig config = null)
        {
            Config = config ?? new RenderConfig();
            ValidationState = validationstate;

            // Pre-register all system-used names globally
            foreach (var n in new string[] { "RDY", "FIN", "ENB", "reentry_guard", Config.CLOCK_SIGNAL_NAME, Config.RESET_SIGNAL_NAME })
                GlobalTokenCounter.Add(n, 1);

            // List of instantiated busses
            AllBusses = validationstate
                .AllInstances
                .OfType<Instance.Bus>()
                .Concat(validationstate.TopLevel.InputBusses)
                .Concat(validationstate.TopLevel.OutputBusses)
                .Distinct()
                .ToList();

            // Figure out which instances are from the same source declaration
            var buscounters = AllBusses.GroupBy(x => x.Source).ToDictionary(x => x.Key, x => x.ToList());

            // Give the instances names, suffixed with the instance number if there are more than one
            BusNames = AllBusses
                .Select(x => new
                {
                    Key = x,
                    Name = SanitizeVHDLName(x.Name + (buscounters[x.Source].Count == 1 ? "" : "_" + (buscounters[x.Source].IndexOf(x) + 1).ToString()))
                })
                .ToDictionary(x => x.Key, x => x.Name);                

            // Extract all enums being referenced in the program
            AllEnums = validationstate
                .AllInstances
                .OfType<Instance.EnumTypeReference>()
                .Concat(validationstate.AllInstances.OfType<Instance.EnumFieldReference>().Select(x => x.ParentType))
                .Select(x => x.Source)
                .Distinct()
                .ToList();

            // Register names for all enums globally
            foreach (var e in AllEnums)
                CreateUniqueGlobalName(e, e.Name.Name);

            // Build a map for each enum field
            EnumFieldNames = AllEnums
                .SelectMany(x => 
                    x.Fields
                        .Select(y => new {
                            Key = y,
                            Name = CreateUniqueGlobalName(y, GlobalNames[x] + "_" + y.Name.Name)
                        })
                )
                .ToDictionary(x => x.Key, x => x.Name);

            // List of instantiated processes
            AllProcesses = validationstate
                .AllInstances
                .OfType<Instance.Process>()                
                .Distinct()
                .ToList();

            // Set up the name scopes
            foreach (var p in AllProcesses)
                NameScopes[p] = new NameScopeHelper();

            var allNetworks = validationstate
                .AllInstances
                .OfType<Instance.Network>()
                .Distinct();

            foreach (var n in allNetworks)
                NameScopes[n] = new NameScopeHelper();

            // Repeat for functions
            foreach (var p in validationstate
                .AllInstances
                .OfType<Instance.FunctionInvocation>()
                .Distinct())
                NameScopes[p] = new NameScopeHelper();

            // Group processes by their names so we can differentiate
            var proccounters = AllProcesses
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Name) || x.Name == "_" ? x.ProcessDefinition.Name.Name : x.Name)
                .ToDictionary(
                    x => x.Key, 
                    x => x.ToList()
                );

            // Give the instances names, suffixed with the instance number if there are more than one
            ProcessNames = proccounters
                .SelectMany(x => 
                    x.Value.Select(
                        y => new {
                            Key = y,
                            Name = SanitizeVHDLName(x.Key + (proccounters[x.Key].Count == 1 ? "" : "_" + (proccounters[x.Key].IndexOf(y) + 1).ToString()))
                        }
                    )
                )
                .ToDictionary(x => x.Key, x => x.Name);

            AllRenderedProcesses = validationstate
                .AllInstances
                .OfType<Instance.Process>()
                .Where(x => !Config.REMOVE_IDENTITY_PROCESSES || x.Type == Instance.ProcessType.Normal)
                .ToArray();
        }

        /// <summary>
        /// Creates the preamble for a file
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public string GenerateVHDLFilePreamble(RenderState state)
        {
            return RenderLines(
                state,

                "library IEEE;",
                "use IEEE.STD_LOGIC_1164.ALL;",
                "use IEEE.NUMERIC_STD.ALL;",
                "",
                // We do not currently have any need for system types
                // "--library SYSTEM_TYPES;",
                // "use work.SYSTEM_TYPES.ALL;",
                "",
                "--library CUSTOM_TYPES;",
                "use work.CUSTOM_TYPES.ALL;",
                "",
                "-- User defined packages here",
                "-- #### USER-DATA-IMPORTS-START",
                "-- #### USER-DATA-IMPORTS-END"
            );
        }

        /// <summary>
        /// Renders a list of lines with indentation
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="lines">The lines to render</param>
        /// <returns>The lines</returns>
        private static string RenderLines(RenderState state, params string[] lines)
        {
            return RenderLines(state, lines.AsEnumerable());
        }

        /// <summary>
        /// Renders a list of lines with indentation
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="lines">The lines to render</param>
        /// <returns>The lines</returns>
        private static string RenderLines(RenderState state, IEnumerable<string> lines)
        {
            return string.Join(
                Environment.NewLine, 
                lines
                    .Where(x => x != null)
                    .Select(x => (state.Indent + x).TrimEnd())
            ) + Environment.NewLine;
        }

        
        /// <summary>
        /// Returns a list of the support files with the resource and the filename
        /// </summary>
        private static IEnumerable<(System.IO.Stream, string)> SupportFiles
        {
            get
            {
                var asm = typeof(VHDLGenerator).Assembly;
                var ns = typeof(VHDLGenerator).Namespace + ".";

                return asm.GetManifestResourceNames()
                    .Where(x => x.StartsWith(ns))
                    .Select(x => (asm.GetManifestResourceStream(x), x.Substring(ns.Length)));
            }
        }
        
        /// <summary>
        /// Copies all embedded support files to the output folder
        /// </summary>
        /// <param name="targetfolder">The folder to extract to</param>
        public void CopySupportFiles(string targetfolder)
        {
            foreach(var (s, file) in SupportFiles)
                    using(var fs = System.IO.File.Create(System.IO.Path.Combine(targetfolder, file)))
                        s.CopyTo(fs);
        }

        /// <summary>
        /// Creates a Makefile for compiling the and testing the generated code with GHDL
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="filenames">The filenames assigned to the processes</param>
        /// <param name="standard">The VHDL standard to use</param>
        /// <param name="extension">The extensions to use for VHDL files</param>
        /// <returns>The generated Makefile</returns>
        public string GenerateMakefile(RenderState state, Dictionary<Instance.Process, string> filenames, string standard, string extension = "vhdl")
        {
            var ndef = ValidationState.TopLevel.NetworkInstance.NetworkDefinition;
            var name = SanitizeVHDLName(RenderIdentifier(state, ndef.Name, ValidationState.TopLevel.NetworkDeclaration.Name.Name.Name));

            var decl = RenderLines(state,
                "all: test export",
                "",
                $"testbench: {name}_tb",
                "build: export testbench",
                "",
                "# Use a temporary folder for compiled stuff",
                "WORKDIR=work",
                "",
                "# All code should be VHDL93 compliant, ",
                "# but 93c is a bit easier to work with",
                $"STD={standard}",
                "",
                "# Eveything should compile with clean IEEE,",
                "# but the test-bench and CSV util's require",
                "# std_logic_textio from Synopsys",
                "IEEE=synopsys",
                "",
                "# VCD trace file for GTKWave",
                "VCDFILE=trace.vcd",
                ""
            );

            var cust_tag = string.Empty;
            var extrafiles = SupportFiles.Select(a => a.Item2).Concat(CustomFiles ?? new string[0]);

            if (extrafiles.Any())
            {
                cust_tag = " custom_files";
                var custfiles = string.Join(" ", extrafiles.Select(x => $"$(WORKDIR)/{System.IO.Path.ChangeExtension(x, null)}.o"));
                decl += RenderLines(state,
                    $"{cust_tag.Trim()}: $(WORKDIR) {custfiles}",
                    ""
                );
            }

            decl += RenderLines(state,
                "$(WORKDIR):",
                "\tmkdir $(WORKDIR)",
                "",
                $"$(WORKDIR)/customtypes.o: customtypes.{extension} $(WORKDIR)",
                $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) customtypes.{extension}",
                ""
            );

            foreach (var file in filenames.Values)
            {
                decl += RenderLines(state,
                    $"$(WORKDIR)/{file}.o: {file}.{extension} $(WORKDIR)/customtypes.o $(WORKDIR){cust_tag}",
                    $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) {file}.{extension}",
                    ""
                );
                
            }
            if (!string.IsNullOrWhiteSpace(cust_tag))
            {
                foreach (var file in extrafiles)
                {
                    decl += RenderLines(state,
                        $"$(WORKDIR)/{System.IO.Path.ChangeExtension(file, null)}.o: {file} $(WORKDIR)/customtypes.o $(WORKDIR)",
                        $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) {file}",
                        ""
                    );
                }
            }

            decl += RenderLines(state,
                $"$(WORKDIR)/toplevel.o: toplevel.{extension} $(WORKDIR)/customtypes.o {string.Join(" ", filenames.Values.Select(x => $"$(WORKDIR)/{x}.o"))}{cust_tag}",
                $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) toplevel.{extension}",
                "",
                $"$(WORKDIR)/testbench.o: testbench.{extension} $(WORKDIR)/toplevel.o",
                $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) testbench.{extension}",
                "",
                $"{name}_tb: $(WORKDIR)/testbench.o",
                $"\tghdl -e --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) {name}_tb",
                "",
                $"export: $(WORKDIR)/toplevel.o",
                $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) export.{extension}",
                "",
                $"test: {name}_tb",
                $"\tcp \"{CSVTracename}\" .",
                $"\tghdl -r --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) {name}_tb --vcd=$(VCDFILE)",
                "",
                "clean:",
                $"\trm -rf $(WORKDIR) *.o {name}_tb",
                "",
                "",
                $".PHONY: all clean test export build{cust_tag}",
                ""
            );

            return decl;
        }

        public string GenerateCustomTypes(RenderState state)
        {
            var decl = RenderLines(state,
                "library IEEE;",
                "use IEEE.STD_LOGIC_1164.ALL;",
                "use IEEE.NUMERIC_STD.ALL;",
                "",                "",
                "-- User defined packages here",
                "-- #### USER-DATA-IMPORTS-START",
                "-- #### USER-DATA-IMPORTS-END",
                "",
                "package CUSTOM_TYPES is",
                "",
                "-- User defined types here",
                "-- #### USER-DATA-CORETYPES-START",
                "-- #### USER-DATA-CORETYPES-END",
                ""
            );

            // Build a lookup table for each function definition
            // so we can name the global VHDL functions with their 
            // code location
            var funcDecls = ValidationState.Modules.Values
                .SelectMany(x => x.All())
                .Where(x => x.Current is AST.FunctionDefinition)
                .ToDictionary(
                    x => x.Current as AST.FunctionDefinition,
                    x => x.Parents.ToArray()
                );

            IEnumerable<IGrouping<string, Instance.FunctionInvocation>> funcs;

            using(state.Indenter())
            using(state.StartScope(ValidationState.TopLevel.ModuleInstance))
            {
                funcs = ValidationState.AllInstances
                    .OfType<Instance.FunctionInvocation>()
                    .Where(x => funcDecls[x.Source].Last() is AST.Module)
                    .GroupBy(x => { 
                        using(state.StartScope(x))
                            return RenderScopeName(funcDecls[x.Source], x.Name) + "(" + FunctionSignature(state, x) + ")";
                    })
                    .ToArray();

                var consts = ValidationState.AllInstances
                    .OfType<Instance.ConstantReference>()
                    // Distinct on the declaration
                    .GroupBy(x => x.Source);

                if (consts.Any())
                {
                    // Build a lookup table for each constant declaration
                    var constDecls = ValidationState.Modules.Values
                        .SelectMany(x => x.All())
                        .Where(x => x.Current is AST.ConstantDeclaration)
                        .ToDictionary(
                            x => x.Current as AST.ConstantDeclaration, 
                            x => x.Parents.ToArray()
                        );

                    var constScopes = ValidationState.AllInstances
                        .OfType<Instance.IDeclarationContainer>()
                        .SelectMany(x => 
                            x.Declarations
                                .OfType<AST.ConstantDeclaration>()
                                .Select(y => new {
                                    Constant = y,
                                    Parent = x
                                })
                        )
                        .GroupBy(x => x.Constant)
                        .ToDictionary(x => x.Key, x => x.First());


                    decl += RenderLines(state,
                        "-- Constant definitions"
                    );

                    // Build all names before rendering, to allow constants to reference other
                    // constants in the initializer
                    var constNames = consts.ToDictionary(
                        x => x.Key,
                        x => CreateUniqueGlobalName(x.Key, SanitizeVHDLName(RenderScopeName(constDecls[x.Key], x.Key.Name.Name)))
                    );

                    decl += RenderLines(state,
                        consts.Select(x => {
                            var scope = constScopes[x.Key];
                            var name = constNames[x.Key];
                            
                            using(state.StartScope(scope.Parent))
                                return $"constant {name}: {RenderNativeType(x.First().ResolvedType)} := {RenderExpression(state, x.Key.Expression)};";
                        })
                    );
                    decl += RenderLines(state,
                        ""
                    );
                }

                // All enums are registered as global types in the VHDL
                if (AllEnums.Any())
                {
                    decl += RenderLines(state,
                        "-- Enum definitions",
                        ""
                    );

                    decl += RenderLines(state,
                        AllEnums.SelectMany(c =>
                            new string[] {
                                $"type {GlobalNames[c]} is ({string.Join(", ", c.Fields.Select(x => EnumFieldNames[x]))});",
                                $"pure function str(b: {GlobalNames[c]}) return string;",
                                $"pure function TO_INTEGER(b: {GlobalNames[c]}) return integer;",
                                $"pure function TO_{GlobalNames[c]}(b: integer) return {GlobalNames[c]};",
                                ""
                            }
                        )
                    );

                }

                if (funcs.Any())
                {
                    decl += RenderLines(state, "-- Function definitions");

                    foreach (var f in funcs)
                    {
                        var fi = f.First();
                        var parents = funcDecls[fi.Source];
                        var name = CreateUniqueGlobalName(fi, SanitizeVHDLName(RenderScopeName(parents, fi.Name)));
                        
                        // Register all calls to the functions with matching arguments to the same name
                        foreach(var fn in f.Skip(1))
                            GlobalNames.Add(fn, name);

                        // Render one of the functions
                        using (state.StartScope(fi))
                        {
                            decl += RenderLines(state, $"procedure {name} (");
                            using (state.Indenter())
                                decl += RenderFunctionArguments(state, fi) + ");";
                            decl += RenderLines(state, "");
                        }                    
                    }

                    decl += RenderLines(state, "");
                }
            }

            decl += RenderLines(state,
                "-- User defined types here",
                "-- #### USER-DATA-TRAILTYPES-START",
                "-- #### USER-DATA-TRAILTYPES-END",
                "",
                "end CUSTOM_TYPES;",
                ""
            );

            if (AllEnums.Any() || funcs.Any())
            {
                decl += RenderLines(state,
                    "package body CUSTOM_TYPES is"
                );

                using (state.Indenter())
                {
                    foreach (var enm in AllEnums)
                    {
                        var name = GlobalNames[enm];

                        decl += RenderLines(state,
                            $"-- Support functions for {name}",
                            "",
                            $"pure function str(b: {name}) return string is",
                            "begin",
                            $"    return {name}'image(b);",
                            "end str;"
                        );

                        decl += RenderLines(state,
                            $"pure function TO_INTEGER(b: {name}) return integer is",
                            "variable s: integer;",
                            "begin"
                        );

                        using(state.Indenter())
                        {
                            decl += RenderLines(state,
                                "case b is"                                
                            );

                            using (state.Indenter())
                            {
                                decl += RenderLines(state,
                                    enm.Fields.Select(x =>
                                        $"when {EnumFieldNames[x]} => s := {x.Value};"
                                    )
                                );

                                decl += RenderLines(state,
                                    "when others => s := -1;"
                                );
                            }

                            decl += RenderLines(state,
                                "end case;",
                                "return s;"
                            );
                        }

                        decl += RenderLines(state,
                            "end TO_INTEGER;",
                            ""
                        );

                        decl += RenderLines(state,
                            $"pure function TO_{name}(b: integer) return {name} is",
                            $"variable s: {name};",
                            "begin"
                        );

                        using (state.Indenter())
                        {
                            decl += RenderLines(state,
                                "case b is"
                            );

                            using (state.Indenter())
                            {
                                decl += RenderLines(state,
                                    enm.Fields.Select(x =>
                                        $"when {x.Value} => s := {EnumFieldNames[x]};"
                                    )
                                );

                                decl += RenderLines(state,
                                    $"when others => s := {EnumFieldNames[enm.Fields.First()]};"
                                );
                            }

                            decl += RenderLines(state,
                                "end case;",
                                "return s;"
                            );
                        }

                        decl += RenderLines(state,
                            $"end TO_{name};",
                            ""
                        );                        
                    }

                    decl += RenderLines(state, "-- Function implementations");

                    foreach (var f in funcs)
                        decl += RenderFunctionImplementation(state, f.First());
                }

                decl += RenderLines(state,
                    "end package body CUSTOM_TYPES;"
                );
            }


            return decl;
        }

        /// <summary>
        /// Renders an implementation of a function
        /// </summary>
        /// <param name="state">The render state to use</param>
        /// <param name="f">The function to implement</param>
        /// <returns>The rendered function</returns>
        private string RenderFunctionImplementation(RenderState state, Instance.FunctionInvocation f)
        {
            var name = GetUniqueLocalName(state, f);

            using (state.StartScope(f))
            {
                var res = RenderLines(state, "", $"procedure {name} (");
                using (state.Indenter())
                {
                    res += RenderFunctionArguments(state, f) + $") is{Environment.NewLine}";

                    if (f.Instances.OfType<Instance.Variable>().Any())
                    {
                        res += RenderLines(state,
                            "-- Variables"
                        );

                        res += RenderLines(
                            state,
                            f.Instances
                                .OfType<Instance.Variable>()
                                .Select(x => RenderVariable(state, x))
                        );
                    }

                    res += RenderLines(state, "begin");

                    using(state.Indenter())
                        res += RenderStatements(state, f.Statements);

                }

                res += RenderLines(state, $"end {name};");
                return res;
            }
        }

        /// <summary>
        /// Returns a string representing the arguments to a function invocation
        /// </summary>
        /// <param name="state">The current render state</param>
        /// <param name="f">The function to render the argument list for</param>
        /// <returns>A string with the rendered argument list</returns>
        private string RenderFunctionArguments(RenderState state, Instance.FunctionInvocation f)
        {
            return RenderLines(state,
                string.Join(
                $";{Environment.NewLine}{state.Indent}",
                    f.MappedParameters
                        .SelectMany(x =>
                        {
                            if (x.MappedItem is Instance.Bus bus)
                            {
                                var busname = GetLocalBusName(state, bus);
                                return bus.Instances
                                    .OfType<Instance.Signal>()
                                    .Select(y => $"signal {GetUniqueLocalName(state, busname, y, x.MatchedParameter.Direction != ParameterDirection.Out)}: {(x.MatchedParameter.Direction == ParameterDirection.Out ? "out" : "in")} {RenderNativeType(y.ResolvedType)}");
                            }
                            else
                            {
                                return new string[] {
                                    $"{SanitizeVHDLName(x.LocalName)}: {(x.MatchedParameter.Direction == ParameterDirection.Out ? "out" : "in")} {RenderNativeType(x.ResolvedType)}"
                                };
                            }
                        })
                )).TrimEnd();
        }

        /// <summary>
        /// Creates a testbench for testing the generated code with GHDL
        /// </summary>
        /// <param name="state">The render state</param>
        /// <returns>The generated testbench</returns>
        public string GenerateTestbench(RenderState state)
        {
            var decl = GenerateVHDLFilePreamble(state);
            var ndef = ValidationState.TopLevel.NetworkInstance.NetworkDefinition;
            var name = SanitizeVHDLName(RenderIdentifier(state, ndef.Name, ValidationState.TopLevel.NetworkDeclaration.Name.Name.Name));

            decl += RenderLines(state,
                "use work.csv_util.all;",
                "use STD.TEXTIO.all;",
                "use IEEE.STD_LOGIC_TEXTIO.all;",            
                "",
                "--User defined packages here",
                "-- #### USER-DATA-IMPORTS-START",
                "-- #### USER-DATA-IMPORTS-END",
                "",
                $"entity {name}_tb is",
                "end;",
                "",
                $"architecture TestBench of {name}_tb is"
            );

            using(state.Indenter())
            {
                decl += RenderLines(state,
                    "",
                    "signal CLOCK : Std_logic;",
                    "signal StopClock : BOOLEAN;",
                    "signal RESET : Std_logic;",
                    "signal ENABLE : BOOLEAN;",
                    ""
                );

                foreach (var bus in AllBusses)
                {
                    decl += RenderLines(state,
                        $"-- Shared bus {BusNames[bus]} signals"
                    );

                    decl +=
                        RenderLines(state,
                            bus
                                .Instances
                                .OfType<Instance.Signal>()
                                .Select(x => $"signal {RenderSignalName(BusNames[bus], x.Name)}: {RenderNativeType(x.ResolvedType)};")
                    );

                    decl += Environment.NewLine;
                }
            }

            decl += RenderLines(state,
                "",
                "begin",                
                ""
            );

            using(state.Indenter())
            {
                decl += RenderLines(state,
                    $"uut: entity work.main_{name}",
                    "port map ("
                );

                var exportsmap = ValidationState.TopLevel.InputBusses.Union(
                    ValidationState.TopLevel.OutputBusses)
                    .ToHashSet();

                var inputsmap = ValidationState.TopLevel.InputBusses.ToHashSet();

                using(state.Indenter())
                {
                    foreach (var bus in AllBusses)
                    {
                        decl += RenderLines(state,
                            $"-- {(exportsmap.Contains(bus) ? "External" : "Internal")} bus {BusNames[bus]} signals"
                        );

                        decl += RenderLines(state,
                            bus
                                .Instances
                                .OfType<Instance.Signal>()
                                .Select(x => $"{(exportsmap.Contains(bus) ? "" : "tb_")}{RenderSignalName(BusNames[bus], x.Name)} => {RenderSignalName(BusNames[bus], x.Name)},")
                        );

                        decl += Environment.NewLine;
                    }

                    decl += RenderLines(state,
                        " -- Control signals",
                        $"{Config.ENABLE_SIGNAL_NAME} => ENABLE,",
                        $"{Config.RESET_SIGNAL_NAME} => RESET,",
                        $"{Config.CLOCK_SIGNAL_NAME} => CLOCK"
                    );

                }

                decl += RenderLines(state,
                    ");",
                    "",
                    "Clk: process",
                    "begin",
                    "    while not StopClock loop",
                    "        CLOCK <= '1';",
                    "        wait for 5 NS;",
                    "        CLOCK <= '0';",
                    "        wait for 5 NS;",
                    "    end loop;",
                    "    wait;",
                    "end process;",
                    "",
                    "TraceFileTester: process",
                    ""
                );

                using(state.Indenter())
                {
                    decl += RenderLines(state,
                        "file F: TEXT;",
                        "variable L: LINE;",
                        "variable Status: FILE_OPEN_STATUS;",
                        "constant filename : string := \"./trace.csv\";",
                        "variable clockcycle : integer:= 0;",
                        "variable tmp : CSV_LINE_T;",
                        "variable readOK : boolean;",
                        "variable fieldno : integer:= 0;",
                        "variable failures : integer:= 0;",
                        "variable newfailures: integer:= 0;",
                        "variable first_failure_tick : integer:= -1;",
                        "variable first_round : boolean:= true;"
                    );
                }

                decl += RenderLines(state,
                    "",
                    "begin",
                    ""
                );

                using (state.Indenter())
                {
                    decl += RenderLines(state,
                        "-- #### USER-DATA-CONDITONING-START",
                        "-- #### USER-DATA-CONDITONING-END",
                        "",
                        "FILE_OPEN(Status, F, filename, READ_MODE);",
                        "if Status /= OPEN_OK then",
                        "    report \"Failed to open CSV trace file\" severity Failure;",
                        "else"
                    );

                    using (state.Indenter())
                    {
                        decl += RenderLines(state,
                            "-- Verify the headers",
                            "READLINE(F, L);",
                            "",
                            "fieldno := 0;"
                        );

                        decl += RenderLines(state,
                            AllBusses
                                .SelectMany(x => x.Instances.OfType<Instance.Signal>())
                                .SelectMany(x => new [] {
                                    "read_csv_field(L, tmp);",
                                    $"assert are_strings_equal(tmp, \"{x.ParentBus.Name}.{x.Name}\") report \"Field #\" & integer'image(fieldno) & \" is not correctly named: \" & truncate(tmp) & \", expected {x.ParentBus.Name}.{x.Name}\" severity Failure;",
                                    "fieldno := fieldno + 1;"
                                })
                        );

                        decl += RenderLines(state,
                            "",
                            "-- Reset the system before testing",
                            $"RESET <= {(Config.RESET_ACTIVE_LOW ? "'0'" : "'1'")};",
                            "ENABLE <= FALSE;",
                            "wait for 5 NS;",
                            $"RESET <= {(Config.RESET_ACTIVE_LOW ? "'1'" : "'0'")};",
                            "ENABLE <= TRUE;",
                            "",
                            "-- Read a line each clock",
                            "while not ENDFILE(F) loop"
                        );

                        using (state.Indenter())
                        {
                            decl += RenderLines(state,
                                "READLINE(F, L);",
                                "",
                                "fieldno := 0;",
                                "newfailures := 0;",
                                "",
                                "-- Write all driver signals out on the clock edge,",
                                "-- except on the first round, where we make sure the reset",
                                "-- values are propagated _before_ the initial clock edge",
                                "if not first_round then",
                                "    wait until rising_edge(CLOCK);",
                                "end if;",
                                "" 
                            );

                            decl += RenderLines(state,
                                    AllBusses
                                    .Where(x => inputsmap.Contains(x))
                                    .SelectMany(x => x.Instances.OfType<Instance.Signal>())
                                    .SelectMany(x => new string[] {
                                        "read_csv_field(L, tmp);",
                                        "if are_strings_equal(tmp, \"U\") then",
                                        $"    {RenderSignalName(BusNames[x.ParentBus], x.Name)} <= {(x.ResolvedType.IsBoolean ? "'U'" : "(others => 'U')")};",
                                        "else",
                                        $"    {RenderSignalName(BusNames[x.ParentBus], x.Name)} <= {(x.ResolvedType.IsBoolean ? "to_std_logic(truncate(tmp))" : FromStdLogicVectorConvertFunction(x.ResolvedType, "to_std_logic_vector(truncate(tmp))"))};",
                                        "end if;",
                                        "fieldno := fieldno + 1;"
                                    })
                            );

                            decl += RenderLines(state,
                                "",
                                "-- First round is special",
                                "if first_round then",
                                "    wait until rising_edge(CLOCK);",
                                "    first_round := false;",
                                "end if;",
                                "",
                                "-- Wait until the signals are settled before veriying the results",
                                "wait until falling_edge(CLOCK);",
                                "",
                                "-- Compare each signal with the value in the CSV file"
                            );

                            decl += RenderLines(state,
                                AllBusses
                                    .Where(x => !inputsmap.Contains(x))
                                    .SelectMany(x => x.Instances.OfType<Instance.Signal>())
                                    .SelectMany(x => new string[] {
                                        $"read_csv_field(L, tmp);",
                                        $"if not are_strings_equal(tmp, \"U\") then",
                                        $"    if not are_strings_equal(str({RenderSignalName(BusNames[x.ParentBus], x.Name)}), tmp) then",
                                        $"        newfailures := newfailures + 1;",
                                        $"        report \"Value for {RenderSignalName(BusNames[x.ParentBus], x.Name)} in cycle \" & integer'image(clockcycle) & \" was: \" & str({RenderSignalName(BusNames[x.ParentBus], x.Name)}) & \" but should have been: \" & truncate(tmp) severity Error;",
                                        $"    end if;",
                                        $"end if;",
                                        $"fieldno := fieldno + 1;"
                                    })
                            );

                            decl += RenderLines(state,
                                "failures := failures + newfailures;",
                                "if newfailures = 0 then",
                                "    first_failure_tick := -1;",
                                "elsif first_failure_tick = -1 then",
                                "    first_failure_tick := clockcycle;",
                                "else",
                                "    if clockcycle - first_failure_tick >= 5 then",
                                "        report \"Stopping simulation due to five consecutive failed cycles\" severity error;",
                                "        StopClock <= true;",
                                "    elsif failures > 20 then",
                                "        report \"Stopping simulation after 20 failures\" severity error;",
                                "        StopClock <= true;",
                                "    end if;",
                                "end if;",
                                "",
                                "clockcycle := clockcycle + 1;"
                            );
                        }

                    }

                    decl += RenderLines(state,
                        "end loop;",
                        "",
                        "FILE_CLOSE(F);"
                    );
                }

                decl += RenderLines(state,
                    "end if;",
                    "",
                    "if failures = 0 then",
                    "    report \"completed successfully after \" & integer'image(clockcycle) & \" clockcycles\";",
                    "else",
                    "    report \"completed with \" & integer'image(failures) & \" error(s) after \" & integer'image(clockcycle) & \" clockcycle(s)\";",
                    "end if;",
                    "StopClock <= true;",
                    "",
                    "wait;"
                );
            }

            decl += RenderLines(state,
                "end process;",
                "end architecture TestBench;"
            );

            return decl;
        }


        /// <summary>
        /// Returns a conversion for the input string
        /// Assumes the lengths match
        /// </summary>
        /// <param name="resolvedType">The type to convert to</param>
        /// <param name="input">The string to convert</param>
        /// <returns>A conversion string</returns>
        private string FromStdLogicVectorConvertFunction(DataType resolvedType, string input)
        {
            if (resolvedType.Type == ILType.SignedInteger && resolvedType.BitWidth != -1)
                return $"signed({input})";
            else if (resolvedType.Type == ILType.UnsignedInteger && resolvedType.BitWidth != -1)
                return $"unsigned({input})";
            else if (resolvedType.Type == ILType.SignedInteger && resolvedType.BitWidth == -1)
                return $"to_integer(signed({input}))";

            throw new Exception($"Unable to convert a std_logic_vector type to {resolvedType}");
        }

        /// <summary>
        /// Creates a Xilinx Vivado .xpf for testing and sythesizing the generated VHDL output with Xilinx Vivado
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="filenames">The filenames assigned to the processes</param>
        /// <param name="extension">The file extensions to use</param>
        /// <returns>The generated xpf file</returns>
        public string GenerateXpf(RenderState state, Dictionary<Instance.Process, string> filenames, string extension = "vhdl")
        {
            var ndef = ValidationState.TopLevel.NetworkInstance.NetworkDefinition;
            var name = SanitizeVHDLName(RenderIdentifier(state, ndef.Name, ValidationState.TopLevel.NetworkDeclaration.Name.Name.Name));
            
            var decl = RenderLines(state, 
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
                $"<Project Version=\"7\" Minor=\"35\" Path=\"./{name}.xpr\">",
                "  <DefaultLaunch Dir=\"$PRUNDIR\"/>",
                "  <Configuration>",
                "    <Option Name=\"Id\" Val=\"da04b7443593460ab7943c9e399803cf\"/>",
                "    <Option Name=\"Part\" Val=\"xc7z020clg484-1\"/>",
                "    <Option Name=\"CompiledLibDir\" Val=\"$PCACHEDIR/compile_simlib\"/>",
                "    <Option Name=\"CompiledLibDirXSim\" Val=\"\"/>",
                "    <Option Name=\"CompiledLibDirModelSim\" Val=\"$PCACHEDIR/compile_simlib/modelsim\"/>",
                "    <Option Name=\"CompiledLibDirQuesta\" Val=\"$PCACHEDIR/compile_simlib/questa\"/>",
                "    <Option Name=\"CompiledLibDirIES\" Val=\"$PCACHEDIR/compile_simlib/ies\"/>",
                "    <Option Name=\"CompiledLibDirXcelium\" Val=\"$PCACHEDIR/compile_simlib/xcelium\"/>",
                "    <Option Name=\"CompiledLibDirVCS\" Val=\"$PCACHEDIR/compile_simlib/vcs\"/>",
                "    <Option Name=\"CompiledLibDirRiviera\" Val=\"$PCACHEDIR/compile_simlib/riviera\"/>",
                "    <Option Name=\"CompiledLibDirActivehdl\" Val=\"$PCACHEDIR/compile_simlib/activehdl\"/>",
                "    <Option Name=\"TargetLanguage\" Val=\"VHDL\"/>",
                "    <Option Name=\"SimulatorLanguage\" Val=\"VHDL\"/>",
                "    <Option Name=\"BoardPart\" Val=\"em.avnet.com:zed:part0:1.3\"/>",
                "    <Option Name=\"ActiveSimSet\" Val=\"sim_1\"/>",
                "    <Option Name=\"DefaultLib\" Val=\"xil_defaultlib\"/>",
                "    <Option Name=\"ProjectType\" Val=\"Default\"/>",
                "    <Option Name=\"IPOutputRepo\" Val=\"$PCACHEDIR/ip\"/>",
                "    <Option Name=\"IPCachePermission\" Val=\"read\"/>",
                "    <Option Name=\"IPCachePermission\" Val=\"write\"/>",
                "    <Option Name=\"EnableCoreContainer\" Val=\"FALSE\"/>",
                "    <Option Name=\"CreateRefXciForCoreContainers\" Val=\"FALSE\"/>",
                "    <Option Name=\"IPUserFilesDir\" Val=\"$PIPUSERFILESDIR\"/>",
                "    <Option Name=\"IPStaticSourceDir\" Val=\"$PIPUSERFILESDIR/ipstatic\"/>",
                "    <Option Name=\"EnableBDX\" Val=\"FALSE\"/>",
                "    <Option Name=\"DSAVendor\" Val=\"xilinx\"/>",
                "    <Option Name=\"DSABoardId\" Val=\"zed\"/>",
                "    <Option Name=\"DSANumComputeUnits\" Val=\"16\"/>",
                "    <Option Name=\"WTXSimLaunchSim\" Val=\"84\"/>",
                "    <Option Name=\"WTModelSimLaunchSim\" Val=\"0\"/>",
                "    <Option Name=\"WTQuestaLaunchSim\" Val=\"0\"/>",
                "    <Option Name=\"WTIesLaunchSim\" Val=\"0\"/>",
                "    <Option Name=\"WTVcsLaunchSim\" Val=\"0\"/>",
                "    <Option Name=\"WTRivieraLaunchSim\" Val=\"0\"/>",
                "    <Option Name=\"WTActivehdlLaunchSim\" Val=\"0\"/>",
                "    <Option Name=\"WTXSimExportSim\" Val=\"0\"/>",
                "    <Option Name=\"WTModelSimExportSim\" Val=\"0\"/>",
                "    <Option Name=\"WTQuestaExportSim\" Val=\"0\"/>",
                "    <Option Name=\"WTIesExportSim\" Val=\"0\"/>",
                "    <Option Name=\"WTVcsExportSim\" Val=\"0\"/>",
                "    <Option Name=\"WTRivieraExportSim\" Val=\"0\"/>",
                "    <Option Name=\"WTActivehdlExportSim\" Val=\"0\"/>",
                "    <Option Name=\"GenerateIPUpgradeLog\" Val=\"TRUE\"/>",
                "    <Option Name=\"XSimRadix\" Val=\"hex\"/>",
                "    <Option Name=\"XSimTimeUnit\" Val=\"ns\"/>",
                "    <Option Name=\"XSimArrayDisplayLimit\" Val=\"1024\"/>",
                "    <Option Name=\"XSimTraceLimit\" Val=\"65536\"/>",
                "    <Option Name=\"SimTypes\" Val=\"rtl\"/>",
                "  </Configuration>",
                "  <FileSets Version=\"1\" Minor=\"31\">",
                "    <FileSet Name=\"sources_1\" Type=\"DesignSrcs\" RelSrcDir=\"$PSRCDIR/sources_1\">",
                "      <Filter Type=\"Srcs\"/>",
                $"      <File Path=\"$PPRDIR/system_types.{extension}\">",
                "        <FileInfo>",
                "          <Attr Name=\"Library\" Val=\"xil_defaultlib\"/>",
                "          <Attr Name=\"IsGlobalInclude\" Val=\"1\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"synthesis\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"simulation\"/>",
                "        </FileInfo>",
                "      </File>",
                $"      <File Path=\"$PPRDIR/Types_{name} #>\">",
                "        <FileInfo>",
                "          <Attr Name=\"Library\" Val=\"xil_defaultlib\"/>",
                "          <Attr Name=\"IsGlobalInclude\" Val=\"1\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"synthesis\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"simulation\"/>",
                "        </FileInfo>",
                "      </File>"
            );

            foreach (var file in filenames.Values)
            {
                decl += RenderLines(state,
                    $"      <File Path=\"$PPRDIR/{file}.{extension}\">",
                    "        <FileInfo>",
                    "          <Attr Name=\"Library\" Val=\"xil_defaultlib\"/>",
                    "          <Attr Name=\"UsedIn\" Val=\"synthesis\"/>",
                    "          <Attr Name=\"UsedIn\" Val=\"simulation\"/>",
                    "        </FileInfo>",
                    "      </File>"
                );
            }

            decl += RenderLines(state,
                $"      <File Path=\"$PPRDIR/{name}.{extension}\">",
                "        <FileInfo>",
                "          <Attr Name=\"Library\" Val=\"xil_defaultlib\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"synthesis\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"simulation\"/>",
                "        </FileInfo>",
                "      </File>",
                "      <Config>",
                "        <Option Name=\"DesignMode\" Val=\"RTL\"/>",
                $"        <Option Name=\"TopModule\" Val=\"{name}\"/>",
                "        <Option Name=\"TopAutoSet\" Val=\"TRUE\"/>",
                "      </Config>",
                "    </FileSet>",
                "    <FileSet Name=\"constrs_1\" Type=\"Constrs\" RelSrcDir=\"$PSRCDIR/constrs_1\">",
                "      <Filter Type=\"Constrs\"/>",
                "      <Config>",
                "        <Option Name=\"ConstrsType\" Val=\"XDC\"/>",
                "      </Config>",
                "    </FileSet>",
                "    <FileSet Name=\"sim_1\" Type=\"SimulationSrcs\" RelSrcDir=\"$PSRCDIR/sim_1\">",
                "      <Filter Type=\"Srcs\"/>",
                "      <File Path=\"$PPRDIR/csv_util.{extension}\">",
                "        <FileInfo>",
                "          <Attr Name=\"Library\" Val=\"xil_defaultlib\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"synthesis\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"simulation\"/>",
                "        </FileInfo>",
                "      </File>",
                $"      <File Path=\"$PPRDIR/TestBench_{name}.{extension}\">",
                "        <FileInfo>",
                "          <Attr Name=\"Library\" Val=\"xil_defaultlib\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"synthesis\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"simulation\"/>",
                "        </FileInfo>",
                "      </File>",
                $"      <File Path=\"$PPRDIR/{CSVTracename}\">",
                "          <Attr Name=\"UsedIn\" Val=\"simulation\"/>",
                "      </File>",
                "      <Config>",
                "        <Option Name=\"DesignMode\" Val=\"RTL\"/>",
                $"        <Option Name=\"TopModule\" Val=\"{name}_tb\"/>",
                "        <Option Name=\"TopLib\" Val=\"xil_defaultlib\"/>",
                "        <Option Name=\"TransportPathDelay\" Val=\"0\"/>",
                "        <Option Name=\"TransportIntDelay\" Val=\"0\"/>",
                "        <Option Name=\"SrcSet\" Val=\"sources_1\"/>",
                $"        <Option Name=\"xsim.simulate.runtime\" Val=\"{((Ticks + 2) * 10) + "ns"}\"/>",
                "      </Config>",
                "    </FileSet>",
                "  </FileSets>",
                "  <Simulators>",
                "    <Simulator Name=\"XSim\">",
                "      <Option Name=\"Description\" Val=\"Vivado Simulator\"/>",
                "      <Option Name=\"CompiledLib\" Val=\"0\"/>",
                "    </Simulator>",
                "    <Simulator Name=\"ModelSim\">",
                "      <Option Name=\"Description\" Val=\"ModelSim Simulator\"/>",
                "    </Simulator>",
                "    <Simulator Name=\"Questa\">",
                "      <Option Name=\"Description\" Val=\"Questa Advanced Simulator\"/>",
                "    </Simulator>",
                "    <Simulator Name=\"Riviera\">",
                "      <Option Name=\"Description\" Val=\"Riviera-PRO Simulator\"/>",
                "    </Simulator>",
                "    <Simulator Name=\"ActiveHDL\">",
                "      <Option Name=\"Description\" Val=\"Active-HDL Simulator\"/>",
                "    </Simulator>",
                "  </Simulators>",
                "  <Runs Version=\"1\" Minor=\"10\">",
                "    <Run Id=\"synth_1\" Type=\"Ft3:Synth\" SrcSet=\"sources_1\" Part=\"xc7z020clg484-1\" ConstrsSet=\"constrs_1\" Description=\"Vivado Synthesis Defaults\" WriteIncrSynthDcp=\"false\" State=\"current\" IncludeInArchive=\"true\">",
                "      <Strategy Version=\"1\" Minor=\"2\">",
                "        <StratHandle Name=\"Vivado Synthesis Defaults\" Flow=\"Vivado Synthesis 2017\"/>",
                "        <Step Id=\"synth_design\"/>",
                "      </Strategy>",
                "      <ReportStrategy Name=\"Vivado Synthesis Default Reports\" Flow=\"Vivado Synthesis 2017\"/>",
                "      <Report Name=\"ROUTE_DESIGN.REPORT_METHODOLOGY\" Enabled=\"1\"/>",
                "    </Run>",
                "    <Run Id=\"impl_1\" Type=\"Ft2:EntireDesign\" Part=\"xc7z020clg484-1\" ConstrsSet=\"constrs_1\" Description=\"Default settings for Implementation.\" WriteIncrSynthDcp=\"false\" State=\"current\" SynthRun=\"synth_1\" IncludeInArchive=\"true\">",
                "      <Strategy Version=\"1\" Minor=\"2\">",
                "        <StratHandle Name=\"Vivado Implementation Defaults\" Flow=\"Vivado Implementation 2017\"/>",
                "        <Step Id=\"init_design\"/>",
                "        <Step Id=\"opt_design\"/>",
                "        <Step Id=\"power_opt_design\"/>",
                "        <Step Id=\"place_design\"/>",
                "        <Step Id=\"post_place_power_opt_design\"/>",
                "        <Step Id=\"phys_opt_design\"/>",
                "        <Step Id=\"route_design\"/>",
                "        <Step Id=\"post_route_phys_opt_design\"/>",
                "        <Step Id=\"write_bitstream\"/>",
                "      </Strategy>",
                "      <ReportStrategy Name=\"Vivado Implementation Default Reports\" Flow=\"Vivado Implementation 2017\"/>",
                "      <Report Name=\"ROUTE_DESIGN.REPORT_METHODOLOGY\" Enabled=\"1\"/>",
                "    </Run>",
                "  </Runs>",
                "  <Board>",
                "    <Jumpers/>",
                "  </Board>",
                "</Project>"
            );

            return decl;
        }

        /// <summary>
        /// Creates an XML file for use with Altera/Intel OpenCL
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="filenames">The filenames assigned to the processes</param>
        /// <param name="extension">The file extensions to use</param>
        /// <returns>The generated xml file</returns>
        public string GenerateAocl(RenderState state, Dictionary<Instance.Process, string> filenames, string extension = "vhdl")
        {
            var ndef = ValidationState.TopLevel.NetworkInstance.NetworkDefinition;
            var name = SanitizeVHDLName(RenderIdentifier(state, ndef.Name, ValidationState.TopLevel.NetworkDeclaration.Name.Name.Name));


            // TODO: These options must be provided by the user
            var decl = RenderLines(state, 
                "<RTL_SPEC>",
                "  <!-- 'name' is how this function will be called from an OpenCL kernel.",
                "       'module' is the top-level HDL module name that implements the function. -->",
                $"  <FUNCTION name=\"sme_{name.ToLowerInvariant()}\" module=\"{name}\">",
                "    <ATTRIBUTES>",
                "      <!-- Setting IS_STALL_FREE=\"yes\" means the function neither generates stalls internally nor can it ",
                "           properly handle incoming stalls (because it simply ignores its stall/valid inputs). If set",
                "           to \"no\", the function must properly handle stall/valid signals. ",
                "           IS_STALL_FREE=\"yes\" requires IS_FIXED_LATENCY=\"yes\". -->",
                "      <IS_STALL_FREE value=\"yes\"/>",
                "",
                "      <!-- If the function always takes known number of clock cycles (specified by EXPECTED_LATENCY)",
                "           to compute its output, set IS_FIXED_LATENCY to \"yes\".",
                "           Note that IS_FIXED_LATENCY could be \"yes\" while IS_STALL_FREE=\"no\". Such a function would",
                "           produce its output in fixed number of cycles but could still deal with stall signals ",
                "           properly.  -->",
                "      <IS_FIXED_LATENCY value=\"yes\"/>",
                "",
                "      <!-- Expected latency of this function. If IS_FIXED_LATENCY=\"yes\", this is the number of ",
                "           pipeline stages inside the function. In this case, EXPECTED_LATENCY must be set exactly",
                "           to the latency of the function, otherwise incorrect hardware will result.",
                "           For variable latency functions, pipeline around this function will be balanced to this ",
                "           value. Setting EXPECTED_LATENCY to a different value will still produce correct results",
                "           but may affect number of stalls inside the pipeline. -->",
                "      <EXPECTED_LATENCY value=\"1\"/>",
                "",
                "      <!-- Number of multiple inputs that can be processed simultaneously by this function.",
                "           If IS_STALL_FREE=\"no\" and IS_FIXED_LATENCY=\"no\", the CAPACITY value must be specified.",
                "           Otherwise, it is not required.",
                "           If CAPACITY is strictly less than EXPECTED_LATENCY, the compiler will automatically ",
                "           insert capacity-balancing FIFOs after this function when required. -->",
                "      <CAPACITY value=\"1\"/>",
                "",
                "      <!-- Set to \"yes\" to indicate that this function has side-effects. Calls to functions",
                "           with side-effects will not be optimized away and only valid data will be fed",
                "           to such functions.",
                "           Functions that have internal state or talk to external memories are examples of functions",
                "           with side-effects. -->",
                "      <HAS_SIDE_EFFECTS value=\"no\"/>",
                "",
                "      <!-- Set to \"yes\" to allow multiple instances of this function to be merged by the compiler.",
                "           This property should be set to \"yes\". ",
                "           Note that marking function with HAS_SIDE_EFFECTS does not prevent merging. -->",
                "      <ALLOW_MERGING value=\"yes\"/>",
                "    </ATTRIBUTES>",
                "    <INTERFACE>",
                $"      <AVALON port=\"{Config.CLOCK_SIGNAL_NAME}\" type=\"clock\"/>",
                $"      <AVALON port=\"{Config.RESET_SIGNAL_NAME}\" type=\"resetn\"/>"
            );

            // TODO: we need to know which busses implement the protocol
            decl += RenderLines(state,
                "      <AVALON port=\"ILiteAvalonInput_InputValid\" type=\"ivalid\"/>",
                "      <AVALON port=\"ILiteAvalonInput_InputReady\" type=\"iready\"/>",
                "      <AVALON port=\"ILiteAvalonOutput_OutputValid\" type=\"ovalid\"/>",
                "      <AVALON port=\"ILiteAvalonOutput_OutputReady\" type=\"oready\"/>",
                "      <INPUT  port=\"ILiteAvalonInput_Value\" width=\"32\"/>",
                "      <OUTPUT port=\"ILiteAvalonOutput_Value\" width=\"32\"/>"
            );

            decl += RenderLines(state,
                "    </INTERFACE>",
                "    <C_MODEL>",
                "      <FILE name=\"c_model.cl\" />",
                "    </C_MODEL>",
                "    <REQUIREMENTS>"
            );

            foreach (var file in filenames.Values)
            {
                decl += RenderLines(state,
                    $"      <FILE name=\"./{file}.{extension}\" />"
                );
            }

            decl += RenderLines(state,
                "    </REQUIREMENTS>",
                "  </FUNCTION>",
                "</RTL_SPEC>",
                ""
            );

            return decl;
        }


        /// <summary>
        /// Returns a VHDL representation of a network
        /// </summary>
        /// <param name="state">The render stater</param>
        /// <returns>The document representing the rendered network</returns>
        public string GenerateExportModule(RenderState state)
        {
            var toplevelbusses = ValidationState.TopLevel.InputBusses
                .Concat(ValidationState.TopLevel.OutputBusses)
                .Distinct()
                .ToArray();

            // We only want std_logic(_vector) external signals
            // because the tools sometimes trip on other types
            var typeconvertedbusses =
                toplevelbusses
                .Where(x => x.ResolvedSignalTypes.Values.Any(y => y.IsNumeric))
                .ToArray();

            var exportnames = new Dictionary<Instance.Bus, string>(BusNames);

            // We could avoid this module by using either init_signal_spy() or VHDL alias (in VHDL 2008)
            // But... GHDL does not currently support either, so this elaborate workaround is all we can do
            using (state.StartScope(ValidationState.TopLevel.NetworkInstance))
            {
                var ndef = ValidationState.TopLevel.NetworkInstance.NetworkDefinition;
                var mainname = SanitizeVHDLName(RenderIdentifier(state, "main_", ndef.Name, ValidationState.TopLevel.NetworkDeclaration.Name.Name.Name));
                var name = SanitizeVHDLName(RenderIdentifier(state, ndef.Name, ValidationState.TopLevel.NetworkDeclaration.Name.Name.Name));
                var decl = GenerateVHDLFilePreamble(state);
                decl += RenderLines(state, $"entity {name} is");
                using (state.Indenter())
                {
                    decl += RenderLines(state, $"port(");
                    using (state.Indenter())
                    {
                        foreach (var b in toplevelbusses)
                            decl += RenderTopLevelBus(state, b, true);

                        decl += RenderLines(state,
                            "-- User defined signals here",
                            "-- #### USER-DATA-ENTITYSIGNALS-START",
                            "-- #### USER-DATA-ENTITYSIGNALS-END",
                            ""
                        );

                        if (!Config.REMOVE_ENABLE_FLAGS)
                        {
                            decl += RenderLines(state,
                                "-- Enable signal",
                                $"{Config.ENABLE_SIGNAL_NAME}: in STD_LOGIC;",
                                ""
                            );
                        }

                        decl += RenderLines(state,
                            "--Reset signal",
                            $"{Config.RESET_SIGNAL_NAME} : in STD_LOGIC;",
                            "",
                            "--Finished signal",
                            "FIN : out STD_LOGIC;",
                            "",
                            "--Clock signal",
                            $"{Config.CLOCK_SIGNAL_NAME} : in STD_LOGIC"
                        );
                    }

                    decl += RenderLines(state, ");");
                }

                decl += RenderLines(state,
                    $"end {name};",
                    $"",
                    $"architecture RTL of {name} is"
                );

                using (state.Indenter())                
                {
                    decl += RenderLines(state,
                        "-- Boolean support signals",
                        "signal FIN_BOOL: boolean;",
                        "signal ENB_BOOL: boolean;",
                        "",
                        "-- User defined signals here",
                        "-- #### USER-DATA-SIGNALS-START",
                        "-- #### USER-DATA-SIGNALS-END"
                    );

                    // For signals that require type casts, prefix the internal signals with 'ext'
                    // and register a local signal to carry the un-converted value
                    foreach (var n in typeconvertedbusses)
                        decl += RenderBusSignals(state, n, exportnames[n] = "ext_" + exportnames[n]);

                    decl += RenderLines(state, "");

                    decl += RenderLines(state,
                        "-- Support functions to convert boolean signals",
                        "pure function TO_STD_LOGIC(src: BOOLEAN) return STD_LOGIC is",
                        "begin",
                        "    if src then",
                        "        return '1';",
                        "    else",
                        "        return'0';",
                        "    end if;",
                        "end function TO_STD_LOGIC;",
                        "",
                        "pure function FROM_STD_LOGIC(src: STD_LOGIC) return BOOLEAN is",
                        "begin",
                        "  return src = '1';",
                        "end function FROM_STD_LOGIC;",
                        ""
                    );
                }

                decl += RenderLines(state, "begin");

                using(state.Indenter())
                {
                    decl += RenderLines(state, 
                        "-- Write out any converted signals with the correct type",
                        "FIN <= TO_STD_LOGIC(FIN_BOOL);",
                        $"ENB_BOOL <= FROM_STD_LOGIC({(Config.REMOVE_ENABLE_FLAGS ? "'1'" : Config.ENABLE_SIGNAL_NAME)});",
                        ""
                    );

                    // Conversion methods from exported type to internal type
                    var conv_input = new Dictionary<ILType, string> {
                        { ILType.SignedInteger, "SIGNED" },
                        { ILType.UnsignedInteger, "UNSIGNED" },
                        { ILType.Bool, "TO_STD_LOGIC" },
                    };

                    // Conversion methods from internal type to exported type
                    var conv_output = new Dictionary<ILType, string> {
                        { ILType.SignedInteger, "STD_LOGIC_VECTOR" },
                        { ILType.UnsignedInteger, "STD_LOGIC_VECTOR" },
                        { ILType.Bool, "FROM_STD_LOGIC" },
                    };

                    // Forward type converted input/output signals
                    foreach (var bus in typeconvertedbusses)
                        decl += RenderLines(state,
                            bus.Instances
                            .OfType<Instance.Signal>()
                            .Select(x =>
                            {
                                if(ValidationState.TopLevel.InputBusses.Contains(x.ParentBus))
                                {
                                    // Convert inputs
                                    conv_input.TryGetValue(x.ResolvedType.Type, out var f);
                                    return $"{RenderSignalName(exportnames[bus], x.Name)} <= {(f ?? "UNKNOWN")}({RenderSignalName(exportnames[bus].Substring("ext_".Length), x.Name)});";
                                }
                                else
                                {
                                    // Convert outputs
                                    conv_output.TryGetValue(x.ResolvedType.Type, out var f);
                                    return $"{RenderSignalName(exportnames[bus].Substring("ext_".Length), x.Name)} <= {(f ?? "UNKNOWN")}({RenderSignalName(exportnames[bus], x.Name)});";
                                }
                            })
                        );


                    decl += RenderLines(state, 
                        "",
                        "-- Wire up the main instance",
                        $"{name}: entity work.{mainname}",
                        "port map ("
                    );

                    using(state.Indenter())
                    {
                        foreach (var bus in toplevelbusses)
                            decl += RenderLines(state,
                                bus.Instances.OfType<Instance.Signal>()
                                .Select(x => $"{RenderSignalName(BusNames[bus], x.Name)} => {RenderSignalName(exportnames[bus], x.Name)},")
                            );

                        decl += RenderLines(state,
                            $"{Config.ENABLE_SIGNAL_NAME} => ENB_BOOL,",
                            $"{Config.RESET_SIGNAL_NAME} => {Config.RESET_SIGNAL_NAME},",
                            "FIN => FIN_BOOL,",
                            $"{Config.CLOCK_SIGNAL_NAME} => {Config.CLOCK_SIGNAL_NAME}"
                        );
                    }

                    decl += RenderLines(state,
                        ");"
                    );

                    decl += RenderLines(state,
                        "",

                        "-- User defined processes here",
                        "-- #### USER-DATA-CODE-START",
                        "-- #### USER-DATA-CODE-END"
                    );
                }

                decl += RenderLines(state, "end RTL;");
                return decl;
            }
        }


        /// <summary>
        /// Returns a VHDL representation of a network
        /// </summary>
        /// <param name="state">The render stater</param>
        /// <returns>The document representing the rendered network</returns>
        public string GenerateMainModule(RenderState state)
        {
            using (state.StartScope(ValidationState.TopLevel.NetworkInstance))
            {
                var ndef = ValidationState.TopLevel.NetworkInstance.NetworkDefinition;
                var name = SanitizeVHDLName(RenderIdentifier(state, "main_", ndef.Name, ValidationState.TopLevel.NetworkDeclaration.Name.Name.Name));
                var decl = GenerateVHDLFilePreamble(state); 
                decl += RenderLines(state, $"entity {name} is");

                // List of all busses not visible outside the network
                var internalbusses = AllBusses
                    .Where(x =>
                        !ValidationState.TopLevel.InputBusses.Contains(x)
                        &&
                        !ValidationState.TopLevel.OutputBusses.Contains(x)
                    ).ToArray();

                // All processes that forward signals
                var forwardprocs = ValidationState
                    .AllInstances
                    .OfType<Instance.Process>()
                    .Where(x => !AllRenderedProcesses.Contains(x))
                    .ToArray();

                using (state.Indenter())
                {
                    decl += RenderLines(state, $"port(");
                    using(state.Indenter())
                    {
                        foreach (var item in ValidationState.TopLevel.InputBusses.Concat(ValidationState.TopLevel.OutputBusses).Distinct())
                            decl += RenderTopLevelBus(state, item);

                        if (internalbusses.Any())
                        {
                            decl += RenderLines(state, 
                                "-- The signals prefixed with tb_ are testbench monitor signals.",
                                "-- They are not connected in the exported design, and are thus optimized away.",
                                "-- If either init_signal_spy or VHDL aliases start working in both Vivado AND GHDL",
                                "-- we can avoid these signals, and the resulting top-level module"
                            );

                            foreach (var item in internalbusses)
                                decl += RenderTopLevelBus(state, item);
                        }

                        decl += RenderLines(state,
                            "-- User defined signals here",
                            "-- #### USER-DATA-ENTITYSIGNALS-START",
                            "-- #### USER-DATA-ENTITYSIGNALS-END",
                            "",
                            "-- Enable signal",
                            $"{Config.ENABLE_SIGNAL_NAME}: in BOOLEAN;",
                            "",
                            "--Finished signal",
                            "FIN : out BOOLEAN;",
                            "",
                            "--Reset signal",
                            $"{Config.RESET_SIGNAL_NAME} : in STD_LOGIC;",
                            "",
                            "--Clock signal",
                            $"{Config.CLOCK_SIGNAL_NAME} : in STD_LOGIC"
                        );
                    }

                    decl += RenderLines(state, $");");
                }

                decl += RenderLines(state, 
                    $"end {name};", 
                    "",
                    $"architecture RTL of {name} is"
                );

                using (state.Indenter())
                {
                    foreach (var b in internalbusses)
                        decl += RenderBusSignals(state, b, BusNames[b]);

                    decl += RenderLines(state, 
                        "",
                        "-- User defined signals here",
                        "-- #### USER-DATA-SIGNALS-START",
                        "-- #### USER-DATA-SIGNALS-END",
                        "",
                        "-- Trigger signals"
                    );

                    decl += RenderLines(state,
                         AllRenderedProcesses
                         .SelectMany(proc => new string[] {
                            $"signal RDY_{ProcessNames[proc]}: BOOLEAN;",
                            $"signal FIN_{ProcessNames[proc]}: BOOLEAN;",
                         })
                    );

                    decl += RenderLines(state,
                        "",
                        "-- The primary ready driver signal",
                        "signal RDY: BOOLEAN;",
                        "",
                        "-- Ready flag flip signal",
                        "signal readyflag: BOOLEAN;",
                        ""
                    );
                }

                decl += RenderLines(state, "begin");

                using(state.Indenter())
                {
                    foreach (var inst in AllRenderedProcesses)
                        decl += RenderProcessInstantiation(state, inst);

                    decl += RenderLines(state,
                        "-- Connect RDY signals"
                    );

                    // We remove all the connect statements by attaching the dependencies directly
                    var graph = BuildPrunedGraph();

                    // Build dependencies from the pruned graph
                    decl += RenderLines(state,
                        graph.Select(
                            x => {
                                var depends = x.Value;
                                var selfsignal = "RDY_" + ProcessNames[x.Key];
                                if (depends.Length == 0)
                                    return $"{selfsignal} <= RDY;";

                                var depsignals = 
                                    string.Join(
                                        " and ", 
                                        x.Value
                                            .Select(
                                                y => "FIN_" + ProcessNames[y]
                                            )
                                    );

                                return
                                    $"{selfsignal} <= {depsignals};";
                            }
                        )
                    );

                    decl += RenderLines(state,
                        "",
                        "-- Connect FIN signals"
                    );

                    // The trail-level signals contribute to the final FIN
                    var finsignals =
                        string.Join(
                            " and ",
                                LeafProcesses(graph)
                                .Select(
                                    y => "FIN_" + ProcessNames[y]
                                )
                        );


                    decl += RenderLines(state,
                        $"FIN <= {finsignals};",
                        ""
                    );

                    decl += RenderLines(state, "-- Wire up all testbench monitor signals");
                    foreach (var bus in internalbusses)
                        decl += RenderLines(state, 
                            bus.Instances.OfType<Instance.Signal>()
                                .Select(x => $"tb_{RenderSignalName(BusNames[bus], x.Name)} <= {RenderSignalName(BusNames[bus], x.Name)};")
                        );

                    // Wire up all the removed forwarding process signals, if any
                    if (forwardprocs.Length > 0)
                    {
                        decl += RenderLines(state, 
                            "",
                            "-- Forwards from connect statements and type casts"
                        );

                        foreach (var rp in forwardprocs)
                        {
                            //decl += RenderLines(state, $"-- Signals from {ProcessNames[rp]}");
                            var sourcebus = rp.MappedParameters.Where(x => x.MappedItem is Instance.Bus && x.MatchedParameter.Direction == AST.ParameterDirection.In).FirstOrDefault();
                            var targetbus = rp.MappedParameters.Where(x => x.MappedItem is Instance.Bus && x.MatchedParameter.Direction == AST.ParameterDirection.Out).FirstOrDefault();
                            if (sourcebus == null || targetbus == null)
                                throw new Exception("Incorrect process definition for identity process");

                            foreach (var stm in rp.Statements.OfType<AST.AssignmentStatement>())
                            {
                                if (stm.Value is AST.NameExpression nme)
                                {
                                    // Extract the bus name
                                    var sourcebusname = nme.Name.Identifier.TakeLast(2).First().Name;
                                    var targetbusname = stm.Name.Identifier.TakeLast(2).First().Name;

                                    // Extract the signal name
                                    var sourcesignalname = nme.Name.Identifier.Last().Name;
                                    var targetsignalname = stm.Name.Identifier.Last().Name;

                                    // Verify that the name of the bus is correct
                                    if (sourcebusname != sourcebus.SourceParameter.Name.Name)
                                        throw new Exception($"Assignment in process for {sourcesignalname} is not reading from the input");
                                    if (targetbusname != targetbus.SourceParameter.Name.Name)
                                        throw new Exception($"Assignment in process for {targetsignalname} is not writing to the output");

                                    // Find the instances being used, so we can name them correctly
                                    var sourcebusinstance = (Instance.Bus)sourcebus.MappedItem;
                                    var targetbusinstance = (Instance.Bus)targetbus.MappedItem;
                                    
                                    decl += RenderLines(state, 
                                        $"{RenderSignalName(BusNames[targetbusinstance], targetsignalname)} <= {RenderSignalName(BusNames[sourcebusinstance], sourcesignalname)};"
                                    );
                                }
                                else
                                {
                                    throw new Exception("Source of the assignment statement must be a name expression");
                                }
                            }
                        }
                    }


                    decl += RenderLines(state,
                        "",
                        "-- Propagate all clocked and feedback signals",
                        $"process({Config.CLOCK_SIGNAL_NAME}, {Config.RESET_SIGNAL_NAME})",
                        "begin",
                        $"    if {Config.RESET_SIGNAL_NAME} = {(Config.RESET_ACTIVE_LOW ? "'0'" : "'1'")} then",
                        "        RDY <= FALSE;",
                        "        readyflag <= TRUE;",
                        $"    elsif rising_edge({Config.CLOCK_SIGNAL_NAME}) then",
                        $"        if {Config.ENABLE_SIGNAL_NAME} then",
                        "            RDY <= not readyflag;",
                        "            readyflag <= not readyflag;",
                        "        end if;",
                        "    end if;",
                        "end process;",
                        ""
                    );

                    decl += RenderLines(state,
                        "",
                        "-- User defined processes here",
                        "-- #### USER-DATA-CODE-START",
                        "-- #### USER-DATA-CODE-END",
                        ""
                    );

                }

                decl += RenderLines(state, "end RTL;");
                return decl;
            }
        }

        /// <summary>
        /// Create a graph where the non-rendered processes are removed 
        /// and dependencies are rewired
        /// </summary>
        /// <returns>A directed acyclic graph</returns>
        private Dictionary<Instance.Process, Instance.Process[]> BuildPrunedGraph()
        {
            // Start by figuring out what removed processes depends on
            var removals = new Dictionary<Instance.Process, Instance.Process[]>();
            foreach (var k in ValidationState.DependencyGraph)
                if (!AllRenderedProcesses.Contains(k.Key))
                    removals[k.Key] = k.Value.SelectMany(
                        x => ValidationState.DependencyGraph[x]
                    )
                    .Distinct()
                    .ToArray();

            // We can have more than one layer of removed processes,
            // so we repeat the roll-up until we have removed all
            var changes = removals.Count > 0;
            while(changes)
            {
                changes = false;
                foreach (var k in removals)
                {
                    if (k.Value.Any(x => removals.ContainsKey(x)))
                    {
                        removals[k.Key] = k.Value.SelectMany(
                            x =>
                                removals.ContainsKey(x)
                                ? removals[x]
                                : new[] { x }
                        )
                        .Distinct()
                        .ToArray();

                        changes = true;
                        break;
                    }
                }
            }

            // We now have a list of what each of the removed processes needs
            // to be replaced by, so we build the graph from that
            var graph = new Dictionary<Instance.Process, Instance.Process[]>();
            foreach (var k in ValidationState.DependencyGraph)
                if (!removals.ContainsKey(k.Key))
                    graph[k.Key] = k.Value.SelectMany(
                        x => removals.ContainsKey(x)
                        ? removals[x]
                        : new [] { x }
                    )
                    .ToArray();


            return graph;
        }

        /// <summary>
        /// Returns the leaf nodes from the given graph
        /// </summary>
        /// <param name="graph">The graph to evaluate</param>
        /// <returns>The leaf nodes</returns>
        public IEnumerable<Instance.Process> LeafProcesses(Dictionary<Instance.Process, Instance.Process[]> graph)
        {
            var ready = new HashSet<Instance.Process>();
            var waves = new List<List<Instance.Process>>();
            var waiting = graph.Keys.ToList();
            if (waiting.Count == 0)
                throw new ArgumentException("No processes to build the leafs from");

            while(waiting.Count > 0)
            {
                var ec = waiting.Count;

                var front = new List<Instance.Process>();
                waves.Add(front);

                for(var i = waiting.Count - 1; i >= 0; i--)
                {
                    var p = waiting[i];
                    if (graph[p].All(x => ready.Contains(x)))
                    {
                        ready.Add(p);
                        front.Add(p);
                        waiting.RemoveAt(i);
                    }
                }

                if (ec == waiting.Count)
                    throw new Exception("Cyclic dependency in reduced graph");
            }

            return waves.Last();
        }

        /// <summary>
        /// Renders the instantiation of a process
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="proc">The instance to render</param>
        /// <returns>The redered instance</returns>
        public string RenderProcessInstantiation(RenderState state, Instance.Process proc)
        {
            var name = ProcessNames[proc];
            var decl = RenderLines(state,
                $"-- Entity {name} from {proc.SourceName}",
                $"{name}: entity work.{name}"
            );

            var genparams = proc
                .MappedParameters
                .Where(x => x.MappedItem is Instance.ConstantReference || x.MappedItem is Instance.Variable)
                .Select(x => new
                {
                    Name = x.LocalName,
                    Item =
                        x.MappedItem is Instance.ConstantReference
                        ? (x.MappedItem as Instance.ConstantReference).Source.Expression
                        : (x.MappedItem as Instance.Variable).Source.Initializer
                })
                .Select(x => $"{SanitizeVHDLName(x.Name)} => {RenderExpression(state, x.Item)};")
                .ToArray();

            if (genparams.Any())
            {
                // Remove the trailing ; of the last entry
                genparams[genparams.Length - 1] = genparams[genparams.Length - 1].Substring(0, genparams[genparams.Length - 1].Length - 1);

                decl += RenderLines(state, "generic map (");
                using (state.Indenter())
                    decl += RenderLines(state, genparams);
                decl += RenderLines(state, ")");
            }

            decl += RenderLines(state, "port map (");
            using(state.Indenter())
            {
                foreach (var busparam in proc.MappedParameters.Where(x => x.MappedItem is Instance.Bus))
                {
                    decl += RenderLines(state, 
                        $"-- {(busparam.MatchedParameter.Direction == AST.ParameterDirection.In ? "Input" : "Output")} bus {busparam.LocalName}"
                    );

                    var bus = (Instance.Bus)busparam.MappedItem;
                    var source = busparam.MatchedParameter;

                    decl += RenderLines(state,
                        bus
                            .Instances
                            .OfType<Instance.Signal>()
                            .Select(x => $"{RenderSignalName(source.Name.Name, x.Name)} => {RenderSignalName(BusNames[bus], x.Name)},")
                    );
                }

                foreach (var localbus in proc.Instances.OfType<Instance.Bus>())
                {
                    var directions = localbus.Instances
                        .OfType<Instance.Signal>()
                        .Where(x => ValidationState.ItemDirection[proc].ContainsKey(x))
                        .Select(x => ValidationState.ItemDirection[proc][x]);

                    var direction =
                        directions.Any(x => x == Validation.ItemUsageDirection.Read)
                        ? Validation.ItemUsageDirection.Read
                        : Validation.ItemUsageDirection.Write;

                    decl += RenderLines(state,
                        $"-- Local {(direction == Validation.ItemUsageDirection.Read ? "input" : "output")} bus {localbus.Name}"
                    );

                    decl += RenderLines(state,
                        localbus
                            .Instances
                            .OfType<Instance.Signal>()
                            .Select(x => $"{RenderSignalName(localbus.Name, x.Name)} => {RenderSignalName(BusNames[localbus], x.Name)},")
                    );
                }                

                decl += RenderLines(state,
                    $"-- Control signals",
                    $"{Config.CLOCK_SIGNAL_NAME} => {Config.CLOCK_SIGNAL_NAME},",
                    $"RDY => RDY_{ProcessNames[proc]},",
                    $"FIN => FIN_{ProcessNames[proc]},",
                    $"{Config.ENABLE_SIGNAL_NAME} => {Config.ENABLE_SIGNAL_NAME},",
                    $"{Config.RESET_SIGNAL_NAME} => {Config.RESET_SIGNAL_NAME}"
                );
            }

            decl += RenderLines(state, 
                ");", 
                ""
            );

            return decl;
        }

        /// <summary>
        /// Renders the signals for a bus
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="bus">The bus to render</param>
        /// <param name="busname">The name of the bus</param>
        /// <param name="direction">An optional direction string for the comments</param>
        /// <returns>The rendered bus definition</returns>
        public string RenderBusSignals(RenderState state, Instance.Bus bus, string busname, string direction = null)
        {
            var decl = RenderLines(
                state,
                "",
                $"-- {(string.IsNullOrWhiteSpace(direction) ? "Bus" : direction + " bus")} {bus.Name} signals"
            );

            decl +=
                RenderLines(
                    state,
                    bus
                        .Instances
                        .OfType<Instance.Signal>()
                        .Select(x => $"signal {RenderSignalName(busname, x.Name)}: {RenderNativeType(x.ResolvedType)};")
                );

            return decl;
        }


        /// <summary>
        /// Renders a top-level bus
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="bus">The bus to render</param>
        /// <param name="useExport">Flag indicating if we should use export types</param>
        /// <returns>The rendered bus definition</returns>
        public string RenderTopLevelBus(RenderState state, Instance.Bus bus, bool useExport = false)
        {
            var direction = "out";
            var text = "Top-level output";

            var prefix = string.Empty;

            if (ValidationState.TopLevel.InputBusses.Contains(bus))
            {
                direction = "in";
                text = "Top-level input";
            }
            else if (ValidationState.TopLevel.OutputBusses.Contains(bus))
            {
                direction = "out";
                text = "Top-level output";
            }
            else
            {
                prefix = "tb_";
                text = "Shared";
            }

            var decl = RenderLines(
                state, 
                $"-- {text} bus {bus.Name} signals"
            );

            decl += 
                RenderLines(
                    state,
                    bus
                        .Instances
                        .OfType<Instance.Signal>()
                        .Select(x => $"{RenderSignalName(prefix + BusNames[bus], x.Name)}: {direction} {(useExport ? RenderExportType(x.ResolvedType) : RenderNativeType(x.ResolvedType))};")
                );

            return decl + Environment.NewLine;
        }

        /// <summary>
        /// Returns a VHDL representation of a process
        /// </summary>
        /// <param name="state">The render stater</param>
        /// <param name="process">The process to render</param>
        /// <returns>The document representing the rendered process</returns>
        public string GenerateProcess(RenderState state, Instance.Process process)  
        {
            using(state.StartScope(process))
            {
                var pdef = process.ProcessDefinition;
                var name = ProcessNames[process];

                var decl = RenderLines(state, $"entity {name} is");
                using(state.Indenter())
                {
                    var genparams = process
                        .MappedParameters
                        .Where(x => x.MappedItem is Instance.ConstantReference || x.MappedItem is Instance.Variable)
                        .Select(x => new
                        {
                            Name = x.LocalName,
                            DataType =
                                x.MappedItem is Instance.ConstantReference
                                ? (x.MappedItem as Instance.ConstantReference).ResolvedType
                                : (x.MappedItem as Instance.Variable).ResolvedType
                        })
                        .Select(x => $"{SanitizeVHDLName(x.Name)}: {RenderNativeType(x.DataType)};")
                        .ToArray();

                    if (genparams.Length != 0)
                    {
                        // Remove the trailing ; of the last entry
                        genparams[genparams.Length - 1] = genparams[genparams.Length - 1].Substring(0, genparams[genparams.Length - 1].Length - 1);

                        decl += RenderLines(state, $"generic(");
                        using (state.Indenter())
                            decl += RenderLines(
                                state,
                                genparams
                            );
                        decl += RenderLines(state, $");");
                    }

                    decl += RenderLines(state, $"port(");
                    using(state.Indenter())
                    {
                        var usages = ValidationState.ItemDirection[process];
                        var busses = process
                            .Instances
                            .OfType<Instance.Bus>()
                            .Concat(
                                process
                                    .MappedParameters
                                    .Select(x => x.MappedItem)
                                    .OfType<Instance.Bus>()
                            );

                        foreach (var localbus in busses)
                            decl += RenderBusInstance(state, localbus, usages);

                        decl += RenderLines(state,
                            "",
                            "-- User defined signals here",
                            "-- #### USER-DATA-ENTITYSIGNALS-START",
                            "-- #### USER-DATA-ENTITYSIGNALS-END",
                            "",
                            "-- Clock signal",
                            $"{Config.CLOCK_SIGNAL_NAME} : in STD_LOGIC;",
                            "",
                            "--Ready signal",
                            "RDY : in BOOLEAN;",
                            "",
                            "--Finished signal",
                            "FIN : out BOOLEAN;",
                            "",
                            "--Enable signal",
                            $"{Config.ENABLE_SIGNAL_NAME} : in BOOLEAN;",
                            "",
                            "--Reset signal",
                            $"{Config.RESET_SIGNAL_NAME} : in STD_LOGIC"
                        );
                    }
                    decl += RenderLines(state, ");");
                }

                decl += RenderLines(state, $"end {name};", "");

                var impl = RenderLines(state, $"architecture RTL of {name} is");
                using(state.Indenter())
                {
                    impl += RenderLines(state,
                        "-- User defined signals here",
                        "-- #### USER-DATA-SIGNALS-START",
                        "-- #### USER-DATA-SIGNALS-END",
                        ""
                    );

                    var funcs = process.Instances
                        .OfType<Instance.FunctionInvocation>()
                        .Where(x => process.ProcessDefinition.Declarations.Contains(x.Source))
                        .Distinct();

                    if (funcs.Any())
                    {
                        impl += RenderLines(state, "-- Functions");

                        var mergedFuncs = funcs
                            .GroupBy(x => x.Name + "(" + FunctionSignature(state, x) + ")");

                        foreach (var f in mergedFuncs)
                        {
                            // Register all merged invocations on the same name
                            var fscope = GetLocalNameScope(state);
                            var fname = GetUniqueLocalName(state, f.First());

                            foreach (var item in f.Skip(1))
                                fscope.LocalNames.Add(item, fname);

                            impl += RenderFunctionImplementation(state, f.First());
                        }

                        impl += RenderLines(state, "");
                    }
                }

                impl += RenderLines(state, "begin");

                using (state.Indenter())
                {
                    impl += RenderLines(state,
                        "",
                        "-- Custom processes go here",
                        "-- #### USER-DATA-PROCESSES-START",
                        "-- #### USER-DATA-PROCESSES-END",
                        ""
                    );

                    impl += RenderLines(state, "process(");
                    using (state.Indenter())
                    {
                        impl += RenderLines(state, 
                            "",
                            "--Custom sensitivity signals here",
                            "-- #### USER-DATA-SENSITIVITY-START",
                            "-- #### USER-DATA-SENSITIVITY-END",
                            "",
                            "RDY,",
                            $"{Config.RESET_SIGNAL_NAME}",
                            ""
                        );
                    }
                    impl += RenderLines(state, ")");

                    if (process.Instances.OfType<Instance.Variable>().Any())
                    {
                        impl += RenderLines(state,
                            "--Internal variables"
                        );

                        impl += RenderLines(
                            state,
                            process
                                .Instances
                                .OfType<Instance.Variable>()
                                .Select(x => RenderVariable(state, x))
                        );
                    }

                    impl += RenderLines(state,
                        "variable reentry_guard: BOOLEAN;",
                        "",
                        "-- #### USER-DATA-NONCLOCKEDVARIABLES-START",
                        "-- #### USER-DATA-NONCLOCKEDVARIABLES-END",
                        ""
                    );

                    impl += RenderLines(state, "begin");

                    using (state.Indenter())
                    {
                        impl += RenderLines(state,
                            "--Initialize code here",
                            "-- #### USER-DATA-NONCLOCKEDSHAREDINITIALIZECODE-START",
                            "-- #### USER-DATA-NONCLOCKEDSHAREDINITIALIZECODE-END",
                            ""
                        );

                        impl += RenderLines (state, $"if {Config.RESET_SIGNAL_NAME} = {(Config.RESET_ACTIVE_LOW ? "'0'" : "'1'")} then");

                        using(state.Indenter())
                        {
                            var variables = process
                                    .Instances
                                    .OfType<Instance.Variable>();

                            // Emit reset statements for variables
                            impl += RenderStatements(
                                state, 

                                variables.Select(x => {
                                    var resetvalue = DefaultValue(state, x);
                                    var expr = TypeCast(state, resetvalue.Item2, x.ResolvedType);
                                    process.AssignedTypes[expr] = x.ResolvedType;
                                    process.AssignedTypes[resetvalue.Item2] = resetvalue.Item1;

                                    return new AST.AssignmentStatement(
                                        pdef.SourceToken, 
                                        x.Source.Name.AsName(),
                                        expr
                                    );
                                })
                                .ToArray()
                            );

                            // Emit reset statements for output signals
                            var usages = ValidationState.ItemDirection[process];

                            var signals = process
                                .MappedParameters
                                .Where(x => x.MappedItem is Instance.Bus)
                                .SelectMany(x => 
                                    ((Instance.Bus)x.MappedItem).Instances
                                        .OfType<Instance.Signal>()
                                        .Select(y => new { 
                                            Signal = y,
                                            Parameter = x
                                        })
                                )
                                .Where(x => usages.ContainsKey(x.Signal))
                                .Where(x => usages[x.Signal] != Validation.ItemUsageDirection.Read)
                                .ToArray();

                            impl += RenderStatements(
                                state,

                                signals.Select(x => {
                                    var resetvalue = DefaultValue(x.Signal);
                                    var expr = TypeCast(state, resetvalue.Item2, x.Signal.ResolvedType);
                                    process.AssignedTypes[expr] = x.Signal.ResolvedType;
                                    process.AssignedTypes[resetvalue.Item2] = resetvalue.Item1;

                                    return new AST.AssignmentStatement(
                                        pdef.SourceToken,
                                        new AST.Name(
                                            x.Signal.Source.SourceToken,
                                            new AST.Identifier[] {
                                                new AST.Identifier(new ParseToken(0, 0, 0, x.Parameter.LocalName)),
                                                x.Signal.Source.Name
                                            },
                                            null
                                        ),
                                        expr
                                    );
                                })
                                .ToArray()
                            );

                            impl += RenderLines(state,
                                "reentry_guard := FALSE;",
                                "FIN <= FALSE;",
                                "",
                                "--Initialize code here",
                                "-- #### USER-DATA-NONCLOCKEDRESETCODE-START",
                                "-- #### USER-DATA-NONCLOCKEDRESETCODE-END",
                                ""
                            );
                        }

                        impl += RenderLines(state, "elsif reentry_guard /= RDY then");

                        using(state.Indenter())
                        {
                            // Main contents
                            impl += RenderStatements(state, process.Statements);

                            impl += RenderLines(state,
                                "reentry_guard := RDY;",
                                "",
                                "--Initialize code here",
                                "-- #### USER-DATA-NONCLOCKEDINITIALIZECODE-START",
                                "-- #### USER-DATA-NONCLOCKEDINITIALIZECODE-END",
                                ""
                            );

                            impl += RenderLines(state, "FIN <= RDY;");
                        }

                        impl += RenderLines(state, "end if;");


                    }

                    impl += RenderLines(state, "end process;");
                }

                impl += RenderLines(state, 
                    "end RTL;",
                    "",
                    "--User defined architectures here",
                    "-- #### USER-DATA-ARCH-START",
                    "-- #### USER-DATA-ARCH-END",
                    ""
                );

                return GenerateVHDLFilePreamble(state) + decl + impl;
            }
        }

        /// <summary>
        /// Injects a typecast if the source type is another type than the target
        /// </summary>
        /// <param name="expression">The expression to cast</param>
        /// <param name="targettype">The destination type</param>
        /// <param name="state">The render state</param>
        /// <returns>An expression that could be a type cast</returns>
        public AST.Expression TypeCast(RenderState state, AST.Expression expression, DataType targettype)
        {
            if (expression is LiteralExpression literalExpression) 
            {
                if (literalExpression.Value is AST.BooleanConstant && targettype.IsBoolean)
                    return literalExpression;
                else if (literalExpression.Value is AST.FloatingConstant && targettype.IsFloat)
                    return literalExpression;
                
                if (literalExpression.Value is AST.IntegerConstant)
                    return new AST.TypeCast(literalExpression, targettype, false);

                throw new ParserException($"Unable to cast expression {literalExpression.Value} to {targettype}", expression);
            }
            else if (expression is NameExpression nameExpression)
            {
                var scope = GetLocalScope(state);
                var symb = ValidationState.FindSymbol(nameExpression.Name, scope);
                if (symb is Instance.ConstantReference con)
                {
                    var sourcetype = ValidationState.InstanceType(con);
                    if (object.Equals(sourcetype, targettype))
                        return expression;
                }
                else if (symb is Instance.EnumFieldReference enm)
                {
                    var sourcetype = new AST.DataType(enm.Source.SourceToken, enm.ParentType.Source);
                    if (object.Equals(sourcetype, targettype))
                        return expression;

                    if (targettype.IsNumeric)
                        return new AST.TypeCast(nameExpression, targettype, false);
                    
                }             
            }

            throw new ParserException($"Unable to cast expression {expression} to {targettype}", expression);
        }

        /// <summary>
        /// Gets the default value for a type
        /// </summary>
        /// <param name="sourceToken">The source token used for the returned literal</param>
        /// <param name="type">The type to get the default value for</param>
        /// <returns>A literal expression that is the default value</returns>
        public Tuple<AST.DataType, AST.Expression> DefaultValue(ParseToken sourceToken, DataType type)
        {
            // Get the default value for the type
            if (type.IsBoolean)
                return new Tuple<DataType, Expression>(
                    new AST.DataType(sourceToken, ILType.Bool, 1),
                    new AST.LiteralExpression(sourceToken, new AST.BooleanConstant(sourceToken, false))
                );

            if (type.IsInteger)
                return new Tuple<DataType, Expression>(
                    new DataType(sourceToken, ILType.SignedInteger, -1),
                    new AST.LiteralExpression(sourceToken, new AST.IntegerConstant(sourceToken, "0"))
                );

            if (type.IsFloat)
                return new Tuple<DataType, Expression>(
                    new DataType(sourceToken, ILType.Float, -1),
                    new AST.LiteralExpression(sourceToken, new AST.FloatingConstant(sourceToken, "0", "0"))
                );

            if (type.IsEnum)
                return new Tuple<DataType, Expression>(
                    new DataType(sourceToken, type.EnumType),
                    new AST.NameExpression(
                        sourceToken, 
                        new Name(sourceToken, new [] { 
                            type.EnumType.Name,
                            type.EnumType.Fields.First().Name
                        }, 
                        null))
                );

            throw new ParserException("No default value for type", type);
        }

        /// <summary>
        /// Returns the default value and type for the variable
        /// </summary>
        /// <param name="state">The current state</param>
        /// <param name="variable">The variable to get the default value from</param>
        /// <returns>An expression that is the default value</returns>
        public Tuple<AST.DataType, AST.Expression> DefaultValue(RenderState state, Instance.Variable variable)
        {
            var decl = variable.Source;
            if (decl.Initializer == null)
                return DefaultValue(decl.SourceToken, variable.ResolvedType);

            if (decl.Initializer is AST.LiteralExpression initExpr)
                return new Tuple<DataType, Expression>(new DataType(decl.SourceToken, initExpr.Value.Type, -1), initExpr);

            if (decl.Initializer is AST.NameExpression nameExpr)
            {
                var scope = GetLocalScope(state);
                var symb = ValidationState.FindSymbol(nameExpr.Name, scope);
                if (symb is Instance.ConstantReference con)
                {
                    return new Tuple<DataType, Expression>(
                        con.ResolvedType,
                        new AST.Name(
                            con.Source.SourceToken, 
                            new Identifier[] { new Identifier(new ParseToken(0, 0, 0, con.Name)) }
                            , null
                        ).AsExpression()
                    );
                }
                else if (symb is Instance.EnumFieldReference enm)
                {
                    return new Tuple<DataType, Expression>(
                        new AST.DataType(decl.Initializer.SourceToken, enm.ParentType.Source),
                        new AST.NameExpression(
                            decl.Initializer.SourceToken,
                            new AST.Name(
                                decl.Initializer.SourceToken,
                                new Identifier[] {
                                    new AST.Identifier(new ParseToken(0, 0, 0, enm.ParentType.Name)),
                                    new AST.Identifier(new ParseToken(0, 0, 0, enm.Name)),
                                },
                                null
                            )
                        )
                    );
                }
            }

            throw new ParserException("Initial value for variable must be a literal", decl);
        }

        /// <summary>
        /// Returns the default value for the signal
        /// </summary>
        /// <param name="signal">The signal to get the default value from</param>
        /// <returns>A literal expression that is the default value</returns>
        public Tuple<AST.DataType, AST.Expression> DefaultValue(Instance.Signal signal)
        {
            var decl = signal.Source;
            if (decl.Initializer == null)
                return DefaultValue(decl.SourceToken, signal.ResolvedType);

            if (decl.Initializer is AST.LiteralExpression initExpr)
                return new Tuple<DataType, Expression>(new DataType(decl.SourceToken, initExpr.Value.Type, -1) , initExpr);

            throw new ParserException("No initial value for variable", decl);
        }        

        /// <summary>
        /// A regex for detecting non-alpha numeric names
        /// </summary>
        private static Regex RX_ALPHANUMERIC = new Regex(@"[^\u0030-\u0039|\u0041-\u005A|\u0061-\u007A]");

        /// <summary>
        /// The list of reserved VHDL words
        /// </summary>
        private static readonly HashSet<string> VHDL_KEYWORDS = new HashSet<string> {
            // Keywords in synthesized code
            "abs", "downto", "library", "srl", "else", "procedure", "subtype", "elsif", 
            "literal", "process", "then", "end", "loop", "to", "entity", "map", "range", 
            "and", "exit", "mod", "record", "type", "architecture", "nand", "array", 
            "for", "function", "next", "rem", "until", "attribute", "generate", "nor", 
            "use", "begin", "generic", "not", "return", "variable", "block", "group", 
            "null", "rol", "wait", "body", "of", "ror", "when", "buffer", "if", "while", 
            "case", "in", "or", "xnor", "component", "others", "signal", "xor", 
            "configuration", "inout", "out", "sla", "constant", "is", "package", "sll",
            "port", "sra",

            // Keywords in non-synthesized code
            "postponed", "access", "linkage", "after", "alias", "pure", "all", 
            "transport", "file", "register", "unaffected", "new", "reject", "units", 
            "assert", "report", "guarded", "on", "select", "bus", "impure", "open", 
            "severity", "with", "shared", "inertial", "disconnect", "label"
        };

        /// <summary>
        /// Cleans up a VHDL component or variable name
        /// </summary>
        /// <param name="name">The name to sanitize</param>
        /// <returns>The sanitized name</returns>
        public static string SanitizeVHDLName(string name)
        {
            var r = RX_ALPHANUMERIC.Replace(name, "_");
            if (VHDL_KEYWORDS.Contains(r.ToLowerInvariant()))
                r = "vhdl_" + r;

            while (r.IndexOf("__", StringComparison.Ordinal) >= 0)
                r = r.Replace("__", "_");

            return r.TrimEnd('_');
        }

        /// <summary>
        /// Returns a composite name based on the elements in the parent list
        /// </summary>
        /// <param name="parents">The parents of the item</param>
        /// <returns>A composite string</returns>
        private string RenderScopeName(IEnumerable<AST.ParsedItem> parents, string selfname)
        {
            return string.Join(".",
                parents
                .Select(x => {
                    if (x is AST.Module m)
                        return ValidationState.TopLevel.Module == m 
                            ? null 
                            : ValidationState.Modules.FirstOrDefault(y => y.Value == m).Key;
                    else if (x is AST.Network n)
                        return n.Name.Name;
                    else if (x is AST.Process p)
                        return p.Name.Name;
                    else if (x is AST.FunctionDefinition f)
                        return f.Name.Name;

                    return null;
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Concat(new [] { selfname })
            );
        }

        /// <summary>
        /// Renders a variable declaration
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="variable">The variable to render</param>
        /// <returns>A VHDL fragment for declaring a variable</returns>
        public string RenderVariable(RenderState state, Instance.Variable variable)
        {
            var name = GetUniqueLocalName(state, variable);

            return $"variable {name}: {RenderNativeType(variable.ResolvedType)};";
        }

        /// <summary>
        /// Helper method to generate a valid VHDL name for a signal
        /// </summary>
        /// <param name="parent">The parent bus</param>
        /// <param name="name">The name of the signal</param>
        /// <param name="suffix">Any suffix to add to the signal</param>
        /// <returns></returns>
        private static string RenderSignalName(string busname, string name, string suffix = null)
        {
            return SanitizeVHDLName(busname + "_" + name + (string.IsNullOrWhiteSpace(suffix) ? "" : "+") + (suffix ?? string.Empty));
        }

        /// <summary>
        /// Gets the assigned type table for the current state
        /// </summary>
        /// <param name="state">The state to get the type table for</param>
        /// <returns>The assigned types</returns>
        private Dictionary<AST.Expression, DataType> GetLocalAssignedTypes(RenderState state)
        {
            var cur = state.ActiveScopes.Last(x => x is Instance.Network || x is Instance.Process || x is Instance.FunctionInvocation);
            if (cur is Instance.Network network)
                return network.AssignedTypes;
            else if (cur is Instance.Process process)
                return process.AssignedTypes;
            else if (cur is Instance.FunctionInvocation func)
                return func.AssignedTypes;
            else
                throw new InvalidOperationException("Returned something unexpected");
        }

        /// <summary>
        /// Gets the local active scope
        /// </summary>
        /// <param name="state">The current state</param>
        /// <returns>The namescope</returns>
        private Validation.ScopeState GetLocalScope(RenderState state)
        {
            var cur = state.ActiveScopes.Last(x => x is Instance.Module || x is Instance.Network || x is Instance.Process || x is Instance.FunctionInvocation || x is Instance.ForLoop);
            return ValidationState.LocalScopes[cur];
        }

        /// <summary>
        /// Gets the local active scope
        /// </summary>
        /// <param name="state">The current state</param>
        /// <returns>The namescope</returns>
        private NameScopeHelper GetLocalNameScope(RenderState state)
        {
            var cur = state.ActiveScopes.Last(x => x is Instance.Network || x is Instance.Process || x is Instance.FunctionInvocation);
            return NameScopes[cur];
        }

        /// <summary>
        /// Extracts the local name for a bus
        /// </summary>
        /// <param name="state">The current state</param>
        /// <param name="bus">The bus to find the name for</param>
        /// <returns>The local name for the bus</returns>
        private string GetLocalBusName(RenderState state, Instance.Bus bus)
        {
            var cur = state.ActiveScopes.Last(x => x is Instance.Network || x is Instance.Process || x is Instance.FunctionInvocation);
            if (cur is Instance.Network network)
            {
                var parmap = network.MappedParameters.FirstOrDefault(x => x.MappedItem == bus);
                return parmap == null ? bus.Name : parmap.LocalName;
            }
            else if (cur is Instance.Process process)
            {
                var parmap = process.MappedParameters.FirstOrDefault(x => x.MappedItem == bus);
                return parmap == null ? bus.Name : parmap.LocalName;
            }
            else if (cur is Instance.FunctionInvocation func)
            {
                var parmap = func.MappedParameters.FirstOrDefault(x => x.MappedItem == bus);
                return parmap == null ? bus.Name : parmap.LocalName;
            }
            else
            {
                throw new InvalidOperationException("Returned something unexpected");
            }
        }

        /// <summary>
        /// Gets or creates a unqiue local name for a signal in a process
        /// </summary>
        /// <param name="state">The current state</param>
        /// <param name="busname">The bus name to use</param>
        /// <param name="signal">The signal to get the local name for</param>
        /// <param name="asRead">If using the variable for reading</param>
        /// <param name="suffix">The suffix to use</param>
        /// <returns>A name that is unique in the process scope</returns>
        private string GetUniqueLocalName(RenderState state, string busname, Instance.Signal signal, bool asRead, string suffix = null)
        {
            var namescope = GetLocalNameScope(state);
            if ((asRead ? namescope.SignalReadNames : namescope.SignalWriteNames).TryGetValue(signal, out var name))
                return name;

            return (asRead ? namescope.SignalReadNames : namescope.SignalWriteNames)[signal] = CreateUniqueLocalName(state, RenderSignalName(busname, signal.Name, suffix));
        }

        /// <summary>
        /// Gets or creates a unqiue local name for a variable in a process
        /// </summary>
        /// <param name="state">The current state</param>
        /// <param name="variable">The variable to get the local name for</param>
        /// <returns>A name that is unique in the process scope</returns>
        private string GetUniqueLocalName(RenderState state, Instance.Variable variable)
        {
            var namescope = GetLocalNameScope(state);
            if (namescope.LocalNames.TryGetValue(variable, out var name))
                return name;
            
            return namescope.LocalNames[variable] = CreateUniqueLocalName(state, SanitizeVHDLName(variable.Name));
        }

        /// <summary>
        /// Gets or creates a unqiue local name for a function in a process
        /// </summary>
        /// <param name="state">The current state</param>
        /// <param name="func">The function to get the local name for</param>
        /// <returns>A name that is unique in the process scope</returns>
        private string GetUniqueLocalName(RenderState state, Instance.FunctionInvocation func)
        {
            // Observe global (static) functions
            if (GlobalNames.TryGetValue(func, out var k))
                return k;

            var namescope = GetLocalNameScope(state);
            if (namescope.LocalNames.TryGetValue(func, out var name))
                return name;

            return namescope.LocalNames[func] = CreateUniqueLocalName(state, SanitizeVHDLName(func.Name));
        }

        /// <summary>
        /// Creates a unique global name for an item
        /// </summary>
        /// <param name="item">The item to register a global name for</param>
        /// <param name="basename">The suggested name for the item</param>
        /// <returns>A unique name for the item</returns>
        private string CreateUniqueGlobalName(object item, string basename)
        {
            var name = basename;
            while (GlobalTokenCounter.TryGetValue(name, out var cnt))
            {
                var newname = SanitizeVHDLName(basename + "." + cnt);
                cnt++;
                GlobalTokenCounter[name] = cnt;
                name = newname;
            }

            GlobalTokenCounter.Add(name, 1);
            GlobalNames.Add(item, name);

            return name;
        }

        /// <summary>
        /// Registers the given name as used, and returns a (possible changed) name that is unique in the process
        /// </summary>
        /// <param name="name">The name to register</param>
        /// <param name="process">The process scope to use</param>
        /// <returns>The Unique name</returns>
        private string CreateUniqueLocalName(RenderState state, string name)
        {
            var cur = state.ActiveScopes.Where(x => x is Instance.Network || x is Instance.Process || x is Instance.FunctionInvocation).Last();
            var namescope = NameScopes[cur];

            // Copy over the global items
            if (GlobalTokenCounter.TryGetValue(name, out var gc) && !namescope.LocalTokenCounter.ContainsKey(name))
                namescope.LocalTokenCounter.Add(name, gc);

            if (namescope.LocalTokenCounter.TryGetValue(name, out var c))
            {
                namescope.LocalTokenCounter[name] = c + 1;
                name = name + "_" + c;
            }
            else
            {
                namescope.LocalTokenCounter[name] = 1;
            }

            return name;
        }

        /// <summary>
        /// Renders a bus instance for use within a process
        /// </summary>
        /// <param name="state">The state of the render</param>
        /// <param name="bus">The bus instance to render</param>
        /// <param name="direction">The bus direction parameter</param>
        /// <returns>A VHDL fragment for the ports in the bus</returns>
        public string RenderBusInstance(RenderState state, Instance.Bus bus, Dictionary<object, Validation.ItemUsageDirection> usages)
        {
            var process = state.ActiveScopes.OfType<Instance.Process>().Last();
            var parmap = process.MappedParameters.FirstOrDefault(x => x.MappedItem == bus);
            var busname = GetLocalBusName(state, bus);

            // Normally signals are in or out
            var signals =
                bus
                .Instances
                .OfType<Instance.Signal>()
                .SelectMany(x =>
                {
                    // We also render unused signals to ensure the VHDL follows the SMEIL signature
                    if (!usages.TryGetValue(x, out var d))
                        d = parmap.MatchedParameter.Direction != ParameterDirection.In ? Validation.ItemUsageDirection.Write : Validation.ItemUsageDirection.Read;

                    if (d == Validation.ItemUsageDirection.Both)
                    {
                        return new[] {
                            $"{GetUniqueLocalName(state, busname, x, true, "in")}: in {RenderNativeType(x.ResolvedType)};",
                            $"{GetUniqueLocalName(state, busname, x, false, "out")}: out {RenderNativeType(x.ResolvedType)};"
                        };
                    } 
                    else 
                    {
                        return new[] { $"{GetUniqueLocalName(state, busname, x, d == Validation.ItemUsageDirection.Read)}: {(d == Validation.ItemUsageDirection.Read ? "in" : "out")} {RenderNativeType(x.ResolvedType)};" };
                    }
                });


            // Return the bus signal declaration with the signals
            return RenderLines(state, 
                $"-- Bus {bus.Name} signals{Environment.NewLine}"
                + RenderLines(state, signals)
                + Environment.NewLine 
                + Environment.NewLine
            );
        }

        /// <summary>
        /// Renders a statement as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="statement">The statement to render</param>
        /// <returns>A VHDL fragment for the statement</returns>
        public string RenderStatement(RenderState state, AST.Statement statement)
        {
            switch (statement)
            {
                case AST.AssignmentStatement assignmentStatement:
                    return RenderAssignmentStatement(state, assignmentStatement);
                case AST.IfStatement ifStatement:
                    return RenderIfStatement(state, ifStatement);
                case AST.ForStatement forStatement:
                    return RenderForStatement(state, forStatement);
                case AST.SwitchStatement switchStatement:
                    return RenderSwitchStatement(state, switchStatement);
                case AST.TraceStatement traceStatement:
                    return RenderTraceStatement(state, traceStatement);
                case AST.AssertStatement assertStatement:
                    return RenderAssertStatement(state, assertStatement);
                case AST.BreakStatement breakStatement:
                    return RenderBreakStatement(state, breakStatement);
                case AST.FunctionStatement functionStatement:
                    return RenderFunctionStatement(state, functionStatement);
            }

            throw new ArgumentException($"Unable to render statement of type: {statement.GetType()}");
        }

        /// <summary>
        /// Renders an assignment statement as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="assignStatement">The statement to render</param>
        /// <returns>A VHDL fragment for the statement</returns>
        public string RenderAssignmentStatement(RenderState state, AST.AssignmentStatement assignStatement)
        {
            var symboltable = GetLocalScope(state);
            var symbol = ValidationState.FindSymbol(assignStatement.Name, symboltable);
            
            string name;
            if (symbol is Instance.Signal signal)
            {
                var busname = GetLocalBusName(state, signal.ParentBus);
                name = GetUniqueLocalName(state, busname, signal, false, null);
            }
            else if (symbol is Instance.Variable variable)
                name = GetUniqueLocalName(state, variable);
            else
                throw new ParserException("Unexpexted symbol type", assignStatement.Name);


            return $"{state.Indent}{ name } {( symbol is Instance.Signal ? "<=" : ":=" )} { RenderExpression(state, assignStatement.Value) };";
        }

        /// <summary>
        /// Renders a list of statements
        /// </summary>
        /// <param name="state">The state of the render</param>
        /// <param name="statements">The statements to render</param>
        /// <returns>A VHDL fragment for the statements</returns>
        private string RenderStatements(RenderState state, AST.Statement[] statements)
        {
            return
                statements == null || statements.Length == 0
                ? string.Empty
                : string.Join(Environment.NewLine, statements.Select(x => RenderStatement(state, x))) + Environment.NewLine;
        }

        /// <summary>
        /// Renders an if statement as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="forStatement">The statement to render</param>
        /// <returns>A VHDL fragment for the statement</returns>
        public string RenderIfStatement(RenderState state, AST.IfStatement ifStatement)
        {
            var indent = state.Indent;
            using(state.Indenter())
            {
                var truepart = 
                    $"{indent}if { RenderExpression(state, ifStatement.Condition) } then{Environment.NewLine}"
                    + RenderStatements(state, ifStatement.TrueStatements);

                var elifs =
                    (ifStatement.ElIfStatements == null || ifStatement.ElIfStatements.Length == 0)
                    ? string.Empty
                    : string.Join(
                        Environment.NewLine,
                        ifStatement.ElIfStatements.Select(x => 
                            $"{indent}elsif { RenderExpression(state, x.Item1) } then{Environment.NewLine}"
                            + RenderStatements(state, x.Item2)
                        )
                    );

                var els =
                    (ifStatement.FalseStatements == null || ifStatement.FalseStatements.Length == 0)
                    ? string.Empty
                    : $"{indent}else{Environment.NewLine}"
                        + RenderStatements(state, ifStatement.FalseStatements);

                return
                    truepart 
                    + elifs 
                    + els
                    + $"{indent}end if;";
            }
        }

        /// <summary>
        /// Renders a trace statement as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="forStatement">The statement to render</param>
        /// <returns>A VHDL fragment for the statement</returns>
        public string RenderForStatement(RenderState state, AST.ForStatement forStatement)
        {
            var indent = state.Indent;
            using (state.Indenter())
                return 
                    $"{indent}for { RenderIdentifier(state, forStatement.Variable) } in { RenderExpression(state, forStatement.FromExpression) } to { RenderExpression(state, forStatement.ToExpression) } loop{Environment.NewLine}"
                    + string.Join(Environment.NewLine, "")
                    + $"{indent}end loop;";
        }

        /// <summary>
        /// Renders a single switch case as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="item">The item to render</param>
        /// <returns>A VHDL fragment for the case</returns>
        private string RenderSwitchCase(RenderState state, Tuple<AST.Expression, AST.Statement[]> item)
        {
            var indent = state.Indent;
            using(state.Indenter())
                return 
                    $"{ indent }when {( item.Item1 == null ? "others" : RenderExpression(state, item.Item1) )} => {Environment.NewLine}"
                    + string.Join(Environment.NewLine, item.Item2.Select(x => RenderStatement(state, x)));
        }

        /// <summary>
        /// Renders a trace statement as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="switchStatement">The statement to render</param>
        /// <returns>A VHDL fragment for the statement</returns>
        public string RenderSwitchStatement(RenderState state, AST.SwitchStatement switchStatement)
        {
            var indent = state.Indent;
            using(state.Indenter())
                return 
                    $"{indent}case { RenderExpression(state, switchStatement.Value) } is { Environment.NewLine }"
                    + string.Join(Environment.NewLine, switchStatement.Cases.Select(x => RenderSwitchCase(state, x)))
                    + $"{Environment.NewLine}{indent}end case;";
        }

        /// <summary>
        /// Renders a trace statement as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="traceStatement">The statement to render</param>
        /// <returns>A VHDL fragment for the statement</returns>
        public string RenderTraceStatement(RenderState state, AST.TraceStatement traceStatement)
        {
            //throw new ArgumentException("Unable to render a trace statement");
            return string.Empty;
        }

        /// <summary>
        /// Renders a trace statement as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="assertStatement">The statement to render</param>
        /// <returns>A VHDL fragment for the statement</returns>
        public string RenderAssertStatement(RenderState state, AST.AssertStatement assertStatement)
        {
            //throw new ArgumentException("Unable to render an assert statement");
            return string.Empty;
        }

        /// <summary>
        /// Renders a break statement as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="traceStatement">The statement to render</param>
        /// <returns>A VHDL fragment for the statement</returns>
        public string RenderBreakStatement(RenderState state, AST.BreakStatement breakStatement)
        {
            throw new ArgumentException("Unable to render a break statement");
        }

        /// <summary>
        /// Renders a function statement as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="traceStatement">The statement to render</param>
        /// <returns>A VHDL fragment for the statement</returns>
        public string RenderFunctionStatement(RenderState state, AST.FunctionStatement functionStatement)
        {
            var process = state.ActiveScopes.OfType<Instance.Process>().Last();
            var scope = GetLocalScope(state);
            var symbol = process.Instances.OfType<Instance.FunctionInvocation>().FirstOrDefault(x => object.Equals(x.Statement.SourceToken, functionStatement.SourceToken));
            if (symbol == null)
                throw new ParserException($"No instance found for function invocation", functionStatement);

            return $"{state.Indent}{GetUniqueLocalName(state, symbol)}({string.Join(", ", symbol.MappedParameters.SelectMany(x => RenderParameterInput(state, x)) )});";
        }

        /// <summary>
        /// Renders a parameter list from the source instance
        /// </summary>
        /// <param name="state">The state of the render</param>
        /// <param name="parameter">The parameter to render</param>
        /// <returns>The parameter as a string</returns>
        public IEnumerable<string> RenderParameterInput(RenderState state, Instance.MappedParameter parameter)
        {
            if (parameter.MappedItem is Instance.Signal signal)
            {
                var busname = GetLocalBusName(state, signal.ParentBus);
                return new string[] { GetUniqueLocalName(state, busname, signal, parameter.MatchedParameter.Direction != ParameterDirection.Out) };
            }
            else if (parameter.MappedItem is Instance.Bus bus)
            {
                var busname = GetLocalBusName(state, bus);
                return bus.Instances.OfType<Instance.Signal>().Select(
                    x => GetUniqueLocalName(state, busname, x, parameter.MatchedParameter.Direction != ParameterDirection.Out)
                );
            }
            else if (parameter.MappedItem is Instance.ConstantReference constRef)
                return new string[] { GlobalNames[constRef.Source] };
            else if (parameter.MappedItem is Instance.Literal literal)
                return new string[] { RenderConstant(state, literal.Source) };
            else if (parameter.MappedItem is Instance.Variable var)
                return new string[] { GetUniqueLocalName(state, var) };
            else
                throw new ParserException($"Unable to render parameter input for type: {parameter.MappedItem.GetType()}", parameter.SourceParameter);
        }

        /// <summary>
        /// Renders an expression as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="expression">The expression to render</param>
        /// <returns>A VHDL fragment for the expression</returns>
        public string RenderExpression(RenderState state, AST.Expression expression)
        {
            switch(expression)
            {
                case AST.LiteralExpression literalExpression:
                    return RenderConstant(state, literalExpression.Value);
                case AST.NameExpression nameExpression:
                    return RenderName(state, nameExpression.Name);
                case AST.TypeCast typeCastExpression:
                    return RenderTypeCast(state, typeCastExpression);
                case AST.ParenthesizedExpression parenthesizedExpression:
                    return "(" + RenderExpression(state, parenthesizedExpression.Expression) + ")";
                case AST.UnaryExpression unaryExpression:
                    return RenderUnaryOperation(state, unaryExpression.Operation) + " " + RenderExpression(state, unaryExpression.Expression);
                case AST.BinaryExpression binaryExpression:
                    return RenderBinaryExpression(state, binaryExpression);
            }

            throw new ArgumentException($"Unable to render expression of type: {expression.GetType()}");
        }

        /// <summary>
        /// Renders a binary expression as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="binaryExpression">The expression to render</param>
        /// <returns>A VHDL fragment for the expression</returns>
        public string RenderBinaryExpression(RenderState state, AST.BinaryExpression binaryExpression)
        {
            if (binaryExpression.Operation.Operation == AST.BinOp.ShiftLeft)
            {
                if (Config.AVOID_SLL_AND_SRL)
                    return $"shift_left({RenderExpression(state, binaryExpression.Left)}, {RenderExpression(state, binaryExpression.Right)})";
                else
                    return $"sll({RenderExpression(state, binaryExpression.Left)}, {RenderExpression(state, binaryExpression.Right)})";
            }

            if (binaryExpression.Operation.Operation == AST.BinOp.ShiftRight)
            {
                if (Config.AVOID_SLL_AND_SRL)
                    return $"shift_right({RenderExpression(state, binaryExpression.Left)}, {RenderExpression(state, binaryExpression.Right)})";
                else
                    return $"srl({RenderExpression(state, binaryExpression.Left)}, {RenderExpression(state, binaryExpression.Right)})";
            }

            return RenderExpression(state, binaryExpression.Left) + " " + RenderBinaryOperation(state, binaryExpression.Operation) + " " + RenderExpression(state, binaryExpression.Right);
        }


        /// <summary>
        /// Renders a unary operation as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="operation">The operation to render</param>
        /// <returns>A VHDL fragment for the operation</returns>
        public string RenderUnaryOperation(RenderState state, AST.UnaryOperation operation)
        {
            switch (operation.Operation)
            {
                case AST.UnaryOperation.UnOp.Negation:
                    return "-";
                case AST.UnaryOperation.UnOp.Identity:
                    return "+";
                case AST.UnaryOperation.UnOp.LogicalNegation:
                    return "not";
                case AST.UnaryOperation.UnOp.BitwiseInvert:
                    return "not";
            }

            throw new ArgumentException($"Unable to render unary operation: {operation}");
        }

        /// <summary>
        /// Renders a binary operation as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="operation">The operation to render</param>
        /// <returns>A VHDL fragment for the operation</returns>
        public string RenderBinaryOperation(RenderState state, AST.BinaryOperation operation)
        {
            switch (operation.Operation)
            {
                case AST.BinOp.Add:
                    return "+";
                case AST.BinOp.Subtract:
                    return "-";
                case AST.BinOp.Multiply:
                    return "*";
                case AST.BinOp.Divide:
                    return "/";
                case AST.BinOp.Modulo:
                    return "";
                case AST.BinOp.Equal:
                    return "==";
                case AST.BinOp.NotEqual:
                    return "!=";
                case AST.BinOp.ShiftLeft:
                    return "sll";
                case AST.BinOp.ShiftRight:
                    return "srl";
                case AST.BinOp.LessThan:
                    return "<";
                case AST.BinOp.GreaterThan:
                    return ">";
                case AST.BinOp.GreaterThanOrEqual:
                    return ">=";
                case AST.BinOp.LessThanOrEqual:
                    return "<=";
                case AST.BinOp.BitwiseAnd:
                    return "and";
                case AST.BinOp.BitwiseOr:
                    return "or";
                case AST.BinOp.BitwiseXor:
                    return "xor";
                case AST.BinOp.LogicalAnd:
                    return "and";
                case AST.BinOp.LogicalOr:
                    return "or";
            }

            throw new ArgumentException($"Unable to render binary operation: {operation}");
        }

        /// <summary>
        /// Renders an identifier
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="identifier">The identifier to render</param>
        /// <returns>A VHDL fragment for the identifier</returns>
        public string RenderIdentifier(RenderState state, AST.Identifier identifier)
        {
            return SanitizeVHDLName(identifier.Name);
        }

        /// <summary>
        /// Renders an identifier
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="identifier">The identifier to render</param>
        /// <param name="instancename">The instancename to use</param>
        /// <returns>A VHDL fragment for the identifier</returns>
        public string RenderIdentifier(RenderState state, AST.Identifier identifier, string instancename)
        {
            return SanitizeVHDLName(identifier.Name);
        }

        /// <summary>
        /// Renders an identifier
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="prefix">A prefix to use</param>
        /// <param name="identifier">The identifier to render</param>
        /// <param name="instancename">The instancename to use</param>
        /// <returns>A VHDL fragment for the identifier</returns>
        public string RenderIdentifier(RenderState state, string prefix, AST.Identifier identifier, string instancename)
        {
            return SanitizeVHDLName(prefix + identifier.Name);
        }

        /// <summary>
        /// Renders a name
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="name">The name to render</param>
        /// <returns>A VHDL fragment for the name</returns>
        public string RenderName(RenderState state, AST.Name name)
        {
            var symboltable = GetLocalScope(state);
            var symbol = ValidationState.FindSymbol(name, symboltable);

            string localname;
            if (symbol is Instance.Signal signal)
                localname = GetUniqueLocalName(state, null, signal, true, null);
            else if (symbol is Instance.Variable variable)
                localname = GetUniqueLocalName(state, variable);
            else if (symbol is Instance.EnumFieldReference enm)
                localname = EnumFieldNames[enm.Source];
            else if (symbol is Instance.Literal lit)
                localname = SanitizeVHDLName(name.AsString); // RenderConstant(state, lit.Source);
            else if (symbol is Instance.ConstantReference cref)
                localname = GlobalNames[cref.Source];
            else
                throw new ParserException($"Unexpected symbol type {symbol?.GetType()}", name);


            return localname;
        }

        /// <summary>
        /// Renders a constant expression
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="constant">The constant to render</param>
        /// <returns>A VHDL fragment for the constant</returns>
        public string RenderConstant(RenderState state, AST.Constant constant)
        {
            switch (constant)
            {
                case AST.FloatingConstant floatingConstant:
                    return RenderConstant(state, floatingConstant.Major) + "." + RenderConstant(state, floatingConstant.Minor);
                case AST.IntegerConstant integerConstant:
                    return integerConstant.Value;
                case AST.BooleanConstant booleanConstant:
                    return booleanConstant.Value ? "TRUE" : "FALSE";
                case AST.StringConstant stringConstant:
                    return "\"" + stringConstant.Value + "\"";
            }

            throw new ArgumentException($"Unable to render constant of type: {constant.GetType()}");
        }

        /// <summary>
        /// Renders a typecast expression as VHDL
        /// </summary>
        /// <param name="state">The state of the render</name>
        /// <param name="typeCast">The expression to render</param>
        /// <returns>A VHDL fragment for the expression</returns>
        public string RenderTypeCast(RenderState state, AST.TypeCast typeCast)
        {
            // TODO: Extract function also
            var scope = GetLocalScope(state);
            var assignedTypes = GetLocalAssignedTypes(state);

            var destType = ValidationState.ResolveTypeName(typeCast.TargetName, scope);
            var sourceType = assignedTypes[typeCast.Expression];

            if (object.Equals(destType, sourceType))
                return RenderExpression(state, typeCast.Expression);

            // Source is an unbounded integer
            if (sourceType.IsInteger && sourceType.BitWidth == -1)
            {
                if (destType.Type == ILType.SignedInteger)
                    return $"TO_SIGNED({RenderExpression(state, typeCast.Expression)}, {destType.BitWidth})";
                else if (destType.Type == ILType.UnsignedInteger)
                    return $"TO_UNSIGNED({RenderExpression(state, typeCast.Expression)}, {destType.BitWidth})";
                else if (destType.Type == ILType.Bool)
                    return $"TO_BOOL({RenderExpression(state, typeCast.Expression)})";
                else
                    throw new ArgumentException($"Unable to type-cast from {sourceType} to {destType}");
            }

            // Source is a bounded integer
            else if (sourceType.IsInteger)
            {
                if (destType.IsInteger)
                {
                    // Bounded to unbounded
                    if (destType.BitWidth == -1)
                        return $"TO_INTEGER({RenderExpression(state, typeCast.Expression)})";

                    // Same type, just needs a resize
                    if (sourceType.Type == destType.Type)
                        return $"RESIZE({RenderExpression(state, typeCast.Expression)}, {destType.BitWidth})";

                    // Needs resize and sign conversion
                    return $"TO_{(destType.Type == ILType.UnsignedInteger ? "UNSIGNED" : "SIGNED")}(TO_INTEGER({RenderExpression(state, typeCast.Expression)}), {destType.BitWidth})";
                }
                else if (destType.Type == ILType.Bool)
                    return $"TO_BOOL({RenderExpression(state, typeCast.Expression)})";
                else
                    throw new ArgumentException($"Unable to type-cast from {sourceType} to {destType}");
            }

            throw new ArgumentException($"Unable to type-cast from {sourceType} to {destType}");
        }

        /// <summary>
        /// Emits a VHDL type from a generic SMEIL type
        /// </summary>
        /// <param name="type">The SMEIL type to map to VHDL</param>
        /// <returns>The VHDL type string</returns>
        public string RenderNativeType(DataType type)
        {
            // We use the "clean" types internally, see:
            // https://www.thecodingforums.com/threads/why-not-use-boolean-all-the-time-for-synthesis.22866/
            if (type.Type == AST.ILType.Bool)
                return "BOOLEAN";
            else if (type.Type == AST.ILType.SignedInteger)
                return type.BitWidth == -1 ? "INTEGER" : $"SIGNED({type.BitWidth - 1} DOWNTO 0)";
            else if (type.Type == AST.ILType.UnsignedInteger)
                return type.BitWidth == -1 ? "INTEGER" : $"UNSIGNED({type.BitWidth - 1} DOWNTO 0)";
            else if (type.Type == AST.ILType.Float)
                throw new Exception("Float types are not yet supported");
            else if (type.Type == AST.ILType.Bus)
                throw new Exception("Cannot declare a bus type");
            else if (type.Type == AST.ILType.Enumeration)
                return GlobalNames[type.EnumType];

            throw new Exception($"Unexpected type: {type}");
        }

        /// <summary>
        /// Emits a VHDL type from a generic SMEIL type
        /// </summary>
        /// <param name="type">The SMEIL type to map to VHDL</param>
        /// <returns>The VHDL type string</returns>
        public string RenderExportType(DataType type)
        {
            if (type.Type == AST.ILType.Bool)
                return "STD_LOGIC";
            else if (type.Type == AST.ILType.SignedInteger)
                return type.BitWidth == -1 ? throw new Exception($"Cannot export a signal of type integer") : $"STD_LOGIC_VECTOR({type.BitWidth - 1} DOWNTO 0)";
            else if (type.Type == AST.ILType.UnsignedInteger)
                return type.BitWidth == -1 ? throw new Exception($"Cannot export a signal of type integer") : $"STD_LOGIC_VECTOR({type.BitWidth - 1} DOWNTO 0)";
            else if (type.Type == AST.ILType.Float)
                throw new Exception("Float types are not yet supported");
            else if (type.Type == AST.ILType.Bus)
                throw new Exception("Cannot declare a bus type");
            else if (type.Type == AST.ILType.Enumeration)
                throw new Exception("Cannot export an enumeration type");

            throw new Exception($"Unexpected type: {type}");
        }

        /// <summary>
        /// Creats a comparable string that describes the argument types for a function
        /// </summary>
        /// <param name="state">The current render state</param>
        /// <param name="f">The function invocation to use</param>
        /// <returns>The comparable string</returns>
        public string FunctionSignature(RenderState state, Instance.FunctionInvocation f)
        {
            return RenderFunctionArguments(state, f);
        }    
    }
}