using DrugSearcher.Configuration;
using DrugSearcher.Managers;
using DrugSearcher.ViewModels;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Indentation;
using ICSharpCode.AvalonEdit.Search;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Shell;
using System.Xml;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.Views;

public partial class CalculatorEditorWindow : Window
{
    private FoldingManager? _foldingManager;
    private System.Windows.Threading.DispatcherTimer? _foldingUpdateTimer;
    public CalculatorEditorWindow()
    {
        InitializeComponent();
        SetupCodeEditor();
        ConfigureWindowChrome();
    }

    private void SetupCodeEditor()
    {
        try
        {
            var fileName = ContainerAccessor.Resolve<ThemeManager>().CurrentTheme.Mode == Enums.ThemeMode.Dark ? "JavaScript-Dark.xshd" : "JavaScript-Light.xshd";
            //加载JavaScript语法高亮
            var resourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", fileName);

            if (File.Exists(resourcePath))
            {
                using var reader = new XmlTextReader(resourcePath);
                var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                CodeEditor.SyntaxHighlighting = definition;
            }
            else
            {
                // 如果文件不存在，使用内置的JavaScript高亮
                CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
            }

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

            // 设置代码折叠
            _foldingManager = FoldingManager.Install(CodeEditor.TextArea);
            _foldingUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _foldingUpdateTimer.Start();
        }
        catch (Exception ex)
        {
            // 如果加载语法高亮失败，继续使用普通文本编辑器
            System.Diagnostics.Debug.WriteLine($"加载语法高亮失败: {ex.Message}");
        }
    }
    /// <summary>
    /// 配置窗口Chrome样式
    /// </summary>
    private void ConfigureWindowChrome()
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
        // 这里可以添加检查是否有未保存的更改的逻辑
        // 暂时返回false
        return false;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is CalculatorEditorViewModel viewModel)
        {
            viewModel.OnCalculatorSaved -= OnCalculatorSaved;
        }
        base.OnClosing(e);
    }

    private void OnCalculatorSaved(DrugSearcher.Models.DosageCalculator calculator)
    {
        DialogResult = true;
        Close();
    }

    public void SetViewModel(CalculatorEditorViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.OnCalculatorSaved += OnCalculatorSaved;
    }
}