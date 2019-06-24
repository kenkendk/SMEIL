using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Unittest.Adder
{
    [TestClass]
    public class Casting : AdderBase
    {
        [TestMethod]
        public void TypeTruncationInternalTypecast()
        {
            TestAdderCasting_core("../../../smeil/casting/type_truncation_internal_typecast.sme");
        }

        [TestMethod]
        public void DownsizeCastingExplicitType()
        {
            TestAdderCasting_core("../../../smeil/casting/downsize_casting_explicit_type.sme");
        }

        [TestMethod]
        public void DownsizeCastingExplicitTypeSimple()
        {
            try { TestAdderCasting_core("../../../smeil/casting/downsize_casting_explicit_type_simple.sme"); }
            catch (SMEIL.Parser.ParserException pex)
            {
                throw new System.ArgumentException($"{pex.Message} - {pex.Location}");
            }
        }

        [TestMethod]
        public void DownsizeCastingImplicitType()
        {
            TestAdderCasting_core("../../../smeil/casting/downsize_casting_implicit_type.sme");
        }

        [TestMethod]
        public void DownsizeCastingWithBusShape()
        {
            TestAdderCasting_core("../../../smeil/casting/downsize_casting_with_busshape.sme");
        }
    }
}