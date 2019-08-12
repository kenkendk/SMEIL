using System;
using System.Collections.Generic;
using System.Linq;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// A mapper class for simplifying access to either a process or a connect statement
    /// </summary>
    public class MetaProcess
    {
        /// <summary>
        /// The process, if this is a process
        /// </summary>
        public readonly Instance.Process Process;
        /// <summary>
        /// The connection, if this is a connection
        /// </summary>
        public readonly Instance.Connection Connection;

        /// <summary>
        /// Constructs a new meta process for a process instance
        /// </summary>
        /// <param name="process">The process instance</param>
        public MetaProcess(Instance.Process process)
        {
            this.Process = process ?? throw new ArgumentNullException(nameof(process));
        }

        /// <summary>
        /// Constructs a new meta process for a connection instance
        /// </summary>
        /// <param name="process">The connection instance</param>
        public MetaProcess(Instance.Connection connection)
        {
            this.Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Returns all input signals for the process or connection
        /// </summary>
        /// <param name="state">The state object</param>
        /// <returns>The input signals</returns>
        public IEnumerable<Instance.Signal> InputSignals(ValidationState state)
        {
            if (Connection != null && Connection.Source is Instance.Signal cs)
                return new[] { cs };
            else if (Connection != null && Connection.Source is Instance.Bus cb)
                return cb.Instances.OfType<Instance.Signal>();
            else if (Process != null)
                return Process.MappedParameters
                    .Where(x => x.MappedItem is Instance.Bus && x.MatchedParameter.Direction == AST.ParameterDirection.In)
                    .Select(x => x.MappedItem)
                    .Cast<Instance.Bus>()
                    .SelectMany(x => x.Instances.OfType<Instance.Signal>())
                    .Concat(
                        Process.Instances
                            .OfType<Instance.Bus>()
                            .SelectMany(x => x.Instances.OfType<Instance.Signal>())
                            .Where(x =>
                                state.ItemDirection[Process].ContainsKey(x)
                                &&
                                state.ItemDirection[Process][x] == ItemUsageDirection.Read
                            )
                    )
                    .Distinct();

            throw new ArgumentException("Unexpected meta process state");
        }

        /// <summary>
        /// Returns all output signals for a process or connection
        /// </summary>
        /// <param name="state">The state object</param>
        /// <returns>The output signals</returns>
        public IEnumerable<Instance.Signal> OutputSignals(ValidationState state)
        {
            if (Connection != null && Connection.Target is Instance.Signal cs)
                return new[] { cs };
            else if (Connection != null && Connection.Target is Instance.Bus cb)
                return cb.Instances.OfType<Instance.Signal>();
            else if (Process != null)
                return Process.MappedParameters
                    .Where(x => x.MappedItem is Instance.Bus && x.MatchedParameter.Direction == AST.ParameterDirection.Out)
                    .Select(x => x.MappedItem)
                    .Cast<Instance.Bus>()
                    .SelectMany(x => x.Instances.OfType<Instance.Signal>())
                    .Concat(
                        Process.Instances
                            .OfType<Instance.Bus>()
                            .SelectMany(x => x.Instances.OfType<Instance.Signal>())
                            .Where(x =>
                                state.ItemDirection[Process].ContainsKey(x)
                                &&
                                state.ItemDirection[Process][x] == ItemUsageDirection.Write
                            )
                    )
                    .Distinct();

            throw new ArgumentException("Unexpected meta process state");
        }

        /// <summary>
        /// Gets a string representation of this element for use in error messages
        /// </summary>
        /// <returns>The name of this item</returns>
        public string ReportedName
        {
            get
            {
                if (Process != null)
                    return Process.Source.Name == null ? Process.Source.SourceItem.ToString() : Process.Source.Name.SourceToken.ToString();
                if (Connection != null)
                    return Connection.DeclarationSource.Target.SourceToken.ToString();

                throw new ArgumentException("Unexpected meta process state");
            }
        }
    }
}