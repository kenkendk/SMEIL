using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a unary operation
    /// </summary>
    public class UnaryOperation : ParsedItem
    {
        /// <summary>
        /// The unary operations
        /// </summary>
        public enum UnOp
        {
            /// <summary>Mathematical negation</summary>
            Negation,
            /// <summary>Mathematical identity</summary>
            Identity,
            /// <summary>Logical negation</summary>
            LogicalNegation,
            /// <summary>Bitwise negation</summary>
            BitwiseInvert
        }

        /// <summary>
        /// The operation
        /// </summary>
        public readonly UnOp Operation;

        /// <summary>
        /// Creates a new unary operation
        /// </summary>
        /// <param name="token">The token where the operation was found</param>
        /// <param name="operation">The operation</param>
        public UnaryOperation(ParseToken token, UnOp operation)
            : base(token)
        {
            Operation = operation;
        }

        /// <summary>
        /// Returns a string representation of the expression, suitable for debugging
        /// </summary>
        public string AsString
        {
            get
            {
                switch (Operation)
                {
                    case UnOp.Negation:
                        return "-";
                    case UnOp.LogicalNegation:
                        return "!";
                    case UnOp.Identity:
                        return "+";
                    case UnOp.BitwiseInvert:
                        return "~";
                    default:
                        return Operation.ToString();
                }
            }
        }


        /// <summary>
        /// Parses a string into a unary operation
        /// </summary>
        /// <param name="value">The string to parse</param>
        /// <returns>The unary operation</returns>
        public static UnOp Parse(ParseToken value)
        {
            try
            {
                return Parse(value.Text);
            }
            catch (System.Exception)
            {
                throw new ArgumentException($"Unable to parse {value.Text} as a unary operation: {value}", nameof(value));
            }
        }
        
        /// <summary>
        /// Parses a string into a unary operation
        /// </summary>
        /// <param name="value">The string to parse</param>
        /// <returns>The unary operation</returns>
        public static UnOp Parse(string value)
        {
            switch (value)
            {
                case "-": return UnOp.Negation;
                case "+": return UnOp.Identity;
                case "!": return UnOp.LogicalNegation;
                case "~": return UnOp.BitwiseInvert;
            }

            throw new ArgumentException($"Unable to parse {value} as a unary operation", nameof(value));
        }
    }
}