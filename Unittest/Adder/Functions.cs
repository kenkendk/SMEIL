using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Unittest.Adder
{
    [TestClass]
    public class Functions : AdderBase
    {
        [TestMethod]
        public void TestAdderFunction()
        {
            TestAdder_core("../../../smeil/function/adder_function.sme");
        }

        [TestMethod]
        public void TestAdderFunctionMultiMerged()
        {
            TestAdder_core("../../../smeil/function/adder_function_multi_merged.sme");
        }

        [TestMethod]
        public void TestEnumConversions()
        {
            TestAdder_core("../../../smeil/function/enum_conversions.sme");
        }

    }
}