using System;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Interface for program validation
    /// </summary>
    public interface IValidator
    {
        /// <summary>
        /// Validates the module
        /// </summary>
        /// <param name="state">The validation state</param>
        void Validate(ValidationState state);
    }
}
