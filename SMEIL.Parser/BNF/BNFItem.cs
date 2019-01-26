using System;
using System.Linq;
using System.Collections.Generic;

namespace SMEIL.Parser.BNF
{
    /// <summary>
    /// The base token class
    /// </summary>
    public abstract class BNFItem
    {
        /// <summary>
        /// Helper method to allow injecting literals as strings in BNFs
        /// </summary>
        /// <param name="value">The string to make a literal of</param>
        public static implicit operator BNFItem(string value) { return new Literal(value); }

        /// <summary>
        /// Performs a match on a sequence
        /// </summary>
        /// <param name="enumerator">The sequence of items</param>
        /// <returns>A match or <c>null</c></returns>
        public virtual Match Match(IEnumerator<ParseToken> enumerator)
        {
            return Match(new BufferedEnumerator<ParseToken>(enumerator));
        }

        /// <summary>
        /// Performs a match on a sequence
        /// </summary>
        /// <param name="enumerator">The sequence of items</param>
        /// <returns>A match or <c>null</c></returns>
        public virtual Match Match(IBufferedEnumerator<ParseToken> enumerator)
        {
            return DoMatch(enumerator, false, new List<Match>());
        }

        /// <summary>
        /// Performs a match on a sequence
        /// </summary>
        /// <param name="enumerator">The sequence of items</param>
        /// <returns>A match or <c>null</c></returns>
        protected virtual Match DoMatch(IBufferedEnumerator<ParseToken> enumerator, bool optional, List<Match> choices)
        {
            Console.WriteLine("Trying: {1} with {0}", this.BuildString(new HashSet<BNFItem>(), 1), enumerator.Current);

            if (enumerator.Empty)
            {
                if (!optional)
                    throw new Exception("No more data?");
                else
                    return new Match(this, default(ParseToken), new Match[0], false);
            }

            var start = enumerator.Current;
            var text = start.Text;

            if (text == "trace")
                Console.Write("");

            if (this is Literal literalToken)
            {
                var res = new Match(this, start, null, text == literalToken.Value);
                if (res.Matched)
                    enumerator.MoveNext();
                else if (!optional)
                    throw new Exception($"Failed to match {this} with {start}");
                return res;
            }
            else if (this is RegEx regExToken)
            {
                var m = regExToken.Expression.Match(text);
                var res = new Match(this, start, null, m.Success && m.Length == text.Length);

                if (res.Matched)
                    enumerator.MoveNext();
                else if (!optional)
                    throw new Exception($"Failed to match {this} with {start}");
                return res;
            }
            else if (this is CustomItem customToken)
            {
                var res = new Match(this, start, null, customToken.Matcher(text));

                if (res.Matched)
                    enumerator.MoveNext();
                else if (!optional)
                    throw new Exception($"Failed to match {this} with {start}");
                return res;
            }
            else if (this is Composite compositeToken)
            {
                if (optional)
                    enumerator.Snapshot();
                var subItems = new List<Match>();

                foreach (var n in compositeToken.Items)
                {
                    var s = n.DoMatch(enumerator, optional, choices);
                    subItems.Add(s);

                    if (!s.Matched)
                    {
                        if (!optional)
                            throw new Exception($"Failed to match {this} with {start}");

                        enumerator.Rollback();
                        return new Match(this, start, subItems.ToArray(), false);
                    }
                }

                if (optional)
                    enumerator.Commit();
                return new Match(this, start, subItems.ToArray(), true);
            }
            else if (this is Choice choiceToken)
            {   
                var subItems = new List<Match>();
                foreach (var n in choiceToken.Choices)
                {
                    if (optional)
                        enumerator.Snapshot();

                    Match s;

                    // Prevent infinite recursion
                    if (choices.Any(x => x.Token == n && object.Equals(x.Item, start)))
                    {
                        s = new Match(n, start, new BNF.Match[0], false);
                    }
                    else
                    {
                        choices.Add(new BNF.Match(n, start, null, false));
                        s = n.DoMatch(enumerator, true, choices);
                        choices.RemoveAt(choices.Count - 1);
                    }

                    subItems.Add(s);
                    if (s.Matched)
                    {
                        if (optional)
                            enumerator.Commit();
                        return new Match(this, start, new Match[] { s }, true);
                    }

                    if (optional)
                        enumerator.Rollback();
                }

                if (optional)
                    return new Match(this, start, subItems.ToArray(), false);
                else
                    throw new Exception($"Failed to match {this} with {start}");

            }
            else if (this is Optional optionalToken)
            {
                if (optional)
                    enumerator.Snapshot();
                var s = optionalToken.Item.DoMatch(enumerator, true, choices);
                if (!s.Matched)
                {
                    if (optional)
                        enumerator.Rollback();
                    return new Match(this, start, new Match[] { }, true);
                }

                if (optional)
                    enumerator.Commit();
                return new Match(this, start, new Match[] { s }, true);
            }
            else if (this is Sequence sequenceToken)
            {
                var items = new List<Match>();
                while(true)
                {
                    if (optional)
                        enumerator.Snapshot();
                    var n = sequenceToken.Items.DoMatch(enumerator, true, choices);
                    if (!n.Matched)
                    {
                        if (optional)
                            enumerator.Rollback();
                        break;
                    }
                    if (optional)
                        enumerator.Commit();
                    items.Add(n);
                }

                return new Match(this, start, items.ToArray(), true);
            }
            else if (this.GetType().IsConstructedGenericType && this.GetType().GetGenericTypeDefinition() == typeof(Mapper<>))
            {
                var prop = this.GetType().GetField(nameof(Mapper<int>.Token));
                var token = prop.GetValue(this);
                var match = ((BNFItem)token).DoMatch(enumerator, optional, choices);
                return new Match(this, start, new BNF.Match[] { match }, match.Matched);
            }

            throw new Exception($"Unable to match for {this.GetType()}, make sure you override the {nameof(Match)} method");
        }

        /// <summary>
        /// Returns all items below this item
        /// </summary>
        /// <value></value>
        private IEnumerable<BNFItem> Children
        {
            get
            {
                IEnumerable<BNFItem> items = null;                
                if (this is Composite compositeToken)
                {
                    items = compositeToken.Items;
                }
                else if (this is Choice choiceToken)
                {
                    items = choiceToken.Choices;
                }
                else if (this is Optional optionalToken)
                {
                    items = new [] { optionalToken.Item };
                }
                else if (this is Sequence sequenceToken)
                {
                    items = new[] { sequenceToken.Items };
                }
                else if (this.GetType().IsConstructedGenericType && this.GetType().GetGenericTypeDefinition() == typeof(Mapper<>))
                {
                    var prop = this.GetType().GetField(nameof(Mapper<int>.Token));
                    var token = (BNFItem)prop.GetValue(this);
                    items = new[] { token };
                }

                if (items != null)
                    foreach (var item in items)
                    {
                        yield return item;
                        foreach (var x in item.Children)
                            yield return x;
                    }
            }
        }

        /// <summary>
        /// Returns a string representation of the item
        /// </summary>
        /// <returns>A string representation of the item</returns>
        public override string ToString()
        {
            return BuildString(new HashSet<BNFItem>(), 8);
        }

        /// <summary>
        /// Builds a string
        /// </summary>
        /// <param name="visited">Previously visited items</param>
        /// <param name="maxdepth">The maximum depth to go</param>
        /// <returns></returns>
        private string BuildString(HashSet<BNFItem> visited, int maxdepth)
        {
            maxdepth--;

            if (this is Literal literalToken)
                return $"\"{literalToken.Value}\"";
            else if (this is RegEx regexToken)
                return $"/{regexToken.Expression}/";
            else if (this is Choice choiceToken)
            {
                if (visited.Contains(this) || maxdepth < 0)
                    return "(...)";

                return $"({string.Join("|", choiceToken.Choices.Select(x => x.BuildString(visited, maxdepth)))})";
            }
            else if (this is Optional optionalToken)
            {
                if (visited.Contains(this) || maxdepth < 0)
                    return "{...}";

                return $"{{{optionalToken.Item.BuildString(visited, maxdepth)}}}";
            }
            else if (this is Composite compositeToken)
            {
                if (visited.Contains(this) || maxdepth < 0)
                    return "...";

                return string.Join(" ", compositeToken.Items.Select(x => x.BuildString(visited, maxdepth)));
            }
            else if (this is Sequence sequenceToken)
            {
                if (visited.Contains(this) || maxdepth < 0)
                    return "[...]";

                return $"[ {sequenceToken.Items.BuildString(visited, maxdepth)} ]";
            }
            else if (this.GetType().IsConstructedGenericType && this.GetType().GetGenericTypeDefinition() == typeof(Mapper<>))
            {
                var prop = this.GetType().GetField(nameof(Mapper<int>.Token));
                var token = (BNFItem)prop.GetValue(this);
                return $"<{this.GetType().GetGenericArguments().First().Name}>({token.BuildString(visited, maxdepth)})";
            }

            return $"@{this.GetType().Name}" ;
        }
    }
}