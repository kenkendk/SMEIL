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
        /// Flag used to toggle some tracing output from the BNF to STDOUT
        /// </summary>
        public static bool DEBUG_BNF_OUTPUT = false;

        /// <summary>
        /// Flag used to deactivate the infinite recursion detection for the BNF
        /// </summary>
        public static bool DISABLE_INFINITE_RECURSION_DETECTION = true;

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
        /// The mapper type of this instance, or null if it is not a mapper type
        /// </summary>
        public Type MapperType => StaticUtil.MapperType(this);

        /// <summary>
        /// Performs a match on a sequence
        /// </summary>
        /// <param name="enumerator">The sequence of items</param>
        /// <returns>A match or <c>null</c></returns>
        public virtual Match Match(IBufferedEnumerator<ParseToken> enumerator)
        {
            return DoProtectedMatch(enumerator, new List<Match>());
        }

        /// <summary>
        /// Performs a match that guards against infinite recursion
        /// </summary>
        /// <param name="enumerator">The enumerator to use</param>
        /// <param name="visited">The visited items</param>
        /// <returns>The match</returns>
        protected virtual Match DoProtectedMatch(IBufferedEnumerator<ParseToken> enumerator, List<Match> visited)
        {
            // Use this statment to debug where tokens are matched with the BNF
            
            if (DEBUG_BNF_OUTPUT)
                Console.WriteLine("Trying: {1} with {0}", this.BuildString(new HashSet<BNFItem>(), 1), enumerator.Current);

            // Quick return, if we do not need the recursion guards
            if (DISABLE_INFINITE_RECURSION_DETECTION)
                return DoUnprotectedMatch(enumerator, visited);

            var start = enumerator.Current;

            // Prevent infinite recursion
            if (visited.Count(x => x.Token == this && object.Equals(x.Item, start)) > 3)
                return new Match(this, start, new BNF.Match[0], false);

            visited.Add(new BNF.Match(this, start, null, false));
            var res = DoUnprotectedMatch(enumerator, visited);
            visited.RemoveAt(visited.Count - 1);

            return res;
        }

        /// <summary>
        /// Performs a match on a sequence
        /// </summary>
        /// <param name="enumerator">The sequence of items</param>
        /// <param name="visited">The visited items</param>
        /// <returns>A match or <c>null</c></returns>
        protected virtual Match DoUnprotectedMatch(IBufferedEnumerator<ParseToken> enumerator, List<Match> visited)
        {
            if (enumerator.Empty)
                return new Match(this, default(ParseToken), new Match[0], false);

            var start = enumerator.Current;
            var text = start.Text;


            if (this is Literal literalToken)
            {
                var res = new Match(this, start, null, text == literalToken.Value);
                if (res.Matched)
                    enumerator.MoveNext();
                return res;
            }
            else if (this is RegEx regExToken)
            {
                var m = regExToken.Expression.Match(text);
                var res = new Match(this, start, null, m.Success && m.Length == text.Length);

                if (res.Matched)
                    enumerator.MoveNext();
                return res;
            }
            else if (this is CustomItem customToken)
            {
                var res = new Match(this, start, null, customToken.Matcher(text));

                if (res.Matched)
                    enumerator.MoveNext();
                return res;
            }
            else if (this is Composite compositeToken)
            {

                enumerator.Snapshot();
                var subItems = new List<Match>();

                foreach (var n in compositeToken.Items)
                {
                    var s = n.DoProtectedMatch(enumerator, visited);
                    subItems.Add(s);

                    if (!s.Matched)
                    {
                        enumerator.Rollback();
                        return new Match(this, start, subItems.ToArray(), false);
                    }
                }

                enumerator.Commit();
                return new Match(this, start, subItems.ToArray(), true);
            }
            else if (this is Choice choiceToken)
            {
                var subItems = new List<Match>();
                foreach (var n in choiceToken.Choices)
                {
                    enumerator.Snapshot();
                    var s = n.DoProtectedMatch(enumerator, visited);

                    subItems.Add(s);
                    if (s.Matched)
                    {
                        enumerator.Commit();
                        return new Match(this, start, new Match[] { s }, true);
                    }

                    enumerator.Rollback();
                }

                return new Match(this, start, subItems.ToArray(), false);
            }
            else if (this is Optional optionalToken)
            {
                enumerator.Snapshot();
                var s = optionalToken.Item.DoProtectedMatch(enumerator, visited);
                if (!s.Matched)
                {
                    enumerator.Rollback();
                    return new Match(this, start, new Match[] { s }, true);
                }

                enumerator.Commit();
                return new Match(this, start, new Match[] { s }, true);
            }
            else if (this is Sequence sequenceToken)
            {
                var items = new List<Match>();
                while(true)
                {
                    enumerator.Snapshot();
                    var n = sequenceToken.Items.DoProtectedMatch(enumerator, visited);
                    if (!n.Matched)
                    {
                        enumerator.Rollback();
                        items.Add(n);
                        break;
                    }
                    enumerator.Commit();
                    items.Add(n);
                }

                return new Match(this, start, items.ToArray(), true);
            }
            else if (this.MapperType != null)
            {
                var prop = this.GetType().GetField(nameof(Mapper<int>.Token));
                var token = prop.GetValue(this);
                var match = ((BNFItem)token).DoProtectedMatch(enumerator, visited);
                return new Match(this, start, new BNF.Match[] { match }, match.Matched);
            }

            throw new Exception($"Unable to match for {this.GetType()}, make sure you override the {nameof(Match)} method");
        }

        /// <summary>
        /// Throws an exception, attempting to give a sane error message
        /// </summary>
        private void ThrowMatchFailure(Match bestshot)
        {
            if (bestshot.Token.MapperType != null)
                throw new ParserException($"Failed to match \"{bestshot.Item.Text}\" expected: {bestshot.Token.MapperType.Name}", bestshot.Item);

            var potentials = bestshot.SubMatches.Select(x => x.Token.AllChildren.Prepend(x.Token).FirstOrDefault(y => y.MapperType != null)).Select(x => x.MapperType).ToArray();
            if (bestshot.Token.MapperType != null)
                potentials = new Type[] { bestshot.Token.MapperType };

            if (potentials.Length == 1)
                throw new ParserException($"Failed to match \"{bestshot.Item.Text}\" expected: {potentials[0].Name}", bestshot.Item);
            else
                throw new ParserException($"Failed to match \"{bestshot.Item.Text}\" expected one of: {string.Join(", ", potentials.Select(x => x.Name))}", bestshot.Item);
        }

        /// <summary>
        /// Returns all items below this item
        /// </summary>
        /// <value></value>
        private IEnumerable<BNFItem> AllChildren
        {
            get
            {
                var work = new Queue<BNFItem>();
                var visited = new HashSet<BNFItem>();
                work.Enqueue(this);

                while(work.Count > 0)
                {
                    var cur = work.Dequeue();
                    if (visited.Contains(cur))
                        continue;
                    visited.Add(cur);
                    yield return cur;
                    foreach (var c in cur.Children)
                        work.Enqueue(c);
                }
            }
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
                else if (this.MapperType != null)
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

        /// <summary>
        /// Builds a string with only the literals
        /// </summary>
        /// <param name="visited">Previously visited items</param>
        /// <param name="maxdepth">The maximum depth to go</param>
        /// <returns></returns>
        private string BuildLiteralString(int maxdepth)
        {
            var sb = new System.Text.StringBuilder();
            var visit = new List<BNFItem>();
            var counter = new Dictionary<BNFItem, int>();
            var earlyout = new HashSet<BNFItem>();

            Visit(x => 
                {
                    var active = visit.LastOrDefault(y => new Type[] { typeof(Choice), typeof(Composite), typeof(Sequence), typeof(Optional) }.Contains(y.GetType()) );

                    visit.Add(x);
                    if (earlyout.Count != 0)
                        return false;

                    if (active != null && active == visit.LastOrDefault())
                    {
                        if (counter.TryGetValue(active, out var c))
                        {
                            if (c == 0)
                            {
                                if (active is Choice)
                                    sb.Append("{");
                                else if (active is Optional)
                                    sb.Append("(");
                                else if (active is Sequence)
                                    sb.Append("[");
                            }
                            else
                            {
                                if (active is Choice)
                                    sb.Append(" | ");
                                else if (active is Composite)
                                    sb.Append(" ");
                                else if (active is Sequence)
                                    sb.Append(", ");
                            }
                            counter[active] = c + 1;
                        }
                    }

                    if (x is Literal lit)
                    {
                        sb.Append($"\"{lit.Value}\"");
                        var lastexit = visit.LastOrDefault(y => new Type[] { typeof(Composite) }.Contains(y.GetType()));
                        if (lastexit != null)
                            earlyout.Add(lastexit);
                    }
                    else if (x is RegEx rx)
                    {
                        sb.Append($"/{rx.Expression}/");
                    }
                    else if (x is Choice)
                    {
                        counter[x] = 0;
                    }
                    else if (x is Optional)
                    {
                        counter[x] = 0;
                    }
                    else if (x is Sequence)
                    {
                        counter[x] = 0;
                    }
                    else if (x is Composite)
                    {
                        counter[x] = 0;
                    }
                    else if (x.GetType().IsConstructedGenericType && x.GetType().GetGenericTypeDefinition() == typeof(Mapper<>))
                    {
                        // Ignore mappers
                    }
                    else
                        sb.Append(x.GetType().Name);

                    return earlyout.Count == 0;
                }, 
                
                x => 
                {
                    visit.RemoveAt(visit.Count - 1);
                    counter.TryGetValue(x, out var c);
                    counter.Remove(x);
                    earlyout.Remove(x);

                    if (x is Choice && c != 0)
                        sb.Append("}");
                    else if (x is Optional && c != 0)
                        sb.Append(")");
                    else if (x is Sequence && c != 0)
                        sb.Append("]");
                }, 
                
                null, 
                maxdepth
            );

            return sb.ToString();
        }

        /// <summary>
        /// Visits a BNFItem calling visit and leave for each element
        /// </summary>
        /// <param name="visitor">The visitor function to call</param>
        /// <param name="leave">The leave function to call</param>
        /// <param name="omitted">The function to call when the element is omitted</param>
        /// <param name="maxdepth">The max depth</param>
        private void Visit(Func<BNFItem, bool> visitor = null, Action<BNFItem> leave = null, Action<BNFItem> omitted = null, int maxdepth = int.MaxValue)
        {
            Visit_internal(new HashSet<BNFItem>(), visitor, leave, omitted, maxdepth);
        }

        /// <summary>
        /// Visits a BNFItem calling visit and leave for each element
        /// </summary>
        /// <param name="visitor">The visitor function to call</param>
        /// <param name="leave">The leave function to call</param>
        /// <param name="omitted">The function to call when the element is omitted</param>
        /// <param name="maxdepth">The max depth</param>
        private void Visit_internal(HashSet<BNFItem> visited, Func<BNFItem, bool> visitor = null, Action<BNFItem> leave = null, Action<BNFItem> omitted = null, int maxdepth = int.MaxValue)
        {
            visitor = visitor ?? (_ => true);
            leave = leave ?? (_ => {});

            var cur = this;
            if (!visitor(cur))
                return;

            if (visited.Contains(cur) || maxdepth < 0)
            {
                omitted?.Invoke(cur);
                return;
            }

            visited.Add(cur);

            if (cur is Literal literalToken)
            {
                // Nothing to do
            }
            else if (cur is RegEx regexToken)
            {
                // Nothing to do
            }
            else if (cur is Choice choiceToken)
            {
                foreach (var c in choiceToken.Choices)
                    c.Visit_internal(visited, visitor, leave, omitted, maxdepth);
            }
            else if (cur is Optional optionalToken)
            {
                optionalToken.Item.Visit_internal(visited, visitor, leave, omitted, maxdepth);
            }
            else if (cur is Composite compositeToken)
            {
                foreach (var c in compositeToken.Items)
                    c.Visit_internal(visited, visitor, leave, omitted, maxdepth);
            }
            else if (cur is Sequence sequenceToken)
            {
                sequenceToken.Items.Visit_internal(visited, visitor, leave, omitted, maxdepth);
            }
            else if (cur.GetType().IsConstructedGenericType && cur.GetType().GetGenericTypeDefinition() == typeof(Mapper<>))
            {
                var prop = cur.GetType().GetField(nameof(Mapper<int>.Token));
                var token = (BNFItem)prop.GetValue(cur);
                token.Visit_internal(visited, visitor, leave, omitted, maxdepth);
            }

            leave(cur);
        }
    }
}