using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using RED.Helper;
using FileAttributes = System.IO.FileAttributes;
using TXT = RED.RedGetText;

namespace RED
{
	/// <summary>
	/// Searches for empty directories
	/// </summary>
	public class FindEmptyDirectoryWorker : BackgroundWorker
	{
		private int folderCount = 0;
		private int possibleEndlessLoop = 0;
		private readonly SemaphoreSlim scanSlots = new SemaphoreSlim(Math.Min(7, Math.Max(0, Environment.ProcessorCount - 1)));

		public int FolderCount
		{
			get { return Interlocked.CompareExchange(ref folderCount, 0, 0); }
		}

		public RuntimeData RunData { get; set; }

		public DeletionErrorEventArgs ErrorInfo { get; set; }

		public int PossibleEndlessLoop
		{
			get { return Interlocked.CompareExchange(ref possibleEndlessLoop, 0, 0); }
			set { Interlocked.Exchange(ref possibleEndlessLoop, value); }
		}

		public FindEmptyDirectoryWorker()
		{
			WorkerReportsProgress = true;
			WorkerSupportsCancellation = true;
		}

		protected override void OnDoWork(DoWorkEventArgs e)
		{
			DirectoryInfo startFolder = (DirectoryInfo)e.Argument;

			this.PossibleEndlessLoop = 0;
			Interlocked.Exchange(ref this.folderCount, 0);
			this.RunData.ScanResults.Clear();

			try
			{
				int rootFileCount;
				DirectorySearchStatusTypes rootStatusType = this.CheckIfDirectoryEmpty(startFolder, 1, out rootFileCount);

				this.ReportProgress(0, new FoundEmptyDirInfoEventArgs(startFolder, rootStatusType, rootFileCount));

				if (this.PossibleEndlessLoop > this.RunData.InfiniteLoopDetectionCount)
				{
					string emsg = TXT.Translate("Detected possible infinite - loop somewhere in the target path {0} (symbolic links can cause this)", RedAssist.DQuote(startFolder.FullName));
					this.RunData.AddLogMessage(emsg);
					throw new Exception(emsg);
				}
			}
			catch (Exception ex)
			{
				e.Cancel = true;
				this.RunData.AddLogMessage(TXT.Translate("An error occurred during the scan process: " + ex.Message));
				this.ErrorInfo = new DeletionErrorEventArgs(startFolder.FullName, ex.Message);
				return;
			}

			if (CancellationPending)
			{
				this.RunData.AddLogMessage(TXT.Translate("Scan process was cancelled"));
				e.Cancel = true;
				e.Result = 0;
				return;
			}

			e.Result = 1;
		}

		private DirectorySearchStatusTypes CheckIfDirectoryEmpty(DirectoryInfo startDir, int depth, out int fileCount)
		{
			fileCount = 0;
			if (this.PossibleEndlessLoop > this.RunData.InfiniteLoopDetectionCount)
			{
				this.ReportProgress(0, new FoundEmptyDirInfoEventArgs(startDir, DirectorySearchStatusTypes.Error, TXT.Translate("Aborted - possible infinite-loop detected")));
				return DirectorySearchStatusTypes.Error;
			}

			try
			{
				// Thread.Sleep(500); -> ?

				if (this.RunData.MaxDepth != -1 && depth > this.RunData.MaxDepth)
				{
					return DirectorySearchStatusTypes.NotEmpty;
				}

				// Cancel process if the user hits stop
				if (CancellationPending)
				{
					return DirectorySearchStatusTypes.NotEmpty;
				}

				int checkedFolderCount = Interlocked.Increment(ref this.folderCount);

				// update status progress bar after 100 steps:
				if (checkedFolderCount % 100 == 0)
				{
					this.ReportProgress(checkedFolderCount, TXT.Translate("Checking directory: {0}", startDir.Name));
				}

				bool containsFiles = false;

				// NotBob - If this directory is on the NeverEmpty list treat it as if it contains files
				if (this.RunData.NeverEmptyDirectoryList.IsOnList(startDir))
				{
					containsFiles = true;
					string msg = TXT.Translate("Directory is on the NeverEmpty list: {0}", RedAssist.DQuote(startDir.FullName));
					this.RunData.AddLogMessage(msg);
					if (!this.RunData.HideIgnoredDirectories)
					{
						this.ReportProgress(0, new FoundEmptyDirInfoEventArgs(startDir, DirectorySearchStatusTypes.NeverEmpty, msg));
					}
				}

				if (!containsFiles)
				{
					// Get file list
					string delPattern = string.Empty;
					try
					{
						foreach (FileInfo file in startDir.EnumerateFiles())
						{
							fileCount++;

							long filesize;
							try
							{
								filesize = RunData.IgnoreEmptyFiles ? file.Length : 0L;
							}
							catch
							{
								// Keep the folder if a file cannot be inspected.
								containsFiles = true;
								break;
							}

							if (!this.RunData.IgnoreFileNameList.IsOnList(file, filesize, RunData.IgnoreEmptyFiles, out delPattern))
							{
								containsFiles = true;
								// One non-ignored file is enough to keep this directory.
								break;
							}
						}
					}
					catch
					{
						// If enumeration fails, keep the directory and report the access error.
						containsFiles = true;
						this.RunData.AddLogMessage(TXT.Translate("Failed to access files in {0}", RedAssist.DQuote(startDir.FullName)));
						this.ReportProgress(0, new FoundEmptyDirInfoEventArgs(startDir, DirectorySearchStatusTypes.Error, TXT.Translate("Failed to access files")));
					}
				}

				List<DirectoryInfo> subFolderList = new List<DirectoryInfo>();
				try
				{
					subFolderList.AddRange(startDir.EnumerateDirectories());
				}
				catch
				{
					// If we can not read the folder -> don't delete it:
					this.RunData.AddLogMessage(TXT.Translate("Failed to access subdirectories in {0}", RedAssist.DQuote(startDir.FullName)));
					this.ReportProgress(0, new FoundEmptyDirInfoEventArgs(startDir, DirectorySearchStatusTypes.Error, TXT.Translate("Failed to access subdirectories")));
					fileCount = 0;
					return DirectorySearchStatusTypes.Error;
				}

				// The folder is empty, break here:
				if (!containsFiles && subFolderList.Count == 0)
				{
					return DirectorySearchStatusTypes.Empty;
				}

				bool allSubDirectoriesEmpty = true;
				int scanCount = 0;

				for (int i = 0; i < subFolderList.Count; i++)
				{
					DirectoryInfo curDir = subFolderList[i];
					FileAttributes attribs = curDir.Attributes;

					bool ignoreSystemDir = (this.RunData.IgnoreSystemFolders && ((attribs & FileAttributes.System) == FileAttributes.System));
					bool ignoreHiddenDir = (this.RunData.IgnoreHiddenFolders && ((attribs & FileAttributes.Hidden) == FileAttributes.Hidden));

					bool ignoreSubDirectory = (ignoreSystemDir || ignoreHiddenDir);

					if (!ignoreSubDirectory && this.RunData.IgnoreDirectoryNameList.IsOnList(curDir))
					{
						this.RunData.AddLogMessage(TXT.Translate("Aborted scan of {0} because it is on the ignore list", RedAssist.DQuote(curDir.FullName)));
						ignoreSubDirectory = true;
						// NotBob - option to exclude ignored directories from the scan window
						if (!this.RunData.HideIgnoredDirectories)
						{
							this.ReportProgress(0, new FoundEmptyDirInfoEventArgs(curDir, DirectorySearchStatusTypes.Ignore));
						}
					}

					if (!ignoreSubDirectory && (attribs & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
					{
						this.RunData.AddLogMessage(TXT.Translate("Aborted scan of {0} because it is a symbolic link", RedAssist.DQuote(curDir.FullName)));
						this.ReportProgress(0, new FoundEmptyDirInfoEventArgs(curDir, DirectorySearchStatusTypes.Error, TXT.Translate("Aborted because directory is a symbolic link")));
						ignoreSubDirectory = true;
					}

					// TODO: Implement more checks
					//else if ((attribs & FileAttributes.Device) == FileAttributes.Device) msg = "Device - Aborted - found";
					//else if ((attribs & FileAttributes.Encrypted) == FileAttributes.Encrypted) msg = "Encrypted -  found";
					// The file will not be indexed by the operating system's content indexing service.
					// else if ((attribs & FileAttributes.NotContentIndexed) == FileAttributes.NotContentIndexed) msg = "NotContentIndexed - Device found";
					//else if ((attribs & FileAttributes.Offline) == FileAttributes.Offline) msg = "Offline -  found";
					//else if ((attribs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) msg = "ReadOnly -  found";
					//else if ((attribs & FileAttributes.Temporary) == FileAttributes.Temporary) msg = "Temporary -  found";

					if (!ignoreSubDirectory)
					{
						// JRS ADDED check for AGE of folder
						if (curDir.CreationTime.AddHours(this.RunData.MinFolderAgeHours) < DateTime.Now)
						{
							subFolderList[scanCount++] = curDir;
						}
						else
						{
							string fmt = string.Format(TXT.Translate("Directory {0} skipped because creation time [{1}] is < {2} hours old"));
							this.RunData.AddLogMessage(string.Format(fmt, RedAssist.DQuote(curDir.FullName), curDir.CreationTime.ToString(), this.RunData.MinFolderAgeHours.ToString()));
							allSubDirectoriesEmpty = false;
						}
					}
					else
					{
						allSubDirectoriesEmpty = false;
					}
				}

				List<Task<DirectorySearchStatusTypes>> scanTasks = null;
				try
				{
					for (int i = 0; i < scanCount; i++)
					{
						DirectoryInfo curDir = subFolderList[i];
						if (CancellationPending)
						{
							allSubDirectoriesEmpty = false;
							break;
						}

						if (scanSlots.Wait(0))
						{
							if (scanTasks == null)
							{
								scanTasks = new List<Task<DirectorySearchStatusTypes>>();
							}

							try
							{
								scanTasks.Add(Task.Run(() =>
								{
									try
									{
										return ScanSubDirectory(curDir, depth + 1);
									}
									finally
									{
										scanSlots.Release();
									}
								}));
							}
							catch
							{
								scanSlots.Release();
								throw;
							}
						}
						else if (ScanSubDirectory(curDir, depth + 1) != DirectorySearchStatusTypes.Empty)
						{
							allSubDirectoriesEmpty = false;
						}
					}
				}
				catch
				{
					if (scanTasks != null)
					{
						Task.WaitAll(scanTasks.ToArray());
					}
					throw;
				}

				if (scanTasks != null)
				{
					Task.WaitAll(scanTasks.ToArray());
					foreach (Task<DirectorySearchStatusTypes> scanTask in scanTasks)
					{
						if (scanTask.Result != DirectorySearchStatusTypes.Empty)
						{
							allSubDirectoriesEmpty = false;
						}
					}
				}

				// All subdirectories are empty
				return (allSubDirectoriesEmpty && !containsFiles) ? DirectorySearchStatusTypes.Empty : DirectorySearchStatusTypes.NotEmpty;
			}
			catch (Exception ex)
			{
				// Error handling
				if (ex is System.IO.PathTooLongException)
				{
					Interlocked.Increment(ref this.possibleEndlessLoop);
				}
				this.RunData.AddLogMessage(TXT.Translate("An unknown error occurred while trying to scan directory: {0} - {1}", RedAssist.DQuote(startDir.FullName), ex.Message));
				this.ReportProgress(0, new FoundEmptyDirInfoEventArgs(startDir, DirectorySearchStatusTypes.Error, ex.Message));
				fileCount = 0;
				return DirectorySearchStatusTypes.Error;
			}
		}

		private DirectorySearchStatusTypes ScanSubDirectory(DirectoryInfo directory, int depth)
		{
			int childFileCount;
			DirectorySearchStatusTypes status = this.CheckIfDirectoryEmpty(directory, depth, out childFileCount);
			if (status == DirectorySearchStatusTypes.Empty)
			{
				this.ReportProgress(0, new FoundEmptyDirInfoEventArgs(directory, status, childFileCount));
			}
			return status;
		}
	}
}
