using System;
using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Class that visits all processes and checks that every input bus is only read and that every output bus is only written
    /// </summary>
    public class CheckInOut : IValidator
    {

        // TODO: Remove this? Check is in ValidationState now

        /// <summary>
        /// Validates the module
        /// </summary>
        /// <param name="state">The validation state</param>
        public void Validate(ValidationState state)
        {
        }
    }
}
