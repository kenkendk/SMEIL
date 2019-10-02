using System;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMEIL.Parser;
using System.Diagnostics;

namespace Unittest.Adder
{
    [TestClass]
    public class Errors 
    {
        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TestConstantError1()
        {
            RunWithPositionArgs("../../../smeil/error/symbol/constant_func_ref.sme", 9, 30, "y");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TestConstantError2()
        {
            RunWithPositionArgs("../../../smeil/error/symbol/variable_constant.sme", 5, 30, "y");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TestConstantError3()
        {
            RunWithPositionArgs("../../../smeil/error/symbol/constant_mutual_ref.sme", 5, 5, "const");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TestConstantError4()
        {
            RunWithPositionArgs("../../../smeil/error/symbol/constant_self_ref.sme", 5, 5, "const");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TestAdderSyntaxError1()
        {
            RunWithPositionArgs("../../../smeil/error/syntax/adder_syntax_error1.sme", 1, 1, "type");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TestAdderSyntaxError2()
        {
            RunWithPositionArgs("../../../smeil/error/syntax/adder_syntax_error2.sme", 1, 1, "type");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TestAdderNoUnificationError()
        {
            RunWithPositionArgs("../../../smeil/error/type/no_unification_error.sme", 7, 12, "valb");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void OutputExpansionError()
        {
            RunWithPositionArgs("../../../smeil/error/type/output_expansion_error.sme", 12, 46, "dest");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void OutputTruncationError()
        {
            RunWithPositionArgs("../../../smeil/error/type/output_truncation_error.sme", 13, 46, "dest");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TypeTruncationError()
        {
            RunWithPositionArgs("../../../smeil/error/type/type_truncation_error.sme", 12, 46, "dest");
        }

        [TestMethod]
        [ExpectedException(typeof(ParserException))]
        public void TypeTruncationImplicit()
        {
            RunWithPositionArgs("../../../smeil/error/type/type_truncation_implicit.sme", 8, 5, "outbus");
        }

        protected void RunWithPositionArgs(string file, int line, int lineoffset, string text)
        {
            try
            {
                Loader.LoadModuleAndImports(file, null, null).Validate();
            }
            catch (ParserException pex)
            {
                if (pex.Location.Line != line || pex.Location.LineOffset != lineoffset || pex.Location.Text != text)
                {
                    if (pex.Location.Line != line || pex.Location.LineOffset != lineoffset)
                        throw new ArgumentException($"Incorrect reported error position: {pex.Location}, expected {text} ({line}:{lineoffset})", pex);
                    if (pex.Location.Text != text)
                        throw new ArgumentException($"Wrong token reported: {pex.Location.Text}, expected {text}");
                }
                throw;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Unexpected exception: {ex}");
                throw ex;
            }
        }
    }
}