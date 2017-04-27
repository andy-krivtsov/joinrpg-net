﻿using System.Collections.Generic;
using System.Linq;
using JoinRpg.Helpers;

namespace JoinRpg.Domain
{
  public static class TagExtensions
  {
    public static IEnumerable<string> ExtractTagNames(this string title)
    {
      return ExtractTagNamesImpl(title).Distinct();
    }

    private static IEnumerable<string> ExtractTagNamesImpl(string title)
    {
      for (var i = 0; i < title.Length; i++)
      {
        if (title[i] != '#') continue;

        var tagName = title.Skip(i + 1).TakeWhile(c => char.IsLetterOrDigit(c) || c == '_').AsString().Trim();
        if (tagName != "")
        {
          yield return tagName;
        }
      }
    }

    public static string RemoveTagNames(this string title)
    {
      return title.RemoveFromString(title.ExtractTagNames().Select(tag => "#" + tag)).Trim();
    }
  }
}
