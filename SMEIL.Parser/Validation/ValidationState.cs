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
    /// Container for various items related to the entry-level module and network
    /// </summary>
    public class TopLevelEntry
    {
        /// <summary>
        /// The top-level network module
        /// </summary>
        public AST.Network SourceNetwork { get; internal set; }

        /// <summary>
        /// The top level (fake) network declaration
        /// </summary>
        public AST.InstanceDeclaration NetworkDeclaration { get; internal set; }

        /// <summary>
        /// The top level network instance
        /// </summary>
        public Instance.Network NetworkInstance { get; internal set; }

        /// <summary>
        /// The top level module instance
        /// </summary>
        /// <value></value>
        public Instance.Module ModuleInstance { get; internal set; }

        /// <summary>
        /// The commandline arguments provided to the top-level network
        /// </summary>
        public string[] CommandlineArguments { get; internal set; }

        /// <summary>
        /// The entry module
        /// </summary>
        public AST.Module Module { get; internal set; }

        /// <summary>
        /// The top-level input busses
        /// </summary>
        public readonly List<Instance.Bus> InputBusses = new List<Bus>();
        /// <summary>
        /// The top-level output busses
        /// </summary>
        public readonly List<Instance.Bus> OutputBusses = new List<Bus>();

    }

    /// <summary>
    /// The state created during validation
    /// </summary>
    public class ValidationState
    {
        /// <summary>
        /// Local scopes created by the validator and parser
        /// </summary>
        public readonly Dictionary<object, ScopeState> LocalScopes = new Dictionary<object, ScopeState>();

        /// <summary>
        /// The root scope
        /// </summary>
        private readonly ScopeState m_rootscope;

        /// <summary>
        /// Walks the parents of the item and gets the closest type scope
        /// </summary>
        /// <param name="item">The item to get the type scope for</param>
        /// <returns>The type scope</returns>
        public ScopeState FindScopeForItem<T>(TypedVisitedItem<T> item)
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
        public readonly Dictionary<string, AST.Module> Modules = new Dictionary<string, AST.Module>();

        /// <summary>
        /// The table of current symbols
        /// </summary>
        public ScopeState CurrentScope => SymbolScopes.Peek();

        /// <summary>
        /// The stack with symbol scopes
        /// </summary>
        private Stack<ScopeState> SymbolScopes = new Stack<ScopeState>();

        /// <summary>
        /// Map of signals and their usage
        /// </summary>
        public readonly Dictionary<Instance.IInstance, Dictionary<object, ItemUsageDirection>> ItemDirection = new Dictionary<Instance.IInstance, Dictionary<object, ItemUsageDirection>>();

        /// <summary>
        /// The top-level entry details
        /// </summary>
        public readonly TopLevelEntry TopLevel = new TopLevelEntry();

        /// <summary>
        /// The graph explaining which processes a process depends on
        /// </summary>
        public Dictionary<Instance.Process, Instance.Process[]> DependencyGraph;

        /// <summary>
        /// A sequence of processes, where items in the inner list can
        /// be scheduled in parallel, and the outer list items have
        /// interdependencies
        /// </summary>
        public List<List<Instance.Process>> SuggestedSchedule;

        /// <summary>
        /// Creates a new validation state shadowing the 
        /// </summary>
        public ValidationState()
        {
            m_rootscope = new ScopeState(SymbolScopes);
        }

        /// <summary>
        /// Returns all instances discovered by starting at the top level
        /// </summary>
        public IEnumerable<Instance.IInstance> AllInstances
            => AllInstancesWithParents.Select(x => x.Self);
        // {
        //     get
        //     {
        //         var work = new Queue<Instance.IInstance>();
        //         work.Enqueue(TopLevel.ModuleInstance);

        //         while (work.Count != 0)
        //         {
        //             var item = work.Dequeue();
        //             yield return item;

        //             // Add newly discovered instances
        //             if (item is Instance.Module m)
        //                 foreach (var n in m.Instances)
        //                     work.Enqueue(n);
        //             else if (item is Instance.Network nw)
        //                 foreach (var n in nw.Instances)
        //                     work.Enqueue(n);
        //             else if (item is Instance.Process pr)
        //                 foreach (var n in pr.Instances)
        //                     work.Enqueue(n);
        //             else if (item is Instance.Bus bs)
        //                 foreach (var n in bs.Instances)
        //                     work.Enqueue(n);
        //             else if (item is Instance.FunctionInvocation func)
        //                 foreach (var n in func.Instances)
        //                     work.Enqueue(n);
        //         }
        //     }
        // }

        /// <summary>
        /// Helper class to keep both the instance and the parent link
        /// </summary>
        public class ParentVisitor
        {
            /// <summary>
            /// The item this entry represents
            /// </summary>
            public readonly Instance.IInstance Self;
            /// <summary>
            /// The parent of this instance, or null if there are no parents
            /// </summary>
            public readonly ParentVisitor Parent;

            /// <summary>
            /// Creates a new <see ref="ParentVisitor" />
            /// </summary>
            /// <param name="parent">The parent item</param>
            /// <param name="self">The item</param>
            public ParentVisitor(ParentVisitor parent, Instance.IInstance self)
            {
                Parent = parent;
                Self = self ?? throw new ArgumentNullException(nameof(self));
            }

            /// <summary>
            /// Returns the list of parents from this instance, 
            /// starting with the closest parent
            /// </summary>
            /// <value>The list of parents</value>
            public IEnumerable<Instance.IInstance> Parents
            {
                get 
                {
                    var n = this;
                    while(n != null)
                    {
                        yield return n.Self;
                        n = n.Parent;
                    }
                }
            }

            /// <summary>
            /// Finds the most immediate module, and returns the list of 
            /// parents up to the nearest module, starting with the item itself
            /// and ending with the module
            /// </summary>
            /// <value>The list of parents</value>
            public IEnumerable<Instance.IInstance> ModuleToInstance
            {
                get
                {
                    // Basically TakeWhile, but returning the match itself as well
                    foreach (var item in Parents)
                    {
                        yield return item;
                        if (item is Instance.Module)
                            break;
                    }
                }
            }
                
        }

        /// <summary>
        /// Returns all instances discovered by starting at the top level,
        /// with the immediate parent attached to each
        /// </summary>
        public IEnumerable<ParentVisitor> AllInstancesWithParents
        {
            get
            {
                var work = new Queue<ParentVisitor>();
                work.Enqueue(new ParentVisitor(null, TopLevel.ModuleInstance));

                while (work.Count != 0)
                {
                    var item = work.Dequeue();
                    yield return item;

                    // Add newly discovered instances
                    if (item.Self is Instance.Module m)
                        foreach (var n in m.Instances)
                            work.Enqueue(new ParentVisitor(item, n));
                    else if (item.Self is Instance.Network nw)
                        foreach (var n in nw.Instances)
                            work.Enqueue(new ParentVisitor(item, n));
                    else if (item.Self is Instance.Process pr)
                        foreach (var n in pr.Instances)
                            work.Enqueue(new ParentVisitor(item, n));
                    else if (item.Self is Instance.Bus bs)
                        foreach (var n in bs.Instances)
                            work.Enqueue(new ParentVisitor(item, n));
                    else if (item.Self is Instance.FunctionInvocation func)
                        foreach (var n in func.Instances)
                            work.Enqueue(new ParentVisitor(item, n));
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
        public void RegisterItemUsageDirection(Instance.IParameterizedInstance scope, object item, ItemUsageDirection direction, AST.ParsedItem sourceExpr)
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
                            Direction = scope.SourceParameters[i].Direction,
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
        /// <param name="scope">The scope to use</param>
        /// <returns>An integer</returns>
        public int ResolveToInteger(Expression expression, ScopeState scope)
        {
            return ResolveToInteger(expression, ResolveSymbol(expression, scope), scope);
        }

        /// <summary>
        /// Takes an instance and reduces it to an integer, or throws an exception if this is not possible
        /// </summary>
        /// <param name="source">The source line, used to give indicative error messags</param>
        /// <param name="instance">The instance to reduce</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>An integer</returns>
        public int ResolveToInteger(ParsedItem source, IInstance instance, ScopeState scope)
        {
            if (instance is Instance.ConstantReference constDecl)
            {
                var dt = ResolveTypeName(constDecl.Source.DataType, scope);
                if (dt == null)
                    throw new ParserException($"Failed to resolve data type: {constDecl.Source.DataType}", source);

                if (!dt.IsInteger)
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
        /// <param name="scope">The scope to use</param>
        /// <returns>The resolved item or <c>null<c/></returns>
        public Instance.IInstance ResolveSymbol(Expression expression, ScopeState scope)
        {
            if (expression is NameExpression name)
            {
                var symbol = FindSymbol(name.Name, scope);
                if (symbol is Instance.IInstance || symbol == null)
                    return (Instance.IInstance)symbol;
                // We no longer quick-patch for constant declarations, but require that they are all inserted as instances
                // if (symbol is AST.ConstantDeclaration cdecl)
                //     return new Instance.ConstantReference(cdecl);

                throw new ParserException($"Got element of type {symbol.GetType().Name} but expected an instance", expression);
            }
            else if (expression is LiteralExpression literal)
                return new Instance.Literal(literal.Value);
            else
                throw new ParserException($"Expression not supported for binding parameters", expression);
        }

        /// <summary>
        /// Starts a new scope using this as the base scope
        /// </summary>
        /// <param name="items">The items to register for the scope
        /// <returns>A disposable that will unset the current scope</returns>
        public ScopeState StartScope(params object[] items)
        {
            var sc = new ScopeState(SymbolScopes);
            if (items != null)
                foreach (var item in items.Where(x => x != null))
                    LocalScopes[item] = sc;
            return sc;
        }

        /// <summary>
        /// Constructs a <see cref="AST.Name" /> instance from a string
        /// </summary>
        /// <param name="name">The string to create a name from</param>
        /// <returns>The name</returns>
        public static AST.Name AsName(string name)
        {
            var ids = name.Split(".").Select(x => new AST.Identifier(new ParseToken(0, 0, 0, x))).ToArray();
            return new AST.Name(ids.First().SourceToken, ids, null);
        }


        /// <summary>
        /// Finds the item with the given name
        /// </summary>
        /// <param name="name">The name to look for</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>The item matching the name, or null</returns>
        public object FindSymbol(string name, ScopeState scope)
        {
            return FindSymbol(AsName(name), scope);
        }

        /// <summary>
        /// Finds the item with the given name
        /// </summary>
        /// <param name="name">The name to look for</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>The item matching the name, or null</returns>
        public object FindSymbol(AST.Identifier name, ScopeState scope)
        {
            return FindSymbol(new AST.Name(name.SourceToken, new [] { name ?? throw new ArgumentNullException(nameof(name)) }, null), scope);
        }

        /// <summary>
        /// Finds the item with the given name
        /// </summary>
        /// <param name="name">The name to look for</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>The item matching the name, or null</returns>
        public object FindSymbol(AST.Name name, ScopeState scope)
        {
            // Keep a list of matches to give better error messages
            var matched = new List<string>();

            object res = null;
            foreach (var id in name.Identifier)
            {
                res = null;
                // Check that we are either in the first access where we find all symbols,
                // or that the symbol is in the local table
                if (matched.Count == 0 || scope.SelfContainsSymbol(id.Name))
                {
                    // This should only fail for the very first item, as the others will fail the check above
                    // and go to the exception message below
                    if (!scope.SymbolTable.TryGetValue(id.Name, out res))
                        throw new ParserException($"Failed to locate \"{id.Name}\" in sequence {string.Join(".", name.Identifier.Select(x => x.Name))}", id);

                    if (res == null)
                        throw new ParserException($"Null value in symbol table for \"{id.Name}\" in sequence {string.Join(".", name.Identifier.Select(x => x.Name))}", id);
                }
                else
                {
                    // No match, look for case sensitive-match, could do hamming distance suggestions
                    var closest = scope.SymbolTable.Keys.Where(x => string.Equals(x, id.Name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (closest != null)
                        throw new ParserException($"No such item \"{id.Name}\" in item {string.Join(".", matched)} (did you mean \"{closest}\"?)", id);

                    throw new ParserException($"No such item \"{id.Name}\" in item {string.Join(".", matched)}", id);
                }

                matched.Add(id.Name);

                // We do not need a symbol table for the last item, but all others need a local symbol table
                if (matched.Count != name.Identifier.Length && !LocalScopes.TryGetValue(res, out scope))
                    throw new ParserException($"No symbol table for \"{id.Name}\" in item {string.Join(".", matched)}", id);
            }

            return res;
        }

        /// <summary>
        /// Finds the item with the given name
        /// </summary>
        /// <param name="name">The name to look for</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>The item matching the name, or null</returns>
        public object FindTypeDefinition(AST.Name name, ScopeState scope)
        {
            // Keep a list of matches to give better error messages
            var matched = new List<string>();

            object res = null;
            foreach (var id in name.Identifier)
            {
                res = null;
                // Check that we are either in the first access where we find all symbols,
                // or that the symbol is in the local table
                if (matched.Count == 0 || scope.SelfContainsTypeDef(id.Name))
                {
                    // This should only fail for the very first item, as the others will fail the check above
                    // and go to the exception message below
                    if (!scope.TypedefinitionTable.TryGetValue(id.Name, out res))
                        throw new ParserException($"Failed to locate type \"{id.Name}\" in sequence {string.Join(".", name.Identifier.Select(x => x.Name))}", id);

                    if (res == null)
                        throw new ParserException($"Null value in symbol table for \"{id.Name}\" in sequence {string.Join(".", name.Identifier.Select(x => x.Name))}", id);
                }
                else
                {
                    throw new ParserException($"No such item \"{id.Name}\" in item {string.Join(".", matched)}", id);
                }

                matched.Add(id.Name);

                // We do not need a symbol table for the last item, but all others need a local symbol table
                if (matched.Count != name.Identifier.Length && !LocalScopes.TryGetValue(res, out scope))
                    throw new ParserException($"No symbol table for \"{id.Name}\" in item {string.Join(".", matched)}", id);
            }

            return res;
        }

        /// <summary>
        /// Resolves a data type in the given scope
        /// </summary>
        /// <param name="name">The name to resolve</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>The datatype</returns>
        public DataType ResolveTypeName(string name, ScopeState scope)
        {
            return ResolveTypeName(new AST.TypeName(AsName(name), null), scope);
        }

        /// <summary>
        /// Resolves a data type in the given scope
        /// </summary>
        /// <param name="name">The name to resolve</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>The datatype</returns>
        public DataType ResolveTypeName(AST.TypeName name, ScopeState scope)
        {
            var visited = new HashSet<AST.Name>();
            var orig = name;

            while(true)
            {
                if (name.IntrinsicType != null)
                    return name.IntrinsicType;

                if (visited.Contains(name.Alias))
                    throw new ParserException($"Circular typedefinition detected", orig);
                visited.Add(name.Alias);

                var rs = FindTypeDefinition(name.Alias, scope);
                if (rs == null)
                    throw new ParserException($"Failed to find type named: {name.Alias}", name);
                if (rs is AST.TypeName nt)                
                    name = nt;
                else if (rs is AST.DataType dt)
                    return dt;
                else if (rs is AST.TypeDefinition td)
                {
                    if (td.Shape != null)
                        return new DataType(td.SourceToken, td.Shape);

                    name = td.Alias;
                }
                else if (rs is AST.EnumDeclaration ed)
                    return new DataType(ed.SourceToken, ed);
                else
                    throw new ParserException($"Resolved {name.Alias} to {rs}, expected a type", name);
            }
        }

        /// <summary>
        /// Returns the actual type for an instance
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public DataType InstanceType(Instance.IInstance instance)
        {
            if (instance is Instance.Bus busInstance)
                return busInstance.ResolvedType;
            else if (instance is Instance.ConstantReference constRef)
                return constRef.ResolvedType;
            else if (instance is Instance.Literal literalInstance)
                return new AST.DataType(literalInstance.Source.SourceToken, literalInstance.Source.Type, -1);
            else if (instance is Instance.Signal signalInstance)
                return signalInstance.ResolvedType;
            else if (instance is Instance.Variable variableInstance)
                return variableInstance.ResolvedType;
            else if (instance is Instance.EnumFieldReference enumFieldReference)
                return new AST.DataType(enumFieldReference.Source.SourceToken, enumFieldReference.ParentType.Source);
            else
                throw new ArgumentException($"Unable to get the type of {instance}");
        }

        /// <summary>
        /// Runs the validator on all loaded modules
        /// </summary>
        public void Validate()
        {
            var modules = new IValidator[] {
                new VerifyIdentifiers(),
                new CreateInstances(),
                new VerifyConstantAssignments(),
                new WireParameters(),
                new AssignTypes(),
                new BuildDependencyGraph(),
            };

            foreach (var validator in modules)
                validator.Validate(this);

        }

        /// <summary>
        /// Registers all symbols for the given module in the given scope
        /// </summary>
        /// <param name="module">The module to find names in</param>
        /// <param name="scope">The scope to use</param>
        public void RegisterSymbols(AST.Declaration decl, ScopeState scope)
        {
            if (decl == null)
                throw new ArgumentNullException(nameof(decl));

            if (decl is EnumDeclaration enumDecl)
            {
                scope.SymbolTable.Add(enumDecl.Name.Name, decl);
                scope.TypedefinitionTable.Add(enumDecl.Name.Name, decl);

                if (!LocalScopes.TryGetValue(decl, out var subscope))
                    using(subscope = StartScope(decl))
                    { /* Dispose immediately */}

                foreach (var e in enumDecl.Fields)
                    subscope.SymbolTable.Add(e.Name.Name, e);
            }
            else if (decl is FunctionDefinition func)
            {
                scope.SymbolTable.Add(func.Name.Name, decl);
            }
            else if (decl is BusDeclaration bus)
            {
                scope.SymbolTable.Add(bus.Name.Name, decl);
                if (!LocalScopes.TryGetValue(decl, out var subscope))
                    using (subscope = StartScope(decl))
                    { /* Dispose immediately */}

                // Resolve signals from the typename
                if (bus.Signals == null)
                {
                    var signalsource = ResolveTypeName(bus.TypeName, scope);
                    if (!signalsource.IsBus)
                        throw new ParserException($"The typename {bus.TypeName.Alias} resolves to {signalsource.Type} but a bus type is required", bus.TypeName);
                    bus.Signals = signalsource.Shape.Signals.Select(x => new AST.BusSignalDeclaration(
                        bus.TypeName.SourceToken,
                        new AST.Identifier(new ParseToken(0, 0, 0, x.Key)),
                        x.Value,
                        null,
                        null
                    )).ToArray();
                }

                foreach (var signal in bus.Signals)
                    subscope.SymbolTable.Add(signal.Name.Name, signal);
            }
            else if (decl is VariableDeclaration variable)
            {
                // Ignored until we can create an instance
            }
            else if (decl is ConstantDeclaration constant)
            {
                // Ignored until we can create an instance
            }
            else if (decl is InstanceDeclaration inst)
            {
                scope.SymbolTable.Add(inst.Name.Name.Name, decl);
            }
            else if (decl is ConnectDeclaration connDecl)
            {
                // No wiring required here for connections
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
        /// <param name="scope">The scope to use</param>
        public void RegisterSymbols(AST.Module module, ScopeState scope)
        {
            foreach (var decl in module.Declarations)
            {
                if (decl is TypeDefinition tdef)
                    scope.TypedefinitionTable.Add(tdef.Name.Name, tdef);
                else
                    RegisterSymbols(decl, scope);
            }

            foreach (var ent in module.Entities)
            {
                if (ent is AST.Process process)
                {
                    scope.SymbolTable.Add(process.Name.Name, ent);

                    foreach (var decl in process.Declarations)
                        RegisterSymbols(decl, scope);
                }
                else if (ent is AST.Network network)
                {
                    scope.SymbolTable.Add(network.Name.Name, ent);
                    foreach (var decl in network.Declarations)
                        RegisterSymbols(decl, scope);
                }
                else 
                    throw new Exception($"Unexpected entity: {ent}");
            }

        }

        /// <summary>
        /// Checks if two data types can be compared for equality
        /// </summary>
        /// <param name="a">One data type</param>
        /// <param name="b">Another data type</param>
        /// <param name="scope">The scope to use</param>
        /// <returns><c>true</c> if the types can be compared for equality; <c>false</c> otherwise</returns>
        public bool CanEqualityCompare(DataType a, DataType b, ScopeState scope)
        {
            return CanUnifyTypes(a, b, scope);
        }

        /// <summary>
        /// Checks if two data types can be unified
        /// </summary>
        /// <param name="a">One data type</param>
        /// <param name="b">Another data type</param>
        /// <param name="scope">The scope to use</param>
        /// <returns><c>true</c> if the types can be unified; <c>false</c> otherwise</returns>
        public bool CanUnifyTypes(DataType a, DataType b, ScopeState scope)
        {
            return TryGetUnifiedType(a, b, scope) != null;
        }

        /// <summary>
        /// Combines two data types into the largest unified type, or throws an exception
        /// </summary>
        /// <param name="a">One data type</param>
        /// <param name="b">Another data type</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>The unified data type</returns>
        private DataType TryGetUnifiedType(DataType a, DataType b, ScopeState scope)
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
                    // Check that the other side is also a bus
                    if (b.Type == ILType.Bus)
                    {
                        var a_signals = ResolveSignalsToIntrinsic(a.Shape.Signals, scope);
                        var b_signals = ResolveSignalsToIntrinsic(b.Shape.Signals, scope);

                        // Build a unified type for the shapes
                        var shape = new BusShape(a.Shape.SourceToken, a_signals);

                        foreach (var n in b_signals)
                            if (!shape.Signals.TryGetValue(n.Key, out var t))
                                shape.Signals.Add(n.Key, n.Value);
                            else if (!object.Equals(t, n.Value))
                                // We do not expand variables inside busses
                                // as there is no good logic for expansion/contraction here
                                //shape.Signals[n.Key] = new AST.TypeName(UnifiedType(n.Value.IntrinsicType, t.IntrinsicType, scope), null);
                                return null;

                        return new DataType(a.SourceToken, shape);
                    }
                    break;

                case ILType.Enumeration:
                    if (b.Type == ILType.Enumeration && a.EnumType == b.EnumType)
                        return a;
                    break;
            }

            return null;
        }

        /// <summary>
        /// Resolves all type aliases on the given input type and returns a similar type with only intrinsic values
        /// </summary>
        /// <param name="input">The item to resolve</param>
        /// <returns>The resolved type</returns>
        public DataType ResolveToIntrinsics(DataType input, ScopeState scope)
        {
            if (input == null || !input.IsBus)
                return input;

            return new DataType(
                input.SourceToken, 
                new BusShape(
                    input.Shape.SourceToken, 
                    ResolveSignalsToIntrinsic(input.Shape.Signals, scope)
                )
            ); 
        }

        /// <summary>
        /// Resolves the data types of all signals on a bus
        /// </summary>
        /// <param name="bus">The bus to examine</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>The data type of the of the bus</returns>
        public DataType ResolveBusSignalTypes(Instance.Bus bus, Validation.ScopeState scope)
        {
            if (bus.ResolvedSignalTypes == null)
            {
                var shape = this.ResolveSignalsToIntrinsic(bus.ResolvedType.Shape.Signals, scope);
                bus.ResolvedSignalTypes = shape.ToDictionary(x => x.Key, x => x.Value.IntrinsicType);
                foreach (var s in bus.Instances.OfType<Instance.Signal>())
                    s.ResolvedType = bus.ResolvedSignalTypes[s.Name];
            }

            return new DataType(bus.Source.SourceToken, new BusShape(bus.Source.SourceToken, bus.Instances.OfType<Instance.Signal>().Select(x => x.Source)));
        }


        /// <summary>
        /// Expands a set of signals to their base types (i.e. erases type definitions)
        /// </summary>
        /// <param name="signals"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        public IDictionary<string, TypeName> ResolveSignalsToIntrinsic(IDictionary<string, TypeName> signals, ScopeState scope)
        {
            return signals
                .Select(x => new KeyValuePair<string, DataType>(x.Key, ResolveTypeName(x.Value, scope)))
                .ToDictionary(x => x.Key, x => new AST.TypeName(x.Value, null));

        }

        /// <summary>
        /// Combines two data types into the largest unified type, or throws an exception
        /// </summary>
        /// <param name="a">One data type</param>
        /// <param name="b">Another data type</param>
        /// <param name="scope">The scope to use</param>
        /// <returns>The unified data type</returns>
        public DataType UnifiedType(DataType a, DataType b, ScopeState scope)
        {
            return TryGetUnifiedType(a, b, scope) ?? throw new Exception($"Unable to unify types {a} and {b}");
        }

        /// <summary>
        /// Checks if a type can be type-casted to another type
        /// </summary>
        /// <param name="sourceType">The source type</param>
        /// <param name="targetType">The type being casted to</param>
        /// <param name="scope">The scope to use</param>
        /// <returns><c>true</c> if the <paramref name="sourceType" /> can be cast to <paramref name="targetType" />; false otherwise</returns>
        public bool CanTypeCast(DataType sourceType, DataType targetType, ScopeState scope)
        {
            if (object.Equals(sourceType, targetType) || CanUnifyTypes(sourceType, targetType, scope))
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
    }
}
