using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMEIL.Parser;

namespace Unittest
{
    [TestClass]
    public class SimpleTests
    {
        [TestMethod]
        public void TestAdderTyped()
        {
            TestAdder_core("../../../smeil/simple/adder_typed.sme");
        }

        [TestMethod]
        public void TestAdderUntyped()
        {
            TestAdder_core("../../../smeil/simple/adder_untyped.sme");
        }

        [TestMethod]
        public void TestAdderIdUntyped()
        {
            TestAdderId_core("../../../smeil/simple/adder_id_untyped.sme");
        }

        [TestMethod]
        public void TestAdderIdTyped()
        {
            TestAdderId_core("../../../smeil/simple/adder_id_typed.sme");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TestAdderSyntaxError1()
        {
            try
            {
                Loader.LoadModuleAndImports("../../../smeil/error/syntax/adder_syntax_error1.sme", null, null);
            }
            catch (ParserException pex)
            {
                if (pex.Location.Line != 1 || pex.Location.LineOffset != 24 || pex.Location.Text != "}")
                    throw new ArgumentException("Incorrect reported error position", pex);
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TestAdderSyntaxError2()
        {
            try
            {
                Loader.LoadModuleAndImports("../../../smeil/error/syntax/adder_syntax_error2.sme", null, null);
            }
            catch (ParserException pex)
            {
                if (pex.Location.Line != 1 || pex.Location.LineOffset != 12 || pex.Location.Text != "{")
                    throw new ArgumentException("Incorrect reported error position", pex);
                throw;
            }
        }

        private void TestAdderId_core(string path)
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
            var outbus_type = inbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, target));
            Assert.IsTrue(object.Equals(outbus_type, target));
        }

        private void TestAdder_core(string path)
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
            var outbus_type = inbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, target));
            Assert.IsTrue(object.Equals(outbus_type, target));
        }



        private static void ParseAndGenerate(string source)
        {

        }
    }
}
