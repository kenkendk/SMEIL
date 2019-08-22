using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SMEIL.Parser.Codegen.VHDL
{
    /// <summary>
    /// Helper class for creating VHDL output files
    /// </summary>
    public static class OutputGenerator
    {
        /// <summary>
        /// Creates the VHDL output files
        /// </summary>
        /// <param name="state">The validated network to create the files for</param>
        /// <param name="outputfolder">The folder to use as the base for the VHDL output</param>
        /// <param name="options">The options for creating the code</param>
        public static void CreateFiles(Validation.ValidationState state, string outputfolder, Program.Options options)
        {
            if (options.GHDLStandard != "93" && options.GHDLStandard != "93c")
                throw new ParserException("Only 93 and 93c are currently supported", null);
            
            var vhdlout = Path.GetFullPath(Path.Combine(outputfolder, options.VHDLOutputFolder));
            if (options.ClearTargetFolder && Directory.Exists(vhdlout))
            {
                if (string.IsNullOrWhiteSpace(options.VHDLOutputFolder) || options.VHDLOutputFolder.Trim().StartsWith("/"))
                    throw new ArgumentException($"Refusing to delete folder, please supply a relative path: {options.VHDLOutputFolder}");
                Directory.Delete(vhdlout, true);
            }

            if (!Directory.Exists(vhdlout))
                Directory.CreateDirectory(vhdlout);

            var config = new RenderConfig();
            if (options.CreateAocFiles)
            {
                Console.WriteLine("Note: aoc file generation activated, forcing options to be compatible with aoc output");
                config.REMOVE_ENABLE_FLAGS = true;
                config.RESET_SIGNAL_NAME = "resetn";
                config.CLOCK_SIGNAL_NAME = "clock";
                config.RESET_ACTIVE_LOW = true;
                options.VHDLFileExtensions = "vhd";
            }

            var generator = new Codegen.VHDL.VHDLGenerator(state, config)
            {
                CSVTracename = options.TraceFile,
                Ticks = File.Exists(options.TraceFile) ? File.ReadAllLines(options.TraceFile).Count() + 2 : 100,
                CustomFiles = options.ExtraVHDLFiles
            };

            var rs = new Codegen.VHDL.VHDLGenerator.RenderState();

            var export = generator.GenerateExportModule(rs);
            File.WriteAllText(Path.Combine(vhdlout, Path.ChangeExtension("export.vhdl", options.VHDLFileExtensions)), export);

            var tdoc = generator.GenerateMainModule(rs);
            File.WriteAllText(Path.Combine(vhdlout, Path.ChangeExtension("toplevel.vhdl", options.VHDLFileExtensions)), tdoc);

            var tbdoc = generator.GenerateTestbench(rs);
            File.WriteAllText(Path.Combine(vhdlout, Path.ChangeExtension("testbench.vhdl", options.VHDLFileExtensions)), tbdoc);

            var custtypes = generator.GenerateCustomTypes(rs);
            File.WriteAllText(Path.Combine(vhdlout, Path.ChangeExtension("customtypes.vhdl", options.VHDLFileExtensions)), custtypes);

            var filenames = new Dictionary<Instance.Process, string>();
            foreach (var p in generator.AllRenderedProcesses)
            {
                var doc = generator.GenerateProcess(rs, p);
                var fn = filenames[p] = generator.ProcessNames[p];
                File.WriteAllText(Path.Combine(vhdlout, Path.ChangeExtension(fn + ".vhdl", options.VHDLFileExtensions)), doc);
            }

            if (options.CreateXpf)
            {
                var txpf = generator.GenerateXpf(rs, filenames, options.VHDLFileExtensions);
                File.WriteAllText(Path.Combine(vhdlout, $"project.xpf"), txpf);
            }

            if (options.CreateAocFiles)
            {
                var aoclproj = generator.GenerateAocl(rs, filenames, options.VHDLFileExtensions);
                File.WriteAllText(Path.Combine(vhdlout, $"opencl_lib.xml"), aoclproj);
            }

            var makefile = generator.GenerateMakefile(rs, filenames, options.GHDLStandard, options.VHDLFileExtensions);
            File.WriteAllText(Path.Combine(vhdlout, $"Makefile"), makefile);

            generator.CopySupportFiles(vhdlout);               
        }
    }
}