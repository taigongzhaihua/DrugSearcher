using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brush = System.Windows.Media.Brush;

namespace DrugSearcher.Behaviors
{
    public static class ThemeBehavior
    {
        #region Background 附加属性

        public static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.RegisterAttached(
                "Background",
                typeof(Brush),
                typeof(ThemeBehavior),
                new PropertyMetadata(null, OnBackgroundChanged));

        public static Brush GetBackground(DependencyObject obj)
        {
            return (Brush)obj.GetValue(BackgroundProperty);
        }

        public static void SetBackground(DependencyObject obj, Brush value)
        {
            obj.SetValue(BackgroundProperty, value);
        }

        #endregion

        #region Foreground 附加属性

        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.RegisterAttached(
                "Foreground",
                typeof(Brush),
                typeof(ThemeBehavior),
                new PropertyMetadata(null, OnForegroundChanged));

        public static Brush GetForeground(DependencyObject obj)
        {
            return (Brush)obj.GetValue(ForegroundProperty);
        }

        public static void SetForeground(DependencyObject obj, Brush value)
        {
            obj.SetValue(ForegroundProperty, value);
        }

        #endregion

        #region BorderBrush 附加属性

        public static readonly DependencyProperty BorderBrushProperty =
            DependencyProperty.RegisterAttached(
                "BorderBrush",
                typeof(Brush),
                typeof(ThemeBehavior),
                new PropertyMetadata(null, OnBorderBrushChanged));

        public static Brush GetBorderBrush(DependencyObject obj)
        {
            return (Brush)obj.GetValue(BorderBrushProperty);
        }

        public static void SetBorderBrush(DependencyObject obj, Brush value)
        {
            obj.SetValue(BorderBrushProperty, value);
        }

        #endregion

        #region Duration 附加属性

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.RegisterAttached(
                "Duration",
                typeof(TimeSpan),
                typeof(ThemeBehavior),
                new PropertyMetadata(TimeSpan.FromMilliseconds(200)));

        public static TimeSpan GetDuration(DependencyObject obj)
        {
            return (TimeSpan)obj.GetValue(DurationProperty);
        }

        public static void SetDuration(DependencyObject obj, TimeSpan value)
        {
            obj.SetValue(DurationProperty, value);
        }

        #endregion

        #region 私有方法

        private static void OnBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && e.NewValue is SolidColorBrush newBrush)
            {
                AnimateProperty(element, "Background", newBrush);
            }
        }

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && e.NewValue is SolidColorBrush newBrush)
            {
                AnimateProperty(element, "Foreground", newBrush);
            }
        }

        private static void OnBorderBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && e.NewValue is SolidColorBrush newBrush)
            {
                AnimateProperty(element, "BorderBrush", newBrush);
            }
        }

        private static void AnimateProperty(FrameworkElement element, string propertyName, SolidColorBrush newBrush)
        {
            var property = element.GetType().GetProperty(propertyName);
            if (property == null) return;
            var duration = GetDuration(element);
            if (property.GetValue(element) is not SolidColorBrush currentBrush || currentBrush.IsFrozen)
            {
                currentBrush = new SolidColorBrush(newBrush.Color);

                property.SetValue(element, currentBrush);
            }
            currentBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
            {
                To = newBrush.Color,
                Duration = duration
            });
        }

        #endregion
    }
}