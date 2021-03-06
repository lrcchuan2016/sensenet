﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SenseNet.Communication.Messaging;
using System.Diagnostics;
using System.Configuration;
using System.IO;
using System.Threading;
using SenseNet.Diagnostics;
using SenseNet.ContentRepository;

namespace SenseNet.Search.Indexing.Activities
{
    [Serializable]
    public abstract class DistributedLuceneActivity : DistributedAction
    {
        public override void DoAction(bool onRemote, bool isFromMe)
        {
            if (!LuceneManager.Running)
                return;

            if (onRemote && !isFromMe)
            {
                //TODO: Remove unnecessary inheritance steps.
                var luceneIndexingActivity = this as LuceneIndexingActivity;
                if (luceneIndexingActivity != null)
                {
                    // We can drop activities here because the queue will load these from the database
                    // anyway when it processed all the previous activities.
                    if (IndexingActivityQueue.IsOverloaded())
                    {
                        SnTrace.Index.Write("IAQ OVERLOAD drop activity FromReceiver A:" + luceneIndexingActivity.Id);
                        return;
                    }

                    luceneIndexingActivity.FromReceiver = true;

                    IndexingActivityQueue.ExecuteActivity(luceneIndexingActivity);
                }
                else
                {
                    this.InternalExecuteIndexingActivity();
                }
            }
        }

        public override void Distribute()
        {
            base.Distribute();
        }

        // ----------------------------------------------------------------------- 

        [NonSerialized]
        private AutoResetEvent _finishSignal = new AutoResetEvent(false);
        [NonSerialized]
        private bool _finished;
        [NonSerialized]
        private int _waitingThreadId;

        internal void InternalExecuteIndexingActivity()
        {
            try
            {
                var persistentActivity = this as SenseNet.Search.Indexing.Activities.LuceneIndexingActivity;
                var id = persistentActivity == null ? "" : ", ActivityId: " + persistentActivity.Id;
                using (var op = SnTrace.Index.StartOperation("IndexingActivity execution: type:{0} id:{1}", this.GetType().Name, id))
                {
                    using (new SenseNet.ContentRepository.Storage.Security.SystemAccount())
                        ExecuteIndexingActivity();

                    op.Successful = true;
                }
            }
            finally
            {
                this.Finish();
            }
        }

        internal abstract void ExecuteIndexingActivity();

        /// <summary>
        /// Waits for a release signal that indicates that this activity has been executed
        /// successfully in the background.
        /// </summary>
        public void WaitForComplete()
        {
            if (_finished)
                return;

            _waitingThreadId = Thread.CurrentThread.ManagedThreadId;

            var indexingActivity = this as LuceneIndexingActivity;

            SnTrace.IndexQueue.Write("IAQ: A{0} blocks the T{1}", indexingActivity.Id, _waitingThreadId);

            if (Debugger.IsAttached)
            {
                _finishSignal.WaitOne();
            }
            else
            {
                if (!_finishSignal.WaitOne(SenseNet.Configuration.Indexing.LuceneActivityTimeoutInSeconds * 1000, false))
                {
                    string message;

                    if (indexingActivity != null)
                        message = string.Format("IndexingActivity is timed out. Id: {0}, Type: {1}. Max task id and exceptions: {2}"
                            , indexingActivity.Id, indexingActivity.ActivityType, IndexingActivityQueue.GetCurrentCompletionState());
                    else
                        message = "Activity is not finishing on a timely manner";

                    throw new ApplicationException(message);
                }
            }
        }

        /// <summary>
        /// Sets the finish signal to release all threads waiting for this activity to complete.
        /// </summary>
        internal virtual void Finish()
        {
            _finished = true;
            if (_finishSignal != null)
            {
                _finishSignal.Set();
                if (_waitingThreadId > 0)
                    SnTrace.IndexQueue.Write("IAQ: waiting resource released T{0}.", _waitingThreadId);
            }
        }

    }
}
