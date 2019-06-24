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
            TestAdder_core("../../../smeil/simple/adder_connect_explicit_signals.sme");
        }

        [TestMethod]
        public void TestAdderConnectInOut()
        {
            TestAdder_core("../../../smeil/simple/adder_connect_inout.sme");
        }

        [TestMethod]
        public void TestAdderConnectTyped()
        {
            TestAdder_core("../../../smeil/simple/adder_connect_typed.sme");
        }
        [TestMethod]

        public void TestAdderConnectUntyped()
        {
            TestAdder_core("../../../smeil/simple/adder_connect_untyped.sme");
        }
    }
}