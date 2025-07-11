using CommunityToolkit.Mvvm.Input;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DrugSearcher.Behaviors;

/// <summary>
/// 按键事件行为
/// </summary>
public class KeyDownBehavior : Behavior<UIElement>
{
    /// <summary>
    /// 键值属性
    /// </summary>
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.Register(nameof(Key), typeof(Key), typeof(KeyDownBehavior));

    /// <summary>
    /// 命令属性
    /// </summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(IRelayCommand), typeof(KeyDownBehavior));

    /// <summary>
    /// 要监听的键
    /// </summary>
    public Key Key
    {
        get => (Key)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>
    /// 执行的命令
    /// </summary>
    public IRelayCommand Command
    {
        get => (IRelayCommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// 附加时绑定事件
    /// </summary>
    protected override void OnAttached()
    {
        AssociatedObject.KeyDown += OnKeyDown;
    }

    /// <summary>
    /// 分离时解绑事件
    /// </summary>
    protected override void OnDetaching()
    {
        AssociatedObject.KeyDown -= OnKeyDown;
    }

    /// <summary>
    /// 按键事件处理
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">事件参数</param>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key && Command.CanExecute(null))
        {
            Command.Execute(null);
        }
    }
}