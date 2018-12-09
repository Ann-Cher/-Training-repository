﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JocysCom.ClassLibrary.IO
{
	public class FileFinder
	{

		public event EventHandler<FileFinderEventArgs> FileFound;

		int _DirectoryIndex;
		List<DirectoryInfo> _Directories;
		public bool IsPaused { get; set; }

		public bool IsStopping { get; set; }

		public List<FileInfo> GetFiles(string searchPattern, bool allDirectories = false, params string[] paths)
		{
			IsStopping = false;
			IsPaused = false;
			var fis = new List<FileInfo>();
			_Directories = paths.Select(x => new DirectoryInfo(x)).ToList();
			for (int i = 0; i < _Directories.Count; i++)
			{
				// Pause or Stop.
				while (IsPaused && !IsStopping)
					System.Threading.Thread.Sleep(500);
				if (IsStopping)
					return fis;
				// Do tasks.
				_DirectoryIndex = i;
				var di = _Directories[i];
				// Skip folders if don't exists.
				if (!di.Exists) continue;
				AddFiles(di, ref fis, searchPattern, allDirectories);
			}
			return fis;
		}

		public void AddFiles(DirectoryInfo di, ref List<FileInfo> fileList, string searchPattern, bool allDirectories)
		{
			try
			{
				var patterns = searchPattern.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				if (patterns.Length == 0)
				{
					// Lookup for all files.
					patterns = new[] { "" };
				}
				for (int p = 0; p < patterns.Length; p++)
				{
					var pattern = patterns[p];
					var files = string.IsNullOrEmpty(pattern)
						? di.GetFiles()
						: di.GetFiles(pattern);
					for (int i = 0; i < files.Length; i++)
					{
						// Pause or Stop.
						while (IsPaused && !IsStopping)
							System.Threading.Thread.Sleep(500);
						if (IsStopping)
							return;
						// Do tasks.
						var fullName = files[i].FullName;
						if (!fileList.Any(x => x.FullName == fullName))
						{
							fileList.Add(files[i]);
							var ev = FileFound;

							if (ev != null)
							{
								var e = new FileFinderEventArgs();
								e.Directories = _Directories;
								e.DirectoryIndex = _DirectoryIndex;
								e.FileIndex = fileList.Count - 1;
								e.Files = fileList;
								ev(this, e);
							}
						}
					}
				}
			}
			catch { }
			try
			{
				// If must search inside subdirectories then...
				if (allDirectories)
				{
					var subDis = di.GetDirectories();
					foreach (DirectoryInfo subDi in subDis)
					{
						// Pause or Stop.
						while (IsPaused && !IsStopping)
							System.Threading.Thread.Sleep(500);
						if (IsStopping)
							return;
						// Do tasks.
						AddFiles(subDi, ref fileList, searchPattern, allDirectories);
					}
				}
			}
			catch { }
		}

	}
}
