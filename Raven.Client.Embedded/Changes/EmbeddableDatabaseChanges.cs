﻿using System;
using System.Threading.Tasks;
using Raven.Abstractions.Data;
using Raven.Abstractions.Util;
using Raven.Client.Changes;
using Raven.Client.Connection;

namespace Raven.Client.Embedded.Changes
{
	internal class EmbeddableDatabaseChanges : IDatabaseChanges
	{
		private readonly EmbeddableObserableWithTask<IndexChangeNotification> indexesObservable;
		private readonly EmbeddableObserableWithTask<DocumentChangeNotification> documentsObservable;

		public EmbeddableDatabaseChanges(EmbeddableDocumentStore embeddableDocumentStore)
		{
			Task = new CompletedTask();
			indexesObservable = new EmbeddableObserableWithTask<IndexChangeNotification>();
			documentsObservable = new EmbeddableObserableWithTask<DocumentChangeNotification>();

			embeddableDocumentStore.DocumentDatabase.TransportState.OnIndexChangeNotification += indexesObservable.Notify;
			embeddableDocumentStore.DocumentDatabase.TransportState.OnDocumentChangeNotification += documentsObservable.Notify;
		}

		public Task Task { get; private set; }

		public IObservableWithTask<IndexChangeNotification> IndexSubscription(string indexName)
		{
			return new FilteringObservableWithTask<IndexChangeNotification>(indexesObservable, 
				notification => string.Equals(indexName, notification.Name, StringComparison.InvariantCultureIgnoreCase));
		}

		public IObservableWithTask<DocumentChangeNotification> DocumentSubscription(string docId)
		{
			return new FilteringObservableWithTask<DocumentChangeNotification>(documentsObservable, 
				notification => string.Equals(docId, notification.Name, StringComparison.InvariantCultureIgnoreCase));
		}

		public IObservableWithTask<DocumentChangeNotification> DocumentPrefixSubscription(string docIdPrefix)
		{
			if (docIdPrefix == null) throw new ArgumentNullException("docIdPrefix");

			return new FilteringObservableWithTask<DocumentChangeNotification>(documentsObservable,
				notification => notification.Name.StartsWith(docIdPrefix, StringComparison.InvariantCultureIgnoreCase));
		}
	}
}