using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SMEIL.Parser.CommandLineOptions
{
    /// <summary>
    /// Base for either an option or a value
    /// </summary>
    public abstract class BaseCommandlineOptionAttribute : Attribute
    {
        /// <summary>
        /// The item is required
        /// </summary>
        public bool Required { get; set; }
        /// <summary>
        /// The default value
        /// </summary>
        public object Default { get; set; }
        /// <summary>
        /// The help text for the item
        /// </summary>
        /// <value></value>
        public string HelpText { get; set; }
        /// <summary>
        /// A placeholder value for the item, to show in help screens
        /// </summary>
        public string MetaValue { get; set; }

        /// <summary>
        /// An optional custom parser method
        /// </summary>
        public Func<string, object> CustomParser { get; set; }
    }

    /// <summary>
    /// Attribute for marking a property as an option
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class OptionAttribute : BaseCommandlineOptionAttribute
    {
        /// <summary>
        /// The option name
        /// </summary>
        public string LongName { get; private set; }

        /// <summary>
        /// The separator used to split values for arrays
        /// </summary>
        public char Separator { get; set; } = ',';

        /// <summary>
        /// Creates a new option
        /// </summary>
        public OptionAttribute()
        { }
        
        /// <summary>
        /// Creats a new option
        /// </summary>
        /// <param name="longname">The name to use</param>
        public OptionAttribute(string longname) 
        { 
            LongName = longname ?? throw new ArgumentNullException(nameof(longname));
        }
    }

    /// <summary>
    /// Attribute for a value
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ValueAttribute : BaseCommandlineOptionAttribute
    {        
        /// <summary>
        /// The position of the item
        /// </summary>
        public int Index { get; }
        /// <summary>
        /// The meta-name to show in help texts
        /// </summary>
        public string MetaName { get; set; }

        /// <summary>
        /// Creates a new value attribute
        /// </summary>
        /// <param name="index">The position of the value</param>
        public ValueAttribute(int index) 
        {
            Index = index; 
        }
    }

    /// <summary>
    /// A simple class for parsing a commandline
    /// </summary>   
    public static class CommandLineParser
    {
        /// <summary>
        /// Parses the commandline and throws an exception if there are unparsed values
        /// </summary>
        /// <param name="commandline">The commandline to parse</param>
        /// <typeparam name="T">The data type to use</typeparam>
        /// <returns>The parsed instance</returns>
        public static T ParseCommandlineStrict<T>(string[] commandline)
            where T : class, new()
        {
            return ParseCommandline<T>(commandline, false, false)?.Item1;
        }

        /// <summary>
        /// Gets the option strings
        /// </summary>
        /// <param name="filteroptions">Optional filter to only return one option</param>
        /// <typeparam name="T">The data type to use</typeparam>
        /// <returns>The filter options</returns>
        public static string GetOptionStrings<T>(string filteroptions = null)
            where T : new()
        {
            return string.Join(Environment.NewLine + Environment.NewLine,
                GetOptions<T>()
                .Where(x => string.IsNullOrWhiteSpace(filteroptions) || string.Equals(filteroptions, x.Key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Key)
                .Select(x => string.Join(Environment.NewLine + "\t", new[] {
                    $"--{x.Key}"
                    + (string.IsNullOrWhiteSpace(x.Value.Attr.MetaValue)
                        ? string.Empty
                        : "=" + x.Value.Attr.MetaValue
                    ),
                    x.Value.Attr.Required ? "* Required" : string.Empty,
                    x.Value.Attr.Default == null ? string.Empty : "* Default value: " + x.Value.Attr.Default.ToString(),
                    x.Value.Attr.HelpText
                }.Where(y => !string.IsNullOrWhiteSpace(y))))
            );
        }


        /// <summary>
        /// Gets a string describing the values and options
        /// </summary>
        /// <typeparam name="T">The data type to use</typeparam>
        /// <returns>The help screen</returns>
            public static string GetHelpScreen<T>()
            where T : new()
        {
            return
                GetUsageString<T>(false)
                + Environment.NewLine 
                + Environment.NewLine
                + GetOptionStrings<T>();
        }

        /// <summary>
        /// Returns a short usage string
        /// </summary>
        /// <param name="includeHelpMessage">Flag indicating if a short &quot;Use --help for help&quot; message is added</param>
        /// <typeparam name="T">The data type to use</typeparam>
        /// <returns>The usage string</returns>
        public static string GetUsageString<T>(bool includeHelpMessage = true)
            where T : new()
        {
            var usage = "dotnet "
                + Path.GetFileName(Assembly.GetEntryAssembly().Location)
                + string.Join(" ", 
                    GetValues<T>()
                    .Select(x => 
                        (x.Attr.Required ? "[" : "<")
                        + x.Attr.MetaName
                        + (
                            string.IsNullOrWhiteSpace(x.Attr.MetaValue)
                            ? string.Empty
                            : "=" + x.Attr.MetaValue
                        )
                        + (x.Attr.Required ? "]" : ">")
                    )
                );

            if (includeHelpMessage)
                usage = usage
                    + Environment.NewLine
                    + Environment.NewLine
                    + $"Use dotnet {Path.GetFileName(Assembly.GetEntryAssembly().Location)} --help for help";

            return usage;
        }

        /// <summary>
        /// Helper class for keeping the property and attribute
        /// </summary>
        private class PropValueAttr
        {
            /// <summary>
            /// The property
            /// </summary>
            public PropertyInfo Prop;
            /// <summary>
            /// The attribute
            /// </summary>
            public ValueAttribute Attr;
        }

        /// <summary>
        /// Helper class for keeping the property and attribute
        /// </summary>
        private class PropOptAttr
        {
            /// <summary>
            /// The property
            /// </summary>
            public PropertyInfo Prop;
            /// <summary>
            /// The attribute
            /// </summary>
            public OptionAttribute Attr;
        }

        /// <summary>
        /// Gets all values for the type
        /// </summary>
        /// <typeparam name="T">The type to get the values for</typeparam>
        /// <returns>The values in sorted order</returns>
        private static PropValueAttr[] GetValues<T>() 
            where T : new()
        {
            var values = typeof(T)
                .GetProperties()
                .Select(x => new PropValueAttr()
                {
                    Prop = x,
                    Attr = x
                        .GetCustomAttributes(typeof(ValueAttribute), true)
                        .Cast<ValueAttribute>()
                        .FirstOrDefault()
                })
                .Where(x => x.Attr != null)
                .OrderBy(x => x.Attr.Index)
                .ToArray();

            // Sanity check
            var required = true;
            var isArray = false;
            for (var i = 0; i < values.Length; i++)
            {
                if (isArray)
                    throw new ArgumentException("Bad options, only the last value can be an array");
                if (values[i].Attr.Index != i)
                    throw new ArgumentException($"Bad options, the values should form a sequence, but no element found for position {i}");
                if (values[i].Attr.Required && !required)
                    throw new ArgumentException($"Bad options, cannot have a required argument after a non-required");
                required = values[i].Attr.Required;
                isArray = values[i].Prop.PropertyType.IsArray;
            }

            return values;
        }

        /// <summary>
        /// Gets the options for the type
        /// </summary>
        /// <typeparam name="T">The type to get the options for</typeparam>
        /// <returns>The options</returns>
        private static Dictionary<string, PropOptAttr> GetOptions<T>()
            where T : new()
        {
            return typeof(T)
                .GetProperties()
                .Select(x => new PropOptAttr()
                {
                    Prop = x,
                    Attr = x
                        .GetCustomAttributes(typeof(OptionAttribute), true)
                        .Cast<OptionAttribute>()
                        .FirstOrDefault()
                })
                .Where(x => x.Attr != null)
                .ToDictionary(
                    x => string.IsNullOrWhiteSpace(x.Attr.LongName)
                        ? x.Prop.Name
                        : x.Attr.LongName,
                    x => x, StringComparer.OrdinalIgnoreCase
                );
        }


        /// <summary>
        /// Parses a commandline, and optionally allows unparsed values
        /// </summary>
        /// <param name="commandline">The commandline to parse</param>
        /// <param name="allowextravalues"><c>true</c> if trailing values are allowed; <c>false</c> otherwise</param>
        /// <param name="allowextraoptions"><c>true</c> if unused options are allowed; <c>false</c> otherwise</param>
        /// <typeparam name="T">The return type to use</typeparam>
        /// <returns>The parsed item and the extra options and values</returns>
        public static Tuple<T, Dictionary<string, string>, List<string>> ParseCommandline<T>(string[] commandline, bool allowextravalues = true, bool allowextraoptions = true)
            where  T : new()
        {
            // Check for basic usage
            if (commandline.Length == 0)
            {
                Console.WriteLine(GetUsageString<T>());
                return null;
            }

            // Check for help requests
            if (commandline.Length == 1
                && (
                    string.Equals(commandline.First(), "help", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(commandline.First(), "--help", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(commandline.First(), "/help", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(commandline.First(), "/h", StringComparison.OrdinalIgnoreCase)
                ))
                {
                    Console.WriteLine(GetHelpScreen<T>());
                    return null;
                }

            // Get the items we will work on
            var values = GetValues<T>();
            var options = GetOptions<T>();

            // Prepare parsing
            var args = new List<string>();
            var opts = new Dictionary<string, string>();
            var reqopts = options
                .Where(x => x.Value.Attr.Required)
                .Select(x => x.Key)
                .ToHashSet();

            // Split into options (with -- prefix) and values
            foreach (var item in commandline.Select(x => x.Trim()))
            {
                if (item.StartsWith("--"))
                {
                    var els = item.Split('=', 2);
                    var key = els.First().Substring(2);
                    var val = els.Skip(1).FirstOrDefault();
                    if (opts.ContainsKey(key))
                        throw new ArgumentException($"Multiple values supplied for {key}");
                    opts.Add(key, val);
                }
                else
                {
                    args.Add(item);
                }
            }
            
            // Check that we have all required values
            var requireCount = values.TakeWhile(x => x.Attr.Required).Count();
            if (args.Count < requireCount)
                throw new ArgumentException($"Missing argument for {values[args.Count].Attr.MetaName} (position {args.Count})");

            // Create the return instance
            var target = new T();

            // Make sure the defaults are set correctly
            foreach (var v in values)
                if (v.Attr.Default != null)
                    try { v.Prop.SetValue(target, v.Attr.Default); }
                    catch (Exception ex) { throw new ArgumentException($"Default value for {v.Prop.Name} could not be assigned", ex); }
            foreach (var v in options.Values)
                if (v.Attr.Default != null)
                    try { v.Prop.SetValue(target, v.Attr.Default); }
                    catch (Exception ex) { throw new ArgumentException($"Default value for {v.Prop.Name} could not be assigned", ex); }

            // Extract all arguments
            for(var i = 0; i < Math.Min(args.Count, values.Length); i++)
            {
                // Special handling if the last value is an array
                if (values[i].Prop.PropertyType.IsArray && values[i].Attr.CustomParser == null)
                {
                    var arr = Array.CreateInstance(values[i].Prop.PropertyType.GetElementType(), args.Count);
                    for (var j = 0; j < arr.Length; j++)
                        arr.SetValue(GetValue(values[i].Prop.PropertyType.GetElementType(), null, args[j]), j);
                    args.Clear();
                }
                else
                {
                    SetProperty(target, values[i].Prop, values[i].Attr, args[0]);
                    args.RemoveAt(0);
                }
            }

            // Parse all properties
            foreach (var item in opts.Keys.ToArray())
            {
                if (options.TryGetValue(item, out var inst))
                {
                    SetProperty(target, inst.Prop, inst.Attr, opts[item]);
                    opts.Remove(item);
                    reqopts.Remove(item);
                }
            }

            // Validate that we have all that is required
            if (reqopts.Count > 0)
                throw new ArgumentException($"Required option{(reqopts.Count == 1 ? "" : "s")} are missing: {string.Join(", ", reqopts)}");

            // Validate that we do not have dangling stuff that is not allowed
            if (!allowextraoptions && opts.Count > 0)
                throw new ArgumentException($"Found options that were not valid: {string.Join(", ", opts.Keys)}");
            if (!allowextravalues && args.Count > 0)
                throw new ArgumentException($"Found {args.Count} extra unused argument{(args.Count == 1 ? "" : "s")}");

            return new Tuple<T, Dictionary<string, string>, List<string>>(target, opts, args);
        }

        /// <summary>
        /// Converts a string to an object
        /// </summary>
        /// <param name="targettype">The target type</param>
        /// <param name="defaultvalue">The default if the value is empty</param>
        /// <param name="value">The value to parse</param>
        /// <returns>The object instance</returns>
        private static object GetValue(Type targettype, object defaultvalue, string value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                    return defaultvalue;
                else if (targettype.IsEnum)
                    return Enum.Parse(targettype, value, true);
                else if (targettype == typeof(bool))
                    return ParseBool(value);
                else if (targettype == typeof(int) || targettype == typeof(uint) || targettype == typeof(long) || targettype == typeof(ulong))
                    return ParseInteger(value);
                else if (targettype == typeof(TimeSpan))
                    return ParseTimeSpan(value);
                else if (targettype == typeof(DateTime))
                    return ParseDateTime(value);
                else if (targettype == typeof(string))
                    return value;
                else
                    throw new ArgumentException($"Unsupported type, needs custom parser: {targettype}");
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Unable to parse value {value} into type {targettype}", ex);
            }
        }

        /// <summary>
        /// Attempts to set the property value for an entry
        /// </summary>
        /// <param name="item">The target item getting the property set</param>
        /// <param name="property">The property to set</param>
        /// <param name="attribute">The attribute configuration</param>
        /// <param name="value">The value to use</param>
        /// <typeparam name="T">The type of the target item</typeparam>
        private static void SetProperty<T>(T item, PropertyInfo property, BaseCommandlineOptionAttribute attribute, string value)
        {
            try
            {
                if (attribute.CustomParser != null)
                    property.SetValue(item, attribute.CustomParser(value));
                else if (property.PropertyType.IsArray && attribute is OptionAttribute opa)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        property.SetValue(item, attribute.Default);
                    else
                    {
                        var parts = value.Split(opa.Separator, StringSplitOptions.RemoveEmptyEntries);
                        var arr = Array.CreateInstance(property.PropertyType.GetElementType(), parts.Length);
                        for (var i = 0; i < arr.Length; i++)
                            arr.SetValue(GetValue(property.PropertyType.GetElementType(), null, parts[i]), i);
                        
                        property.SetValue(item, arr);
                    }
                }
                else
                    property.SetValue(item, GetValue(property.PropertyType, attribute.Default, value));
            }
            catch (Exception ex)
            {                
                throw new ArgumentException($"Unable to parse value {value} into type {property.PropertyType} for property {property.Name}", ex);
            }
        }

        /// <summary>
        /// Parses a date-time value
        /// </summary>
        /// <param name="value">The value to parse</param>
        /// <returns>The parsed value</returns>
        private static DateTime ParseDateTime(string value)
        {
            if (string.Equals(value, "now", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now;
            else if (string.Equals(value, "tomorrow", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.AddDays(1);
            else if (string.Equals(value, "yesterday", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.AddDays(-1);
            else
                return DateTime.Parse(value);
        }

        /// <summary>
        /// Parses a boolean string
        /// </summary>
        /// <param name="value">The value to parse</param>
        /// <returns>The parsed value</returns>
        private static bool ParseBool(string value)
        {
            return !(string.Equals("0", value, StringComparison.OrdinalIgnoreCase)
            || string.Equals("off", value, StringComparison.OrdinalIgnoreCase)
            || string.Equals("false", value, StringComparison.OrdinalIgnoreCase)
            || string.Equals("no", value, StringComparison.OrdinalIgnoreCase)
            || string.Equals("f", value, StringComparison.OrdinalIgnoreCase)
            || string.Equals("n", value, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Parses a timespan value
        /// </summary>
        /// <param name="value">The value to parse</param>
        /// <returns>The parsed value</returns>
        private static TimeSpan ParseTimeSpan(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Empty string is not a valid duration", nameof(value));

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
                return TimeSpan.FromSeconds(r);

            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var n))
                return n;

            var res = new TimeSpan(0);
            var len = 0;
            foreach (var m in new Regex("(?<number>[-|+]?[0-9]+)(?<suffix>[wdhms])", RegexOptions.IgnoreCase).Matches(value).Cast<Match>())
            {
                if (!m.Success)
                    break;
                len += m.Length;

                var number = int.Parse(m.Groups["number"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                switch (m.Groups["suffix"].Value.ToLowerInvariant()[0])
                {
                    case 'w':
                        res += TimeSpan.FromDays(number * 7);
                        break;
                    case 'd':
                        res += TimeSpan.FromDays(number);
                        break;
                    case 'h':
                        res += TimeSpan.FromHours(number);
                        break;
                    case 'm':
                        res += TimeSpan.FromMinutes(number);
                        break;
                    case 's':
                        res += TimeSpan.FromSeconds(number);
                        break;
                    default:
                        throw new ArgumentException($"Invalid suffix: \"{m.Groups["suffix"].Value}\"", value);
                }
            }

            if (len != value.Length)
                throw new ArgumentException($"String is not a valid duration: \"{value}\"", nameof(value));

            return res;
        }

        /// <summary>
        /// Parses a size string
        /// </summary>
        /// <param name="value">The string to parse</param>
        /// <returns>The size</returns>
        private static long ParseInteger(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Empty string is not a valid number", nameof(value));

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
                return r;

            var m = new Regex("(?<number>[0-9,.]+)\\s*(?<suffix>[ptgmk]i?b)?", RegexOptions.IgnoreCase).Match(value);
            if (!m.Success || m.Length != value.Length)
                throw new ArgumentException($"String is not a valid number or size: \"{value}\"", nameof(value));

            var suffix = m.Groups["suffix"].Success ? m.Groups["suffix"].Value.ToLowerInvariant()[0] : 'b';
            var number = float.Parse(m.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture);
            switch (suffix)
            {
                case 'p':
                    return (long)(number * Math.Pow(1024, 5));
                case 't':
                    return (long)(number * Math.Pow(1024, 4));
                case 'g':
                    return (long)(number * Math.Pow(1024, 3));
                case 'm':
                    return (long)(number * Math.Pow(1024, 2));
                case 'k':
                    return (long)(number * Math.Pow(1024, 1));
                case 'b':
                    // No suffix or 'b' must be a valid integer number
                    return long.Parse(m.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture);
                default:
                    throw new ArgumentException($"Invalid suffix: \"{suffix}\"", nameof(value));
            }
        }        
    }



}