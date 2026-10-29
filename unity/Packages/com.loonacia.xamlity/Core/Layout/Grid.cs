using System;
using System.Collections.Generic;
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
        public static readonly DependencyProperty RowSpanProperty = DependencyProperty.RegisterAttached(
            "RowSpan", typeof(int), typeof(Grid),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure));
        public static readonly DependencyProperty ColumnSpanProperty = DependencyProperty.RegisterAttached(
            "ColumnSpan", typeof(int), typeof(Grid),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure));

        private Track[] rows = Array.Empty<Track>();
        private Track[] columns = Array.Empty<Track>();
        private readonly List<SpanRequest> spans = new List<SpanRequest>();
        private int[] distributionOrder = Array.Empty<int>();

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
        public static int GetRowSpan(UIElement element) =>
            (int)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(RowSpanProperty);
        public static void SetRowSpan(UIElement element, int value) =>
            (element ?? throw new ArgumentNullException(nameof(element))).SetValue(RowSpanProperty, value);
        public static int GetColumnSpan(UIElement element) =>
            (int)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(ColumnSpanProperty);
        public static void SetColumnSpan(UIElement element, int value) =>
            (element ?? throw new ArgumentNullException(nameof(element))).SetValue(ColumnSpanProperty, value);

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
                    : RowDefinitions[i].Height, double.IsPositiveInfinity(availableSize.Height),
                    RowDefinitions.Count == 0 ? 0 : RowDefinitions[i].MinHeight,
                    RowDefinitions.Count == 0 ? double.PositiveInfinity : RowDefinitions[i].MaxHeight);
            for (int i = 0; i < columnCount; i++)
                columns[i] = new Track(ColumnDefinitions.Count == 0 ? new GridLength(1, GridUnitType.Star)
                    : ColumnDefinitions[i].Width, double.IsPositiveInfinity(availableSize.Width),
                    ColumnDefinitions.Count == 0 ? 0 : ColumnDefinitions[i].MinWidth,
                    ColumnDefinitions.Count == 0 ? double.PositiveInfinity : ColumnDefinitions[i].MaxWidth);

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
            spans.Clear();
            foreach (UIElement child in Children)
            {
                int row = ClampIndex(GetRow(child), rows.Length);
                int column = ClampIndex(GetColumn(child), columns.Length);
                int rowSpan = ClampSpan(GetRowSpan(child), row, rows.Length);
                int columnSpan = ClampSpan(GetColumnSpan(child), column, columns.Length);
                GridUnitType rowType = GetRangeType(rows, row, rowSpan);
                GridUnitType columnType = GetRangeType(columns, column, columnSpan);
                int childGroup = rowType == GridUnitType.Star
                    ? (columnType == GridUnitType.Auto ? 2 : 4)
                    : (columnType == GridUnitType.Star ? 3 : 1);
                if (childGroup != group)
                    continue;
                child.Measure(new Size(GetMeasureSize(columns, column, columnSpan, columnType),
                    GetMeasureSize(rows, row, rowSpan, rowType)));
                RegisterDesiredSize(columns, column, columnSpan, child.DesiredSize.Width);
                RegisterDesiredSize(rows, row, rowSpan, child.DesiredSize.Height);
            }
            spans.Sort((a, b) => a.Count.CompareTo(b.Count));
            foreach (SpanRequest span in spans)
                EnsureMinSizeInRange(span.Tracks, span.Start, span.Count, span.Size);
        }

        private static GridUnitType GetRangeType(Track[] tracks, int start, int count)
        {
            GridUnitType type = GridUnitType.Pixel;
            for (int i = start; i < start + count; i++)
            {
                if (tracks[i].MeasureType == GridUnitType.Star)
                    return GridUnitType.Star;
                if (tracks[i].MeasureType == GridUnitType.Auto)
                    type = GridUnitType.Auto;
            }
            return type;
        }

        private static double GetMeasureSize(Track[] tracks, int start, int count, GridUnitType type)
        {
            if (type == GridUnitType.Auto)
                return double.PositiveInfinity;
            double size = 0;
            for (int i = start; i < start + count; i++)
                size += tracks[i].MeasureType == GridUnitType.Auto ? tracks[i].MinSize : tracks[i].Size;
            return size;
        }

        private void RegisterDesiredSize(Track[] tracks, int start, int count, double size)
        {
            if (count == 1)
            {
                tracks[start].MinSize = Math.Max(tracks[start].MinSize, Math.Min(size, tracks[start].MaxSize));
                return;
            }
            for (int i = 0; i < spans.Count; i++)
            {
                SpanRequest span = spans[i];
                if (span.Tracks == tracks && span.Start == start && span.Count == count)
                {
                    span.Size = Math.Max(span.Size, size);
                    spans[i] = span;
                    return;
                }
            }
            spans.Add(new SpanRequest { Tracks = tracks, Start = start, Count = count, Size = size });
        }

        private void EnsureMinSizeInRange(Track[] tracks, int start, int count, double requested)
        {
            double minimum = 0, preferred = 0, maximum = 0, maxMaxSize = 0;
            int autoCount = 0;
            for (int i = start; i < start + count; i++)
            {
                minimum += tracks[i].MinSize;
                preferred += tracks[i].PreferredSize;
                maximum += tracks[i].EffectiveMax;
                maxMaxSize = Math.Max(maxMaxSize, tracks[i].EffectiveMax);
                if (tracks[i].Length.IsAuto)
                    autoCount++;
            }
            if (requested <= minimum)
                return;
            if (requested > maximum)
            {
                double equal = requested / count;
                for (int i = start; i < start + count; i++)
                {
                    double max = tracks[i].EffectiveMax;
                    tracks[i].MinSize = equal < maxMaxSize
                        ? max + (maxMaxSize - max) * ((requested - maximum) / (maxMaxSize * count - maximum))
                        : equal;
                }
                return;
            }

            bool usePreferred = requested <= preferred;
            if (distributionOrder.Length < count)
                distributionOrder = new int[count];
            // Sort indices without moving the tracks themselves: Auto first for preferred,
            // Auto last for max, then ascending by the applicable size limit.
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                int j = i;
                while (j > 0 && CompareDistribution(tracks[index], tracks[distributionOrder[j - 1]], usePreferred) < 0)
                {
                    distributionOrder[j] = distributionOrder[j - 1];
                    j--;
                }
                distributionOrder[j] = index;
            }
            double remaining = usePreferred ? requested : requested - preferred;
            for (int i = 0; i < count; i++)
            {
                ref Track track = ref tracks[distributionOrder[i]];
                if (usePreferred)
                {
                    double size = track.Length.IsAuto ? track.MinSize
                        : Math.Min(remaining / (count - i), track.PreferredSize);
                    track.MinSize = Math.Max(track.MinSize, size);
                    remaining -= size;
                }
                else
                {
                    double size = track.PreferredSize;
                    int remainingCount = track.Length.IsAuto ? count - i : count - autoCount - i;
                    track.MinSize = Math.Min(size + remaining / remainingCount, track.EffectiveMax);
                    remaining -= track.MinSize - size;
                }
            }
        }

        private static int CompareDistribution(Track a, Track b, bool preferred)
        {
            if (a.Length.IsAuto != b.Length.IsAuto)
                return a.Length.IsAuto == preferred ? -1 : 1;
            return preferred ? a.PreferredSize.CompareTo(b.PreferredSize) : a.EffectiveMax.CompareTo(b.EffectiveMax);
        }

        private static void ResolveStar(Track[] tracks, double available)
        {
            DistributeStars(tracks, available, false);
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
                int row = ClampIndex(GetRow(child), rows.Length);
                int column = ClampIndex(GetColumn(child), columns.Length);
                int rowSpan = ClampSpan(GetRowSpan(child), row, rows.Length);
                int columnSpan = ClampSpan(GetColumnSpan(child), column, columns.Length);
                child.Arrange(new Rect(columns[column].Offset, rows[row].Offset,
                    GetFinalSize(columns, column, columnSpan), GetFinalSize(rows, row, rowSpan)));
            }
            return finalSize;
        }

        private static void ArrangeTracks(Track[] tracks, double finalSize)
        {
            DistributeStars(tracks, finalSize, true);
            double offset = 0;
            for (int i = 0; i < tracks.Length; i++)
            {
                tracks[i].Offset = offset;
                offset += tracks[i].Size;
            }
        }

        private static void DistributeStars(Track[] tracks, double available, bool arrange)
        {
            double occupied = 0;
            for (int i = 0; i < tracks.Length; i++)
            {
                bool star = arrange ? tracks[i].Length.IsStar : tracks[i].MeasureType == GridUnitType.Star;
                tracks[i].Resolved = star ? 0 : 2;
                if (arrange || star)
                    tracks[i].Size = tracks[i].MinSize;
                if (tracks[i].Resolved == 2)
                    occupied += arrange || tracks[i].MeasureType == GridUnitType.Auto
                        ? tracks[i].MinSize : tracks[i].Size;
            }

            // Resolve the largest proportional discrepancy first. Reconsider the opposite
            // bound if all stars are fixed with spare space, or minima exhaust the space.
            while (true)
            {
                double remaining = available - occupied;
                double maxWeight = 0;
                int pending = 0;
                for (int i = 0; i < tracks.Length; i++)
                {
                    if (tracks[i].Resolved == 0)
                    {
                        maxWeight = Math.Max(maxWeight, tracks[i].Length.Value);
                        pending++;
                    }
                    else if (tracks[i].Resolved != 2)
                        remaining -= tracks[i].Size;
                }
                if (remaining < 0 || (pending == 0 && remaining > 0))
                {
                    int reconsider = remaining < 0 ? 1 : -1;
                    bool reopened = false;
                    for (int i = 0; i < tracks.Length; i++)
                        if (tracks[i].Resolved == reconsider)
                        {
                            tracks[i].Resolved = 0;
                            reopened = true;
                        }
                    if (reopened)
                    {
                        // Keep bounds from the completed phase fixed. Only the opposite
                        // bounds are candidates in the next phase, guaranteeing progress.
                        for (int i = 0; i < tracks.Length; i++)
                            if (tracks[i].Resolved == -reconsider)
                            {
                                occupied += tracks[i].Size;
                                tracks[i].Resolved = 2;
                            }
                        continue;
                    }
                }
                if (pending == 0)
                    return;
                if (maxWeight == 0)
                    maxWeight = 1;
                double weight = 0;
                double minRatio = double.PositiveInfinity, maxRatio = -1;
                int minIndex = -1, maxIndex = -1;
                for (int i = 0; i < tracks.Length; i++)
                {
                    if (tracks[i].Resolved == 0)
                    {
                        double scaled = tracks[i].Length.Value / maxWeight;
                        weight += scaled;
                        if (tracks[i].MinSize > 0 && scaled / tracks[i].MinSize <= minRatio)
                        {
                            minRatio = scaled / tracks[i].MinSize;
                            minIndex = i;
                        }
                        if (!double.IsPositiveInfinity(tracks[i].EffectiveMax) && scaled / tracks[i].EffectiveMax >= maxRatio)
                        {
                            maxRatio = scaled / tracks[i].EffectiveMax;
                            maxIndex = i;
                        }
                    }
                }
                double proportion = weight / remaining;
                bool violatesMin = remaining > 0 && minRatio < proportion;
                bool violatesMax = remaining > 0 && maxRatio > proportion;
                if (!violatesMin && !violatesMax)
                {
                    // Finish in decreasing weight order, as WPF does, to retain precision
                    // and apply remaining minima when the available space is exhausted.
                    while (pending-- > 0)
                    {
                        int index = -1;
                        for (int i = 0; i < tracks.Length; i++)
                            if (tracks[i].Resolved == 0 && (index < 0 || tracks[i].Length.Value > tracks[index].Length.Value))
                                index = i;
                        double scaled = tracks[index].Length.Value / maxWeight;
                        double share = scaled > 0 ? Math.Max(0, remaining) * (scaled / weight) : 0;
                        tracks[index].Size = Math.Max(tracks[index].MinSize, Math.Min(share, tracks[index].MaxSize));
                        tracks[index].Resolved = 2;
                        remaining -= tracks[index].Size;
                        weight = 0;
                        for (int i = 0; i < tracks.Length; i++)
                            if (tracks[i].Resolved == 0)
                                weight += tracks[i].Length.Value / maxWeight;
                    }
                    return;
                }
                bool chooseMin = violatesMin;
                if (violatesMin && violatesMax)
                {
                    // Compare squared ratios using the same power-of-two scaling as WPF.
                    double power = Math.Floor((Math.Floor(Math.Log(minRatio, 2)) + Math.Floor(Math.Log(maxRatio, 2))) / 2);
                    double scale = Math.Pow(2, power);
                    chooseMin = (proportion / scale) * (proportion / scale) > (minRatio / scale) * (maxRatio / scale);
                }
                int candidate = chooseMin ? minIndex : maxIndex;
                tracks[candidate].Size = chooseMin ? tracks[candidate].MinSize : tracks[candidate].EffectiveMax;
                tracks[candidate].Resolved = chooseMin ? -1 : 1;
            }
        }

        private static double GetFinalSize(Track[] tracks, int start, int count)
        {
            double size = 0;
            for (int i = start; i < start + count; i++)
                size += tracks[i].Size;
            return size;
        }

        private static int ClampIndex(int index, int count) => Math.Max(0, Math.Min(index, count - 1));
        private static int ClampSpan(int span, int start, int count) => Math.Max(1, Math.Min(span, count - start));

        private struct SpanRequest
        {
            internal Track[] Tracks;
            internal int Start;
            internal int Count;
            internal double Size;
        }

        private struct Track
        {
            internal GridLength Length;
            internal GridUnitType MeasureType;
            internal double MinSize;
            internal double MaxSize;
            internal double Size;
            internal double Offset;
            internal int Resolved;
            internal double EffectiveMax => Math.Max(MinSize, MaxSize);
            internal double PreferredSize => MeasureType == GridUnitType.Auto ? MinSize : Math.Max(MinSize, Size);

            internal Track(GridLength length, bool infinite, double minimum, double maximum)
            {
                if (double.IsNaN(minimum) || double.IsInfinity(minimum) || minimum < 0)
                    throw new ArgumentException("Definition minimum must be finite and non-negative.");
                if (double.IsNaN(maximum) || maximum < 0)
                    throw new ArgumentException("Definition maximum must be non-negative.");
                Length = length;
                MeasureType = infinite && length.IsStar ? GridUnitType.Auto : length.GridUnitType;
                MaxSize = maximum;
                MinSize = length.IsAbsolute ? Math.Max(minimum, Math.Min(length.Value, maximum)) : minimum;
                Size = MeasureType == GridUnitType.Auto ? double.PositiveInfinity : MinSize;
                Offset = 0;
                Resolved = 0;
            }
        }
    }
}
