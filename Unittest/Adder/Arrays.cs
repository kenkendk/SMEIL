using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMEIL.Parser;

namespace Unittest.Adder
{
    [TestClass]
    public class Arrays : AdderBase
    {
        [TestMethod]
        public void ArraySummation()
        {
            TestAdder_Array("../../../smeil/array/array_sum.sme");
        }

        [TestMethod]
        public void FirExample()
        {
            TestAdder_Array("../../../smeil/array/array_fir.sme");
        }

        public static void TestAdder_Array(string path)
        {
            var state = Loader.LoadModuleAndImports(path, null, null);
            state.Validate();

            GenerateVHDLAndVerify(path);
        }
    }
}