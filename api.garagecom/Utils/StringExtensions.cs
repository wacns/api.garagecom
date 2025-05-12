#region

using System.Net;
using System.Text.RegularExpressions;

#endregion

namespace api.garagecom.Utils;

public static class StringExtensions
{
    // 1. Compile once: match ANY character that's NOT
    //    - A–Z or a–z
    //    - 0–9
    //    - underscore (_)
    //    - dot (.)
    private static readonly Regex _invalidChars = new(
        @"[^\w\.]+", // + means “one or more in a row”
        RegexOptions.Compiled);

    // 2. Compile once: collapse multiple underscores
    private static readonly Regex _multiUnderscores = new(
        @"_+", // one or more _
        RegexOptions.Compiled);
    
    
    /// <summary>
    ///   Converts any HTML entities (e.g. &#129392;) into their real Unicode chars.
    /// </summary>
    public static string HtmlUnescape(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // WebUtility.HtmlDecode will handle &#129392; → 🥸 and also named entities (&amp; &lt; etc.)
        return WebUtility.HtmlDecode(input);
    }
    

    /// <summary>
    ///     Sanitizes any string into a filename containing only
    ///     letters, digits, underscore or dot—whitespace → underscore,
    ///     everything else stripped out.
    /// </summary>
    public static string SanitizeFileName(this string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        // a) Trim leading/trailing whitespace so we don't end up
        //    with leading/trailing underscores
        fileName = fileName.Trim();

        // b) Replace any whitespace (space, tab, newline…) with underscore
        fileName = Regex.Replace(fileName, @"\s+", "_");

        // c) Remove all characters NOT in [A-Za-z0-9_.]
        fileName = _invalidChars.Replace(fileName, string.Empty);

        // d) Collapse runs of underscores into a single one
        fileName = _multiUnderscores.Replace(fileName, "_");

        // e) Trim underscores (and dots) off the ends, if any
        return fileName.Trim('_', '.');
    }
}