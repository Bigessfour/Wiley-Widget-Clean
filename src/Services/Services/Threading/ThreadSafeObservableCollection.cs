using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Thread-safe observable collection that marshals collection changes to the UI thread.
/// Based on Microsoft WPF threading best practices for collection binding.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public class ThreadSafeObservableCollection<T> : ObservableCollection<T>
{
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the ThreadSafeObservableCollection class.
    /// </summary>
    public ThreadSafeObservableCollection()
        : this(Dispatcher.CurrentDispatcher)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ThreadSafeObservableCollection class with a specific dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for UI thread marshaling.</param>
    public ThreadSafeObservableCollection(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Initializes a new instance of the ThreadSafeObservableCollection class with initial items.
    /// </summary>
    /// <param name="collection">The initial collection of items.</param>
    public ThreadSafeObservableCollection(IEnumerable<T> collection)
        : this(Dispatcher.CurrentDispatcher, collection)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ThreadSafeObservableCollection class with initial items and a specific dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use for UI thread marshaling.</param>
    /// <param name="collection">The initial collection of items.</param>
    public ThreadSafeObservableCollection(Dispatcher dispatcher, IEnumerable<T> collection)
        : base(collection)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Adds an item to the collection asynchronously, ensuring UI thread safety.
    /// </summary>
    /// <param name="item">The item to add to the collection.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddAsync(T item)
    {
        if (_dispatcher.CheckAccess())
        {
            Add(item);
        }
        else
        {
            await _dispatcher.InvokeAsync(() => Add(item));
        }
    }

    /// <summary>
    /// Adds a range of items to the collection asynchronously, ensuring UI thread safety.
    /// </summary>
    /// <param name="items">The items to add to the collection.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddRangeAsync(IEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        if (_dispatcher.CheckAccess())
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }
        else
        {
            await _dispatcher.InvokeAsync(() =>
            {
                foreach (var item in items)
                {
                    Add(item);
                }
            });
        }
    }

    /// <summary>
    /// Replaces all items in the collection asynchronously, ensuring UI thread safety.
    /// </summary>
    /// <param name="items">The new items to replace the collection contents.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReplaceAllAsync(IEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        if (_dispatcher.CheckAccess())
        {
            Clear();
            foreach (var item in items)
            {
                Add(item);
            }
        }
        else
        {
            await _dispatcher.InvokeAsync(() =>
            {
                Clear();
                foreach (var item in items)
                {
                    Add(item);
                }
            });
        }
    }

    /// <summary>
    /// Clears the collection asynchronously, ensuring UI thread safety.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ClearAsync()
    {
        if (_dispatcher.CheckAccess())
        {
            Clear();
        }
        else
        {
            await _dispatcher.InvokeAsync(() => Clear());
        }
    }

    /// <summary>
    /// Removes an item from the collection asynchronously, ensuring UI thread safety.
    /// </summary>
    /// <param name="item">The item to remove from the collection.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveAsync(T item)
    {
        if (_dispatcher.CheckAccess())
        {
            Remove(item);
        }
        else
        {
            await _dispatcher.InvokeAsync(() => Remove(item));
        }
    }

    /// <summary>
    /// Inserts an item at the specified index asynchronously, ensuring UI thread safety.
    /// </summary>
    /// <param name="index">The zero-based index at which item should be inserted.</param>
    /// <param name="item">The item to insert into the collection.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertAsync(int index, T item)
    {
        if (_dispatcher.CheckAccess())
        {
            Insert(index, item);
        }
        else
        {
            await _dispatcher.InvokeAsync(() => Insert(index, item));
        }
    }

    /// <summary>
    /// Moves an item within the collection asynchronously, ensuring UI thread safety.
    /// </summary>
    /// <param name="oldIndex">The zero-based index specifying the location of the item to be moved.</param>
    /// <param name="newIndex">The zero-based index specifying the new location of the item.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task MoveAsync(int oldIndex, int newIndex)
    {
        if (_dispatcher.CheckAccess())
        {
            Move(oldIndex, newIndex);
        }
        else
        {
            await _dispatcher.InvokeAsync(() => Move(oldIndex, newIndex));
        }
    }

    /// <summary>
    /// Raises the PropertyChanged event on the UI thread.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_dispatcher.CheckAccess())
        {
            base.OnPropertyChanged(e);
        }
        else
        {
            _dispatcher.InvokeAsync(() => base.OnPropertyChanged(e), DispatcherPriority.DataBind);
        }
    }

    /// <summary>
    /// Raises the CollectionChanged event on the UI thread.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_dispatcher.CheckAccess())
        {
            base.OnCollectionChanged(e);
        }
        else
        {
            _dispatcher.InvokeAsync(() => base.OnCollectionChanged(e), DispatcherPriority.DataBind);
        }
    }
}