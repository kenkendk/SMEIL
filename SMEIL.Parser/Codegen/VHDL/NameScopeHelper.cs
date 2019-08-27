using System.Collections.Generic;

namespace SMEIL.Parser.Codegen.VHDL
{
    /// <summary>
    /// A helper class to store the local names for variables and signals,
    /// such that they can be computed once and used consistently
    /// </summary>
    public class NameScopeHelper
    {
        /// <summary>
        /// Map of signal names when being read
        /// </summary>
        public readonly Dictionary<Instance.Signal, string> SignalReadNames = new Dictionary<Instance.Signal, string>();
        /// <summary>
        /// Map of signal names when being written
        /// </summary>
        public readonly Dictionary<Instance.Signal, string> SignalWriteNames = new Dictionary<Instance.Signal, string>();
        /// <summary>
        /// Map of variable names
        /// </summary>
        public readonly Dictionary<Instance.Variable, string> VariableNames = new Dictionary<Instance.Variable, string>();

        /// <summary>
        /// Map of used local token names
        /// </summary>
        public readonly Dictionary<string, int> LocalTokenCounter = new Dictionary<string, int>();        
    }
}