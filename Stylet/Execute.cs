﻿using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Stylet
{
    /// <summary>
    /// Generalised dispatcher, which can post and send
    /// </summary>
    public interface IDispatcher
    {
        /// <summary>
        /// Execute asynchronously
        /// </summary>
        void Post(Action action);

        /// <summary>
        /// Execute synchronously
        /// </summary>
        void Send(Action action);

        /// <summary>
        /// True if invocation isn't required
        /// </summary>
        bool IsCurrent { get; }
    }

    internal class DispatcherWrapper : IDispatcher
    {
        private readonly Dispatcher dispatcher;

        public DispatcherWrapper(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public void Post(Action action)
        {
            this.dispatcher.BeginInvoke(action);
        }

        public void Send(Action action)
        {
            this.dispatcher.Invoke(action);
        }

        public bool IsCurrent
        {
            get { return this.dispatcher.CheckAccess(); }
        }
    }

    internal class SynchronousDispatcher : IDispatcher
    {
        public void Post(Action action)
        {
            action();
        }

        public void Send(Action action)
        {
            action();
        }

        public bool IsCurrent
        {
            get { return true; }
        }
    }

    /// <summary>
    /// Static class providing methods to easily run an action on the UI thread in various ways, and some other things
    /// </summary>
    public static class Execute
    {
        private static IDispatcher _dispatcher;

        /// <summary>
        /// Should be set to the UI thread's Dispatcher. This is normally done by the Bootstrapper.
        /// </summary>
        public static IDispatcher Dispatcher
        {
            get
            {
                if (_dispatcher == null)
                    _dispatcher = new SynchronousDispatcher();
                return _dispatcher;
            }

            set
            {
                if (value == null)
                    throw new ArgumentNullException();
                _dispatcher = value;
            }
        }

        private static bool? inDesignMode;

        /// <summary>
        /// Default dispatcher used by PropertyChanged events. Defaults to OnUIThread
        /// </summary>
        public static Action<Action> DefaultPropertyChangedDispatcher = a => a();

        /// <summary>
        /// Default dispatcher used by CollectionChanged events. Defaults to OnUIThreadSync
        /// </summary>
        public static Action<Action> DefaultCollectionChangedDispatcher = Execute.OnUIThreadSync;

        /// <summary>
        /// Dispatches the given action to be run on the UI thread asynchronously, even if the current thread is the UI thread
        /// </summary>
        /// <param name="action">Action to run on the UI thread</param>
        public static void PostToUIThread(Action action)
        {
            Dispatcher.Post(action);
        }

        /// <summary>
        /// Dispatches the given action to be run on the UI thread asynchronously, and returns a task which completes when the action completes, even if the current thread is the UI thread
        /// </summary>
        /// <remarks>DO NOT BLOCK waiting for this Task - you'll cause a deadlock. Use PostToUIThread instead</remarks>
        /// <param name="action">Action to run on the UI thread</param>
        /// <returns>Task which completes when the action has been run</returns>
        public static Task PostToUIThreadAsync(Action action)
        {
            return PostOnUIThreadInternalAsync(action);
        }

        /// <summary>
        /// Dispatches the given action to be run on the UI thread asynchronously, or runs it synchronously if the current thread is the UI thread
        /// </summary>
        /// <param name="action">Action to run on the UI thread</param>
        public static void OnUIThread(Action action)
        {
            if (Dispatcher.IsCurrent)
                action();
            else
                Dispatcher.Post(action);
        }

        /// <summary>
        /// Dispatches the given action to be run on the UI thread and blocks until it completes, or runs it synchronously if the current thread is the UI thread
        /// </summary>
        /// <param name="action">Action to run on the UI thread</param>
        public static void OnUIThreadSync(Action action)
        {
            Exception exception = null;
            if (Dispatcher.IsCurrent)
            {
                action();
            }
            else
            {
                Dispatcher.Send(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        exception = e;
                    }
                });

                if (exception != null)
                    throw new System.Reflection.TargetInvocationException("An error occurred while dispatching a call to the UI Thread", exception);
            }
        }

        /// <summary>
        /// Dispatches the given action to be run on the UI thread and returns a task that completes when the action completes, or runs it synchronously if the current thread is the UI thread
        /// </summary>
        /// <param name="action">Action to run on the UI thread</param>
        /// <returns>Task which completes when the action has been run</returns>
        public static Task OnUIThreadAsync(Action action)
        {
            if (Dispatcher.IsCurrent)
            {
                action();
                return Task.FromResult(false);
            }
            else
            {
                return PostOnUIThreadInternalAsync(action);
            }
        }

        private static Task PostOnUIThreadInternalAsync(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            Dispatcher.Post(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });
            return tcs.Task;
        }

        /// <summary>
        /// Determing if we're currently running in design mode
        /// </summary>
        public static bool InDesignMode
        {
            get
            {
                if (inDesignMode == null)
                {
                    var descriptor = DependencyPropertyDescriptor.FromProperty(DesignerProperties.IsInDesignModeProperty, typeof(FrameworkElement));
                    inDesignMode = (bool)descriptor.Metadata.DefaultValue;
                }

                return inDesignMode.Value;
            }
        }
    }
}
