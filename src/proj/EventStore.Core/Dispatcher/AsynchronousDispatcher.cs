namespace EventStore.Dispatcher
{
	using System;
	using System.Linq;
	using System.Threading;
	using Persistence;

	public class AsynchronousDispatcher : IDispatchCommits
	{
		private readonly IPublishMessages bus;
		private readonly IPersistStreams persistence;
		private readonly Action<Commit, Exception> handleException;
		private bool disposed;

		public AsynchronousDispatcher(
			IPublishMessages bus, IPersistStreams persistence, Action<Commit, Exception> handleException)
		{
			this.bus = bus;
			this.persistence = persistence;
			this.handleException = handleException ?? ((c, e) => { });

			this.Start();
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing || this.disposed)
				return;

			this.disposed = true;
			this.bus.Dispose();
			this.persistence.Dispose();
		}

		private void Start()
		{
			var commits = this.persistence.GetUndispatchedCommits() ?? new Commit[] { };
			foreach (var commit in commits.ToList())
				this.Dispatch(commit);
		}

		public virtual void Dispatch(Commit commit)
		{
			ThreadPool.QueueUserWorkItem(state => this.BeginDispatch(commit));
		}
		protected virtual void BeginDispatch(Commit commit)
		{
			try
			{
				this.bus.Publish(commit);
				this.persistence.MarkCommitAsDispatched(commit);
			}
			catch (Exception e)
			{
				this.handleException(commit, e);
			}
		}
	}
}