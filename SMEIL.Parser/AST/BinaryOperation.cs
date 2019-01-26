using System;
using System.Collections.Generic;

namespace SMEIL.Parser.AST
{
    public static class BinOpHelper
    {
        /// <summary>
        /// Helper method to clasify an operation as numeric
        /// </summary>
        /// <param name="self">The operation to evaluate</param>
        /// <returns><c>true</c> if the operation is a numeric operation; <c>false</c> otherwise</returns>
        public static bool IsNumericOperation(this BinOp self)
        {
            switch (self)
            {
                case BinOp.Add:
                case BinOp.Subtract:
                case BinOp.Multiply:
                case BinOp.Modulo:
                case BinOp.LessThanOrEqual:
                case BinOp.GreaterThan:
                case BinOp.GreaterThanOrEqual:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Helper method to clasify an operation as logical
        /// </summary>
        /// <param name="self">The operation to evaluate</param>
        /// <returns><c>true</c> if the operation is a logical operation; <c>false</c> otherwise</returns>
        public static bool IsLogicalOperation(this BinOp self)
        {
            switch (self)
            {
                case BinOp.LogicalAnd:
                case BinOp.LogicalOr:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Helper method to clasify an operation as equality
        /// </summary>
        /// <param name="self">The operation to evaluate</param>
        /// <returns><c>true</c> if the operation is an equality operation; <c>false</c> otherwise</returns>
        public static bool IsEqualityOperation(this BinOp self)
        {
            switch (self)
            {
                case BinOp.Equal:
                case BinOp.NotEqual:
                    return true;

                default:
                    return false;
            }

        }
    }

    /// <summary>
    /// The supported binary operations
    /// </summary>
    public enum BinOp
    {
        /// <summary>Addition</summary>
        Add,
        /// <summary>Subtraction</summary>
        Subtract,
        /// <summary>Multiplication</summary>
        Multiply,
        /// <summary>Division</summary>
        Divide,
        /// <summary>Modulo</summary>
        Modulo,
        /// <summary>Equality</summary>
        Equal,
        /// <summary>Non-equality</summary>
        NotEqual,
        /// <summary>Shift bits left</summary>
        ShiftLeft,
        /// <summary>Shift bits right</summary>
        ShiftRight,
        /// <summary>Less than</summary>
        LessThan,
        /// <summary>Greater than</summary>
        GreaterThan,
        /// <summary>Greater than or equal</summary>
        GreaterThanOrEqual,
        /// <summary>Less than or equal</summary>
        LessThanOrEqual,
        /// <summary>Bitwise and</summary>
        BitwiseAnd,
        /// <summary>Bitwise or</summary>
        BitwiseOr,
        /// <summary>Bitwise xor</summary>
        BitwiseXor,
        /// <summary>Logical conjunction</summary>
        LogicalAnd,
        /// <summary>Logical disjunction</summary>
        LogicalOr
    }

    /// <summary>
    /// Represents a binary operation
    /// </summary>
    public class BinaryOperation : ParsedItem
    {
        /// <summary>
        /// The operation
        /// </summary>
        public readonly BinOp Operation;

        /// <summary>
        /// Gets a value indicating if the operation is a numeric operation
        /// </summary>
        public bool IsNumericOperation => Operation.IsNumericOperation();

        /// <summary>
        /// Gets a value indicating if the operation is a logical operation
        /// </summary>
        public bool IsLogicalOperation => Operation.IsLogicalOperation();

        /// <summary>
        /// Gets a value indicating if the operation is an equality operation
        /// </summary>
        public bool IsEqualityOperation => Operation.IsEqualityOperation();

        /// <summary>
        /// Constructs a new binary operation
        /// </summary>
        /// <param name="token">The token where the operation was found</param>
        /// <param name="operation">The operation</param>
        public BinaryOperation(ParseToken token, BinOp operation)
            : base(token)
        {
            Operation = operation;
        }

        /// <summary>
        /// Parses a string into a binary operation
        /// </summary>
        /// <param name="value">The string to parse</param>
        /// <returns>The parsed binary operation</returns>
        public static BinOp Parse(ParseToken value)
        {
            try
            {
                return Parse(value.Text);
            }
            catch (System.Exception)
            {
                throw new ArgumentException($"Unable to parse {value.Text} as a binary operation: {value}", nameof(value));
            }
        }
        
        /// <summary>
        /// Parses a string into a binary operation
        /// </summary>
        /// <param name="value">The string to parse</param>
        /// <returns>The parsed binary operation</returns>
        public static BinOp Parse(string value)
        {
            switch (value)
            {
                case "+": return BinOp.Add;
                case "-": return BinOp.Subtract;
                case "*": return BinOp.Multiply;
                case "/": return BinOp.Divide;
                case "%": return BinOp.Modulo;
                case "==": return BinOp.Equal;
                case "!=": return BinOp.NotEqual;
                case "<<": return BinOp.ShiftLeft;
                case ">>": return BinOp.ShiftRight;
                case "<": return BinOp.LessThan;
                case ">": return BinOp.GreaterThan;
                case "<=": return BinOp.LessThanOrEqual;
                case ">=": return BinOp.GreaterThanOrEqual;
                case "&": return BinOp.BitwiseAnd;
                case "|": return BinOp.BitwiseOr;
                case "^": return BinOp.BitwiseXor;
                case "&&": return BinOp.LogicalAnd;
                case "||": return BinOp.LogicalOr;
            }

            throw new ArgumentException($"Unable to parse {value} as a binary operation", nameof(value));
        }
    }
}