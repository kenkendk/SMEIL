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
        Bus
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
        /// The index expression, if any
        /// </summary>
        public readonly Expression Indexer;

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
        /// <param name="parent">The array data type.</param>
        /// <param name="indexer">The index expression.</param>
        public DataType(ParseToken token, DataType parent, Expression indexer)
            : base(token)
        {
            Type = parent.Type;
            BitWidth = parent.BitWidth;
            Indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
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
            if (token.Text == "bool")
                return new DataType(token, ILType.Bool, 1);
            if (token.Text == "f32")
                return new DataType(token, ILType.Float, 1);
            if (token.Text == "f64")
                return new DataType(token, ILType.Float, 1);
            if (token.Text.StartsWith("i", StringComparison.Ordinal) || token.Text.StartsWith("u", StringComparison.Ordinal))
            {
                if (!int.TryParse(token.Text.Substring(1), out var width) || width <= 0)
                    throw new Exception($"Failed to parse bit width for variable: {token}");
                return new DataType(token, token.Text.StartsWith("i", StringComparison.Ordinal) ? ILType.SignedInteger : ILType.UnsignedInteger, width);
            }

            throw new Exception($"Failed to parse type: {token}");
        }

        /// <summary>
        /// Checks if two data types can be compared for equality
        /// </summary>
        /// <param name="a">One data type</param>
        /// <param name="b">Another data type</param>
        /// <returns><c>true</c> if the types can be compared for equality; <c>false</c> otherwise</returns>
        public static bool CanEqualityCompare(DataType a, DataType b)
        {
            return CanUnifyTypes(a, b);
        }

        /// <summary>
        /// Checks if two data types can be unified
        /// </summary>
        /// <param name="a">One data type</param>
        /// <param name="b">Another data type</param>
        /// <returns><c>true</c> if the types can be unified; <c>false</c> otherwise</returns>
        public static bool CanUnifyTypes(DataType a, DataType b)
        {
            return TryGetUnifiedType(a, b) != null;
        }

        /// <summary>
        /// Combines two data types into the largest unified type, or throws an exception
        /// </summary>
        /// <param name="a">One data type</param>
        /// <param name="b">Another data type</param>
        /// <returns>The unified data type</returns>
        private static DataType TryGetUnifiedType(DataType a, DataType b)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));
            if (b == null)
                throw new ArgumentNullException(nameof(b));

            switch (a.Type)
            {
                case ILType.SignedInteger:
                    if (b.Type == ILType.SignedInteger)
                        return new DataType(a.SourceToken, ILType.SignedInteger, Math.Max(a.BitWidth, b.BitWidth));
                    else if (b.Type == ILType.UnsignedInteger)
                        return new DataType(a.SourceToken, ILType.SignedInteger, Math.Max(a.BitWidth, b.BitWidth) + (a.BitWidth <= b.BitWidth && a.BitWidth != -1 ? 1 : 0));
                    break;

                case ILType.UnsignedInteger:
                    if (b.Type == ILType.UnsignedInteger)
                        return new DataType(a.SourceToken, ILType.UnsignedInteger, Math.Max(a.BitWidth, b.BitWidth));
                    else if (b.Type == ILType.SignedInteger)
                        return new DataType(a.SourceToken, ILType.UnsignedInteger, Math.Max(a.BitWidth, b.BitWidth) + (a.BitWidth >= b.BitWidth && a.BitWidth != -1 ? 1 : 0));
                    break;


                case ILType.Float:
                    if (b.Type == ILType.Float)
                        return new DataType(a.SourceToken, ILType.Float, Math.Max(a.BitWidth, b.BitWidth));
                    break;

                case ILType.Bool:
                    if (b.Type == ILType.Bool)
                        return a;
                    break;

                case ILType.Bus:
                    // Build a unified type for the shapes
                    var shape = new BusShape(a.Shape.Signals);

                    foreach (var n in b.Shape.Signals)
                        if (!shape.Signals.TryGetValue(n.Key, out var t))
                            shape.Signals.Add(n.Key, n.Value);
                        else if (!object.Equals(t, n.Value))
                            shape.Signals[n.Key] = UnifiedType(n.Value, t);

                    return new DataType(a.SourceToken, shape);
            }

            return null;
        }

        /// <summary>
        /// Combines two data types into the largest unified type, or throws an exception
        /// </summary>
        /// <param name="a">One data type</param>
        /// <param name="b">Another data type</param>
        /// <returns>The unified data type</returns>
        public static DataType UnifiedType(DataType a, DataType b)
        {
            return TryGetUnifiedType(a, b) ?? throw new Exception($"Unable to unify types {a} and {b}");
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

                foreach (var n in this.Shape.Signals.Keys)
                    if (!other.Shape.Signals.ContainsKey(n))
                        return false;
            }
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
            return Equals(obj as DataType);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            if (this.Type == ILType.Bool)
                return this.Type.GetHashCode();
            else if (this.Type == ILType.Bus)
                return this.Type.GetHashCode() ^ this.Shape.Signals.Count ^ (this.Shape.Signals.Count == 0 ? 0 : this.Shape.Signals.Select(x => x.GetHashCode()).Aggregate((a,b) => a ^ b));
            else
                return this.Type.GetHashCode() ^ this.BitWidth;
        }

        /// <summary>
        /// Checks if a type can be type-casted to another type
        /// </summary>
        /// <param name="sourceType">The source type</param>
        /// <param name="targetType">The type being casted to</param>
        /// <returns><c>true</c> if the <paramref name="sourceType" /> can be cast to <paramref name="targetType" />; false otherwise</returns>
        public static bool CanTypeCast(DataType sourceType, DataType targetType)
        {
            if (object.Equals(sourceType, targetType) || CanUnifyTypes(sourceType, targetType))
                return true;

            // We do not allow casting to/from booleans
            if (sourceType.IsBoolean || targetType.IsBoolean && sourceType.IsBoolean != targetType.IsBoolean)
                return false;

            // No casting to/from a bus type
            if (sourceType.IsBus || targetType.IsBus)
                return false;

            // Numeric casting is allowed, even with precision loss
            if (sourceType.IsNumeric && targetType.IsNumeric)
                return true;

            // No idea what the user has attempted :)
            return false;

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

                default:
                    return "???";
            }
        }
    }
}
