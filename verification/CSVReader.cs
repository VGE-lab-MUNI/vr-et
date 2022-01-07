// Lightweight CSV reader for Unity
// Teemu Ikonen, 2014
// https://bravenewmethod.com/2014/09/13/lightweight-csv-reader-for-unity/
//
// The script uses a List<Dictionary<string,object>> datastructure to load up on CSV files.
// These are best to be loaded on Awake, so that they can be further processed by other scripts on Start.
//   That is, to respect script execution order: https://docs.unity3d.com/Manual/ExecutionOrder.html
//
// Usage: void Awake() { List<Dictionary<string,object>> data = CSVReader.Read ("example"); }
//   The loaded datafile needs to be located in this project folder: Assets/Resources/
// for(var i=0; i < data.Count; i++) { print ("name " + data[i]["name"] + ...); }

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

//set CultureInfo first, so that number format is okay
//System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
//var currentCulture = System.Threading.Thread.CurrentThread.CurrentCulture.Name;
//var ci = new CultureInfo(currentCulture)
//{
//    NumberFormat = { NumberDecimalSeparator = "." },
//    DateTimeFormat = { DateSeparator = "/" }
//};
//System.Threading.Thread.CurrentThread.CurrentCulture = ci;
//System.Threading.Thread.CurrentThread.CurrentUICulture = ci;
//FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
//new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

public class CSVReader
{
    static string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
    static string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";
    static char[] TRIM_CHARS = { '\"' };
 
    public static List<Dictionary<string, object>> Read(string file)
    {
        var list = new List<Dictionary<string, object>>();
        TextAsset data = Resources.Load (file) as TextAsset;
 
        var lines = Regex.Split (data.text, LINE_SPLIT_RE);
 
        if(lines.Length <= 1) return list;
 
        var header = Regex.Split(lines[0], SPLIT_RE);
        for(var i=1; i < lines.Length; i++) {
 
            var values = Regex.Split(lines[i], SPLIT_RE);
            if(values.Length == 0 ||values[0] == "") continue;
 
            var entry = new Dictionary<string, object>();
            for(var j=0; j < header.Length && j < values.Length; j++ ) {
                string value = values[j];
                value = value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\\", "");
                object finalvalue = value;
                int n;
                float f;
                if(int.TryParse(value, out n)) {
                    finalvalue = n;
                } else if (float.TryParse(value, out f)) {
                    finalvalue = f;
                }
                entry[header[j]] = finalvalue;
            }
            list.Add (entry);
        }
        return list;
    }
}