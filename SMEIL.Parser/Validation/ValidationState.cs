using System;
using System.Linq;
using System.Collections.Generic;
using SMEIL.Parser.AST;
using SMEIL.Parser.Instance;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// The usage of a given item
    /// </summary>
    [Flags]
    public enum ItemUsageDirection
    {
        /// <summary>The parameter is only read</summary>
        Read = 1,
        /// <summary>The parameter is only written</summary>
        Write = 2,
        /// <summary>The parameter is read and written</summary>
        Both = 3
    }

    /// <summary>
    /// The state created during validation
    /// </summary>
    public class ValidationState
    {
        /// <summary>
        /// Local scopes created by the validator and parser
        /// </summary>
        public readonly Dictionary<object, IDictionary<string, object>> LocalScopes = new Dictionary<object, IDictionary<string, object>>();

        /// <summary>
        /// Walks the parents of the item and gets the closest type scope
        /// </summary>
        /// <param name="item">The item to get the type scope for</param>
        /// <returns>The type scope</returns>
        public IDictionary<string, object> FindSymbolTable<T>(TypedVisitedItem<T> item)
            where T : AST.ParsedItem
        {
            foreach (var n in new [] { item.Current }.Concat(item.Parents))
            {
                if (LocalScopes.TryGetValue(n, out var res))
                    return res;

                if (n is Parser.Instance.IInstance pi && LocalScopes.TryGetValue(pi.Name, out res))
                        return res;
            }

            throw new Exception("No symbol table found for item");
        }

        /// <summary>
        /// The list of loaded modules, where the key is the path
        /// </summary>
        public readonly Dictionary<string, AST.Module> Modules = new Dictionary<string, Module>();

        /// <summary>
        /// The table of current symbols
        /// </summary>
        public IDictionary<string, object> SymbolTable => SymbolScopes.Peek();

        /// <summary>
        /// The stack with symbol scopes
        /// </summary>
        private Stack<ChainedDictionary<string, object>> SymbolScopes = new Stack<ChainedDictionary<string, object>>();

        /// <summary>
        /// The top-level network module
        /// </summary>
        public AST.Network TopLevelNetwork { get; internal set; }

        /// <summary>
        /// The top level (fake) network declaration
        /// </summary>
        public AST.InstanceDeclaration TopLevelNetworkDeclaration { get; internal set; }

        /// <summary>
        /// The top level network instance
        /// </summary>
        public Instance.Network TopLevelNetworkInstance { get; internal set; }

        /// <summary>
        /// The entry module
        /// </summary>
        public AST.Module EntryModule { get; internal set; }

        /// <summary>
        /// Map of signals and their usage
        /// </summary>
        public readonly Dictionary<Instance.Process, Dictionary<object, ItemUsageDirection>> ItemDirection = new Dictionary<Instance.Process, Dictionary<object, ItemUsageDirection>>();

        /// <summary>
        /// Creates a new validation state shadowing the 
        /// </summary>
        public ValidationState()
        {
            SymbolScopes.Push(new ChainedDictionary<string, object>(null));
        }

        /// <summary>
        /// Returns all instances discovered by starting at the top level
        /// </summary>
        public IEnumerable<Instance.IInstance> AllInstances
        {
            get
            {
                var work = new Queue<Instance.IInstance>();
                work.Enqueue(TopLevelNetworkInstance);

                while (work.Count != 0)
                {
                    var item = work.Dequeue();
                    yield return item;

                    // Add newly discovered instances
                    if (item is Instance.Network nw)
                        foreach (var n in nw.Instances)
                            work.Enqueue(n);
                }
            }
        }

        /// <summary>
        /// Registers an item for usage in a particular direction
        /// </summary>
        /// <param name="scope">The scope to use</param>
        /// <param name="item">The item to register</param>
        /// <param name="direction">The direction to register</param>
        /// <param name="sourceExpr">The source expression used for error messages</param>
        public void RegisterItemUsageDirection(Instance.Process scope, object item, ItemUsageDirection direction, AST.ParsedItem sourceExpr)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!ItemDirection.TryGetValue(scope, out var usagescope))
                ItemDirection[scope] = usagescope = new Dictionary<object, ItemUsageDirection>();

            if (item is Instance.Signal signalInstance)
            {
                // Get list of parameters with direction from instantiation
                var directionParam = scope
                    .MappedParameters
                    .Zip(
                        Enumerable.Range(0, scope.MappedParameters.Count),
                        (x, i) => new
                        {
                            Direction = scope.ProcessDefinition.Parameters[i].Direction,
                            Bus = x.MappedItem
                        }
                    )
                    .Where(x => x.Bus == scope)
                    .FirstOrDefault();

                // Local bus is bi-directional and does not need checking
                if (directionParam != null) 
                {
                    var definedDirection = directionParam.Direction;

                    if (definedDirection == ParameterDirection.Out && direction != ItemUsageDirection.Write)
                        throw new ParserException($"Can only write to output signal: {signalInstance.Name}", sourceExpr);
                    if (definedDirection == ParameterDirection.In && direction != ItemUsageDirection.Read)
                        throw new ParserException($"Can only read from input signal: {signalInstance.Name}", sourceExpr);
                }
            }

            if (!usagescope.TryGetValue(item, out var d))
                usagescope[item] = direction;
            else if (d != direction)
                usagescope[item] = ItemUsageDirection.Both;
        }

        /// <summary>
        /// Resolves an expression to an integer constant, or throws an exception if this is not possible
        /// </summary>
        /// <param name="expression">The expression to resolve</param>
        /// <param name="symboltable">The optional symbol table</param>
        /// <returns>An integer</returns>
        public int ResolveToInteger(Expression expression, Dictionary<string, object> symboltable = null)
        {
            return ResolveToInteger(expression, ResolveSymbol(expression, symboltable));
        }

        /// <summary>
        /// Takes an instance and reduces it to an integer, or throws an exception if this is not possible
        /// </summary>
        /// <param name="source">The source line, used to give indicative error messags</param>
        /// <param name="instance">The instance to reduce</param>
        /// <returns>An integer</returns>
        public int ResolveToInteger(ParsedItem source, IInstance instance)
        {
            if (instance is Instance.ConstantReference constDecl)
            {
                if (!constDecl.Source.DataType.IsInteger)
                    throw new ParserException($"Cannot use item of type {constDecl.Source.DataType} as an integer is required", source);
                return ((AST.IntegerConstant)((AST.LiteralExpression)constDecl.Source.Expression).Value).ToInt32;
            }
            else if (instance is Instance.Literal lit)
            {
                if (!(lit.Source is AST.IntegerConstant intConst))
                    throw new ParserException($"Cannot use literal of type {lit.Source} as an integer is required", source);

                return intConst.ToInt32;
            }

            throw new ParserException($"Must use a constant or literal integer value, got {instance}", source);
        }

        /// <summary>
        /// Resolves a symbol to be a variable or a bus
        /// </summary>
        /// <param name="expression">The expression to resolve</param>
        /// <param name="symboltable">The symbol table to resolve with</param>
        /// <returns>The resolved item or <c>null<c/></returns>
        public Instance.IInstance ResolveSymbol(Expression expression, IDictionary<string, object> symboltable = null)
        {
            if (expression is NameExpression name)
            {
                var symbol = FindSymbol(name.Name, symboltable);
                if (symbol is Instance.IInstance || symbol == null)
                    return (Instance.IInstance)symbol;

                throw new ParserException($"Got element of type {symbol.GetType().Name} but expected an instance", expression);
            }
            else if (expression is LiteralExpression literal)
                return new Instance.Literal(literal.Value);
            else
                throw new ParserException($"Composite expressions not yet supported for binding parameters", expression);
        }

        // /// <summary>
        // /// Re-enters a scope using an instance
        // /// </summary>
        // /// <param name="item">The item used to register the scope
        // /// <returns>A disposable that </returns>
        // public IDisposable EnterScope(object item)
        // {
        //     return new ReScopeDisposer(this, LocalScopes[item] as ChainedDictionary<string, object>);
        // }

        /// <summary>
        /// Starts a new scope using this as the base scope
        /// </summary>
        /// <param name="items">The items to register for the scope
        /// <returns>A disposable that will unset the current scope</returns>
        public IDisposable StartScope(params object[] items)
        {
            var sc = new SubScopeDisposer(SymbolScopes);
            if (items != null)
                foreach (var item in items.Where(x => x != null))
                    LocalScopes[item] = SymbolTable;
            return sc;
        }

        /// <summary>
        /// Starts a new scope using this as the base scope
        /// </summary>
        /// <returns>A disposable that will unset the current scope</returns>
        public IDisposable StartScope(object item = null) {
            var sc = new SubScopeDisposer(SymbolScopes);
            if (item != null)
                LocalScopes[item] = SymbolTable;
            return sc;
        }

        // /// <summary>
        // /// Internal class for reattaching a chained symbol scope
        // /// </summary>
        // private class ReScopeDisposer : IDisposable
        // {
        //     /// <summary>
        //     /// The previous stack
        //     /// </summary>
        //     private readonly Stack<ChainedDictionary<string, object>> m_previous;
        //     /// <summary>
        //     /// The validation state parent
        //     /// </summary>
        //     private readonly ValidationState m_parent;
        //     /// <summary>
        //     /// The scope just created
        //     /// </summary>
        //     private readonly ChainedDictionary<string, object> m_item;
        //     /// <summary>
        //     /// Flag keeping track of the dispose state
        //     /// </summary>
        //     private bool m_isDisposed = false;

        //     /// <summary>
        //     /// Creates a new disposable scope
        //     /// </summary>
        //     /// <param name="parent">The scope stack</param>
        //     public ReScopeDisposer(ValidationState parent, ChainedDictionary<string, object> self)
        //     {
        //         m_parent = parent ?? throw new ArgumentNullException(nameof(parent));
        //         m_previous = m_parent.SymbolScopes;
        //         parent.SymbolScopes = new Stack<ChainedDictionary<string, object>>((self ?? throw new ArgumentNullException(nameof(self))).GetList());
        //     }

        //     /// <summary>
        //     /// Disposes the current scope
        //     /// </summary>
        //     public void Dispose()
        //     {
        //         if (!m_isDisposed)
        //         {
        //             m_parent.SymbolScopes = m_previous;
        //             m_isDisposed = true;
        //         }
        //     }            
        // }


        /// <summary>
        /// Internal class for starting a new chained symbol scope
        /// </summary>
        private class SubScopeDisposer : IDisposable
        {
            /// <summary>
            /// The stack of scopes
            /// </summary>
            private readonly Stack<ChainedDictionary<string, object>> m_parent;
            /// <summary>
            /// The scope just created
            /// </summary>
            private readonly ChainedDictionary<string, object> m_item;
            /// <summary>
            /// Flag keeping track of the dispose state
            /// </summary>
            private bool m_isDisposed = false;

            /// <summary>
            /// Creates a new disposable scope
            /// </summary>
            /// <param name="parent">The scope stack</param>
            public SubScopeDisposer(Stack<ChainedDictionary<string, object>> parent)
            {
                m_parent = parent ?? throw new ArgumentNullException(nameof(parent));
                m_item = new ChainedDictionary<string, object>(m_parent.Peek());
                m_parent.Push(m_item);
            }

            /// <summary>
            /// Disposes the current scope
            /// </summary>
            public void Dispose()
            {
                if (!m_isDisposed)
                {
                    m_isDisposed = true;
                    if (m_parent.Peek() != m_item)
                        throw new Exception("Unexpected scope disposal");
                    m_parent.Pop();
                }
            }
        }

        /// <summary>
        /// Finds the item with the given name
        /// </summary>
        /// <param name="name">The name to look for</param>
        /// <param name="symboltable">The table to use, defaults to the current</param>
        /// <returns>The item matching the name, or null</returns>
        public object FindSymbol(AST.Identifier name, IDictionary<string, object> symboltable = null)
        {
            return FindSymbol(new AST.Name(name.SourceToken, new [] { name ?? throw new ArgumentNullException(nameof(name)) }, null), symboltable);
        }

        /// <summary>
        /// Finds the item with the given name
        /// </summary>
        /// <param name="name">The name to look for</param>
        /// <param name="symboltable">The table to use, defaults to the current</param>
        /// <returns>The item matching the name, or null</returns>
        public object FindSymbol(AST.Name name, IDictionary<string, object> symboltable = null)
        {
            if (symboltable == null)
                LocalScopes.TryGetValue(name, out symboltable);

            // Keep a list of matches to give better error messages
            var matched = new List<string>();

            symboltable = symboltable ?? SymbolTable;
            object res = null;
            foreach (var id in name.Identifier)
            {
                res = null;
                // Check that we are either in the first access where we find all symbols,
                // or that the symbol is in the local table
                if (matched.Count == 0 || (((ChainedDictionary<string, object>)symboltable).SelfContainsKey(id.Name)))
                {
                    // This should only fail for the very first item, as the others will fail the check above
                    // and go to the exception message below
                    if (!symboltable.TryGetValue(id.Name, out res))
                        throw new ParserException($"Failed to locate \"{id.Name}\" in sequence {string.Join(".", name.Identifier.Select(x => x.Name))}", name);

                    if (res == null)
                        throw new ParserException($"Null value in symbol table for \"{id.Name}\" in sequence {string.Join(".", name.Identifier.Select(x => x.Name))}", name);
                }
                else
                {
                    throw new ParserException($"No such item \"{id.Name}\" in item {string.Join(".", matched)}", name);
                }

                matched.Add(id.Name);

                // We do not need a symbol table for the last item, but all others need a local symbol table
                if (matched.Count != name.Identifier.Length && !LocalScopes.TryGetValue(res, out symboltable))
                    throw new ParserException($"No symbol table for \"{id.Name}\" in item {string.Join(".", matched)}", name);
            }

            return res;
        }

        /// <summary>
        /// Runs the validator on all loaded modules
        /// </summary>
        public void Validate()
        {
            var modules = new IValidator[] {                
                new CheckInOut(),
                new CreateInstances(),
                new WireParameters(),
                new AssignTypes(),
            };

            foreach (var validator in modules)
                validator.Validate(this);

        }

        /// <summary>
        /// Registers all symbols for the given module in the given scope
        /// </summary>
        /// <param name="module">The module to find names in</param>
        /// <param name="symboltable">The table to use, defaults to the current</param>
        public void RegisterSymbols(AST.Declaration decl, IDictionary<string, object> symboltable = null)
        {
            if (decl == null)
                throw new ArgumentNullException(nameof(decl));

            if (symboltable == null)
                LocalScopes.TryGetValue(decl, out symboltable);

            symboltable = symboltable ?? SymbolTable;

            if (decl is EnumDeclaration enumDecl)
            {
                symboltable.Add(enumDecl.Name.Name, decl);
                if (!LocalScopes.TryGetValue(decl, out var subscope))
                    LocalScopes[decl] = subscope = new ChainedDictionary<string, object>(symboltable);

                foreach (var e in enumDecl.Fields)
                    subscope.Add(e.Name.Name, e);
            }
            else if (decl is FunctionDeclaration func)
            {
                symboltable.Add(func.Name.Name, decl);
            }
            else if (decl is BusDeclaration bus)
            {
                symboltable.Add(bus.Name.Name, decl);
                if (!LocalScopes.TryGetValue(decl, out var subscope))
                    LocalScopes[decl] = subscope = new ChainedDictionary<string, object>(symboltable);

                foreach (var signal in bus.Signals)
                    subscope.Add(signal.Name.Name, signal);
            }
            else if (decl is VariableDeclaration variable)
            {
                symboltable.Add(variable.Name.Name, decl);
            }
            else if (decl is ConstantDeclaration constant)
            {
                symboltable.Add(constant.Name.Name, decl);
            }
            else if (decl is InstanceDeclaration inst)
            {
                symboltable.Add(inst.Name.Name.Name, decl);
            }
            else
            {
                throw new Exception($"Unexpeced declaration type: {decl.GetType()}");
            }
        }

        /// <summary>
        /// Registers all symbols for the given module in the given scope
        /// </summary>
        /// <param name="module">The module to find names in</param>
        /// <param name="symboltable">The table to use, defaults to the current</param>
        public void RegisterSymbols(AST.Module module, IDictionary<string, object> symboltable = null)
        {
            if (symboltable == null)
                LocalScopes.TryGetValue(module, out symboltable);

            symboltable = symboltable ?? SymbolTable;

            foreach (var ent in module.Entities)
            {
                if (ent is AST.Process process)
                {
                    symboltable.Add(process.Name.Name, ent);

                    foreach (var decl in process.Declarations)
                        RegisterSymbols(decl, symboltable);
                }
                else if (ent is AST.Network network)
                {
                    symboltable.Add(network.Name.Name, ent);
                    foreach (var decl in network.Declarations)
                        RegisterSymbols(decl, symboltable);
                }
                else 
                    throw new Exception($"Unexpected ");
            }
        }
    }
}
