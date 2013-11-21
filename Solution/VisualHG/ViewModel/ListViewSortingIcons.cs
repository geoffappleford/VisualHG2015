﻿using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace VisualHg.ViewModel
{
    public class ListViewSortingIcons : Adorner
    {
        private static Geometry ascGeometry = Geometry.Parse("M 0 4 L 3.5 0 L 7 4 Z");
        private static Geometry descGeometry = Geometry.Parse("M 0 0 L 3.5 4 L 7 0 Z");

        public ListSortDirection Direction { get; private set; }

        public ListViewSortingIcons(UIElement element, ListSortDirection dir)
            : base(element)
        {
            Direction = dir;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var size = AdornedElement.RenderSize;

            if (size.Width < 20)
            {
                return;
            }

            var transform = new TranslateTransform(size.Width - 15, (size.Height - 5) / 2);
            
            drawingContext.PushTransform(transform);

            var geometry = ascGeometry;

            if (Direction == ListSortDirection.Descending)
            {
                geometry = descGeometry;
            }

            drawingContext.DrawGeometry(Brushes.Black, null, geometry);
            drawingContext.Pop();
        }
    }
}