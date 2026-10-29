using System;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Layout
{
    public class Grid : Panel
    {
        public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
            "Row", typeof(int), typeof(Grid),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));
        public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached(
            "Column", typeof(int), typeof(Grid),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        private Track[] rows = Array.Empty<Track>();
        private Track[] columns = Array.Empty<Track>();

        public Grid()
        {
            RowDefinitions = new RowDefinitionCollection(this);
            ColumnDefinitions = new ColumnDefinitionCollection(this);
        }

        public RowDefinitionCollection RowDefinitions { get; }
        public ColumnDefinitionCollection ColumnDefinitions { get; }

        public static int GetRow(UIElement element) =>
            (int)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(RowProperty);
        public static void SetRow(UIElement element, int value) =>
            (element ?? throw new ArgumentNullException(nameof(element))).SetValue(RowProperty, value);
        public static int GetColumn(UIElement element) =>
            (int)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(ColumnProperty);
        public static void SetColumn(UIElement element, int value) =>
            (element ?? throw new ArgumentNullException(nameof(element))).SetValue(ColumnProperty, value);

        protected override Size MeasureOverride(Size availableSize)
        {
            if (RowDefinitions.Count == 0 && ColumnDefinitions.Count == 0)
            {
                double width = 0;
                double height = 0;
                foreach (UIElement child in Children)
                {
                    child.Measure(availableSize);
                    width = Math.Max(width, child.DesiredSize.Width);
                    height = Math.Max(height, child.DesiredSize.Height);
                }
                return new Size(width, height);
            }

            int rowCount = Math.Max(1, RowDefinitions.Count);
            int columnCount = Math.Max(1, ColumnDefinitions.Count);
            if (rows.Length != rowCount)
                rows = new Track[rowCount];
            if (columns.Length != columnCount)
                columns = new Track[columnCount];
            for (int i = 0; i < rowCount; i++)
                rows[i] = new Track(RowDefinitions.Count == 0 ? new GridLength(1, GridUnitType.Star)
                    : RowDefinitions[i].Height, double.IsPositiveInfinity(availableSize.Height));
            for (int i = 0; i < columnCount; i++)
                columns[i] = new Track(ColumnDefinitions.Count == 0 ? new GridLength(1, GridUnitType.Star)
                    : ColumnDefinitions[i].Width, double.IsPositiveInfinity(availableSize.Width));

            MeasureGroup(1);
            ResolveStar(rows, availableSize.Height);
            MeasureGroup(2);
            ResolveStar(columns, availableSize.Width);
            MeasureGroup(3);
            MeasureGroup(4);

            double desiredWidth = 0;
            double desiredHeight = 0;
            for (int i = 0; i < columnCount; i++)
                desiredWidth += columns[i].MinSize;
            for (int i = 0; i < rowCount; i++)
                desiredHeight += rows[i].MinSize;
            return new Size(desiredWidth, desiredHeight);
        }

        private void MeasureGroup(int group)
        {
            foreach (UIElement child in Children)
            {
                int row = ClampIndex(GetRow(child), rows.Length);
                int column = ClampIndex(GetColumn(child), columns.Length);
                GridUnitType rowType = rows[row].MeasureType;
                GridUnitType columnType = columns[column].MeasureType;
                int childGroup = rowType == GridUnitType.Star
                    ? (columnType == GridUnitType.Auto ? 2 : 4)
                    : (columnType == GridUnitType.Star ? 3 : 1);
                if (childGroup != group)
                    continue;
                child.Measure(new Size(columns[column].Size, rows[row].Size));
                columns[column].MinSize = Math.Max(columns[column].MinSize, child.DesiredSize.Width);
                rows[row].MinSize = Math.Max(rows[row].MinSize, child.DesiredSize.Height);
            }
        }

        private static void ResolveStar(Track[] tracks, double available)
        {
            double occupied = 0;
            double maxWeight = 0;
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i].MeasureType == GridUnitType.Star)
                    maxWeight = Math.Max(maxWeight, tracks[i].Length.Value);
                else
                    occupied += tracks[i].Length.IsAbsolute ? tracks[i].Length.Value : tracks[i].MinSize;
            }
            if (maxWeight == 0)
                return;
            double weight = 0;
            for (int i = 0; i < tracks.Length; i++)
                if (tracks[i].MeasureType == GridUnitType.Star)
                    weight += tracks[i].Length.Value / maxWeight;
            double remaining = Math.Max(0, available - occupied);
            for (int i = 0; i < tracks.Length; i++)
                if (tracks[i].MeasureType == GridUnitType.Star)
                    tracks[i].Size = remaining * ((tracks[i].Length.Value / maxWeight) / weight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (RowDefinitions.Count == 0 && ColumnDefinitions.Count == 0)
            {
                foreach (UIElement child in Children)
                    child.Arrange(new Rect(new Point(0, 0), finalSize));
                return finalSize;
            }

            ArrangeTracks(rows, finalSize.Height);
            ArrangeTracks(columns, finalSize.Width);
            for (int i = 0; i < RowDefinitions.Count; i++)
                RowDefinitions[i].ActualHeight = rows[i].Size;
            for (int i = 0; i < ColumnDefinitions.Count; i++)
                ColumnDefinitions[i].ActualWidth = columns[i].Size;
            foreach (UIElement child in Children)
            {
                Track row = rows[ClampIndex(GetRow(child), rows.Length)];
                Track column = columns[ClampIndex(GetColumn(child), columns.Length)];
                child.Arrange(new Rect(column.Offset, row.Offset, column.Size, row.Size));
            }
            return finalSize;
        }

        private static void ArrangeTracks(Track[] tracks, double finalSize)
        {
            double remaining = finalSize;
            for (int i = 0; i < tracks.Length; i++)
            {
                tracks[i].Resolved = !tracks[i].Length.IsStar || tracks[i].Length.Value == 0;
                tracks[i].Size = tracks[i].MinSize;
                if (tracks[i].Resolved)
                    remaining -= tracks[i].Size;
            }

            // Freeze stars at their content minimum, then redistribute the remaining space.
            while (true)
            {
                double maxWeight = 0;
                for (int i = 0; i < tracks.Length; i++)
                    if (!tracks[i].Resolved)
                        maxWeight = Math.Max(maxWeight, tracks[i].Length.Value);
                if (maxWeight == 0)
                    break;
                double weight = 0;
                for (int i = 0; i < tracks.Length; i++)
                    if (!tracks[i].Resolved)
                        weight += tracks[i].Length.Value / maxWeight;
                double space = Math.Max(0, remaining);
                bool clamped = false;
                for (int i = 0; i < tracks.Length; i++)
                {
                    if (tracks[i].Resolved)
                        continue;
                    double share = space * ((tracks[i].Length.Value / maxWeight) / weight);
                    if (share < tracks[i].MinSize)
                    {
                        tracks[i].Size = tracks[i].MinSize;
                        tracks[i].Resolved = true;
                        remaining -= tracks[i].Size;
                        clamped = true;
                    }
                    else
                        tracks[i].Size = share;
                }
                if (!clamped)
                    break;
            }

            double offset = 0;
            for (int i = 0; i < tracks.Length; i++)
            {
                tracks[i].Offset = offset;
                offset += tracks[i].Size;
            }
        }

        private static int ClampIndex(int index, int count) => Math.Max(0, Math.Min(index, count - 1));

        private struct Track
        {
            internal GridLength Length;
            internal GridUnitType MeasureType;
            internal double MinSize;
            internal double Size;
            internal double Offset;
            internal bool Resolved;

            internal Track(GridLength length, bool infinite)
            {
                Length = length;
                MeasureType = infinite && length.IsStar ? GridUnitType.Auto : length.GridUnitType;
                MinSize = length.IsAbsolute ? length.Value : 0;
                Size = MeasureType == GridUnitType.Auto ? double.PositiveInfinity : MinSize;
                Offset = 0;
                Resolved = false;
            }
        }
    }
}
