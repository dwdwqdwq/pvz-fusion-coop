using System;
using System.Globalization;
using System.Text;
namespace PvZCoop;
public static class PlayerNames {
 public static string Short(string value){
  if(string.IsNullOrEmpty(value))return "";
  int[] starts=StringInfo.ParseCombiningCharacters(value);
  return starts.Length>8?value[..starts[8]]+"...":value;
 }
 public static string Clean(string value,string fallback){
  if(string.IsNullOrWhiteSpace(value))return fallback;
  var result=new StringBuilder();var elements=StringInfo.GetTextElementEnumerator(value);
  int count=0;while(elements.MoveNext()&&count<32){string s=elements.GetTextElement();if(result.Length+s.Length>128)break;
   foreach(char c in s)if(!char.IsControl(c)&&c!='<'&&c!='>')result.Append(c);count++;
  }
  return result.ToString().Trim() is var name && name.Length>0?name:fallback;
 }
}
