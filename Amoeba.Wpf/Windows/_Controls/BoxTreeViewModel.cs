﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Library;
using Library.Net.Amoeba;

namespace Amoeba.Windows
{
    sealed class BoxTreeViewModel : TreeViewModelBase
    {
        private bool _isSelected;
        private bool _isExpanded;
        private Box _value;

        private ReadOnlyObservableCollection<TreeViewModelBase> _readonlyChildren;
        private ObservableCollectionEx<TreeViewModelBase> _children;

        public BoxTreeViewModel(TreeViewModelBase parent, Box value)
            : base(parent)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            _children = new ObservableCollectionEx<TreeViewModelBase>();
            _readonlyChildren = new ReadOnlyObservableCollection<TreeViewModelBase>(_children);

            this.Value = value;
        }

        private static int GetTotalSeedCount(Box box)
        {
            var boxList = new List<Box>();
            var seedList = new List<Seed>();

            boxList.Add(box);

            for (int i = 0; i < boxList.Count; i++)
            {
                boxList.AddRange(boxList[i].Boxes);
                seedList.AddRange(boxList[i].Seeds);
            }

            return seedList.Count;
        }

        public void Update_Header()
        {
            this.NotifyPropertyChanged(nameof(this.Name));

            if (this.Parent is BoxTreeViewModel)
            {
                var parentBoxTreeViewModel = (BoxTreeViewModel)this.Parent;

                parentBoxTreeViewModel.Update_Header();
            }
        }

        public void Update()
        {
            this.Update_Header();

            foreach (var item in _children.OfType<BoxTreeViewModel>().ToArray())
            {
                if (!_value.Boxes.Any(n => object.ReferenceEquals(n, item.Value)))
                {
                    _children.Remove(item);
                }
            }

            foreach (var item in _value.Boxes)
            {
                if (!_children.OfType<BoxTreeViewModel>().Any(n => object.ReferenceEquals(n.Value, item)))
                {
                    _children.Add(new BoxTreeViewModel(this, item));
                }
            }

            this.Sort();
        }

        private void Sort()
        {
            var list = _children.OfType<BoxTreeViewModel>().ToList();

            list.Sort((x, y) =>
            {
                int c = x.Value.Name.CompareTo(y.Value.Name);
                if (c != 0) return c;
                c = y.Value.Seeds.Count.CompareTo(x.Value.Seeds.Count);
                if (c != 0) return c;
                c = y.Value.Boxes.Count.CompareTo(x.Value.Boxes.Count);
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
                return string.Format("{0} ({1})", this.Value.Name, BoxTreeViewModel.GetTotalSeedCount(this.Value));
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
                return _isExpanded;
            }
            set
            {
                if (value != _isExpanded)
                {
                    _isExpanded = value;

                    this.NotifyPropertyChanged(nameof(this.IsExpanded));
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

        public Box Value
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
