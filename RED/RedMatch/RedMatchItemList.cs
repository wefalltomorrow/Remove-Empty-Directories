using System.Collections;
using System.Collections.Generic;
using Alphaleonis.Win32.Filesystem;

namespace RED.Match
{
	public class RedMatchItemList : IEnumerable<RedMatchItem>
	{
		public RedMatchItemList()
		{
			FilterType = RedMatchFilterType.Generic;
			Items = new List<RedMatchItem>();
			itemsByMethod = new List<RedMatchItem>[(int)RedMatchMethodType.RegExPath + 1];
			for (int i = 0; i < itemsByMethod.Length; i++)
			{
				itemsByMethod[i] = new List<RedMatchItem>();
			}
		}

		private readonly List<RedMatchItem>[] itemsByMethod;

		public RedMatchItemList(RedMatchFilterType filterType) : this()
		{
			FilterType = filterType;
		}

		// Default Indexer for this object
		public RedMatchItem this[int index]
		{
			get { return Items[index]; }
			set
			{
				Items[index] = value;
				RebuildMethodLists();
			}
		}

		private void RebuildMethodLists()
		{
			for (int i = 0; i < itemsByMethod.Length; i++)
			{
				itemsByMethod[i].Clear();
			}
			foreach (RedMatchItem item in Items)
			{
				int methodIndex = item == null ? -1 : (int)item.MatchMethod;
				if (methodIndex >= 0 && methodIndex < itemsByMethod.Length)
				{
					itemsByMethod[methodIndex].Add(item);
				}
			}
		}

		public IEnumerator<RedMatchItem> GetEnumerator()
		{
			return Items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public RedMatchFilterType FilterType { get; private set; }

		public List<RedMatchItem> Items { get; private set; }

		public List<string> ToStringList()
		{
			List<string> list = new List<string>();
			foreach (RedMatchItem item in Items)
			{
				list.Add(item.ToString());
			}
			return list;
		}

		public void AddItem(bool enabled, RedMatchMethodType matchMethod, string matchText)
		{
			AddItem(RedMatchItem.FormatToString(enabled, matchMethod, matchText));
		}

		private void AddItem(string v)
		{
			/// The text is a pipe delimited string in the order "EnabledFlag|MatchCode|MatchText"
			/// EnabledFflag is a single character. + for enabled, - for disabled
			/// MatchCode is a short code indicating MatchMethod to be used
			/// MatchText is the text to be matched against directory or file names
			string enabledFlag;
			string matchCode;
			string matchText;

			if (!string.IsNullOrWhiteSpace(v))
			{
				string[] items = v.Trim().Split('|');
				switch (items.Length)
				{
					case 1:
						enabledFlag = "+";
						matchCode = "N";
						matchText = v;
						break;
					case 2:
						enabledFlag = "+";
						matchCode = items[0];
						matchText = items[1];
						break;
					case 3:
						enabledFlag = items[0];
						matchCode = items[1];
						matchText = items[2];
						break;
					default:
						// invalid
						enabledFlag = "-";
						matchCode = "?";
						matchText = v;
						break;
				}

				// MatchText only
				if (matchText.Contains("*") || (matchText.StartsWith("/") && matchText.EndsWith("/")))
				{
					matchCode = "RN";
				}

				RedMatchMethodType matchMethod = RedMatchItem.CodeToMatchMethod(matchCode);

				if (matchMethod != RedMatchMethodType.Invalid && !string.IsNullOrWhiteSpace(matchText))
				{
					RedMatchItem item = new RedMatchItem(matchMethod, matchText, enabledFlag == "+");
					Items.Add(item);
					itemsByMethod[(int)matchMethod].Add(item);
				}
			}
		}

		public void Transform(List<string> v, RedMatchFilterType filterType)
		{
			FilterType = filterType;
			Transform(v);
		}

		public void Transform(List<string> v)
		{
			Items.Clear();
			for (int i = 0; i < itemsByMethod.Length; i++)
			{
				itemsByMethod[i].Clear();
			}
			foreach (string item in v)
			{
				if (!string.IsNullOrWhiteSpace(item))
				{
					AddItem(item);
				}
			}
		}

		public bool IsOnList(DirectoryInfo dirinfo)
		{
			string nameToCheck = dirinfo.Name.ToLowerInvariant();
			string pathToCheck = dirinfo.FullName.ToLowerInvariant();
			return IsOnList(nameToCheck, pathToCheck, 0, false, out _);
		}

		public bool IsOnList(FileInfo fileinfo, int filesize, bool ignoreEmptyFiles, out string delPattern)
		{
			return IsOnList(fileinfo, (long)filesize, ignoreEmptyFiles, out delPattern);
		}

		public bool IsOnList(FileInfo fileinfo, long filesize, bool ignoreEmptyFiles, out string delPattern)
		{
			string nameToCheck = fileinfo.Name.ToLowerInvariant();
			string pathToCheck = fileinfo.FullName.ToLowerInvariant();
			return IsOnList(nameToCheck, pathToCheck, filesize, ignoreEmptyFiles, out delPattern);
		}

		private string GetCheckText(string matchText, string nameToCheck, string pathToCheck)
		{
			if (FilterType == RedMatchFilterType.Directory)
			{
				return matchText.Contains(Path.DirectorySeparatorChar.ToString()) ? pathToCheck : nameToCheck;
			}
			else
			{
				return nameToCheck;
			}
		}

		private bool IsOnList(string nameToCheck, string pathToCheck, long filesize, bool ignoreEmptyFiles, out string delPattern)
		{
			bool matched = false;

			delPattern = "";

			if (Items.Count > 0)
			{
				string textToCheck;

				if (!matched && FilterType == RedMatchFilterType.Files)
				{
					if (ignoreEmptyFiles && filesize == 0)
					{
						delPattern = "[Empty file]";
						matched = true;
					}
				}
				if (!matched)
				{
					foreach (RedMatchItem matchItem in itemsByMethod[(int)RedMatchMethodType.NameExact])
					{
						if (matchItem.Enabled && nameToCheck == matchItem.MatchTextToCompare)
						{
							matched = true;
							delPattern = matchItem.MatchText;
							break;
						}
					}
				}
				if (!matched)
				{
					foreach (RedMatchItem matchItem in itemsByMethod[(int)RedMatchMethodType.Contains])
					{
						if (!matchItem.Enabled) continue;
						textToCheck = GetCheckText(matchItem.MatchTextToCompare, nameToCheck, pathToCheck);
						if (textToCheck.Contains(matchItem.MatchTextToCompare))
						{
							matched = true;
							delPattern = matchItem.MatchText;
							break;
						}
					}
				}
				if (!matched)
				{
					foreach (RedMatchItem matchItem in itemsByMethod[(int)RedMatchMethodType.Endswith])
					{
						if (!matchItem.Enabled) continue;
						textToCheck = GetCheckText(matchItem.MatchTextToCompare, nameToCheck, pathToCheck);
						if (textToCheck.EndsWith(matchItem.MatchTextToCompare))
						{
							matched = true;
							delPattern = matchItem.MatchText;
							break;
						}
					}
				}
				if (!matched)
				{
					foreach (RedMatchItem matchItem in itemsByMethod[(int)RedMatchMethodType.Startwith])
					{
						if (!matchItem.Enabled) continue;
						textToCheck = GetCheckText(matchItem.MatchTextToCompare, nameToCheck, pathToCheck);
						if (textToCheck.StartsWith(matchItem.MatchTextToCompare))
						{
							matched = true;
							delPattern = matchItem.MatchText;
							break;
						}
					}
				}
				if (!matched)
				{
					foreach (RedMatchItem matchItem in itemsByMethod[(int)RedMatchMethodType.RegExName])
					{
						if (!matchItem.Enabled) continue;
						if (matchItem.RegEx.IsMatch(nameToCheck))
						{
							matched = true;
							delPattern = matchItem.MatchText;
							break;
						}
					}
				}
				if (!matched && FilterType == RedMatchFilterType.Directory)
				{
					if (!matched)
					{
						foreach (RedMatchItem matchItem in itemsByMethod[(int)RedMatchMethodType.RegExPath])
						{
							if (!matchItem.Enabled) continue;
							if (matchItem.RegEx.IsMatch(pathToCheck))
							{
								matched = true;
								delPattern = matchItem.MatchText;
								break;
							}
						}
					}
					if (!matched)
					{
						foreach (RedMatchItem matchItem in itemsByMethod[(int)RedMatchMethodType.NameExactWithPath])
						{
							if (matchItem.Enabled && pathToCheck == matchItem.MatchTextToCompare)
							{
								matched = true;
								delPattern = matchItem.MatchText;
								break;
							}
						}
					}
				}
			}
			return matched;
		}
	}
}
