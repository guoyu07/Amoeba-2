﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Amoeba.Windows
{
    sealed class SearchTreeViewModel : TreeViewModelBase
    {
        private bool _isSelected;
        private bool _isHit;
        private int _count;
        private SearchTreeItem _value;

        private ReadOnlyObservableCollection<TreeViewModelBase> _readonlyChildren;
        private ObservableCollectionEx<TreeViewModelBase> _children;

        public SearchTreeViewModel(TreeViewModelBase parent, SearchTreeItem value)
            : base(parent)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            _children = new ObservableCollectionEx<TreeViewModelBase>();
            _readonlyChildren = new ReadOnlyObservableCollection<TreeViewModelBase>(_children);

            this.Value = value;
        }

        public void Update()
        {
            this.NotifyPropertyChanged(nameof(this.Name));
            this.NotifyPropertyChanged(nameof(this.IsExpanded));

            foreach (var item in _children.OfType<SearchTreeViewModel>().ToArray())
            {
                if (!_value.Children.Any(n => object.ReferenceEquals(n, item.Value)))
                {
                    _children.Remove(item);
                }
            }

            foreach (var item in _value.Children)
            {
                if (!_children.OfType<SearchTreeViewModel>().Any(n => object.ReferenceEquals(n.Value, item)))
                {
                    _children.Add(new SearchTreeViewModel(this, item));
                }
            }

            this.Sort();
        }

        private void Sort()
        {
            var list = _children.OfType<SearchTreeViewModel>().ToList();

            list.Sort((x, y) =>
            {
                int c = x.Value.SearchItem.Name.CompareTo(y.Value.SearchItem.Name);
                if (c != 0) return c;
                c = x.Count.CompareTo(y.Count);
                if (c != 0) return c;

                return x.GetHashCode().CompareTo(y.GetHashCode());
            });

            for (int i = 0; i < list.Count; i++)
            {
                var o = _children.IndexOf(list[i]);

                if (i != o) _children.Move(o, i);
            }
        }

        public override string Name
        {
            get
            {
                return string.Format("{0} ({1})", _value.SearchItem.Name, _count);
            }
        }

        public override bool IsSelected
        {
            get
            {
                return _isSelected;
            }
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;

                    this.NotifyPropertyChanged(nameof(this.IsSelected));
                }
            }
        }

        public bool IsExpanded
        {
            get
            {
                return _value.IsExpanded;
            }
            set
            {
                if (value != _value.IsExpanded)
                {
                    _value.IsExpanded = value;

                    this.NotifyPropertyChanged(nameof(this.IsExpanded));
                }
            }
        }

        public bool IsHit
        {
            get
            {
                return _isHit;
            }
            set
            {
                if (value != _isHit)
                {
                    _isHit = value;

                    this.NotifyPropertyChanged(nameof(this.IsHit));
                }
            }
        }

        public int Count
        {
            get
            {
                return _count;
            }
            set
            {
                if (value != _count)
                {
                    _count = value;

                    this.NotifyPropertyChanged(nameof(this.Name));
                    this.NotifyPropertyChanged(nameof(this.Count));
                }
            }
        }

        public override IReadOnlyCollection<TreeViewModelBase> Children
        {
            get
            {
                return _readonlyChildren;
            }
        }

        public SearchTreeItem Value
        {
            get
            {
                return _value;
            }
            private set
            {
                _value = value;

                this.Update();
            }
        }
    }
}
