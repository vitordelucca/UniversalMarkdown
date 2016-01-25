﻿// Copyright (c) 2016 Quinn Damerell
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.


using System;
using System.Collections.Generic;
using System.Linq;
using UniversalMarkdown.Parse;
using UniversalMarkdown.Parse.Elements;

namespace UniversalMarkdown.Helpers
{
    internal class Common
    {
        internal enum InlineParseMethod
        {
            Bold,
            Code,
            Italic,
            MarkdownLink,
            AngleBracketLink,
            Url,
            RedditLink,
            PartialLink,
            Email,
            Strikethrough,
            Superscript,
        }

        /// <summary>
        /// A helper class for the trip chars. This is an optimization. If we ask each class to go
        /// through the rage and look for itself we end up looping through the range n times, once
        /// for each inline. This class represent a character that an inline needs to have a
        /// possible match. We will go through the range once and look for everyone's trip chars,
        /// and if they can make a match from the trip char then we will commit to them.
        /// </summary>
        internal class InlineTripCharHelper
        {
            // Note! Everything in first char and suffix should be lower case!
            public char FirstChar;
            public InlineParseMethod Method;
        }

        private static List<InlineTripCharHelper> s_triggerList = new List<InlineTripCharHelper>();
        private static char[] s_tripCharacters;

        static Common()
        {
            BoldTextInline.AddTripChars(s_triggerList);
            ItalicTextInline.AddTripChars(s_triggerList);
            MarkdownLinkInline.AddTripChars(s_triggerList);
            HyperlinkInline.AddTripChars(s_triggerList);
            StrikethroughTextInline.AddTripChars(s_triggerList);
            SuperscriptTextInline.AddTripChars(s_triggerList);
            CodeInline.AddTripChars(s_triggerList);
            // Text run doesn't have one.

            // Create an array of characters to search against using IndexOfAny.
            s_tripCharacters = s_triggerList.Select(trigger => trigger.FirstChar).Distinct().ToArray();
        }

        /// <summary>
        /// This function can be called by any element parsing. Given a start and stopping point this will
        /// parse all found elements out of the range.
        /// </summary>
        /// <param name="markdown"></param>
        /// <param name="startingPos"></param>
        /// <param name="maxEndingPos"></param>
        /// <param name="ignoreLinks"> Indicates whether to parse links. </param>
        /// <returns> A list of parsed inlines. </returns>
        public static List<MarkdownInline> ParseInlineChildren(string markdown, int startingPos, int maxEndingPos, bool ignoreLinks = false)
        {
            int currentParsePosition = startingPos;

            var inlines = new List<MarkdownInline>();
            while (currentParsePosition < maxEndingPos)
            {
                // Find the next inline element.
                var parseResult = Common.FindNextInlineElement(markdown, currentParsePosition, maxEndingPos, ignoreLinks);

                // If the element we found doesn't start at the position we are looking for there
                // is text between the element and the start of the parsed element. We need to wrap
                // it into a text run.
                if (parseResult.Start != currentParsePosition)
                {
                    var textRun = TextRunInline.Parse(markdown, currentParsePosition, parseResult.Start);
                    inlines.Add(textRun);
                }

                // Add the parsed element.
                inlines.Add(parseResult.ParsedElement);

                // Update the current position.
                currentParsePosition = parseResult.End;
            }
            return inlines;
        }

        /// <summary>
        /// Represents the result of parsing an inline element.
        /// </summary>
        internal class InlineParseResult
        {
            public InlineParseResult(MarkdownInline parsedElement, int start, int end)
            {
                ParsedElement = parsedElement;
                Start = start;
                End = end;
            }

            /// <summary>
            /// The element that was parsed (can be <c>null</c>).
            /// </summary>
            public MarkdownInline ParsedElement { get; private set; }

            /// <summary>
            /// The position of the first character in the parsed element.
            /// </summary>
            public int Start { get; private set; }

            /// <summary>
            /// The position of the character after the last character in the parsed element.
            /// </summary>
            public int End { get; private set; }
        }

        /// <summary>
        /// Finds the next inline element by matching trip chars and verifying the match.
        /// </summary>
        /// <param name="markdown"> The markdown text to parse. </param>
        /// <param name="start"> The position to start parsing. </param>
        /// <param name="end"> The position to stop parsing. </param>
        /// <param name="ignoreLinks"> Indicates whether to parse links. </param>
        /// <returns></returns>
        private static InlineParseResult FindNextInlineElement(string markdown, int start, int end, bool ignoreLinks)
        {
            // Search for the next inline sequence.
            for (int pos = start; pos < end; pos++)
            {
                // IndexOfAny should be the fastest way to skip characters we don't care about.
                pos = markdown.IndexOfAny(s_tripCharacters, pos, end - pos);
                if (pos < 0)
                    break;

                // Find the trigger(s) that matched.
                char currentChar = markdown[pos];
                foreach (InlineTripCharHelper currentTripChar in s_triggerList)
                {
                    // Check if our current char matches the suffix char.
                    if (currentChar == currentTripChar.FirstChar)
                    {
                        // Don't match if the previous character was a backslash.
                        if (pos > start && markdown[pos - 1] == '\\')
                            continue;

                        // If we are here we have a possible match. Call into the inline class to verify.
                        InlineParseResult parseResult = null;
                        switch (currentTripChar.Method)
                        {
                            case InlineParseMethod.Bold:
                                parseResult = BoldTextInline.Parse(markdown, pos, end);
                                break;
                            case InlineParseMethod.Italic:
                                parseResult = ItalicTextInline.Parse(markdown, pos, end);
                                break;
                            case InlineParseMethod.MarkdownLink:
                                if (!ignoreLinks)
                                    parseResult = MarkdownLinkInline.Parse(markdown, pos, end);
                                break;
                            case InlineParseMethod.AngleBracketLink:
                                if (!ignoreLinks)
                                    parseResult = HyperlinkInline.ParseAngleBracketLink(markdown, pos, end);
                                break;
                            case InlineParseMethod.Url:
                                if (!ignoreLinks)
                                    parseResult = HyperlinkInline.ParseUrl(markdown, pos, end);
                                break;
                            case InlineParseMethod.RedditLink:
                                if (!ignoreLinks)
                                    parseResult = HyperlinkInline.ParseRedditLink(markdown, pos, end);
                                break;
                            case InlineParseMethod.PartialLink:
                                if (!ignoreLinks)
                                    parseResult = HyperlinkInline.ParsePartialLink(markdown, pos, end);
                                break;
                            case InlineParseMethod.Email:
                                if (!ignoreLinks)
                                    parseResult = HyperlinkInline.ParseEmailAddress(markdown, pos, end);
                                break;
                            case InlineParseMethod.Strikethrough:
                                parseResult = StrikethroughTextInline.Parse(markdown, pos, end);
                                break;
                            case InlineParseMethod.Superscript:
                                parseResult = SuperscriptTextInline.Parse(markdown, pos, end);
                                break;
                            case InlineParseMethod.Code:
                                parseResult = CodeInline.Parse(markdown, pos, end);
                                break;
                        }

                        if (parseResult != null)
                            return parseResult;
                    }
                }
            }

            // If we didn't find any elements we have a normal text block.
            // Let us consume the entire range.
            return new InlineParseResult(TextRunInline.Parse(markdown, start, end), start, end);
        }


        /// <summary>
        /// Returns the next \n or \r\n in the markdown.
        /// </summary>
        /// <param name="markdown"></param>
        /// <param name="startingPos"></param>
        /// <param name="endingPos"></param>
        /// <param name="startOfNextLine"></param>
        /// <returns></returns>
        public static int FindNextSingleNewLine(string markdown, int startingPos, int endingPos, out int startOfNextLine)
        {
            // A line can end with CRLF (\r\n) or just LF (\n).
            int lineFeedPos = markdown.IndexOf('\n', startingPos);
            if (lineFeedPos == -1)
            {
                startOfNextLine = endingPos;
                return endingPos;
            }
            startOfNextLine = lineFeedPos + 1;

            // Check if it was a CRLF.
            if (lineFeedPos > startingPos && markdown[lineFeedPos - 1] == '\r')
                return lineFeedPos - 1;
            return lineFeedPos;
        }

        /// <summary>
        /// Helper function for index of with a start and an ending.
        /// </summary>
        /// <param name="markdown"></param>
        /// <param name="search"></param>
        /// <param name="startingPos"></param>
        /// <param name="endingPos"></param>
        /// <returns></returns>
        public static int IndexOf(string markdown, string search, int startingPos, int endingPos, bool reverseSearch = false)
        {
            // Check the ending isn't out of bounds.
            if (endingPos > markdown.Length)
            {
                endingPos = markdown.Length;
                DebuggingReporter.ReportCriticalError("IndexOf endingPos > string length");
            }

            // Figure out how long to go
            int count = endingPos - startingPos;
            if (count < 0)
            {
                return -1;
            }

            // Make sure we don't go too far.
            int remainingCount = markdown.Length - startingPos;
            if (count > remainingCount)
            {
                DebuggingReporter.ReportCriticalError("IndexOf count > remaing count");
                count = remainingCount;
            }

            // Check the ending. Since we use inclusive ranges we need to -1 from this for
            // reverses searches.
            if (reverseSearch && endingPos > 0)
            {
                endingPos -= 1;
            }

            return reverseSearch ? markdown.LastIndexOf(search, endingPos, count) : markdown.IndexOf(search, startingPos, count);
        }

        /// <summary>
        /// Helper function for index of with a start and an ending.
        /// </summary>
        /// <param name="markdown"></param>
        /// <param name="search"></param>
        /// <param name="startingPos"></param>
        /// <param name="endingPos"></param>
        /// <returns></returns>
        public static int IndexOf(string markdown, char search, int startingPos, int endingPos, bool reverseSearch = false)
        {
            // Check the ending isn't out of bounds.
            if (endingPos > markdown.Length)
            {
                endingPos = markdown.Length;
                DebuggingReporter.ReportCriticalError("IndexOf endingPos > string length");
            }

            // Figure out how long to go
            int count = endingPos - startingPos;
            if (count < 0)
            {
                return -1;
            }

            // Make sure we don't go too far.
            int remainingCount = markdown.Length - startingPos;
            if (count > remainingCount)
            {
                DebuggingReporter.ReportCriticalError("IndexOf count > remaing count");
                count = remainingCount;
            }

            // Check the ending. Since we use inclusive ranges we need to -1 from this for
            // reverses searches.
            if (reverseSearch && endingPos > 0)
            {
                endingPos -= 1;
            }

            return reverseSearch ? markdown.LastIndexOf(search, endingPos, count) : markdown.IndexOf(search, startingPos, count);
        }

        /// <summary>
        /// Finds the next whitespace in a range.
        /// </summary>
        /// <param name="markdown"></param>
        /// <param name="startingPos"></param>
        /// <param name="endingPos"></param>
        /// <returns></returns>
        public static int FindNextWhiteSpace(string markdown, int startingPos, int endingPos, bool ifNotFoundReturnLength)
        {
            int currentPos = startingPos;
            while (currentPos < markdown.Length && currentPos < endingPos)
            {
                if (Char.IsWhiteSpace(markdown[currentPos]))
                {
                    return currentPos;
                }
                currentPos++;
            }
            return ifNotFoundReturnLength ? endingPos : -1;
        }

        /// <summary>
        /// Determines if a character is a whitespace character.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool IsWhiteSpace(char c)
        {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }

        /// <summary>
        /// Determines if a string is blank or comprised entirely of whitespace characters.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsBlankOrWhiteSpace(string str)
        {
            for (int i = 0; i < str.Length; i++)
                if (!IsWhiteSpace(str[i]))
                    return false;
            return true;
        }
    }
}
