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
        /// The folder where data is place
        /// </summary>
        public readonly string TargetFolder;
        /// <summary>
        /// The folder where backups are stored
        /// </summary>
        public readonly string BackupFolder;
        /// <summary>
        /// The name of the file where a CSV trace is stored
        /// </summary>
        public readonly string CSVTracename;

        /// <summary>
        /// Sequence of custom VHDL files to include in the compilation
        /// </summary>
        public readonly IEnumerable<string> CustomFiles;

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
        }

        /// <summary>
        /// Creates the preamble for a file
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public string GenerateFilePreamble(RenderState state)
        {
            return RenderLines(
                state,

                "library IEEE;",
                "use IEEE.STD_LOGIC_1164.ALL;",
                "use IEEE.NUMERIC_STD.ALL;",
                "",
                "--library SYSTEM_TYPES;",
                "use work.SYSTEM_TYPES.ALL;",
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
        /// Returns a VHDL representation of a network
        /// </summary>
        /// <param name="state">The render stater</param>
        /// <param name="network">The network to render</param>
        /// <returns>The document representing the rendered network</returns>
        public string GenerateNetwork(RenderState state, Instance.Network network)
        {
            using (state.StartScope(network))
            {
                var ndef = network.NetworkDefinition;
                var name = SanitizeVHDLName(RenderIdentifier(state, ndef.Name, network.Name));
                var decl = RenderLines(state, $"entity {name} is");

                using (state.Indenter())
                {
                    decl += RenderLines(state, $"port(");
                    using(state.Indenter())
                    {
                        // TODO: TopLevel busses, how?

                        decl += RenderLines(state,
                            "",
                            "--User defined signals here",
                            "-- #### USER-DATA-ENTITYSIGNALS-START",
                            "-- #### USER-DATA-ENTITYSIGNALS-END",
                            "",
                            "-- Enable signal",
                            "ENB: in Std_logic;",
                            "",
                            "--Finished signal",
                            "FIN : out Std_logic;",
                            "",
                            "--Reset signal",
                            "RST : in Std_logic;",
                            "",
                            "--Clock signal",
                            "CLK : in Std_logic"
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
                    decl += RenderLines(state, 
                        "--User defined signals here",
                        "-- #### USER-DATA-SIGNALS-START",
                        "-- #### USER-DATA-SIGNALS-END",
                        ""
                    );

                    // TODO: Triggers and feedback signals

                    decl += RenderLines(state,
                        "--The primary ready driver signal",
                        "signal RDY: std_logic;",
                        ""
                    );
                }

                decl += RenderLines(state, "begin");

                using(state.Indenter())
                {
                    // foreach (var inst in network.Instances.OfType<Instance.Process>())
                    //     decl += RenderProcessInstantiation(state, inst);


                    decl += RenderLines(state, "--Connect ready signals");

                    decl += RenderLines(state, "--Setup the FIN feedback signals");
                        
                    decl += RenderLines(state,
                        "--Propagate all clocked and feedback signals",
                        "process(CLK, RST)",
                        "    signal readyflag: STD_LOGIC;",
                        "begin",
                        "    if RST = '1' then",
                        "        RDY <= '0';",
                        "        readyflag <= '1';",
                        "    elsif rising_edge(CLK) then",
                        "        if ENB = '1' then",
                        "            RDY <= not readyflag;",
                        "            readyflag <= not readyflag;",
                        "            --Forward feedback signals"
                    );

                    // Tripple indent the feedback signals
                    using(state.Indenter())
                    using (state.Indenter())
                    using (state.Indenter())
                    {

                    }

                    decl += RenderLines(state,
                        "",
                        "        end if;",
                        "    end if;",
                        "end process;",
                        ""
                    );


                    decl += RenderLines(state, "-- Send feedback outputs to the actual output");

                    decl += RenderLines(state,
                        "",
                        "--User defined processes here",
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
                var name = SanitizeVHDLName(RenderIdentifier(state, pdef.Name, process.Name));

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
                                ? (x.MappedItem as Instance.ConstantReference).Source.DataType
                                : (x.MappedItem as Instance.Variable).Source.Type
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
                                        var expr = TypeCast(resetvalue, x.Source.Type);

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

                return GenerateFilePreamble(state) + decl + impl;
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
                if (decl.Type.IsBoolean)
                    return new AST.LiteralExpression(decl.SourceToken, new AST.BooleanConstant(decl.SourceToken, false));
                if (decl.Type.IsInteger)
                    return new AST.LiteralExpression(decl.SourceToken, new AST.IntegerConstant(decl.SourceToken, "0"));
                if (decl.Type.IsFloat)
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
            return $"variable {SanitizeVHDLName(variable.Name)}: {RenderNativeType(variable.Source.Type)};";
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
                            $"{RenderSignalName(busname, x.Name, "in")}: in {RenderNativeType(x.Source.Type)};",
                            $"{RenderSignalName(busname, x.Name, "out")}: out {RenderNativeType(x.Source.Type)};"
                        };
                    }
                    else
                        return new[] { $"{RenderSignalName(busname, x.Name)}: {(usages[x] == Validation.ItemUsageDirection.Read ? "in" : "out")} {RenderNativeType(x.Source.Type)};" };
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
            return identifier.Name;
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
            return identifier.Name;
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
    }
}