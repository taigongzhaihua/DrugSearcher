using DrugSearcher.Configuration;
using DrugSearcher.Managers;
using DrugSearcher.Models;
using DrugSearcher.Services;
using DrugSearcher.ViewModels;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Indentation;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.Views;

public partial class CalculatorEditorWindow
{
    private System.Windows.Threading.DispatcherTimer? _foldingUpdateTimer;
    private FoldingManager? _foldingManager;
    private CodeCompletionService? _codeCompletionService;
    private RealTimeSyntaxService? _syntaxService;
    private readonly ILogger<RealTimeSyntaxService> _logger;
    private readonly ThemeManager _themeManager;

    public CalculatorEditorWindow()
    {
        InitializeComponent();

        // 获取服务
        _logger = ContainerAccessor.Resolve<ILogger<RealTimeSyntaxService>>();
        _themeManager = ContainerAccessor.Resolve<ThemeManager>();

        // 使用 Loaded 事件确保所有控件都已初始化
        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;

        // 订阅主题变化事件
        _themeManager.ThemeChanged += OnThemeChanged;
    }

    /// <summary>
    /// 窗口加载完成事件处理
    /// </summary>
    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // 确保所有控件都已初始化
        SetupCodeEditor();
        ConfigureWindowChrome();
        // 注册窗口以启用边框颜色跟随主题
        _themeManager.RegisterWindow(this);

        // 延迟初始化需要编辑器完全准备好的功能
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            SetupFolding();
            SetupCodeCompletion();
            SetupSyntaxChecking();
            UpdateCodeCompletionParameters();
        }));
    }
    private void OnWindowClosed(object? _, EventArgs _1)
    {
        // 注销窗口
        _themeManager.UnregisterWindow(this);
    }

    private void SetupCodeEditor()
    {
        try
        {
            // 确保 CodeEditor 不为 null
            if (CodeEditor == null)
            {
                _logger.LogError("CodeEditor is null");
                return;
            }

            // 使用统一的语法高亮生成器
            var isDarkTheme = _themeManager.CurrentTheme.Mode == Enums.ThemeMode.Dark;
            var definition = JavaScriptSyntaxHighlightingGenerator.GenerateDefinition(isDarkTheme);
            CodeEditor.SyntaxHighlighting = definition;

            // 设置编辑器选项
            CodeEditor.Options.EnableEmailHyperlinks = false;
            CodeEditor.Options.EnableHyperlinks = false;
            CodeEditor.Options.ConvertTabsToSpaces = true;
            CodeEditor.Options.IndentationSize = 4;
            CodeEditor.Options.ShowTabs = true;
            CodeEditor.Options.ShowSpaces = false;
            CodeEditor.Options.ShowEndOfLine = false;
            CodeEditor.Options.WordWrapIndentation = 4;
            CodeEditor.Options.InheritWordWrapIndentation = true;
            CodeEditor.Options.EnableVirtualSpace = false;
            CodeEditor.Options.EnableTextDragDrop = true;
            CodeEditor.Options.EnableRectangularSelection = true;
            CodeEditor.Options.CutCopyWholeLine = true;
            CodeEditor.Options.ShowColumnRuler = true;
            CodeEditor.Options.ColumnRulerPosition = 100;

            // 设置缩进策略
            CodeEditor.TextArea.IndentationStrategy = new DefaultIndentationStrategy();

            // 启用搜索面板
            SearchPanel.Install(CodeEditor.TextArea);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置代码编辑器失败");
            Debug.WriteLine($"加载语法高亮失败: {ex.Message}");

            // 如果自定义高亮失败，尝试使用内置的JavaScript高亮
            try
            {
                if (HighlightingManager.Instance != null)
                {
                    CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
                }
            }
            catch
            {
                // 如果所有尝试都失败，不使用语法高亮
                CodeEditor.SyntaxHighlighting = null;
            }
        }
    }

    /// <summary>
    /// 设置代码折叠
    /// </summary>
    private void SetupFolding()
    {
        try
        {
            // 确保 CodeEditor 和 TextArea 都不为 null
            if (CodeEditor?.TextArea == null)
            {
                _logger.LogWarning("Cannot setup folding: CodeEditor or TextArea is null");
                return;
            }

            // 确保文档已初始化
            if (CodeEditor.Document == null)
            {
                _logger.LogWarning("Cannot setup folding: Document is null");
                return;
            }

            _foldingManager = FoldingManager.Install(CodeEditor.TextArea);

            _foldingUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _foldingUpdateTimer.Tick += UpdateFolding;
            _foldingUpdateTimer.Start();

            // 立即执行一次折叠更新
            UpdateFolding(null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置代码折叠失败");
            Debug.WriteLine($"设置代码折叠失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新代码折叠
    /// </summary>
    private void UpdateFolding(object? sender, EventArgs? e)
    {
        try
        {
            if (_foldingManager == null || CodeEditor?.Document == null) return;

            BraceFoldingStrategy.UpdateFoldings(_foldingManager, CodeEditor.Document);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "更新代码折叠时出错");
        }
    }

    /// <summary>
    /// 设置代码提示
    /// </summary>
    private void SetupCodeCompletion()
    {
        try
        {
            if (CodeEditor == null)
            {
                _logger.LogWarning("Cannot setup code completion: CodeEditor is null");
                return;
            }

            _codeCompletionService = new CodeCompletionService(CodeEditor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置代码提示失败");
        }
    }

    /// <summary>
    /// 设置语法检测
    /// </summary>
    private void SetupSyntaxChecking()
    {
        try
        {
            if (CodeEditor == null)
            {
                _logger.LogWarning("Cannot setup syntax checking: CodeEditor is null");
                return;
            }

            _syntaxService = new RealTimeSyntaxService(CodeEditor, _logger);

            // 订阅状态变化事件
            _syntaxService.StatusChanged += OnSyntaxStatusChanged;
            _syntaxService.ValidationCompleted += OnSyntaxValidationCompleted;

            // 开始语法检测
            _syntaxService.StartSyntaxChecking();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置语法检测失败");
        }
    }

    /// <summary>
    /// 语法状态变化处理
    /// </summary>
    private void OnSyntaxStatusChanged(object? sender, string? status) =>
        // 更新UI中的语法状态
        Dispatcher.Invoke(() =>
        {
            if (SyntaxStatusText == null) return;

            SyntaxStatusText.Text = status;

            // 根据状态设置颜色
            if (status != null && status.Contains('✓'))
            {
                SyntaxStatusText.Foreground = new SolidColorBrush(Colors.Green);
            }
            else if (status != null && status.Contains('✗'))
            {
                SyntaxStatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
            else if (status != null && status.Contains('⚠'))
            {
                SyntaxStatusText.Foreground = new SolidColorBrush(Colors.Orange);
            }
            else if (status != null && status.Contains('ℹ'))
            {
                SyntaxStatusText.Foreground = new SolidColorBrush(Colors.Blue);
            }
            else
            {
                SyntaxStatusText.Foreground = Application.Current.FindResource("PrimaryTextBrush") as Brush;
            }
        });

    /// <summary>
    /// 语法验证完成处理
    /// </summary>
    private void OnSyntaxValidationCompleted(object? sender, SyntaxValidationResult result)
    {
        if (DataContext is CalculatorEditorViewModel viewModel)
        {
            // 更新状态栏消息
            var errorCount = result.Errors.Count(e => e.Severity == SyntaxErrorSeverity.Error);
            var warningCount = result.Errors.Count(e => e.Severity == SyntaxErrorSeverity.Warning);
            var infoCount = result.Errors.Count(e => e.Severity == SyntaxErrorSeverity.Info);

            if (errorCount > 0 || warningCount > 0 || infoCount > 0)
            {
                var parts = new List<string>();
                if (errorCount > 0) parts.Add($"{errorCount} 个错误");
                if (warningCount > 0) parts.Add($"{warningCount} 个警告");
                if (infoCount > 0) parts.Add($"{infoCount} 个提示");

                viewModel.StatusMessage = $"发现 {string.Join(", ", parts)}";
            }
            else
            {
                viewModel.StatusMessage = "代码语法正确";
            }
        }
    }

    /// <summary>
    /// 配置窗口Chrome样式
    /// </summary>
    private void ConfigureWindowChrome()
    {
        try
        {
            var windowChrome = new WindowChrome
            {
                ResizeBorderThickness = new Thickness(5),
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(8),
                GlassFrameThickness = new Thickness(-1),
                NonClientFrameEdges = NonClientFrameEdges.Bottom |
                                      NonClientFrameEdges.Left |
                                      NonClientFrameEdges.Right,
                UseAeroCaptionButtons = false
            };

            WindowChrome.SetWindowChrome(this, windowChrome);
            Debug.WriteLine("窗口Chrome配置完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "配置窗口Chrome失败");
        }
    }

    /// <summary>
    /// 主题变化处理
    /// </summary>
    private void OnThemeChanged(object? sender, ThemeConfig themeConfig) => Dispatcher.Invoke(() =>
    {
        try
        {
            // 更新语法高亮
            var isDarkTheme = _themeManager.CurrentTheme.Mode == Enums.ThemeMode.Dark;
            var definition = JavaScriptSyntaxHighlightingGenerator.GenerateDefinition(isDarkTheme);
            CodeEditor.SyntaxHighlighting = definition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新语法高亮失败");
        }
    });

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (HasUnsavedChanges())
        {
            var result = MessageBox.Show("有未保存的更改，确定要关闭吗？", "确认关闭",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                return;
            }
        }

        DialogResult = false;
        Close();
    }

    private bool HasUnsavedChanges()
    {
        // 可以通过比较原始代码和当前代码来判断
        if (DataContext is CalculatorEditorViewModel viewModel)
        {
            return viewModel.HasUnsavedChanges;
        }

        return false;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 取消 Loaded 事件订阅
        Loaded -= OnWindowLoaded;

        if (DataContext is CalculatorEditorViewModel viewModel)
        {
            viewModel.OnCalculatorSaved -= OnCalculatorSaved;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;

            // 清理参数相关的事件订阅
            if (viewModel.Parameters != null)
            {
                viewModel.Parameters.CollectionChanged -= OnParametersCollectionChanged;

                foreach (var parameter in viewModel.Parameters)
                {
                    parameter.PropertyChanged -= OnParameterPropertyChanged;
                }
            }

            // 清理视图模型资源
            viewModel.Cleanup();
        }

        // 取消订阅主题变化事件
        _themeManager.ThemeChanged -= OnThemeChanged;

        // 释放服务
        _codeCompletionService?.Dispose();
        _syntaxService?.Dispose();
        _foldingUpdateTimer?.Stop();
        _foldingUpdateTimer = null;

        base.OnClosing(e);
    }

    private void OnCalculatorSaved(DosageCalculator calculator)
    {
        DialogResult = true;
        Close();
    }

    public void SetViewModel(CalculatorEditorViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.OnCalculatorSaved += OnCalculatorSaved;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 监听参数集合变化
        if (viewModel.Parameters != null)
        {
            viewModel.Parameters.CollectionChanged += OnParametersCollectionChanged;

            // 监听每个参数的属性变化
            foreach (var parameter in viewModel.Parameters)
            {
                parameter.PropertyChanged += OnParameterPropertyChanged;
            }
        }

        // 初始化参数到代码提示
        UpdateCodeCompletionParameters();
    }


    /// <summary>
    /// 处理参数集合变化
    /// </summary>
    private void OnParametersCollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // 处理新增的参数
        if (e.NewItems != null)
        {
            foreach (DosageParameterViewModel parameter in e.NewItems)
            {
                parameter.PropertyChanged += OnParameterPropertyChanged;
            }
        }

        // 处理删除的参数
        if (e.OldItems != null)
        {
            foreach (DosageParameterViewModel parameter in e.OldItems)
            {
                parameter.PropertyChanged -= OnParameterPropertyChanged;
            }
        }

        // 更新代码提示参数
        UpdateCodeCompletionParameters();
    }

    /// <summary>
    /// 处理单个参数属性变化
    /// </summary>
    private void OnParameterPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 当参数的重要属性发生变化时，更新代码提示
        if (e.PropertyName
            is nameof(DosageParameterViewModel.Name)
            or nameof(DosageParameterViewModel.DataTypeName)
            or nameof(DosageParameterViewModel.DisplayName))
        {
            UpdateCodeCompletionParameters();
        }
    }

    /// <summary>
    /// 处理ViewModel属性变化
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalculatorEditorViewModel.Parameters))
        {
            UpdateCodeCompletionParameters();
        }
    }

    /// <summary>
    /// 更新代码提示中的参数
    /// </summary>
    private void UpdateCodeCompletionParameters()
    {
        if (DataContext is not CalculatorEditorViewModel viewModel) return;
        var parameters = viewModel.Parameters?.Select(p => p.ToModel()).ToList() ?? [];

        // 更新代码提示参数
        _codeCompletionService?.UpdateParameters(parameters);

        // 更新语法检测参数
        _syntaxService?.UpdateParameters(parameters);
    }
}

/// <summary>
/// 大括号折叠策略
/// </summary>
public class BraceFoldingStrategy
{
    public static void UpdateFoldings(FoldingManager manager, ICSharpCode.AvalonEdit.Document.TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var stack = new Stack<int>();

        for (var i = 0; i < document.TextLength; i++)
        {
            var ch = document.GetCharAt(i);

            switch (ch)
            {
                case '{':
                    stack.Push(i);
                    break;
                case '}' when stack.Count > 0:
                    {
                        var start = stack.Pop();
                        // 只为多行代码块创建折叠
                        var startLine = document.GetLineByOffset(start).LineNumber;
                        var endLine = document.GetLineByOffset(i).LineNumber;

                        if (endLine > startLine)
                        {
                            foldings.Add(new NewFolding(start, i + 1));
                        }

                        break;
                    }
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        manager.UpdateFoldings(foldings, -1);
    }
}