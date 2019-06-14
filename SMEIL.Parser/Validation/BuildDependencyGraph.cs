using System;
using System.Collections.Generic;
using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    public class BuildDependencyGraph : IValidator
    {
        /// <summary>
        /// Returns all input signals for a process
        /// </summary>
        /// <param name="state">The state object</param>
        /// <param name="process">The proccess to get the input signals for</param>
        /// <returns>The input signals</returns>
        private IEnumerable<Instance.Signal> InputSignals(ValidationState state, Instance.Process process)
        {
            return process.MappedParameters
                .Where(x => x.MappedItem is Instance.Bus && x.MatchedParameter.Direction == AST.ParameterDirection.In)
                .Select(x => x.MappedItem)
                .Cast<Instance.Bus>()
                .SelectMany(x => x.Instances.OfType<Instance.Signal>())
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
        /// Returns all output signals for a process
        /// </summary>
        /// <param name="state">The state object</param>
        /// <param name="process">The proccess to get the output signals for</param>
        /// <returns>The output signals</returns>
        private IEnumerable<Instance.Signal> OutputSignals(ValidationState state, Instance.Process process)
        {
            return process.MappedParameters
                .Where(x => x.MappedItem is Instance.Bus && x.MatchedParameter.Direction == AST.ParameterDirection.Out)
                .Select(x => x.MappedItem)
                .Cast<Instance.Bus>()
                .SelectMany(x => x.Instances.OfType<Instance.Signal>())
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

        /// <summary>
        /// Validates the module
        /// </summary>
        /// <param name="state">The validation state</param>
        public void Validate(ValidationState state)
        {
            // Keep a list of unscheduled processes
            var remainingprocesses = state.AllInstances.OfType<Instance.Process>().ToList();
            
            // Keep track of wavefronts of processes
            var roots = new List<List<Instance.Process>>();

            // Map all output signals to their writers
            var writers = state.AllInstances
                .OfType<Instance.Process>()
                .SelectMany(
                    x => OutputSignals(state, x)
                        .Select(z => new { P = x, S = z })
                )
                .GroupBy(x => x.S)
                .ToDictionary(x => x.Key, y => y.Select(x => x.P).ToList());
            

            var doublewrite = writers
                .Where(x => x.Value.Count > 1)
                .Select(x => x.Key)
                .FirstOrDefault();

            // The code here handles double writes, but FPGA tools generally dislike double writers
            if (doublewrite != null)
                throw new ParserException($"Multiple writers found for signal {doublewrite.Name}: {Environment.NewLine} {string.Join(Environment.NewLine, writers[doublewrite].Select(x => x.Source.Name == null ? x.Source.SourceItem.ToString() : x.Source.Name.SourceToken.ToString()))}", doublewrite.Source);

            // Register all inputs as having no writers
            foreach (var s in state.TopLevel.InputBusses.SelectMany(x => x.Instances.OfType<Instance.Signal>()))
                writers.Add(s, new List<Instance.Process>());

            // Prepare a list of signals processed by all writers
            var ready = new HashSet<Instance.Signal>(
                writers.Where(x => x.Value.Count == 0).Select(x => x.Key)
            );

            // Find all processes that each process depends on
            var dependsOn = state.AllInstances
                .OfType<Instance.Process>()
                .SelectMany(
                    x => InputSignals(state, x)
                        .Select(z => new { P = x, D = writers[z] })
                )
                .GroupBy(x => x.P)
                .ToDictionary(x => x.Key, x => x.SelectMany(y => y.D).ToArray());

            // Keep removing processes until all have been scheduled
            while(remainingprocesses.Count > 0)
            {
                var current = new List<Instance.Process>();

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

                if (current.Count == 0)
                    throw new Exception("Cicular bus dependency detected");

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
    }
}
