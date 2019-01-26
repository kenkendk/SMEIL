= BNF module =

This folder contains the implementation of a specification system that looks much like an embedded language when used. The purpose of the system is to describe formal grammer in a Backus-Naur Format (BNF).

Each `BNFItem` simply matches a token or not. The `Literal` token will match string literals, where the `RegEx` token will match a given regular expression. For a custom match function, the `CustomItem` function takes a function that returns `true` or `false` for a given input.

Items that are required to follow each other are described with `Composite`, which takes one or more `BNFItem` elements to define the sequence. Items that are optional can be wrapped with the `Optional` item.

If an item can accept multiple subtypes, a `Choice` will select the first that matches. Finally a `Sequence` will accept zero or more occurences of the item it wraps.

All the items, except the literal and custom items accept other `BNFItem`s as input, allowing a composite description. An example for a constant declaration in a small language could be:

```csharp
using static SMEIL.Parse.BNF.StaticUtil;
...

var integer = Choice(
    RegEx(@"[0-9]+"),
    RegEx(@"0x([0-9|a-f|A-F])+"),
    RegEx(@"0o([0-7])")
);

var name = RegEx("\w(\w\d\_)*");
var constvar = Composite("const", name, "=", integer, ";");

// Perform the matching on an input string
var match = constvar.Match(SMEIL.Parser.Tokenizer.Tokenize("const two = 2;"));

if (match == null) throw new Exception("Input string was invalid");
if (match.Token != constvar) throw new Exception("Expected a constant");
//Since constvar is [literal, name, literal, integer, literal] we can access the matches
// for the individual items with the correct index
Console.WriteLine($"Name={constvar.SubMatches[1].Item.Text}, value={constvar.SubMatches[3].Item.Text}");
// We could also use LINQ to grab the one that matches the name expression
Console.WriteLine($"Name={constvar.SubMatches.First(x => x.Token == name).Item.Text}");
```

If you have a model of the items you are parsing, you can add `Mapper` items that provide a method to construct a specific instance from the input. An example using the above code could be:

```csharp
using System.Linq;
...

// The model item for a name
public class Name 
{ 
    public string ID; 
}

// The model item for a constant
public class Constant 
{ 
    public Name Name; 
    public int Value; 
}

// Now set up mappers for the sub-items
var intmap = Mapper(integer, x => int.Parse(x.Text));
var namemap = Mapper(name, x => new Name() { ID = x.Text });

// The upper items can then use the lower items 
// to construct composite items
var constmap = Mapper(constvar, x => new Constant() {
    Name = x.InvokeMappers(namemap).First(),
    Value = x.InvokeMappers(intmap).First()
});

```

