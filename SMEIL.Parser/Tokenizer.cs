using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SMEIL.Parser
{
    /// <summary>
    /// A token instance
    /// </summary>
    public struct ParseToken
    {
        /// <summary>
        /// The token text
        /// </summary>
        public readonly string Text;

        /// <summary>
        /// The character offset from the start of the file
        /// </summary>
        public readonly int CharOffset;

        /// <summary>
        /// The line where the token starts
        /// </summary>
        public readonly int Line;

        /// <summary>
        /// The number of chars into the line
        /// </summary>
        public readonly int LineOffset;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.Token"/> struct.
        /// </summary>
        /// <param name="charoffset">The character offset.</param>
        /// <param name="line">The line where the token starts.</param>
        /// <param name="lineoffset">The number of characters into the line.</param>
        /// <param name="text">The text token.</param>
        public ParseToken(int charoffset, int line, int lineoffset, string text)
        {
            CharOffset = charoffset;
            Line = line;
            LineOffset = lineoffset;
            Text = text;
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:SMEIL.Parser.Token"/>.
        /// </summary>
        /// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:SMEIL.Parser.Token"/>.</returns>
        public override string ToString()
        {
            return $"{Text} ({Line}:{LineOffset})";
        }
    }

    /// <summary>
    /// A class that splits an input string
    /// </summary>
    public static class Tokenizer
    {
        /// <summary>
        /// The set of reserved characters for the SMEIL language
        /// </summary>
        private readonly static char[] DELIMITERS = new char[] { 
            '(', ')', 
            '{', '}', 
            '[', ']', 
            ':', 
            ';', 
            ',', 
            '.', 
            '=', 
            '+', 
            '-', 
            '*', 
            '/', 
            '%', 
            '\\', 
            '|', 
            '&', 
            '^',
            '<',
            '>',
            '"'
        };

        /// <summary>
        /// Splits the input into tokens
        /// </summary>
        /// <returns>The tokenized results.</returns>
        /// <param name="reader">The reader to extract the tokens for.</param>
        public static IEnumerable<ParseToken> Tokenize(string reader)
        {
            return Tokenize(new StringReader(reader));
        }

        /// <summary>
        /// Splits the input into tokens
        /// </summary>
        /// <returns>The tokenized results.</returns>
        /// <param name="reader">The reader to extract the tokens for.</param>
        public static IEnumerable<ParseToken> Tokenize(TextReader reader)
        {
            var sb = new StringBuilder();
            int cur;
            int line = 1;
            int charoffset = 1;
            int lineoffset = 1;

            while ((cur = reader.Read()) > 0)
            {
                var c = (char)cur;
                var iswhitespace = char.IsWhiteSpace(c);
                if (iswhitespace || Array.IndexOf(DELIMITERS, c) >= 0)
                {
                    if (sb.Length != 0)
                    {
                        yield return new ParseToken(charoffset, line, lineoffset, sb.ToString());
                        charoffset += sb.Length;
                        lineoffset += sb.Length;
                        sb.Clear();
                    }

                    // Eat the entire line if we hit a comment
                    if (c == '/' && reader.Peek() == cur)
                    {
                        var ct = new ParseToken(charoffset, line, lineoffset, '/' + reader.ReadLine());

                        charoffset += ct.Text.Length;
                        lineoffset = 1;
                        line++;

                        //yield return ct;
                        continue;
                    }

                    if (!iswhitespace)
                        yield return new ParseToken(charoffset, line, lineoffset, c.ToString());
                    
                    charoffset++;
                    lineoffset++;
                    if (c == '\r' || c == '\n')
                    {
                        if (c == '\r' && reader.Peek() == (int)'\n')
                            reader.Read();

                        line++;
                        lineoffset = 1;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length != 0)
                yield return new ParseToken(charoffset, line, lineoffset, sb.ToString());
        }
    }
}
