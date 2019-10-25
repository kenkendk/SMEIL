using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMEIL.Parser;

namespace Unittest.Adder
{
    [TestClass]
    public class Busses : AdderBase
    {
        [TestMethod]
        public void TestSignalDirection()
        {
            Direction_core("../../../smeil/busses/signal_direction.sme");
        }

        public static void Direction_core(string path)
        {
            var state = Loader.LoadModuleAndImports(path, null, null);
            state.Validate();

            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Network>().Count());
            Assert.AreEqual(1, state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().Count());

            var mainproc = state.AllInstances.OfType<SMEIL.Parser.Instance.Process>().First();
            var scope = state.LocalScopes[mainproc];
            var inbus = state.FindSymbol("upstream", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Input bus instance was of wrong type");
            var outbus = state.FindSymbol("downstream", scope) as SMEIL.Parser.Instance.Bus
                ?? throw new ArgumentException("Output bus instance was of wrong type");
            var target = state.ResolveTypeName("ext_proto", scope)
                ?? throw new ArgumentException("ext_proto definition was of wrong type");

            var inbus_type = inbus.ResolvedType;
            var outbus_type = outbus.ResolvedType;

            Assert.IsTrue(object.Equals(inbus_type, target));
            Assert.IsTrue(object.Equals(outbus_type, target));

            GenerateVHDLAndVerify(path);
        }
    }
}