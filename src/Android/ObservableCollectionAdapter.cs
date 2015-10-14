﻿namespace Nine.Application
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using Android.App;
    using Android.Database;
    using Android.Views;
    using Android.Widget;

    public class ObservableCollectionAdapter<T> : BaseAdapter<T> where T : class
    {
        struct Entry { public T Data; public bool Dirty; }

        private readonly IReadOnlyList<T> _items;
        private readonly int _resource;
        private readonly INotifyCollectionChanged _incc;

        private readonly Dictionary<View, Entry> _initializedViews = new Dictionary<View, Entry>();

        private readonly Activity _context;

        private readonly Action<View> _newView;
        private readonly Action<View, T> _prepareView;

        private int _observeCount;

        public ObservableCollectionAdapter(Activity context, int resource, IReadOnlyList<T> items, Action<View> newView, Action<View, T> prepareView)
        {
            _context = context;
            _resource = resource;
            _items = items;
            _prepareView = prepareView;
            _newView = newView;
            _incc = items as INotifyCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => NotifyDataSetChanged();

        private void OnItemChanged(object sender, EventArgs e) => NotifyDataSetChanged();

        public override T this[int position] => _items[position];

        public override int Count => _items.Count;

        public override long GetItemId(int position) => 0;

        public override void RegisterDataSetObserver(DataSetObserver observer)
        {
            if (_observeCount == 0 && _incc != null) _incc.CollectionChanged += OnCollectionChanged;

            _observeCount++;

            base.RegisterDataSetObserver(observer);
        }

        public override void UnregisterDataSetObserver(DataSetObserver observer)
        {
            _observeCount--;

            if (_observeCount == 0)
            {
                if (_incc != null) _incc.CollectionChanged -= OnCollectionChanged;

                foreach (var entry in _initializedViews.Values)
                {
                    var inpc = entry.Data as INotifyPropertyChanged;
                    if (inpc != null) inpc.PropertyChanged -= OnItemChanged;
                }

                _initializedViews.Clear();
            }

            base.UnregisterDataSetObserver(observer);
        }

        public override View GetView(int position, View view, ViewGroup parent)
        {
            Entry entry;

            var item = this[position];
            
            if (view == null || !_initializedViews.TryGetValue(view, out entry))
            {
                // Initialize a new view
                view = _context.LayoutInflater.Inflate(_resource, parent, false);

                _newView?.Invoke(view);

                view.SetDataContext(item);

                _prepareView?.Invoke(view, item);

                _initializedViews.Add(view, new Entry { Data = item });

                var inpc = item as INotifyPropertyChanged;
                if (inpc != null) inpc.PropertyChanged += OnItemChanged;

                return view;
            }

            if (ReferenceEquals(entry.Data, item))
            {
                // Update existing view if the item has changed
                if (entry.Dirty)
                {
                    _prepareView?.Invoke(view, item);
                }
                return view;
            }
            else
            {
                // Data context has changed
                var inpc = entry.Data as INotifyPropertyChanged;
                if (inpc != null) inpc.PropertyChanged -= OnItemChanged;

                inpc = item as INotifyPropertyChanged;
                if (inpc != null) inpc.PropertyChanged += OnItemChanged;

                _initializedViews[view] = new Entry { Data = item };

                view.SetDataContext(item);

                _prepareView?.Invoke(view, item);

                return view;
            }
        }
    }
}
