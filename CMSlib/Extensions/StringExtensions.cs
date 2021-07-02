﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CMSlib.ConsoleModule;

namespace CMSlib.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns a the string, in a form where the length is always <paramref name="maxLength"/> or less.
        /// </summary>
        /// <param name="str">The string to format</param>
        /// <param name="maxLength">The length</param>
        public static string Ellipse(this string str, int maxLength)
        {
            if (maxLength < 4)
                throw new ArgumentException("The length parameter of the Ellipse method must be 4 or greater.");
            if (str.Length > maxLength)
            {
                return str.Substring(0, maxLength - 3) + "...";
            }
            return str;
        }
        

        public static string PadToDivisible(this string str, int divisor)
        {
            if (str.Length % divisor == 0) return str;
            return str + new string(' ', divisor - str.Length % divisor);
        }

        public static int VisibleLength(this string str)
        {
            int returns = 0;
            bool inEsc = false;
            int strLen = str.Length;
            for(int i = 0; i < strLen; i++)
            {
                if (str[i] == '\u001B')
                {
                    inEsc = true;
                    i++;
                    continue;
                }
                if(str[i] == '\u0000')
                    continue;

                if (inEsc)
                {
                    if (str[i] >= '\u0040' || str[i] == '\u0000')
                        inEsc = false; 
                    continue;
                }
                returns++;
            }

            return returns;
        }
        public static string PadToVisibleDivisible(this string str, int divisor)
        {
            if (str is null)
                return new string(' ', divisor);
            int visibleLength = str.VisibleLength();
            if (visibleLength == 0)

                return str + new string(' ', str.Length);
            
            if (visibleLength % divisor == 0)
                return str;
            return str + new string(' ', divisor - visibleLength % divisor);
        }

        public static bool IsVisible(this string str)
        {
            return Regex.Replace(str, "\u001B(.)[\\d;]*(.)", "").Any();
        }
        public static string[] SplitOnLength(this string str, int length)
        {
            if (length <= 0) return Array.Empty<string>();
            string[] output = new string[str.Length % length == 0 ? str.Length / length : str.Length / length + 1];
            for(int i = 0; i < str.Length / length; i++)
            {
                output[i] = str.Substring(i * length, length);
            }
            if (str.Length % length != 0) output[^1] = str.Substring((output.Length -1) * (length));
            return output;
            
        }

        public static IEnumerable<string> SplitOnNonEscapeLength(this string str, int length)
        {
            bool inEscapeCharacter = false; //whether [i] is in an escape sequence
            int visibleTotal = 0; //total visible characters (non-escape)
            int strStart = 0; //start of current yield
            int strLength = str.Length;
            int escStart = 0; //index of <ESC> literal
            List<string> currentSgr = new(); //stores current modifiers
            string[] prevSgr = null;
            for (int i = 0; i < strLength; i++)
            {
                if (str[i] == '\u001B') //<ESC> Literal
                {
                    inEscapeCharacter = true;
                    escStart = i;
                    i++;
                    continue;
                }
                if (inEscapeCharacter){
                    if (str[i] >= '\u0040' || str[i] == '\u0000') //end of Control sequence
                    {
                        inEscapeCharacter = false;
                        string ansiEsc = str[escStart..(i + 1)];
                        if (ansiEsc == AnsiEscape.SgrClear)
                        {
                            currentSgr.Clear(); // previous sgr is now invalid
                            continue;
                        }
                        currentSgr.Add(str[escStart..(i + 1)]); // if it's a modifier, add it to the list
                    }
                    else
                        continue;//don't add to visible total, still in escape sequence
                }
                else
                {
                    visibleTotal++;
                }
                if (visibleTotal >= length) // if reached the end of a yield
                {
                    if (i < strLength - 1 && str[i + 1] == '\u001B')
                    {
                        //continue;
                    }
                    visibleTotal = 0;
                    int from = strStart;
                    strStart = i + 1;
                    yield return (prevSgr is null ? "" : string.Concat(prevSgr)) + str[from..(i+1)] + AnsiEscape.SgrClear + AnsiEscape.AsciiMode;
                    prevSgr = new string[currentSgr.Count];
                    currentSgr.CopyTo(prevSgr);
                    continue;
                }
                if (i == strLength - 1)
                {
                    yield return str[strStart..];
                }
            }
        }

        public static string Censor(this string str, string word, out bool wordFound , string replacementString = "*")
        {
            if (!str.ToLower().Contains(word.ToLower())) {
                wordFound = false;
                return str;
            }

            StringBuilder output = new StringBuilder(str.Substring(0, str.ToLower().IndexOf(word.ToLower())));
            for(int i = 0;i<word.Length; i++)
            {
                output.Append(replacementString);
            }
            if (str.ToLower().IndexOf(word.ToLower()) + word.Length != str.Length)
                output.Append(str.Substring(str.ToLower().IndexOf(word.ToLower()) + word.Length));
            wordFound = true;
            return output.ToString();
        }

        public static string DiscordMarkdownStrip(this string str)
        {
            return str.Replace("*", "\\*").Replace("|", "\\|").Replace(">", "\\>").Replace("<", "\\<").Replace("@", "\\@").Replace("~", "\\~").Replace("`", "\\`").Replace("#", "\\#").Replace("_", "\\_");
        }

        public static string AddPlural(this string str, int number)
        {

            if (number != -1 && number != 1)
                return str + "s";
            return str;
        }
        public static int ParseInt(this string str)
        {
            return int.Parse(str);
        }
        public static void KeybdType(this string str)
        {
            for(int i = 0; i < str.Length; i++)
            {
                char current = str[i];
                if(current >= 'A' && current <= 'Z')
                {
                    ((byte)0x10).HoldKey();
                    ((byte)(0x41 + current - 'A')).KeyPress();
                    ((byte)0x10).ReleaseKey();
                    continue;
                }
                if(current >= 'a' && current <= 'z')
                {
                    ((byte)(0x41 + current - 'a')).KeyPress();
                    continue;
                }
                if(current >= '0' && current <= '9')
                {
                    ((byte)(0x30 + current - '0')).KeyPress();
                }
            }
        }

        public static string Multiply(this string str, int times)
        {
            StringBuilder builder = new StringBuilder(str.Length * times);
            for (int i = 0; i < times; i++)
            {
                builder.Append(str);
            }

            return builder.ToString();
        }

        public static string GuaranteeLength(this string str, int length)
        {
            if (str is null || str.Length == 0) return new string(' ', length);
            if (str.Length == length) return str;
            return str.Length > length ? str[..length] : str.PadToVisibleDivisible(length);
        }


    }
    
}
