using System;

namespace PvZCoop;

public static class LanguageChoice
{
    public static string Normalize(string code)
    {
        string value=(code??"").Trim().ToLowerInvariant();
        return value is "uk" or "ua" or "uk-ua"?"uk":"en";
    }
    public static bool IsUkrainian(string code)=>Normalize(code)=="uk";
    public static bool IsSupported(string code)=>code is "en" or "uk";
}
