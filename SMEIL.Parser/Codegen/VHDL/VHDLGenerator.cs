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
        /// The list of all busses
        /// </summary>
        public readonly List<Instance.Bus> AllBusses;

        /// <summary>
        /// The list of all processes
        /// </summary>
        public readonly List<Instance.Process> AllProcesses;

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
        public VHDLGenerator(Validation.ValidationState validationstate)
        {
            ValidationState = validationstate;

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
                    Name = x.Name + (buscounters[x.Source].Count == 1 ? "" : "_" + (buscounters[x.Source].IndexOf(x) + 1).ToString())
                })
                .ToDictionary(x => x.Key, x => x.Name);

            // List of instantiated busses
            AllProcesses = validationstate
                .AllInstances
                .OfType<Instance.Process>()
                .Distinct()
                .ToList();

            // Figure out which instances are from the same source declaration
            var proccounters = AllProcesses.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.ToList());

            // Give the instances names, suffixed with the instance number if there are more than one
            ProcessNames = AllProcesses
                .Select(x => new
                {
                    Key = x,
                    Name = x.Name + (proccounters[x.Name].Count == 1 ? "" : "_" + (proccounters[x.Name].IndexOf(x) + 1).ToString())
                })
                .ToDictionary(x => x.Key, x => x.Name);
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
            return string.Join(Environment.NewLine, lines.Select(x => (state.Indent + x).TrimEnd())) + Environment.NewLine;
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
        /// <returns>The generated Makefile</returns>
        public string GenerateMakefile(RenderState state, Dictionary<Instance.Process, string> filenames, string standard)
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
                $"$(WORKDIR)/customtypes.o: customtypes.vhdl $(WORKDIR)",
                $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) customtypes.vhdl",
                ""
            );

            foreach (var file in filenames.Values)
            {
                decl += RenderLines(state,
                    $"$(WORKDIR)/{file}.o: {file}.vhdl $(WORKDIR)/customtypes.o $(WORKDIR){cust_tag}",
                    $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) {file}.vhdl",
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
                $"$(WORKDIR)/toplevel.o: toplevel.vhdl $(WORKDIR)/customtypes.o {string.Join(" ", filenames.Values.Select(x => $"$(WORKDIR)/{x}.o"))}{cust_tag}",
                $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) toplevel.vhdl",
                "",
                $"$(WORKDIR)/testbench.o: testbench.vhdl $(WORKDIR)/toplevel.o",
                $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) testbench.vhdl",
                "",
                $"{name}_tb: $(WORKDIR)/testbench.o",
                $"\tghdl -e --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) {name}_tb",
                "",
                $"export: $(WORKDIR)/toplevel.o",
                $"\tghdl -a --std=$(STD) --ieee=$(IEEE) --workdir=$(WORKDIR) export.vhdl",
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

            using(state.Indenter())
            {
                var consts = ValidationState.AllInstances
                    .OfType<Instance.ConstantReference>()
                    .Select(x => x.Source)
                    .Distinct();

                if (consts.Any())
                {
                    decl += RenderLines(state,
                        "-- Constant definitions",
                        ""
                    );

                    decl += RenderLines(state,
                        consts.Select(c => 
                            $"constant {c.Name}: {c.DataType} := {c.Expression};"
                        )
                    );
                }

                // var enums = ValidationState.AllInstances
                //     .OfType<Instance.Variable>()
                //     .Select(x => x.Source)
                //     .Select(x => x.Type.IntrinsicType)
                //     .Where(x => x != null)
                //     .Select(x => x.IsEnum)
                //     .Distinct();

                // if (enums.Any())
                // {
                //     decl += RenderLines(state,
                //         "-- Enum definitions",
                //         ""
                //     );

                //     decl += RenderLines(state,
                //         enums.Select(c =>
                //             $"type {c.Name} is {string.Join(", ", c.Values)};"
                //         )
                //     );

                // }
                

            }

            decl += RenderLines(state,
                "-- User defined types here",
                "-- #### USER-DATA-TRAILTYPES-START",
                "-- #### USER-DATA-TRAILTYPES-END",
                "",
                "end CUSTOM_TYPES;",
                ""
            );

            return decl;
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
                    "signal ENABLE : Std_logic;",
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
                        "ENB => ENABLE,",
                        "RST => RESET,",
                        "CLK => CLOCK"
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
                            "RESET <= '1';",
                            "ENABLE <= '0';",
                            "wait for 5 NS;",
                            "RESET <= '0';",
                            "ENABLE <= '1';",
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
        /// Creates a Xilinx Vivado .xpf for testing the generated code with GHDL
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="filenames">The filenames assigned to the processes</param>
        /// <returns>The generated xpf file</returns>
        public string GenerateXpf(RenderState state, Dictionary<Instance.Process, string> filenames)
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
                "      <File Path=\"$PPRDIR/system_types.vhdl\">",
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
                    $"      <File Path=\"$PPRDIR/{file}.vhdl\">",
                    "        <FileInfo>",
                    "          <Attr Name=\"Library\" Val=\"xil_defaultlib\"/>",
                    "          <Attr Name=\"UsedIn\" Val=\"synthesis\"/>",
                    "          <Attr Name=\"UsedIn\" Val=\"simulation\"/>",
                    "        </FileInfo>",
                    "      </File>"
                );
            }

            decl += RenderLines(state,
                $"      <File Path=\"$PPRDIR/{name}.vhdl\">",
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
                "      <File Path=\"$PPRDIR/csv_util.vhdl\">",
                "        <FileInfo>",
                "          <Attr Name=\"Library\" Val=\"xil_defaultlib\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"synthesis\"/>",
                "          <Attr Name=\"UsedIn\" Val=\"simulation\"/>",
                "        </FileInfo>",
                "      </File>",
                $"      <File Path=\"$PPRDIR/TestBench_{name}.vhdl\">",
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
                            "",
                            "-- Enable signal",
                            "ENB: in STD_LOGIC;",
                            "",
                            "--Reset signal",
                            "RST : in STD_LOGIC;",
                            "",
                            "--Finished signal",
                            "FIN : out STD_LOGIC;",
                            "",
                            "--Clock signal",
                            "CLK : in STD_LOGIC"
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
                        "-- User defined signals here",
                        "-- #### USER-DATA-SIGNALS-START",
                        "-- #### USER-DATA-SIGNALS-END"
                    );

                    // For signals that require type casts, prefix the internal signals with 'ext'
                    // and register a local signal to carry the un-converted value
                    foreach (var n in typeconvertedbusses)
                        decl += RenderBusSignals(state, n, exportnames[n] = "ext_" + exportnames[n]);

                    decl += RenderLines(state, "");
                }

                decl += RenderLines(state, "begin");

                using(state.Indenter())
                {
                    decl += RenderLines(state, "-- Write out any converted signals with the correct type");

                    // Forward type converted input/output signals
                    foreach (var bus in typeconvertedbusses)
                        decl += RenderLines(state,
                            bus.Instances
                            .OfType<Instance.Signal>()
                            .Select(x =>
                            {
                                return ValidationState.TopLevel.InputBusses.Contains(x.ParentBus)
                                    ? $"{RenderSignalName(exportnames[bus], x.Name)} <= {(x.ResolvedType.Type == ILType.SignedInteger ? "SIGNED" : "UNSIGNED")}({RenderSignalName(exportnames[bus].Substring("ext_".Length), x.Name)});"
                                    : $"{RenderSignalName(exportnames[bus].Substring("ext_".Length), x.Name)} <= STD_LOGIC_VECTOR({RenderSignalName(exportnames[bus], x.Name)});";
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
                            "ENB => ENB,",
                            "RST => RST,",
                            "FIN => FIN,",
                            "CLK => CLK"
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

                var internalbusses = AllBusses
                    .Where(x =>
                        !ValidationState.TopLevel.InputBusses.Contains(x)
                        &&
                        !ValidationState.TopLevel.OutputBusses.Contains(x)
                    ).ToArray();

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
                            "ENB: in STD_LOGIC;",
                            "",
                            "--Finished signal",
                            "FIN : out STD_LOGIC;",
                            "",
                            "--Reset signal",
                            "RST : in STD_LOGIC;",
                            "",
                            "--Clock signal",
                            "CLK : in STD_LOGIC"
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
                        ValidationState
                        .AllInstances
                        .OfType<Instance.Process>()
                        .SelectMany(proc => new string[] {
                            $"signal {RenderIdentifier(state, "RDY_", proc.DeclarationSource.Name.Name, null)}: STD_LOGIC;",
                            $"signal {RenderIdentifier(state, "FIN_", proc.DeclarationSource.Name.Name, null)}: STD_LOGIC;",
                        })
                    );

                    decl += RenderLines(state,
                        "",
                        "-- The primary ready driver signal",
                        "signal RDY: STD_LOGIC;",
                        "",
                        "-- Ready flag flip signal",
                        "signal readyflag: STD_LOGIC;",
                        ""
                    );
                }

                decl += RenderLines(state, "begin");

                using(state.Indenter())
                {
                    foreach (var inst in ValidationState.AllInstances.OfType<Instance.Process>())
                        decl += RenderProcessInstantiation(state, inst);

                    decl += RenderLines(state, 
                        "-- Connect RDY signals"
                    );

                    decl += RenderLines(state,
                        ValidationState.DependencyGraph.Select(
                            x => {
                                var depends = x.Value;
                                var selfsignal = RenderIdentifier(state, "RDY_", x.Key.DeclarationSource.Name.Name, null);
                                if (depends.Length == 0)
                                    return $"{selfsignal} <= RDY;";

                                var depsignals = 
                                    string.Join(
                                        " and ", 
                                        x.Value
                                            .Select(
                                                y => RenderIdentifier(state, "FIN_", y.DeclarationSource.Name.Name, null)
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
                    // We could do reverse lookup in the DependencyGraph, 
                    // but lookup via the schedule is easier
                    var finsignals =
                        string.Join(
                            " and ",
                            ValidationState.SuggestedSchedule
                                .Last()
                                .Select(
                                    y => RenderIdentifier(state, "FIN_", y.DeclarationSource.Name.Name, null)
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

                    decl += RenderLines(state,
                        "",
                        "-- Propagate all clocked and feedback signals",
                        "process(CLK, RST)",
                        "begin",
                        "    if RST = '1' then",
                        "        RDY <= '0';",
                        "        readyflag <= '1';",
                        "    elsif rising_edge(CLK) then",
                        "        if ENB = '1' then",
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
        /// Renders the instantiation of a process
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="proc">The instance to render</param>
        /// <returns>The redered instance</returns>
        public string RenderProcessInstantiation(RenderState state, Instance.Process proc)
        {
            var name = ProcessNames[proc];
            var decl = RenderLines(state,
                $"-- Entity {name} from {proc.DeclarationSource.Name.Name}",
                $"{name}: entity work.{name}",
                "port map ("
            );

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
                    $"CLK => CLK,",
                    $"RDY => {RenderIdentifier(state, "RDY_", proc.DeclarationSource.Name.Name, null)},",
                    $"FIN => {RenderIdentifier(state, "FIN_", proc.DeclarationSource.Name.Name, null)},",
                    $"ENB => ENB,",
                    $"RST => RST"
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
                            "CLK : in STD_LOGIC;",
                            "",
                            "--Ready signal",
                            "RDY : in STD_LOGIC;",
                            "",
                            "--Finished signal",
                            "FIN : out STD_LOGIC;",
                            "",
                            "--Enable signal",
                            "ENB : in STD_LOGIC;",
                            "",
                            "--Reset signal",
                            "RST : in STD_LOGIC"
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
                            "RST",
                            ""
                        );
                    }
                    impl += RenderLines(state, ")");

                    if (process.Instances.OfType<Instance.Variable>().Any())
                    {
                        impl += RenderLines(state,
                            "--Internal variables",
                            ""
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
                        "variable reentry_guard: STD_LOGIC;",
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

                        impl += RenderLines (state, $"if RST = '1' then");

                        using(state.Indenter())
                        {
                            // Emit reset statements
                            RenderStatements(
                                state, 

                                process
                                    .MappedParameters
                                    .Select(x => x.MappedItem)
                                    .OfType<Instance.Variable>()
                                    .Select(x => {
                                        var resetvalue = DefaultValue(x);
                                        var expr = TypeCast(resetvalue, x.ResolvedType);

                                        return new AST.AssignmentStatement(
                                            pdef.SourceToken, 
                                            x.Source.Name.AsName(),
                                            expr
                                        );
                                    })
                                    .ToArray()
                            );

                            impl += RenderLines(state,
                                "reentry_guard := '0';",
                                "FIN <= '0';",
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
                            impl += RenderStatements(state, pdef.Statements);

                            impl += RenderLines(state,
                                "reentry_guard:= RDY;",
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
        /// Injects a typecast if the source type is another type of the target
        /// </summary>
        /// <param name="expression">The expression to cast</param>
        /// <param name="targettype">The destination type</param>
        /// <param name="sourcetype">The source type</param>
        /// <returns>An expression that could be a type cast</returns>
        public AST.Expression TypeCast(AST.LiteralExpression expression, DataType targettype)
        {
            if (expression.Value is AST.BooleanConstant && targettype.IsBoolean)
                return expression;
            else if (expression.Value is AST.FloatingConstant && targettype.IsFloat)
                return expression;
            
            if (expression.Value is AST.IntegerConstant)
                return new AST.TypeCast(expression, targettype, false);

            throw new ParserException($"Unable to cast expression with {expression.Value} to {targettype}", expression);
        }

        /// <summary>
        /// Returns the default value for the variable
        /// </summary>
        /// <param name="variable">The variable to get the default value from</param>
        /// <returns>A literal expression that is the default value</returns>
        public AST.LiteralExpression DefaultValue(Instance.Variable variable)
        {
            var decl = variable.Source;
            if (decl.Initializer == null)
            {
                // Get the default value for the type
                if (variable.ResolvedType.IsBoolean)
                    return new AST.LiteralExpression(decl.SourceToken, new AST.BooleanConstant(decl.SourceToken, false));
                if (variable.ResolvedType.IsInteger)
                    return new AST.LiteralExpression(decl.SourceToken, new AST.IntegerConstant(decl.SourceToken, "0"));
                if (variable.ResolvedType.IsFloat)
                    return new AST.LiteralExpression(decl.SourceToken, new AST.FloatingConstant(decl.SourceToken, "0", "0"));

                throw new ParserException("No initial value for variable", decl);
            }
            else
            {
                if (decl.Initializer is AST.LiteralExpression initExpr)
                    return initExpr;
            }

            throw new ParserException("No initial value for variable", decl);
        }

        /// <summary>
        /// A regex for detecting non-alpha numeric names
        /// </summary>
        private static Regex RX_ALPHANUMERIC = new Regex(@"[^\u0030-\u0039|\u0041-\u005A|\u0061-\u007A]");

        /// <summary>
        /// Cleans up a VHDL component or variable name
        /// </summary>
        /// <param name="name">The name to sanitize</param>
        /// <returns>The sanitized name</returns>
        public static string SanitizeVHDLName(string name)
        {
            var r = RX_ALPHANUMERIC.Replace(name, "_");
            if (new string[] { "register", "record", "variable", "process", "if", "then", "else", "begin", "end", "architecture", "of", "is", "wait" }.Contains(r.ToLowerInvariant()))
                r = "vhdl_" + r;

            while (r.IndexOf("__", StringComparison.Ordinal) >= 0)
                r = r.Replace("__", "_");

            return r.TrimEnd('_');
        }

        /// <summary>
        /// Renders a variable declaration
        /// </summary>
        /// <param name="state">The render state</param>
        /// <param name="variable">The variable to render</param>
        /// <returns>A VHDL fragment for declaring a variable</returns>
        public string RenderVariable(RenderState state, Instance.Variable variable)
        {
            return $"variable {SanitizeVHDLName(variable.Name)}: {RenderNativeType(variable.ResolvedType)};";
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
            var busname = parmap == null ? bus.Name : parmap.LocalName;

            // Normally signals are in or out
            var signals = 
                bus
                .Instances
                .OfType<Instance.Signal>()
                .Where(x => usages.ContainsKey(x))
                .SelectMany(x => {
                    var d = usages[x];
                    if (d == Validation.ItemUsageDirection.Both)
                    {
                        return new[] { 
                            $"{RenderSignalName(busname, x.Name, "in")}: in {RenderNativeType(x.ResolvedType)};",
                            $"{RenderSignalName(busname, x.Name, "out")}: out {RenderNativeType(x.ResolvedType)};"
                        };
                    }
                    else
                        return new[] { $"{RenderSignalName(busname, x.Name)}: {(usages[x] == Validation.ItemUsageDirection.Read ? "in" : "out")} {RenderNativeType(x.ResolvedType)};" };
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
            var process = state.ActiveScopes.OfType<Instance.Process>().Last();
            var symboltable = ValidationState.LocalScopes[process];
            var symbol = ValidationState.FindSymbol(assignStatement.Name, symboltable);

            return $"{state.Indent}{ RenderName(state, assignStatement.Name) } {( symbol is Instance.Signal ? "<=" : ":=" )} { RenderExpression(state, assignStatement.Value) };";
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
                    + $"{indent}endif;";
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
                    $"{ indent }when { RenderExpression(state, item.Item1) } => {Environment.NewLine}"
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
                    + $"{indent}end case;";
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
                    return "(" + RenderExpression(state, typeCastExpression.Expression) + ")";
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
            return SanitizeVHDLName(string.Join("_", name.Identifier.Select(x => RenderIdentifier(state, x))));
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
                    return booleanConstant.Value ? "1" : "0";
                case AST.StringConstant stringConstant:
                    return "\"" + stringConstant.Value + "\"";
            }

            throw new ArgumentException($"Unable to render constant of type: {constant.GetType()}");
        }

        /// <summary>
        /// Emits a VHDL type from a generic SMEIL type
        /// </summary>
        /// <param name="type">The SMEIL type to map to VHDL</param>
        /// <returns>The VHDL type string</returns>
        public string RenderNativeType(DataType type)
        {
            if (type.Type == AST.ILType.Bool)
                return "STD_LOGIC";
            else if (type.Type == AST.ILType.SignedInteger)
                return type.BitWidth == -1 ? "INTEGER" : $"SIGNED({type.BitWidth - 1} DOWNTO 0)";
            else if (type.Type == AST.ILType.UnsignedInteger)
                return type.BitWidth == -1 ? "INTEGER" : $"UNSIGNED({type.BitWidth - 1} DOWNTO 0)";
            else if (type.Type == AST.ILType.Float)
                throw new Exception("Float types are not yet supported");
            else if (type.Type == AST.ILType.Bus)
                throw new Exception("Cannot declare a bus type");

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

            throw new Exception($"Unexpected type: {type}");
        }        
    }
}