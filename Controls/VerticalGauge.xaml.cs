using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SystemMonitorApp.Controls
{
    public partial class VerticalGauge : UserControl
    {
        // Dependency Properties
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(VerticalGauge),
                new PropertyMetadata(0.0, OnValueChanged));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register("MinValue", typeof(double), typeof(VerticalGauge),
                new PropertyMetadata(0.0, OnRangeChanged));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register("MaxValue", typeof(double), typeof(VerticalGauge),
                new PropertyMetadata(100.0, OnRangeChanged));

        public static readonly DependencyProperty GaugeTitleProperty =
            DependencyProperty.Register("GaugeTitle", typeof(string), typeof(VerticalGauge),
                new PropertyMetadata("Gauge"));

        public static readonly DependencyProperty UnitsProperty =
            DependencyProperty.Register("Units", typeof(string), typeof(VerticalGauge),
                new PropertyMetadata("", OnUnitsChanged));

        private static readonly DependencyPropertyKey FormattedValuePropertyKey =
            DependencyProperty.RegisterReadOnly("FormattedValue", typeof(string), typeof(VerticalGauge),
                new PropertyMetadata("0"));

        public static readonly DependencyProperty FormattedValueProperty = FormattedValuePropertyKey.DependencyProperty;

        // Properties
        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public string GaugeTitle
        {
            get => (string)GetValue(GaugeTitleProperty);
            set => SetValue(GaugeTitleProperty, value);
        }

        public string Units
        {
            get => (string)GetValue(UnitsProperty);
            set => SetValue(UnitsProperty, value);
        }

        public string FormattedValue
        {
            get => (string)GetValue(FormattedValueProperty);
            private set => SetValue(FormattedValuePropertyKey, value);
        }

        public VerticalGauge()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            Loaded += (s, e) => UpdateGauge();
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gauge = (VerticalGauge)d;
            gauge.UpdateFillBar();
            gauge.UpdateFormattedValue();
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gauge = (VerticalGauge)d;
            gauge.DrawTickMarks();
            gauge.UpdateFillBar();
            gauge.UpdateFormattedValue();
        }

        private static void OnUnitsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((VerticalGauge)d).UpdateFormattedValue();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.HeightChanged || e.WidthChanged)
            {
                DrawTickMarks();
                UpdateFillBar();
            }
        }

        private void UpdateFormattedValue()
        {
            FormattedValue = $"{Value:0.##} {Units}";
        }

        private void UpdateGauge()
        {
            DrawTickMarks();
            UpdateFillBar();
            UpdateFormattedValue();
        }

        private void UpdateFillBar()
        {
            if (GaugeTrack == null || FillBar == null) return;

            double range = MaxValue - MinValue;
            double percentage = range > 0 ? (Value - MinValue) / range : 0;
            percentage = Math.Clamp(percentage, 0, 1);

            double newHeight = GaugeTrack.ActualHeight * percentage;

            var animation = new DoubleAnimation(
                FillBar.Height,
                newHeight,
                new Duration(TimeSpan.FromMilliseconds(300)),
                FillBehavior.HoldEnd);

            animation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            FillBar.BeginAnimation(HeightProperty, animation);
        }

        private void DrawTickMarks()
        {
            if (TickCanvas == null) return;

            TickCanvas.Children.Clear();
            double canvasHeight = TickCanvas.ActualHeight;
            double canvasWidth = TickCanvas.ActualWidth;

            if (canvasHeight <= 0 || canvasWidth <= 0) return;

            int tickCount = 5; // Number of major ticks
            double range = MaxValue - MinValue;

            for (int i = 0; i <= tickCount; i++)
            {
                double ratio = (double)i / tickCount;
                double value = MinValue + (range * ratio);
                double yPos = canvasHeight - (canvasHeight * ratio);

                // Draw tick line
                var line = new Line
                {
                    X1 = 0,
                    X2 = 10,
                    Y1 = yPos,
                    Y2 = yPos,
                    Stroke = Brushes.White,
                    StrokeThickness = i == 0 || i == tickCount ? 2 : 1
                };

                // Draw value text
                var textBlock = new TextBlock
                {
                    Text = value.ToString("0"),
                    Foreground = Brushes.White,
                    FontSize = 9,
                    Margin = new Thickness(12, yPos - 8, 0, 0)
                };

                TickCanvas.Children.Add(line);
                TickCanvas.Children.Add(textBlock);
            }
        }
    }
}