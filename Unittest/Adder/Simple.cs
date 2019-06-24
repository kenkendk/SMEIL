using System;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMEIL.Parser;

namespace Unittest.Adder
{
    [TestClass]
    public class Simple : AdderBase
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
        public void TestAdderExpanded()
        {
            TestAdderExpanded_core("../../../smeil/simple/adder_id_untyped_expanded.sme");
        }

        [TestMethod]
        public void TestAdderInOutTyped()
        {
            TestAdderDualId_core("../../../smeil/simple/adder_inout_untyped.sme");
        }

        [TestMethod]
        public void TestAdderExtraWhitespace()
        {
            TestAdder_core("../../../smeil/simple/adder_extra_whitespace.sme");
        }

    }
}