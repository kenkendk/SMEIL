using System;
using System.Collections.Generic;
using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    public class BuildDependencyGraph : IValidator
    {
        /// <summary>
        /// Validates the module
        /// </summary>
        /// <param name="state">The validation state</param>
        public void Validate(ValidationState state)
        {
            // Keep a list of unscheduled processes
            var remainingprocesses = state.AllInstances
                .OfType<Instance.Process>()
                .ToList();
            
            // Keep track of wavefronts of processes
            var roots = new List<List<Instance.Process>>();

            // Map all output signals to their writers
            var writers = remainingprocesses
                .SelectMany(
                    x => OutputSignals(state, x)
                        .Select(z => new { P = x, S = z })
                )
                .GroupBy(x => x.S)
                .ToDictionary(x => x.Key, y => y.Select(x => x.P).ToList());

            // The code in SME handles double writes, but FPGA tools generally dislike double writers
            var doublewrite = writers
                .Where(x => x.Value.Count > 1)
                .Select(x => x.Key)
                .FirstOrDefault();

            if (doublewrite != null)
            {
                var dblnames = string.Join(Environment.NewLine, writers[doublewrite].Select(x => x.Name));
                throw new ParserException($"Multiple writers found for signal {doublewrite.Name}: {Environment.NewLine} {dblnames}", doublewrite.Source);
            }

            // Find all signals that are inputs
            var inputSignals = state
                .TopLevel
                .InputBusses
                .SelectMany(
                    x => x.Instances
                        .OfType<Instance.Signal>()
                        .Where(y => y.Source.Direction == SignalDirection.Normal)
                )
                .Concat(
                    state
                        .TopLevel
                        .OutputBusses
                        .SelectMany(
                            x => x.Instances
                                .OfType<Instance.Signal>()
                                .Where(y => y.Source.Direction == SignalDirection.Inverse)
                        )
                )
                ;

            // Register all inputs as having no writers
            foreach (var s in inputSignals)
                writers.Add(s, new List<Instance.Process>());

            // Prepare a list of signals processed by all writers
            var ready = new HashSet<Instance.Signal>(
                writers.Where(x => x.Value.Count == 0).Select(x => x.Key)
            );

            // Find signals with no writers
            var orphansSignal = remainingprocesses
                .SelectMany(
                    x => InputSignals(state, x)
                    .Where(z => !writers.ContainsKey(z))
                )
                .FirstOrDefault();

            if (orphansSignal != null)
                throw new ParserException($"No writers found for signal {orphansSignal.Name}: {orphansSignal.Source.SourceToken}", orphansSignal.Source);

            // Find all processes that each process depends on
            var dependsOn = remainingprocesses
                .SelectMany(
                    x => InputSignals(state, x)
                        .Select(z => new { 
                            P = x, 
                            D = writers[z]
                        }
                    )
                )
                .GroupBy(x => x.P)
                .ToDictionary(x => x.Key, x => x.SelectMany(y => y.D).ToArray());

            // Keep removing processes until all have been scheduled
            while(remainingprocesses.Count > 0)
            {
                var current = new List<Instance.Process>();

                // Find all processes where all signals are ready
                // and remove them from the list of waiters
                for(var i = remainingprocesses.Count - 1; i >= 0; i--)
                {
                    var rp = remainingprocesses[i];
                    var allInputsReady = 
                        InputSignals(state, rp)
                        .All(x => ready.Contains(x));

                    if (allInputsReady)
                    {
                        current.Add(rp);
                        remainingprocesses.RemoveAt(i);
                    }
                }

                // If a round does not remove any items, we have a circular dependency
                if (current.Count == 0)
                    throw new Exception("Cicular bus dependency detected, remaining processes: " + string.Join(Environment.NewLine, remainingprocesses.Select(x => x.Name)));

                // Register all signals that are now fully written
                var completedsignals = current
                    .SelectMany(
                        x => OutputSignals(state, x)
                    )
                    .Where(
                        x => !ready.Contains(x) && !writers[x]
                            .Any(
                                y => remainingprocesses.Contains(y)
                            )
                    )
                    .Distinct();

                foreach (var n in completedsignals)
                    ready.Add(n);

                roots.Add(current);
            }

            state.DependencyGraph = dependsOn;
            state.SuggestedSchedule = roots;

        }

        /// <summary>
        /// Returns all input signals for the process or connection
        /// </summary>
        /// <param name="state">The state object</param>
        /// <returns>The input signals</returns>
        public IEnumerable<Instance.Signal> InputSignals(ValidationState state, Instance.Process process)
        {
            return process.MappedParameters
                .Where(x => x.MappedItem is Instance.Bus)
                .SelectMany(x =>
                {
                    var isIn = x.MatchedParameter.Direction == AST.ParameterDirection.In;
                    var parentBus = x.MappedItem as Instance.Bus;

                    return parentBus
                        .Instances
                        .OfType<Instance.Signal>()
                        .Where(y =>
                                y.Source.Direction == SignalDirection.Normal
                                ? isIn
                                : !isIn
                        );
                })
                .Concat(
                    process.Instances
                        .OfType<Instance.Bus>()
                        .SelectMany(x => x.Instances.OfType<Instance.Signal>())
                        .Where(x =>
                            state.ItemDirection[process].ContainsKey(x)
                            &&
                            state.ItemDirection[process][x] == ItemUsageDirection.Read
                        )
                )
                .Distinct();
        }

        /// <summary>
        /// Returns all output signals for a process or connection
        /// </summary>
        /// <param name="state">The state object</param>
        /// <returns>The output signals</returns>
        public IEnumerable<Instance.Signal> OutputSignals(ValidationState state, Instance.Process process)
        {
            return process.MappedParameters
                .Where(x => x.MappedItem is Instance.Bus)
                .SelectMany(x => {
                    var isOut = x.MatchedParameter.Direction == AST.ParameterDirection.Out;
                    var parentBus = x.MappedItem as Instance.Bus;

                    return parentBus
                        .Instances
                        .OfType<Instance.Signal>()
                        .Where(y => 
                                y.Source.Direction == SignalDirection.Normal
                                ? isOut
                                : !isOut
                        );
                })
                .Concat(
                    process.Instances
                        .OfType<Instance.Bus>()
                        .SelectMany(x => x.Instances.OfType<Instance.Signal>())
                        .Where(x =>
                            state.ItemDirection[process].ContainsKey(x)
                            &&
                            state.ItemDirection[process][x] == ItemUsageDirection.Write
                        )
                )
                .Distinct();

        }
    }
}
