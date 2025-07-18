using DrugSearcher.Models;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace DrugSearcher.Views.Controls;

/// <summary>
/// 快捷键输入控件 - MVVM模式
/// </summary>
public partial class HotKeyInputControl : UserControl
{
    #region 依赖属性

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(HotKeySetting), typeof(HotKeyInputControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(HotKeyInputControl),
            new PropertyMetadata("点击设置快捷键"));

    public static readonly DependencyProperty IsRecordingProperty =
        DependencyProperty.Register(nameof(IsRecording), typeof(bool), typeof(HotKeyInputControl),
            new PropertyMetadata(false, OnIsRecordingChanged));

    public static readonly DependencyProperty HasValueProperty =
        DependencyProperty.Register(nameof(HasValue), typeof(bool), typeof(HotKeyInputControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(HotKeyInputControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(HotKeyInputControl),
            new PropertyMetadata(null));

    #endregion

    #region 公共属性

    /// <summary>
    /// 快捷键设置值
    /// </summary>
    public HotKeySetting? Value
    {
        get => (HotKeySetting?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// 显示文本
    /// </summary>
    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    /// <summary>
    /// 是否正在录制
    /// </summary>
    public bool IsRecording
    {
        get => (bool)GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    /// <summary>
    /// 是否有值
    /// </summary>
    public bool HasValue
    {
        get => (bool)GetValue(HasValueProperty);
        set => SetValue(HasValueProperty, value);
    }

    /// <summary>
    /// 值变更时执行的命令
    /// </summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// 命令参数
    /// </summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    #endregion

    #region 私有字段

    private bool _isCapturingKeys;

    #endregion

    #region 构造函数

    public HotKeyInputControl()
    {
        InitializeComponent();

        // 设置焦点和键盘导航属性
        Focusable = true;
        IsTabStop = true;

        // 设置默认样式
        UpdateDisplayText();

        // 绑定事件
        Loaded += OnLoaded;
    }

    #endregion

    #region 事件处理

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 确保控件可以获取焦点
        if (MainBorder != null)
        {
            MainBorder.Focusable = true;
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        // 确保获取焦点
        if (!IsKeyboardFocused)
        {
            Focus();
            Keyboard.Focus(this);
        }

        StartRecording();
        e.Handled = true;
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        Debug.WriteLine("HotKeyInputControl: 获取焦点");
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        Debug.WriteLine("HotKeyInputControl: 失去焦点");

        // 失去焦点时停止录制
        if (IsRecording)
        {
            StopRecording();
        }
    }

    // 关键修复：同时处理PreviewKeyDown和PreviewSystemKey事件
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsRecording && _isCapturingKeys)
        {
            Debug.WriteLine($"PreviewKeyDown: Key={e.Key}, SystemKey={e.SystemKey}, Modifiers={Keyboard.Modifiers}");
            e.Handled = true;
            HandleKeyInput(e.Key, e.SystemKey, Keyboard.Modifiers);
        }
        else
        {
            base.OnPreviewKeyDown(e);
        }
    }

    // 重要：处理系统键事件
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (IsRecording && _isCapturingKeys)
        {
            e.Handled = true; // 阻止事件传播
        }
        else
        {
            base.OnPreviewKeyUp(e);
        }
    }

    // 处理系统键（Alt组合键）
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsRecording && _isCapturingKeys)
        {
            Debug.WriteLine($"KeyDown: Key={e.Key}, SystemKey={e.SystemKey}, Modifiers={Keyboard.Modifiers}");
            e.Handled = true;
            HandleKeyInput(e.Key, e.SystemKey, Keyboard.Modifiers);
        }
        else if (e.Key is Key.Space or Key.Enter)
        {
            // 空格键或回车键开始录制
            StartRecording();
            e.Handled = true;
        }
        else
        {
            base.OnKeyDown(e);
        }
    }

    // 修复：正确处理键盘输入
    private void HandleKeyInput(Key key, Key systemKey, ModifierKeys modifiers)
    {
        Debug.WriteLine($"HandleKeyInput: Key={key}, SystemKey={systemKey}, Modifiers={modifiers}");

        // 处理实际的按键
        var actualKey = key;

        // 关键修复：当key为System时，使用SystemKey
        if (key == Key.System && systemKey != Key.None)
        {
            actualKey = systemKey;
            Debug.WriteLine($"使用SystemKey: {systemKey}");
        }

        // 忽略只按修饰键的情况
        if (IsModifierKey(actualKey))
        {
            Debug.WriteLine($"忽略修饰键: {actualKey}");
            return;
        }

        // Escape键取消录制
        if (actualKey == Key.Escape)
        {
            Debug.WriteLine("按下Escape，取消录制");
            StopRecording();
            return;
        }

        // 创建新的快捷键设置
        var newHotKey = new HotKeySetting(actualKey, modifiers);
        Debug.WriteLine($"创建快捷键: {newHotKey}");

        // 验证快捷键
        if (ValidateHotKey(newHotKey))
        {
            Debug.WriteLine($"快捷键验证通过: {newHotKey}");
            // 更新值并触发命令
            SetValueAndExecuteCommand(newHotKey);
        }
        else
        {
            Debug.WriteLine($"快捷键验证失败: {newHotKey}");
        }

        StopRecording();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SetValueAndExecuteCommand(null);
        e.Handled = true;
    }

    #endregion

    #region 私有方法

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotKeyInputControl control)
        {
            control.UpdateDisplayText();
        }
    }

    private static void OnIsRecordingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotKeyInputControl control)
        {
            control.OnIsRecordingChanged();
        }
    }

    private void OnIsRecordingChanged()
    {
        Debug.WriteLine($"录制状态变更: {IsRecording}");

        if (IsRecording)
        {
            _isCapturingKeys = true;

            // 捕获鼠标，防止点击其他地方
            CaptureMouse();

            // 确保获取焦点
            if (!IsKeyboardFocused)
            {
                Focus();
                Keyboard.Focus(this);
            }

            Debug.WriteLine("开始录制快捷键...");
        }
        else
        {
            _isCapturingKeys = false;

            // 释放鼠标捕获
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            Debug.WriteLine("停止录制快捷键");
        }
    }

    private void UpdateDisplayText()
    {
        if (Value != null)
        {
            DisplayText = Value.ToString();
            HasValue = true;
        }
        else
        {
            DisplayText = "点击设置快捷键";
            HasValue = false;
        }
    }

    private void StartRecording()
    {
        Debug.WriteLine("开始录制快捷键");
        IsRecording = true;

        // 确保获取焦点
        if (!IsKeyboardFocused)
        {
            Focus();
            Keyboard.Focus(this);
        }
    }

    private void StopRecording()
    {
        Debug.WriteLine("停止录制快捷键");
        IsRecording = false;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
    }

    private static bool ValidateHotKey(HotKeySetting hotKey)
    {
        Debug.WriteLine($"验证快捷键: {hotKey}");

        // 基本验证：至少需要一个修饰键（除了F1-F12功能键）
        if (hotKey is { Modifiers: ModifierKeys.None, Key: < Key.F1 or > Key.F24 })
        {
            MessageBox.Show("快捷键必须包含至少一个修饰键（Ctrl、Alt、Shift、Win）", "无效的快捷键",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // 检查是否是系统保留的快捷键
        if (!IsSystemReservedHotKey(hotKey)) return true;
        MessageBox.Show("这是系统保留的快捷键，请选择其他组合", "无效的快捷键",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;

    }

    private static bool IsSystemReservedHotKey(HotKeySetting hotKey)
    {
        return hotKey switch
        {
            // 检查常见的系统保留快捷键
            { Modifiers: ModifierKeys.Alt, Key: Key.F4 or Key.Tab }
                or { Modifiers: ModifierKeys.Control, Key: Key.Escape }
                or { Modifiers: (ModifierKeys.Control | ModifierKeys.Alt), Key: Key.Delete } => true,
            _ => false
        };
    }

    private void SetValueAndExecuteCommand(HotKeySetting? newValue)
    {
        Debug.WriteLine($"设置快捷键值: {newValue}");

        // 更新值
        Value = newValue;

        // 执行命令
        if (Command != null && Command.CanExecute(CommandParameter))
        {
            Debug.WriteLine("执行命令");
            Command.Execute(CommandParameter);
        }
        else
        {
            Debug.WriteLine("命令为空或无法执行");
        }
    }

    #endregion

    #region 重写方法以支持焦点

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (IsKeyboardFocused) return;
        Focus();
        Keyboard.Focus(this);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 确保控件可以获取焦点
        Focusable = true;
        IsTabStop = true;
    }

    #endregion
}