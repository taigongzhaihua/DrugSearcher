using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DrugSearcher.Behaviors
{
    /// <summary>
    /// 滚动传播行为
    /// </summary>
    public class ScrollPropagationBehavior : Behavior<FrameworkElement>
    {
        private ScrollViewer? _childScrollViewer;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseWheel += OnPreviewMouseWheel;
            AssociatedObject.Loaded += OnLoaded;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseWheel -= OnPreviewMouseWheel;
            AssociatedObject.Loaded -= OnLoaded;
            base.OnDetaching();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 查找并缓存子ScrollViewer
            _childScrollViewer = FindVisualChild<ScrollViewer>(AssociatedObject);
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;

            // 检查是否应该处理滚动
            if (ShouldHandleScroll(e.Delta))
            {
                return; // 让当前控件处理
            }

            // 传播到父级
            e.Handled = true;
            var parentScrollViewer = FindParentScrollViewer(AssociatedObject);
            
            if (parentScrollViewer != null)
            {
                // 计算滚动量
                var scrollAmount = e.Delta * SystemParameters.WheelScrollLines * 0.5;
                var newOffset = parentScrollViewer.VerticalOffset - scrollAmount;
                
                // 确保在有效范围内
                newOffset = Math.Max(0, Math.Min(parentScrollViewer.ScrollableHeight, newOffset));
                
                parentScrollViewer.ScrollToVerticalOffset(newOffset);
            }
        }

        private bool ShouldHandleScroll(double delta)
        {
            if (_childScrollViewer == null) return false;

            // 如果ScrollViewer被禁用，不处理
            if (_childScrollViewer.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
                return false;

            // 检查是否可以在指定方向滚动
            if (delta > 0) // 向上滚动
            {
                return _childScrollViewer.VerticalOffset > 0;
            }
            else // 向下滚动
            {
                return _childScrollViewer.VerticalOffset < _childScrollViewer.ScrollableHeight;
            }
        }

        private static ScrollViewer? FindParentScrollViewer(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            
            while (parent != null)
            {
                if (parent is ScrollViewer scrollViewer)
                    return scrollViewer;
                    
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild)
                    return typedChild;
                    
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            
            return null;
        }
    }
}