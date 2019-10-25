using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// Helper visitor that loads signals for a bus
    /// </summary>
    public class PopulateBusSignals : IValidator
    {
        /// <summary>
        /// Runs the validator and loads all busses
        /// </summary>
        /// <param name="state">The state to examine</param>
        public void Validate(ValidationState state)
        {
            foreach (var busitem in state.Modules.SelectMany(x => x.Value.All().OfType<AST.BusDeclaration>()))
            {
                var bus = busitem.Current;
                var scope = state.FindScopeForItem(busitem);

                // Resolve signals from the typename
                if (bus.Signals == null)
                {
                    var signalsource = state.ResolveTypeName(bus.TypeName, scope);
                    if (!signalsource.IsBus)
                        throw new ParserException($"The typename {bus.TypeName.Alias} resolves to {signalsource.Type} but a bus type is required", bus.TypeName);
                    bus.Signals = signalsource.Shape.Signals.Select(x => new AST.BusSignalDeclaration(
                        bus.TypeName.SourceToken,
                        new AST.Identifier(new ParseToken(0, 0, 0, x.Key)),
                        x.Value.Type,
                        null,
                        null,
                        x.Value.Direction
                    )).ToArray();
                }
            }
        }
    }
}