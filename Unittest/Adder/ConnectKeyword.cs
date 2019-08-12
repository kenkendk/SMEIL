using System;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMEIL.Parser;

namespace Unittest.Adder
{
    [TestClass]
    public class ConnectKeyword : AdderBase
    {
        [TestMethod]
        public void TestAdderConnectExplicit()
        {
            TestAdderConnect_core("../../../smeil/simple/adder_connect_explicit_signals.sme");
        }

        [TestMethod]
        public void TestAdderConnectInOut()
        {
            TestAdderConnectIO_core("../../../smeil/simple/adder_connect_inout.sme");
        }

        [TestMethod]
        public void TestAdderConnectTyped()
        {
            TestAdderConnect_core("../../../smeil/simple/adder_connect_typed.sme");
        }
        [TestMethod]

        public void TestAdderConnectUntyped()
        {
            TestAdderConnect_core("../../../smeil/simple/adder_connect_untyped.sme");
        }

        public static void TestAdderConnect_core(string path)
        {
            var state = Loader.LoadModuleAndImports(path, null, null);
            state.Validate();

            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().Count());
            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().Count());

            var mainproc = state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().First();
            var mainnet = state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().First();
            
            var inbus = state.FindSymbol("inbus", state.LocalScopes[mainproc]) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Input bus instance was of wrong type");
            var outbus = state.FindSymbol("dest", state.LocalScopes[mainnet]) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Output bus instance was of wrong type");
            var target = state.ResolveTypeName("tdata", state.LocalScopes[state.TopLevel.Module])
                ?? throw new ArgumentException("tdata definition was of wrong type");

            Assert.AreEqual(inbus, state.FindSymbol("plusone_net.plusone_inst.inbus", state.LocalScopes[state.TopLevel.Module]));
            Assert.AreEqual(outbus, state.FindSymbol("plusone_net.dest", state.LocalScopes[state.TopLevel.Module]));

            var inbus_type = inbus.ResolvedType;
            var outbus_type = outbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, target));
            Assert.IsTrue(object.Equals(outbus_type, target));

            GenerateVHDLAndVerify(path);
        }

        public static void TestAdderConnectIO_core(string path)
        {
            var state = Loader.LoadModuleAndImports(path, null, null);
            state.Validate();

            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().Count());
            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().Count());

            var mainproc = state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().First();
            var mainnet = state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().First();

            var inbus = state.FindSymbol("source", state.LocalScopes[mainnet]) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Input bus instance was of wrong type");
            var outbus = state.FindSymbol("dest", state.LocalScopes[mainnet]) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Output bus instance was of wrong type");
            var target = state.ResolveTypeName("tdata", state.LocalScopes[state.TopLevel.Module])
                ?? throw new ArgumentException("tdata definition was of wrong type");

            var inbus_type = inbus.ResolvedType;
            var outbus_type = outbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, target));
            Assert.IsTrue(object.Equals(outbus_type, target));

            GenerateVHDLAndVerify(path);
        }
    }
}