using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SMEIL.Parser.AST;
using SMEIL.Parser.CommandLineOptions;

namespace SMEIL.Parser
{
    public static class Program
    {
        /// <summary>
        /// The VHDL standards that GHDL uses
        /// </summary>
        private static readonly string[] GHDL_STANDARDS = new string[] { "87", "93", "93c", "00", "02" };

        /// <summary>
        /// The options supported
        /// </summary>
        public class Options
        {
            [Option(HelpText = "The VHDL output folder", Default = "./VHDL")]
            public string VHDLOutputFolder { get; set; }
            [Option(HelpText = "The top-level output folder", Default = "output")]
            public string OutputFolder { get; set; }
            [Option(HelpText = "Create an .xpf file for loading with Xilinx Vivado", Default = true)]
            public bool CreateXpf { get; set; }
            [Option(HelpText = "The path to the CSV trace file from the simulation", Default = "trace.csv")]
            public string TraceFile { get; set; }
            [Option(HelpText = "An additional set of VHDL files that should be added to project and make files", Separator = ';')]
            public string[] ExtraVHDLFiles { get; set; }
            [Option(HelpText = "Deletes the target folder before creating the output files")]
            public bool ClearTargetFolder { get; set; }
            [Option(HelpText = "Sets the GHDL standard to use", Default = "93c")]
            public string GHDLStandard { get; set; }


            [Value(0, MetaName = "filename", HelpText = "The SMEIL filename to parse", Required = true)]
            public string EntryFile { get; set; }
            [Value(1, MetaName = "top-network", HelpText = "The top-level network to use, if the file contains multiple networks")]
            public string TopLevelNetwork { get; set; }
            [Value(2, MetaName = "top-arguments", HelpText = "Arguments for the top-level network")]
            public string[] Arguments { get; set ;}
        }

        /// <summary>
        /// The main entry point for the application
        /// </summary>
        /// <param name="args">The commandline arguments</param>
        /// <returns>an exit code</returns>
        public static int Main(string[] args)
        {
            Options options;
            try
            {
                options = CommandLineOptions.CommandLineParser.ParseCommandlineStrict<Options>(args);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 2;
            }

            if (options.GHDLStandard != "93" && options.GHDLStandard != "93c")
            {
                Console.WriteLine("Only 93 and 93c are currently supported");
            }

            try
            {                
                var state = Loader.LoadModuleAndImports(options.EntryFile, options.TopLevelNetwork, options.Arguments);
                state.Validate();

                var outputfolder = options.OutputFolder;
                if (string.IsNullOrWhiteSpace(outputfolder))
                    outputfolder = ".";

                outputfolder = Path.GetFullPath(outputfolder);
                if (!Directory.Exists(outputfolder))
                    Directory.CreateDirectory(outputfolder);

                var vhdlout = Path.GetFullPath(Path.Combine(outputfolder, options.VHDLOutputFolder));
                if (options.ClearTargetFolder)
                    Directory.Delete(vhdlout);

                if (!Directory.Exists(vhdlout))
                    Directory.CreateDirectory(vhdlout);

                var generator = new Codegen.VHDL.VHDLGenerator(state) {
                    CSVTracename = options.TraceFile,
                    Ticks = File.Exists(options.TraceFile) ? File.ReadAllLines(options.TraceFile).Count() + 2 : 100,
                    CustomFiles = options.ExtraVHDLFiles
                };

                var rs = new Codegen.VHDL.VHDLGenerator.RenderState();

                var export = generator.GenerateExportModule(rs);
                File.WriteAllText(Path.Combine(vhdlout, "export.vhdl"), export);

                var tdoc = generator.GenerateMainModule(rs);
                File.WriteAllText(Path.Combine(vhdlout, "toplevel.vhdl"), tdoc);

                var tbdoc = generator.GenerateTestbench(rs);
                File.WriteAllText(Path.Combine(vhdlout, "testbench.vhdl"), tbdoc);

                var custtypes = generator.GenerateCustomTypes(rs);
                File.WriteAllText(Path.Combine(vhdlout, "customtypes.vhdl"), custtypes);

                var filenames = new Dictionary<Instance.Process, string>();
                foreach (var nv in state.AllInstances.OfType<Instance.Network>())
                {
                    foreach (var p in nv.Instances.OfType<Instance.Process>())
                    {
                        var doc = generator.GenerateProcess(rs, p);
                        var fn = filenames[p] = generator.ProcessNames[p];
                        File.WriteAllText(Path.Combine(vhdlout, fn + ".vhdl"), doc);
                        //Console.WriteLine(doc);
                    }
                }

                if (options.CreateXpf)
                {
                    var txpf = generator.GenerateXpf(rs, filenames);
                    File.WriteAllText(Path.Combine(vhdlout, $"project.xpf"), txpf);
                }

                var makefile = generator.GenerateMakefile(rs, filenames, options.GHDLStandard);
                File.WriteAllText(Path.Combine(vhdlout, $"Makefile"), makefile);

                generator.CopySupportFiles(vhdlout);

            }
            catch (ParserException ex)
            {
                Console.WriteLine("[{0}:{1}] \"{2}\": {3}", ex.Location.Line, ex.Location.LineOffset, ex.Location.Text, ex.Message);
                return 3;
            }

            return 0;
        }
    }

    
}
