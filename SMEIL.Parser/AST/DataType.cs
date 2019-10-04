using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// The different IL types
    /// </summary>
    public enum ILType
    {
        /// <summary>
        /// A signed integer type
        /// </summary>
        SignedInteger,
        /// <summary>
        /// An unsigned integer type
        /// </summary>
        UnsignedInteger,
        /// <summary>
        /// A floating point type
        /// </summary>
        Float,
        /// <summary>
        /// The boolean type
        /// </summary>
        Bool,
        /// <summary>
        /// A bus instance type
        /// </summary>
        Bus,
        /// <summary>
        /// The special undefined token
        /// </summary>
        Special,
        /// <summary>
        /// A string type
        /// </summary>
        String,
        /// <summary>
        /// An enumeration type
        /// </summary>
        Enumeration
    }

    /// <summary>
    /// Implementation of the built-in data types in SMEIL
    /// </summary>
    public class DataType : ParsedItem, IEquatable<DataType>
    {
        /// <summary>
        /// The type of the data
        /// </summary>
        public readonly ILType Type;

        /// <summary>
        /// The number of bits in the type, or -1 for unconstrained
        /// </summary>
        public readonly int BitWidth;

        /// <summary>
        /// The shape of the bus, if the type is a bus
        /// </summary>
        public readonly BusShape Shape;

        /// <summary>
        /// The source enum type, if type is an enumeration
        /// </summary>
        public readonly AST.EnumDeclaration EnumType;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.DataType"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        /// <param name="shape">The bus shape.</param>
        public DataType(ParseToken token, BusShape shape)
            : base(token)
        {
            Type = ILType.Bus;
            BitWidth = -1;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.DataType"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        /// <param name="type">The data type.</param>
        /// <param name="bits">The number of bits for the type.</param>
        public DataType(ParseToken token, ILType type, int bits)
            : base(token)
        {
            Type = type;
            BitWidth = bits;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.DataType"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        /// <param name="parent">The enumeration type.</param>
        public DataType(ParseToken token, AST.EnumDeclaration parent)
            : base(token)
        {
            Type = ILType.Enumeration;
            BitWidth = -1;
            EnumType = parent ?? throw new ArgumentNullException(nameof(parent));
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.DataType"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        /// <param name="parent">The array data type.</param>
        public DataType(ParseToken token, DataType parent)
            : base(token)
        {
            Type = parent.Type;
            BitWidth = parent.BitWidth;
            Shape = parent.Shape;
            EnumType = parent.EnumType;
        }

        /// <summary>
        /// Gets a value indicating if the item is a numeric type
        /// </summary>
        public bool IsNumeric => IsFloat || IsInteger;

        /// <summary>
        /// Gets a value indicating if the item is an integer type
        /// </summary>
        public bool IsInteger => Type == ILType.SignedInteger || Type == ILType.UnsignedInteger;

        /// <summary>
        /// Gets a value indicating if the item is an float
        /// </summary>
        public bool IsFloat => Type == ILType.Float;

        /// <summary>
        /// Gets a value indicating if the item is an boolean
        /// </summary>
        public bool IsBoolean => Type == ILType.Bool;

        /// <summary>
        /// Gets a value indicating if the item is a bus
        /// </summary>
        public bool IsBus => Type == ILType.Bus;

        /// <summary>
        /// Gets a value indicating if the item is a value type
        /// </summary>
        public bool IsValue => !IsBus;

        /// <summary>
        /// Gets a value indicating if the item is an enumeration type
        /// </summary>
        public bool IsEnum => Type == ILType.Enumeration;

        /// <summary>
        /// Helper method to parse a token for a data type
        /// </summary>
        /// <returns>The parsed data type.</returns>
        /// <param name="token">The source token.</param>
        public static DataType Parse(ParseToken token)
        {
            if (token.Text == "int")
                return new DataType(token, ILType.SignedInteger, -1);
            if (token.Text == "uint")
                return new DataType(token, ILType.UnsignedInteger, -1);
            if (token.Text == "float")
                return new DataType(token, ILType.Bool, -1);
            if (token.Text == "bool")
                return new DataType(token, ILType.Bool, 1);
            if (token.Text == "f8")
                return new DataType(token, ILType.Float, 8);
            if (token.Text == "f16")
                return new DataType(token, ILType.Float, 16);
            if (token.Text == "f32")
                return new DataType(token, ILType.Float, 32);
            if (token.Text == "f64")
                return new DataType(token, ILType.Float, 64);

            if (token.Text.StartsWith("i", StringComparison.Ordinal) || token.Text.StartsWith("u", StringComparison.Ordinal))
            {
                if (!int.TryParse(token.Text.Substring(1), out var width) || width <= 0)
                    throw new Exception($"Failed to parse bit width for variable: {token}");
                return new DataType(token, token.Text.StartsWith("i", StringComparison.Ordinal) ? ILType.SignedInteger : ILType.UnsignedInteger, width);
            }

            throw new Exception($"Failed to parse type: {token}");
        }

        /// <summary>
        /// Checks if the two instances are the same data type
        /// </summary>
        /// <param name="other">The type to compare with</param>
        /// <returns><c>true</c> if the types match, <c>false</c> otherwise</returns>
        public bool Equals(DataType other)
        {
            if (other == null)
                return false;

            if (other.Type != this.Type)
                return false;

            if (this.Type == ILType.Bus)
            {
                if (this.Shape.Signals.Count != other.Shape.Signals.Count)
                    return false;

                foreach (var n in this.Shape.Signals)
                    if (!other.Shape.Signals.TryGetValue(n.Key, out var x) || !object.Equals(n.Value, x))
                        return false;
            }
            else if (this.Type == ILType.Enumeration)            
                return this.EnumType == other.EnumType;
            else if (this.Type != ILType.Bool)
            {
                if (other.BitWidth != this.BitWidth)
                   return false;
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;
            return Equals(obj as DataType);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            if (this.Type == ILType.Bool)
                return this.Type.GetHashCode();
            else if (this.Type == ILType.Bus)
                return this.Type.GetHashCode() ^ this.Shape.Signals.Count ^ (this.Shape.Signals.Count == 0 ? 0 : this.Shape.Signals.Select(x => x.GetHashCode()).Aggregate((a,b) => a ^ b));
            else if (this.Type == ILType.Enumeration)
                return this.Type.GetHashCode() ^ this.EnumType.GetHashCode();
            else
                return this.Type.GetHashCode() ^ this.BitWidth;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            switch (this.Type)
            {
                case ILType.Bool:
                    return "bool";
                case ILType.Bus:
                    return Shape.ToString();
                case ILType.Float:
                    return "f" + BitWidth.ToString();
                case ILType.SignedInteger:
                    if (BitWidth == -1)
                        return "int";
                    return "i" + BitWidth.ToString();
                case ILType.UnsignedInteger:
                    if (BitWidth == -1)
                        return "uint";
                    return "u" + BitWidth.ToString();
                case ILType.Enumeration:
                    return "enum " + EnumType.Name.Name;

                default:
                    return "???";
            }
        }
    }
}
