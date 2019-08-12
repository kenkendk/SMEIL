using System;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMEIL.Parser;

namespace Unittest.Adder
{
    /// <summary>
    /// Helper module for tests based on a simple addition process
    /// </summary>
    public class AdderBase
    {
        public static void TestAdderExpanded_core(string path)
        {
            var state = Loader.LoadModuleAndImports(path, null, null);
            state.Validate();

            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().Count());
            Assert.AreEqual(2, state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().Count());

            var mainproc = state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().First(x => x.SourceName == "id");
            var scope = state.LocalScopes[mainproc];
            var inbus = state.FindSymbol("inbus", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Input bus instance was of wrong type");
            var outbus = state.FindSymbol("outbus", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Output bus instance was of wrong type");
            var target1 = state.ResolveTypeName("tdata1", scope)
                ?? throw new ArgumentException("tdata1 definition was of wrong type");
            var target2 = state.ResolveTypeName("tdata2", scope)
                ?? throw new ArgumentException("tdata2 definition was of wrong type");

            Assert.AreEqual(inbus, state.FindSymbol("plusone_net.id_inst.inbus", state.LocalScopes[state.TopLevel.Module]));
            Assert.AreEqual(outbus, state.FindSymbol("plusone_net.id_inst.outbus", state.LocalScopes[state.TopLevel.Module]));

            var inbus_type = inbus.ResolvedType;
            var outbus_type = outbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, target2));
            Assert.IsTrue(object.Equals(outbus_type, target2));

            GenerateVHDLAndVerify(path);
        }

        public static void TestAdderId_core(string path)
        {
            var state = Loader.LoadModuleAndImports(path, null, null);
            state.Validate();

            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().Count());
            Assert.AreEqual(2, state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().Count());

            var mainproc = state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().First(x => x.SourceName == "id");
            var scope = state.LocalScopes[mainproc];
            var inbus = state.FindSymbol("inbus", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Input bus instance was of wrong type");
            var outbus = state.FindSymbol("outbus", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Output bus instance was of wrong type");
            var target = state.ResolveTypeName("tdata", scope)
                ?? throw new ArgumentException("tdata definition was of wrong type");

            Assert.AreEqual(inbus, state.FindSymbol("plusone_net.id_inst.inbus", state.LocalScopes[state.TopLevel.Module]));
            Assert.AreEqual(outbus, state.FindSymbol("plusone_net.id_inst.outbus", state.LocalScopes[state.TopLevel.Module]));

            var inbus_type = inbus.ResolvedType;
            var outbus_type = outbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, target));
            Assert.IsTrue(object.Equals(outbus_type, target));

            GenerateVHDLAndVerify(path);
        }

        public static void TestAdderCasting_core(string path)
        {
            var state = Loader.LoadModuleAndImports(path, null, null);
            state.Validate();

            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().Count());
            Assert.AreEqual(2, state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().Count());

            var mainproc = state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().First();
            var scope = state.LocalScopes[mainproc];
            var inbus = state.FindSymbol("inbus", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Input bus instance was of wrong type");
            var source = state.ResolveTypeName("tdata2", scope)
                ?? throw new ArgumentException("tdata definition was of wrong type");
            var target = state.ResolveTypeName("tdata1", scope)
                ?? throw new ArgumentException("tdata definition was of wrong type");

            Assert.AreEqual(inbus, state.FindSymbol("plusone_net.cutoff_inst.inbus", state.LocalScopes[state.TopLevel.Module]));
            var outbus = state.FindSymbol("plusone_net.plusone_inst.outbus", state.LocalScopes[state.TopLevel.Module]) as SMEIL.Parser.Instance.Bus;

            var inbus_type = inbus.ResolvedType;
            var outbus_type = outbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, source));
            Assert.IsTrue(object.Equals(outbus_type, target));

            GenerateVHDLAndVerify(path);
        }

        public static void TestAdder_core(string path)
        {
            var state = Loader.LoadModuleAndImports(path, null, null);
            state.Validate();

            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().Count());
            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().Count());

            var mainproc = state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().First();
            var scope = state.LocalScopes[mainproc];
            var inbus = state.FindSymbol("inbus", scope) as SMEIL.Parser.Instance.Bus 
                ?? throw new ArgumentException("Input bus instance was of wrong type");
            var outbus = state.FindSymbol("outbus", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Output bus instance was of wrong type");
            var target = state.ResolveTypeName("tdata", scope)
                ?? throw new ArgumentException("tdata definition was of wrong type");

            Assert.AreEqual(inbus, state.FindSymbol("plusone_net.plusone_inst.inbus", state.LocalScopes[state.TopLevel.Module]));
            Assert.AreEqual(outbus, state.FindSymbol("plusone_net.plusone_inst.outbus", state.LocalScopes[state.TopLevel.Module]));

            var inbus_type = inbus.ResolvedType;
            var outbus_type = outbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, target));
            Assert.IsTrue(object.Equals(outbus_type, target));

            GenerateVHDLAndVerify(path);
        }

        public static void TestAdderDualId_core(string path)
        {
            var state = Loader.LoadModuleAndImports(path, null, null);
            state.Validate();

            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().Count());
            Assert.AreEqual(3, state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().Count());

            var mainproc = state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().First(x => x.SourceName == "id");
            var scope = state.LocalScopes[mainproc];
            var inbus = state.FindSymbol("inbus", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Input bus instance was of wrong type");
            var outbus = state.FindSymbol("outbus", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Output bus instance was of wrong type");
            var target = state.ResolveTypeName("tdata", scope)
                ?? throw new ArgumentException("tdata definition was of wrong type");

            var inbus_type = inbus.ResolvedType;
            var outbus_type = inbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, target));
            Assert.IsTrue(object.Equals(outbus_type, target));

            GenerateVHDLAndVerify(path);
        }

        /// <summary>
        /// Parses the program from source and creates the VHDL output for the program
        /// </summary>
        /// <param name="source">The source file</param>
        public static void GenerateVHDLAndVerify(string source)
        {
            var outname = Path.GetFileNameWithoutExtension(source);
            var targetdir = Path.Combine("output", outname);
            var opts = new Program.Options()
            {
                EntryFile = source,
                Arguments = new string[0],
                OutputFolder = targetdir,
                ClearTargetFolder = true
            };

            Program.RunCompiler(opts);

            var vhdldir = Path.Combine(targetdir, opts.VHDLOutputFolder);

            // Run Make test and check exit code
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() {
               FileName = "make",

               // Since we do not have a trace file, we only test that the source can be build with GHDL
               Arguments = "build",
               WorkingDirectory = vhdldir
            });

            p.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
            if (!p.HasExited || p.ExitCode != 0)
                throw new Exception($"Bad exit code from \"make\" in folder {Path.GetFullPath(vhdldir)}");
        }
    }
}
