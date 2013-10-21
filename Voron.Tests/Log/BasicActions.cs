﻿// -----------------------------------------------------------------------
//  <copyright file="ForceLogFlushes.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using Voron.Impl;
using Xunit;

namespace Voron.Tests.Log
{
	public class BasicActions : StorageTest
	{
		// all tests here relay on the fact than one log file can contains max 10 pages
		protected override void Configure(StorageOptions options, IVirtualPager pager)
		{
			options.LogFileSize = 10*pager.PageSize;
		}

		[Fact]
		public void CanUseMultipleLogFiles()
		{
			var bytes = new byte[1024];
			new Random().NextBytes(bytes);

			for (var i = 0; i < 15; i++)
			{
				using (var tx = Env.NewTransaction(TransactionFlags.ReadWrite))
				{
					Env.Root.Add(tx, "item/" + i, new MemoryStream(bytes));
					tx.Commit();
				}
			}

			Assert.True(Env.Log.FilesInUse > 1);

			for (var i = 0; i < 15; i++)
			{
				using (var tx = Env.NewTransaction(TransactionFlags.Read))
				{
					Assert.NotNull(Env.Root.Read(tx, "item/" + i));
				}
			}
		}

		[Fact]
		public void CanSplitTransactionIntoMultipleLogFiles()
		{
			var bytes = new byte[1024];
			new Random().NextBytes(bytes);

			// everything is done in one transaction
			using (var tx = Env.NewTransaction(TransactionFlags.ReadWrite))
			{
				for (int i = 0; i < 15; i++)
				{
					Env.Root.Add(tx, "item/" + i, new MemoryStream(bytes));
				}

				tx.Commit();
			}

			// however we put that into 2 log files
			Assert.True(Env.Log.FilesInUse == 2);

			// and still can read from both files
			for (var i = 0; i < 15; i++)
			{
				using (var tx = Env.NewTransaction(TransactionFlags.Read))
				{
					Assert.NotNull(Env.Root.Read(tx, "item/" + i));
				}
			}
		}

		[Fact]
		public void ShouldNotReadUncommittedTransaction()
		{
			using (var tx = Env.NewTransaction(TransactionFlags.ReadWrite))
			{
				Env.Root.Add(tx, "items/1", StreamFor("values/1"));
				// tx.Commit(); uncommitted transaction
			}

			using (var tx = Env.NewTransaction(TransactionFlags.Read))
			{
				Assert.Null(Env.Root.Read(tx, "items/1"));
			}
		}

		[Fact]
		public void LogFileShouldOverridePagesAllocatedByAbortedTransaction()
		{
			using (var tx1 = Env.NewTransaction(TransactionFlags.ReadWrite))
			{
				Env.Root.Add(tx1, "items/1", StreamFor("values/1"));
				// tx1.Commit(); aborted transaction
			}
			var writePosition = Env.Log._currentFile.WritePagePosition;

			// should reuse pages allocated by tx1
			using (var tx2 = Env.NewTransaction(TransactionFlags.ReadWrite))
			{
				Env.Root.Add(tx2, "items/2", StreamFor("values/2"));
				tx2.Commit();
			}

			Assert.Equal(0, Env.Log._currentFile.Number); // still the same log
			Assert.Equal(writePosition, Env.Log._currentFile.WritePagePosition);
		}

		[Fact]
		public void CanFlushDataFromLogToDataFile()
		{
			for (var i = 0; i < 100; i++)
			{
				using (var tx = Env.NewTransaction(TransactionFlags.ReadWrite))
				{
					Env.Root.Add(tx, "items/" + i, StreamFor("values/" + i));
					tx.Commit();
				}
			}

			Assert.True(Env.Log.FilesInUse > 1);
			var usedLogFiles = Env.Log.FilesInUse;

			Env.FlushLogToDataFile();

			Assert.True(Env.Log.FilesInUse <= 1 && Env.Log.FilesInUse < usedLogFiles);

			for (var i = 0; i < 100; i++)
			{
				using (var tx = Env.NewTransaction(TransactionFlags.Read))
				{
					var readKey = ReadKey(tx, "items/" + i);
					Assert.Equal("values/" + i, readKey.Item2);
				}
			}
		}
	}
}