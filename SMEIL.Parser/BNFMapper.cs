using System.Linq.Expressions;
using System;
using System.Linq;
using System.Collections.Generic;

using static SMEIL.Parser.BNF.StaticUtil;

namespace SMEIL.Parser
{
    /// <summary>
    /// Implementation of a BNF based mapper which constructs AST instances from their parsed tokens.
    /// The code in this module enforces the SMEIL grammar document
    /// </summary>
    public static class BNFMapper
    {
        /// <summary>
        /// Helper class to capture left-recursive binary operations
        /// </summary>
        private class BinOpMatch
        {
            /// <summary>
            /// The matched operation
            /// </summary>
            public AST.BinaryOperation Operation;
            /// <summary>
            /// The matched expression
            /// </summary>
            public AST.Expression Expression;
        }

        /// <summary>
        /// Parses the token stream and returns an abstract syntax tree
        /// </summary>
        /// <param name="tokens">The tokens to parse</param>
        /// <returns>The parsed syntax tree</returns>
        public static AST.Module Parse(IEnumerable<ParseToken> tokens)
        {
            return Parse(new BufferedEnumerator<ParseToken>(tokens.GetEnumerator()));
        }

        /// <summary>
        /// Parses the token stream and returns an abstract syntax tree
        /// </summary>
        /// <param name="tokens">The tokens to parse</param>
        /// <returns>The parsed syntax tree</returns>
        public static AST.Module Parse(IBufferedEnumerator<ParseToken> tokens)
        {
            // Basic identifier definition
            var ident = Mapper(RegEx(@"\w[\w\d\-_]*"), x => new AST.Identifier(x.Item));
            
            var integer = Mapper(
                RegEx(@"[0-9]+|(0x[0-9|a-f|A-F]+)|(0o[0-7]+)"),
                x => new AST.IntegerConstant(x.Item, x.Item.Text)
            );

            // The int32 limitation is not in the BNF, but we use it here
            // to limit the size of generated arrays and types
            // to something that can be expressed in hardware
            var int32literal = Mapper(
                CustomItem(x => int.TryParse(x, out var _)),
                x => int.Parse(x.Item.Text)
            );

            var int64literal = Mapper(
                CustomItem(x => long.TryParse(x, out var _)),
                x => long.Parse(x.Item.Text)
            );

            var floating = Mapper(
                Composite(
                    Optional(integer),
                    ".",
                    integer
                ),

                x => new AST.FloatingConstant(
                    x.Item, 

                    x.FindSubMatch(0, 0) == null || !x.FindSubMatch(0, 0).Matched
                        ? new AST.IntegerConstant(x.Item, "0")
                        : x.SubMatches[0].FirstMapper(integer),

                    x.SubMatches[2].FirstMapper(integer)
                )
            );

            var stringliteral = Mapper(
                Composite(
                    "\"",
                    Sequence(RegEx(@"[^\x00-\x19""]*")),
                    "\""
                ),

                x => new AST.StringConstant(x.Item, string.Join(" ", x.FindSubMatch(0, 1).Flat.Where(n => n.Token is BNF.RegEx).Select(n => n.Item.Text)))
            );

            var booleanliteral = Mapper(
                Choice(
                    "true",
                    "false"
                ),

                x => new AST.BooleanConstant(x.Item, x.Item.Text == "true")
            );

            var arrayIndexLiteral = Mapper(
                Composite("[", integer, "]"),
                x => new AST.ArrayIndexLiteral(x.Item, x.FirstMapper(integer))
            );
            
            var specialLiteral = Mapper(Literal("U"), x => new AST.SpecialLiteral(x.Item));

            var simpletypename = Mapper(
                Choice(
                    CustomItem(x => AST.DataType.IsValidIntrinsicType(x))
                ),

                x => AST.DataType.Parse(x.Item)
            );

            var literal = Mapper<AST.Constant>(
                Choice(
                    integer, 
                    floating,
                    stringliteral,
                    arrayIndexLiteral,
                    booleanliteral,
                    specialLiteral
                ),

                x => x.FirstDerivedMapper<AST.Constant>()
            );

            var unaryOperation = Mapper(
                Choice(
                    "-",
                    "+",
                    "!",
                    "~"
                ),

                x => new AST.UnaryOperation(
                    x.Item,
                    AST.UnaryOperation.Parse(
                        x.Flat
                            .First(n => n.Token is BNF.Literal)
                            .Item
                    )
                ) 
            );

            // Create the upper mapper to allow referencing it
            // in the recursive definition below
            var expression = Mapper(
                null,
                x => x.FirstDerivedMapper<AST.Expression>()
            );

            var arrayIndex = Mapper(
                Composite(
                    "[",
                    expression,
                    "]"
                ),

                x => { 
                    return new AST.ArrayIndex(x.Item, x.FirstMapper(expression)); 
                }
            );

            var nameitem = Mapper(
                Composite(
                    ident,
                    Optional(
                        arrayIndex
                    )
                ),

                x => {
                    return new
                    {
                        Name = x.FirstMapper(ident),
                        Index = x.FirstOrDefaultMapper(arrayIndex)
                    };
                }
            );

            var dotprefixedname = Mapper(
                Composite(
                    ".",
                    nameitem
                ),

                x => x.FindSubMatch(0).InvokeFirstLevelMappers(nameitem).First()
            );

            var name = Mapper(
                Composite(
                    nameitem,
                    Sequence(dotprefixedname)
                ),

                x => {
                    var entries = new [] { 
                        x.FindSubMatch(0).InvokeFirstLevelMappers(nameitem).First() 
                    }.Concat(x.FindSubMatch(0, 1).InvokeFirstLevelMappers(dotprefixedname));


                    return new AST.Name(
                        x.Item,
                        entries.Select(y => y.Name).ToArray(),
                        entries.Select(y => y.Index).ToArray()
                    );
                }
            );

            var typename = Mapper(
                Composite(
                    Choice(
                        simpletypename,
                        name
                    ),
                    Optional(
                        Composite("[", expression, "]")
                    )
                ),

                x => {
                    var subitem = x.FindSubMatch(0, 0, 0);

                    return subitem?.Token == simpletypename
                        ? new AST.TypeName(new AST.DataType(x.Item, x.FirstMapper(simpletypename)), x.FirstOrDefaultMapper(expression))
                        : new AST.TypeName(subitem.FirstMapper(name), x.FirstOrDefaultMapper(expression));
                }
            );            

            // Mapping expressions to parameters
            var parammap = Mapper(
                Composite(
                    Optional(
                        Composite(
                            ident,
                            ":"
                        )
                    ),
                    expression
                ),

                x => new AST.ParameterMap(
                    x.Item,
                    x.FindSubMatch(0, 0, 0) == null || !x.FindSubMatch(0, 0, 0).Matched
                        ? null
                        : x.FirstMapper(ident),
                    x.FirstMapper(expression)
                )
            );


            // Simple wrapping expressions
            var nameExpression = Mapper(name, x => new AST.NameExpression(x.Item, x.FirstMapper(name)));
            var literalExpression = Mapper(literal, x => new AST.LiteralExpression(x.Item, x.FirstMapper(literal)));
            var parenthesisExpression = Mapper(Composite("(", expression, ")"), x => new AST.ParenthesizedExpression(x.Item, x.FirstMapper(expression)));
            
            // We allow typecasts and unary operations only on 
            // terminating expressions
            var highBindExpr = Mapper(
                null,
                x => x.FirstDerivedMapper<AST.Expression>()
            );

            var unaryExpresssion = Mapper(
                Composite(unaryOperation, highBindExpr),

                x => new AST.UnaryExpression(
                    x.Item,
                    x.FirstMapper(unaryOperation),
                    x.FirstMapper(highBindExpr)
                )
            );

            var typeCastExpression = Mapper(
                Composite(
                    "(",
                    typename,
                    ")",
                    highBindExpr
                ),

                x => new AST.TypeCast(
                    x.Item,
                    x.FirstMapper(highBindExpr),
                    x.FirstMapper(typename),
                    true
                )
            );

            // Terminating expressions have highest precedence
            highBindExpr.Token =
                Choice(
                    literalExpression, 
                    nameExpression, 
                    unaryExpresssion,
                    typeCastExpression,
                    parenthesisExpression                    
                );

            // Create expressions to capture the operator precedence
            var binopPrecedence = new string[][] {
                new string[] { "*", "/", "%" },        // lvl1
                new string[] { "-", "+" },             // lvl2
                new string[] { "<<", ">>" },           // lvl3
                new string[] { "<", ">", "<=", ">=" }, // lvl4
                new string[] { "==", "!=" },           // lvl5
                new string[] { "&", "^", "|" },        // lvl6
                new string[] { "&&" },                 // lvl7
                new string[] { "||" },                 // lvl8
            };

            // The BNF simply has "expression ::= expression op expression",
            // but this is a left-recursive non-terminal and does not encode 
            // operator precedence.
            // To solve this, we re-factor each precedence level
            // to avoid the left-recursive non-terminal,
            // and chain the precedence levels

            // Group the terminal expressions for easy reference
            var terminatingExpression = highBindExpr;

            // The prev variable is the "next" level, starting with a terminal
            var prev = terminatingExpression;
            var binOpSeq = binopPrecedence.Select(
                (x, i) => {
                    // Copy into this scope
                    var prev_locked = prev;

                    // Match any character from the operation list
                    var opMap = Mapper(
                        Choice(x.Select(y => new BNF.Literal(y)).ToArray()),
                        y => new AST.BinaryOperation(y.Item)
                    );

                    // Create a term where the starting token is a terminal,
                    // namely the operation literal
                    var tmp = Mapper<BinOpMatch>(null, null);
                    tmp.Matcher = 
                        y => {
                            var op = y.FirstOrDefaultMapper(opMap);
                            var exp = y.FirstOrDefaultMapper(prev_locked);

                            // If we matched the rhs non-terminal, 
                            // check for the optional trailing component (of same precedence)
                            if (exp != null)
                            {
                                var trailing = y.FindSubMatch(0,0).InvokeFirstLevelMappers(tmp).FirstOrDefault();
                                if (trailing.Expression != null)
                                    exp = new AST.BinaryExpression(exp.SourceToken, exp, trailing.Operation, trailing.Expression);
                            }
                            
                            return new BinOpMatch() {
                                Operation = op,
                                Expression = exp,
                            };
                        };

                    // Recursive definition, but starting with a terminal
                    tmp.Token = Optional(
                        Composite(
                            opMap,
                            prev_locked,
                            tmp
                        )
                    );

                    // The start item in each level matches the non-terminal
                    // a single time and then matches the recusive part
                    var topItem = Mapper<AST.Expression>(
                        Composite(
                            prev_locked,
                            tmp
                        ),
                        n =>
                        {
                            // If this is a single terminal expression, just return it
                            var pm = n.FirstMapper(prev_locked);
                            var subOp = n.FindSubMatch(0).InvokeFirstLevelMappers(tmp).FirstOrDefault();
                            if (subOp.Operation == null)
                                return pm;

                            // Otherwise re-wire into a composite expression
                            return new AST.BinaryExpression(
                                n.Item,
                                pm,
                                subOp.Operation,
                                subOp.Expression
                            );
                        }
                    );

                    return prev = topItem;
                }
            ).ToArray();

            // The final token will recurse into the others
            // and trail out in the terminals
            expression.Token = prev;

            var statement = Mapper(
                null,
                x => x.FirstDerivedMapper<AST.Statement>()
            );

            var assignmentStatement = Mapper(
                Composite(
                    name,
                    "=",
                    expression,
                    ";"
                ),

                x => new AST.AssignmentStatement(
                    x.Item,
                    x.FirstMapper(name),
                    x.FindSubMatch(0,2).FirstMapper(expression)
                )
            );

            var elifBlock = Mapper(
                Composite(
                    "elif",
                    expression,
                    "{",
                    Sequence(statement),
                    "}"
                ),
                
                x => new Tuple<AST.Expression, AST.Statement[]>(
                    x.FirstMapper(expression),
                    x.FindSubMatch(0, 3).InvokeFirstLevelMappers(statement).ToArray()
                )
            );

            var elseBlock = Mapper(
                Composite(
                    "else",
                    "{",
                    Sequence(statement),
                    "}"
                ),

                x => x.FindSubMatch(0, 2).InvokeFirstLevelMappers(statement).ToArray()
            );

            var ifStatement = Mapper(
                Composite(
                    "if", 
                    expression, 
                    "{", 
                    Sequence(statement) , 
                    "}",
                    Sequence(elifBlock),
                    Optional(
                        elseBlock
                    )
                ),

                x => new AST.IfStatement(
                    x.Item,
                    x.FirstMapper(expression),
                    x.FindSubMatch(0, 3).InvokeFirstLevelMappers(statement).ToArray(),
                    x.FindSubMatch(0, 5).InvokeFirstLevelMappers(elifBlock).ToArray(),
                    x.FindSubMatch(0, 6).InvokeFirstLevelMappers(elseBlock).FirstOrDefault()
                )
            );

            var forStatement = Mapper(
                Composite(
                    "for",
                    ident,
                    "=",
                    expression,
                    "to",
                    expression,
                    "{",
                    Sequence(statement),
                    "}"
                ),

                x => new AST.ForStatement(
                    x.Item,
                    x.FirstMapper(ident), 
                    x.FindSubMatch(0, 3).FirstMapper(expression),
                    x.FindSubMatch(0, 5).FirstMapper(expression),
                    x.FindSubMatch(0, 7).InvokeFirstLevelMappers(statement).ToArray()
                )
            );

            // A simple expression is either a literal or a name
            var simpleExpression = Mapper(Choice(literalExpression, nameExpression), x => x.InvokeDerivedMappers<AST.Expression>().First());

            var switchCase = Mapper(
                Composite(
                    "case",
                    simpleExpression,
                    "{",
                    Sequence(statement),
                    "}"                    
                ),

                x => new Tuple<AST.Expression, AST.Statement[]>(
                    x.FirstMapper(simpleExpression),
                    x.FindSubMatch(0, 3).InvokeFirstLevelMappers(statement).ToArray()
                )
            );

            var switchStatement = Mapper(
                Composite(
                    "switch",
                    simpleExpression,
                    "{",
                    // The BNF specifies at least one, but we allow
                    // zero here, and throw a more descriptive error
                    Sequence(switchCase),
                    Optional(                        
                        Composite(
                            "default",
                            "{",
                            Sequence(statement),
                            "}"
                        )
                    ),
                    "}"
                ),

                x => 
                {
                    var defaultCase = x.FindSubMatch(0, 4, 0, 2).InvokeFirstLevelMappers(statement).ToArray();
                    // we cannot use the normal recursive mapper as we may have embedded switch statements
                    var cases = x.FindSubMatch(0, 3).InvokeFirstLevelMappers(switchCase);

                    if (defaultCase.Length > 0)
                        cases = cases.Append(new Tuple<AST.Expression, AST.Statement[]>(null, defaultCase));

                    return new AST.SwitchStatement(
                        x.Item,
                        x.FirstMapper(simpleExpression),
                        cases.ToArray()
                    );
                }
            );

            var functionStatement = Mapper(
                Composite(
                    name,
                    "(",
                    Optional(
                        Composite(
                            parammap,
                            Sequence(
                                Composite(
                                    ",",
                                    parammap
                                )
                            )
                        )
                    ),
                    ")",
                    ";"
                ),

                x => new AST.FunctionStatement(
                    x.Item,
                    x.FirstMapper(name),
                    x.InvokeMappers(parammap).ToArray()
                )
            );

            var traceStatement = Mapper(
                Composite(
                    "trace",
                    "(",
                    stringliteral,
                    Sequence(
                        Composite(
                            ",",
                            expression
                        )
                    ),
                    ")",
                    ";"
                ),

                x => new AST.TraceStatement(
                    x.Item,
                    x.FirstMapper(stringliteral).Value,
                    x.InvokeFirstLevelMappers(expression).ToArray()
                )
            );

            var assertStatement = Mapper(
                Composite(
                    "assert",
                    "(",
                    expression,
                    Optional(stringliteral),
                    ")",
                    ";"
                ),

                x => new AST.AssertStatement(
                    x.Item,
                    x.FirstMapper(expression),
                    x.FirstOrDefaultMapper(stringliteral)?.Value
                )
            );

            var breakStatement = Mapper(
                Composite("break", ";"),
                x => new AST.BreakStatement(x.Item)
            );

            statement.Token = Choice(
                assignmentStatement,
                ifStatement,
                forStatement,
                switchStatement,
                traceStatement,
                assertStatement,
                breakStatement,
                functionStatement
            );

            var varDecl = Mapper(
                Composite(
                    "var",
                    ident,
                    ":",
                    typename,
                    Optional(
                        Composite(
                            "=",
                            expression
                        )
                    ),
                    ";"
                ),

                x => new AST.VariableDeclaration(
                    x.Item, 
                    x.FirstMapper(ident),
                    x.FirstMapper(typename),
                    x.FirstOrDefaultMapper(expression)
                )
            );

            var arrayindex = Mapper(
                Choice(
                    "*",
                    expression
                ),

                x => x.SubMatches[0].Token is BNF.Literal
                    ? new AST.ArrayIndex(x.Item)
                    : new AST.ArrayIndex(x.Item, x.FirstMapper(expression))
            );

            var direction = Mapper(
                Choice(
                    "in",
                    "out",
                    "const"
                ),
                x => (AST.ParameterDirection)Enum.Parse(typeof(AST.ParameterDirection), x.Item.Text, true)
            );

            // Note: we restrict index to 32bit, but specs say unlimited
            var parameter = Mapper(
                Composite(
                    direction,
                    ident,
                    Optional(
                        Composite(
                            ":",
                            typename
                        )
                    )
                ),

                x => new AST.Parameter(
                    x.Item,
                    x.FirstMapper(direction),
                    x.FirstMapper(ident),
                    x.FirstOrDefaultMapper(typename)
                )
            );

            var parameters = Mapper(
                Composite(parameter, Sequence(Composite(",", parameter))),
                x => x.InvokeMappers(parameter).ToArray()
            );

            var enumFieldDeclaration = Mapper(
                Composite(
                    ident,
                    Optional(
                        Composite(
                            "=",
                            int32literal
                        )
                    )
                ),

                x => new AST.EnumField(
                    x.Item, 
                    x.FirstMapper(ident), 
                    x.FindSubMatch(0, 1, 0, 1) == null
                        ? -1 
                        : x.FirstOrDefaultMapper(int32literal)
                )
            );

            var enumDecl = Mapper(
                Composite(
                    "enum",
                    ident,
                    "{",
                    // The BNF specifies at least one, but we allow
                    // zero in the parsing to give better error messages
                    Optional(
                        Composite(
                            enumFieldDeclaration,
                            Sequence(Composite(",", enumFieldDeclaration))
                        )
                    ),
                    "}",
                    ";"
                ),

                x => new AST.EnumDeclaration(
                    x.Item, 
                    x.FirstMapper(ident),
                    x.InvokeMappers(enumFieldDeclaration).ToArray()
                )
            );

            var constDecl = Mapper(
                Composite(
                    "const",
                    ident,
                    ":",
                    typename,
                    "=",
                    expression,
                    ";"
                ),

                x => new AST.ConstantDeclaration(
                    x.Item,
                    x.FirstMapper(ident),
                    x.FirstMapper(typename),
                    x.FirstMapper(expression)
                )
            );            

            var funcDecls = Mapper(
                Choice(
                    varDecl,
                    constDecl,
                    enumDecl
                ),

                x => x.InvokeDerivedMappers<AST.Declaration>().First()
            );

            var funcDef = Mapper(
                Composite(
                    "function",
                    ident,
                    "(",
                    parameters,
                    ")",
                    Sequence(funcDecls),
                    "{",
                    Sequence(statement),                    
                    "}"
                ),

                x => new AST.FunctionDefinition(
                    x.Item,
                    x.FirstMapper(ident),
                    x.FirstMapper(parameters),
                    x.InvokeMappers(funcDecls).ToArray(),
                    x.InvokeMappers(statement).ToArray()
                )
            );

            var busSignalDirection = Mapper(
                Choice(
                    "normal",
                    "inverse"
                ),

                x => string.Equals("inverse", x.Item.Text, StringComparison.OrdinalIgnoreCase)
                    ? AST.SignalDirection.Inverse
                    : AST.SignalDirection.Normal
            );

            var busSignalDeclaration = Mapper(
                Composite(
                    ident,
                    ":",
                    typename,
                    Optional(
                        Composite(
                            "=",
                            expression
                        )
                    ),
                    Optional(
                        Composite(
                            ",",
                            busSignalDirection
                        )
                    ),
                    ";"
                ),

                x => new AST.BusSignalDeclaration(
                    x.Item, 
                    x.FirstMapper(ident),
                    x.FirstMapper(typename),
                    x.FindSubMatch(0, 3).FirstOrDefaultMapper(expression),
                    x.FindSubMatch(0, 4).FirstOrDefaultMapper(busSignalDirection)
                )
            );

            var busDecl = Mapper(
                Composite(
                    "bus",
                    ident,
                    ":",
                    Choice(
                        Composite(
                            "{",
                            // The BNF specifies at least one signal, but
                            // we allow zero, and throw a descriptive error
                            Sequence(busSignalDeclaration),
                            "}"
                        ),
                        typename
                    ),
                    ";"
                ),

                x => {
                    var usingTypeName = x.FindSubMatch(0, 3, 0).Token == typename;
                    
                    return new AST.BusDeclaration(
                        x.Item,
                        x.FirstMapper(ident),
                        usingTypeName ? null : x.FindSubMatch(0, 3).InvokeMappers(busSignalDeclaration).ToArray(),
                        usingTypeName ? x.FindSubMatch(0, 3).FirstOrDefaultMapper(typename) : null
                    );
                }
            );

            var qualifiedspec = Composite("as", ident);

            var importname = Mapper(
                Composite(
                    ident, 
                    Sequence(
                        Composite(
                            ".", 
                            ident
                        )
                    )
                ),

                x => new AST.ImportName(
                    x.Item, 
                    x.InvokeMappers<AST.Identifier>().ToArray()
                )
            );

            var multipleidents = Composite(ident, Sequence(Composite(",", ident)));

            var fullimport = Composite("import", importname, qualifiedspec, ";");
            var limitedimport = Composite(
                "from", importname, 
                "import", multipleidents, 
                qualifiedspec, ";");

            var importstatement = Mapper(
                Choice(fullimport, limitedimport),
                x => 
                {
                    return new AST.ImportStatement(
                        x.Item,
                        // The import module name
                        x.FirstMapper(importname),
                        
                        // If we have multiple idents, extract them
                        x.FirstOrDefault(multipleidents)                            
                            ?.InvokeMappers(ident)
                            .ToArray(),

                        // Get then name we map to
                        x.First(qualifiedspec)
                            .FirstMapper(ident)
                    );
                }
            );

            var typedefs = Mapper(
                Composite(
                    "type",
                    ident,
                    ":",
                    Choice(
                        typename,
                        Composite(
                            "{",
                            Sequence(busSignalDeclaration),
                            "}"
                        )
                    ),
                    ";"
                ),

                x =>
                    x.FindSubMatch(0, 3, 0)?.Token == typename
                        ? new AST.TypeDefinition(x.Item, x.FirstMapper(ident), x.FirstMapper(typename))
                        : new AST.TypeDefinition(x.Item, x.FirstMapper(ident), x.InvokeMappers(busSignalDeclaration))
            );

            var instanceName = Mapper(
                Choice(
                    Composite(ident, "[", expression, "]"),
                    ident,
                    "_"
                ),
                x => x.SubMatches.First().Token is BNF.Literal
                    ? null // Anonymous is null
                    : new AST.InstanceName(
                        x.Item, 
                        x.FirstMapper(ident), 
                        x.FirstOrDefaultMapper(expression)
                    )
            );

            var instDecl = Mapper(
                Composite(
                    "instance", instanceName, 
                    "of", ident,
                    "(", 
                        Optional(
                            Composite(
                                parammap, 
                                Sequence(
                                    Composite(",", parammap)
                                )
                            )
                        ), 
                    ")",
                    ";"
                ),
                x => new AST.InstanceDeclaration(
                    x.Item,
                    x.FirstMapper(instanceName),
                    x.InvokeMappers(ident).Skip(1).First(),
                    x.InvokeMappers(parammap).ToArray()
                )
            );

            var connectEntry = Mapper(
                Composite(
                    name,
                    "->",
                    name
                ),

                x => new AST.ConnectEntry(x.Item, x.FirstMapper(name), x.LastMapper(name))
            );

            var connectDecl = Mapper(
                Composite(
                    "connect",
                    connectEntry,
                    Sequence(
                        Composite(
                            ",",
                            connectEntry
                        )
                    ),
                    ";"
                ),

                x => new AST.ConnectDeclaration(x.Item, x.InvokeMappers(connectEntry).ToArray())
            );

            var networkDecl = Mapper(
                null,
                x => x.FirstDerivedMapper<AST.NetworkDeclaration>()
            );

            var genDecl = Mapper(
                Composite(
                    "generate",
                    ident,
                    "=",
                    expression,
                    "to",
                    expression,
                    "{",
                    Sequence(networkDecl),
                    "}"
                ),

                x => new AST.GeneratorDeclaration(
                    x.Item,
                    x.FirstMapper(ident),
                    x.FindSubMatch(0, 3).FirstMapper(expression),
                    x.FindSubMatch(0, 5).FirstMapper(expression),
                    x.FindSubMatch(0, 7).InvokeMappers(networkDecl).ToArray()
                )
            );

            var declaration = Mapper(
                Choice(
                    varDecl,
                    constDecl,
                    busDecl,
                    enumDecl,
                    funcDef,
                    instDecl,
                    genDecl
                ),

                x => x.FirstDerivedMapper<AST.Declaration>()
            );

            networkDecl.Token = Choice(
                instDecl,
                busDecl,
                constDecl,
                genDecl,
                connectDecl
            );

            var process = Mapper(
                Composite(
                    Optional(
                        "clocked"
                    ),
                    "proc",
                    ident,
                    "(",
                    Optional(parameters),
                    ")",
                    Sequence(declaration),
                    "{",
                    Sequence(statement),
                    "}"
                ),

                x => new AST.Process(
                    x.Item,
                    x.FindSubMatch(0, 0).Item.Text == "clocked",
                    x.FirstMapper(ident),
                    x.FirstOrDefaultMapper(parameters),
                    x.FindSubMatch(0, 6).InvokeMappers(declaration).ToArray(),
                    x.FindSubMatch(0, 8).InvokeFirstLevelMappers(statement).ToArray()
                )
            );


            var network = Mapper(
                Composite(
                    "network", 
                    ident, 
                    "(", 
                    Optional(parameters), 
                    ")", 
                    "{", 
                    Sequence(networkDecl), 
                    "}"
                ),

                x => new AST.Network(
                    x.Item,
                    x.FirstMapper(ident),
                    x.FirstOrDefaultMapper(parameters),
                    x.InvokeMappers(networkDecl).ToArray()
                )
            );

            var entity = Mapper(
                Choice(
                    network, 
                    process
                ),

                x => x.FirstDerivedMapper<AST.Entity>()
            );

            var moduleDecl = Mapper(
                Choice(
                    typedefs,
                    constDecl,
                    enumDecl,
                    funcDef
                ),

                x => x.FirstDerivedMapper<AST.Declaration>()
            );

            var module = Mapper(
                Composite(
                    Sequence(
                        importstatement
                    ),
                    Sequence(
                        moduleDecl
                    ),
                    Sequence(entity)
                ),

                x => new AST.Module(
                    x.Item,
                    x.InvokeMappers(importstatement).ToArray(),
                    x.InvokeMappers(moduleDecl).ToArray(),
                    x.InvokeMappers(entity).ToArray()
                )
            );

            //var match = module.Match(tokens);
            var match = module.Match(tokens);

            // If we have trailing unparsed text, make a guess as to why it fails to parse
            if (!tokens.Empty)
            {
                var start = match;
                while(start != null && start.Matched)
                    start = start.SubMatches.LastOrDefault();

                if (start != null && !start.Matched)
                    ThrowParserError(start);

                // General error message
                throw new ParserException($"Unable to parse item \"{tokens.Current}\"", tokens.Current);
            }
                
            if (!match.Matched)
                ThrowParserError(match);

            return match.FirstMapper(module);
        }

        public static void ThrowParserError(BNF.Match match)
        {
            var bestmatch = match.LongestAttempt();
            var firsterror = bestmatch.Last(x => !x.Matched);
            if (firsterror.Token is BNF.Composite sequence)
            {
                var err = firsterror.SubMatches.First(x => !x.Matched);
                throw new ParserException($"Found \"{err.Item.Text}\" but expected \"{err.Token}\"", err.Item);
            }
            else if (firsterror.Token is BNF.Choice choice)
            {
                throw new ParserException($"Found \"{firsterror.Item.Text}\" but expected one of: \"{string.Join("\", \"", choice.Choices.Select(x => x.ToString()))}\"", firsterror.Item);
            }
            else
            {
                throw new ParserException($"Found \"{firsterror.Item.Text}\" but expected: \"{firsterror.Token}\"", firsterror.Item);
            }
        }
    }
}