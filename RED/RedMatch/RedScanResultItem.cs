using System;
using Alphaleonis.Win32.Filesystem;

namespace RED.Match
{
	public class RedScanResultItem
	{
		public RedScanResultItem(DirectoryInfo di, DirectorySearchStatusTypes status) : this(di, status, string.Empty) { }

		public RedScanResultItem(DirectoryInfo di, DirectorySearchStatusTypes status, string errorMsg)
			: this(di, status, errorMsg, 0) { }

		public RedScanResultItem(DirectoryInfo di, DirectorySearchStatusTypes status, int fileCount)
			: this(di, status, string.Empty, fileCount) { }

		private RedScanResultItem(DirectoryInfo di, DirectorySearchStatusTypes status, string errorMsg, int fileCount)
		{
			Populate(di, status, errorMsg, fileCount);
		}

		public DirectoryInfo Directory { get; private set; }
		public string FullPath { get { return Directory?.FullName; } }
		public string Name { get { return Directory?.Name; } }
		public DirectorySearchStatusTypes SearchStatus { get; private set; }
		public DirectorySearchStatusTypes SearchStatusOriginal { get; private set; }
		public string ErrorMessage { get; private set; }
		public int FileCount { get; private set; }

		private void Populate(DirectoryInfo di, DirectorySearchStatusTypes status, string errorMsg, int fileCount)
		{
			try
			{
				Directory = di;
				SearchStatus = status;
				SearchStatusOriginal = status;
				ErrorMessage = errorMsg;
				FileCount = fileCount;
			}
			catch (Exception ex)
			{
				ErrorMessage = errorMsg + Environment.NewLine + ex.Message;
			}
		}
	}
}
