using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Models;
using DrugSearcher.Services;
using DrugSearcher.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace DrugSearcher.ViewModels;

/// <summary>
/// 本地数据管理页面的ViewModel
/// </summary>
public partial class LocalDataManagementViewModel : ObservableObject
{
    private readonly ILocalDrugService _localDrugService;

    public LocalDataManagementViewModel(ILocalDrugService localDrugService)
    {
        _localDrugService = localDrugService;
        DrugItems = [];
        SelectedDrugs = [];

        // 加载初始数据
        _ = LoadDataAsync();
    }

    #region Properties

    /// <summary>
    /// 药物列表
    /// </summary>
    public ObservableCollection<LocalDrugInfo> DrugItems { get; }

    /// <summary>
    /// 选中的药物列表
    /// </summary>
    public ObservableCollection<LocalDrugInfo> SelectedDrugs { get; }

    /// <summary>
    /// 搜索关键词
    /// </summary>
    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    /// <summary>
    /// 当前页码
    /// </summary>
    [ObservableProperty]
    private int _currentPage = 1;

    /// <summary>
    /// 每页大小
    /// </summary>
    [ObservableProperty]
    private int _pageSize = 50;

    /// <summary>
    /// 总页数
    /// </summary>
    [ObservableProperty]
    private int _totalPages = 1;

    /// <summary>
    /// 总记录数
    /// </summary>
    [ObservableProperty]
    private int _totalRecords;

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "就绪";

    /// <summary>
    /// 药物统计信息
    /// </summary>
    [ObservableProperty]
    private DrugStatistics? _statistics;

    /// <summary>
    /// 选中的药物
    /// </summary>
    [ObservableProperty]
    private LocalDrugInfo? _selectedDrug;

    /// <summary>
    /// 是否可以上一页
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private bool _canGoPrevious;

    /// <summary>
    /// 是否可以下一页
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private bool _canGoNext;

    #endregion

    #region Commands - 使用RelayCommand属性

    /// <summary>
    /// 加载数据命令
    /// </summary>
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "正在加载数据...";

            var pageIndex = CurrentPage - 1;
            var (items, totalCount) = await _localDrugService.GetDrugsPagedAsync(pageIndex, PageSize);

            DrugItems.Clear();
            foreach (var item in items)
            {
                DrugItems.Add(item);
            }

            TotalRecords = totalCount;
            TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);

            // 更新导航状态
            UpdateNavigationState();

            // 加载统计信息
            Statistics = await _localDrugService.GetStatisticsAsync();

            StatusMessage = $"已加载 {items.Count} 条记录，共 {totalCount} 条";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载数据失败：{ex.Message}";
            MessageBox.Show($"加载数据时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 搜索命令
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "正在搜索...";

            if (string.IsNullOrWhiteSpace(SearchKeyword))
            {
                await LoadDataAsync();
                return;
            }

            var results = await _localDrugService.SearchDrugsAsync(SearchKeyword);

            DrugItems.Clear();
            foreach (var item in results)
            {
                DrugItems.Add(item);
            }

            StatusMessage = $"搜索到 {results.Count} 条结果";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索失败：{ex.Message}";
            MessageBox.Show($"搜索时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 添加命令
    /// </summary>
    [RelayCommand]
    private void Add() => ShowAddDialog();

    /// <summary>
    /// 编辑命令
    /// </summary>
    [RelayCommand]
    private void Edit(LocalDrugInfo? drug) => ShowEditDialog(drug);

    /// <summary>
    /// 删除命令
    /// </summary>
    [RelayCommand]
    private async Task Delete(LocalDrugInfo? drug) => await DeleteDrugAsync(drug);

    /// <summary>
    /// 批量删除命令
    /// </summary>
    [RelayCommand]
    private async Task BatchDeleteAsync()
    {
        if (SelectedDrugs.Count == 0)
        {
            MessageBox.Show("请先选择要删除的药物", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show($"确定要删除选中的 {SelectedDrugs.Count} 个药物吗？", "确认批量删除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsLoading = true;
            StatusMessage = "正在批量删除...";

            var ids = SelectedDrugs.Select(d => d.Id).ToList();
            var success = await _localDrugService.DeleteDrugsAsync(ids);

            if (success)
            {
                foreach (var drug in SelectedDrugs.ToList())
                {
                    DrugItems.Remove(drug);
                }
                SelectedDrugs.Clear();
                StatusMessage = "批量删除成功";
                await LoadDataAsync(); // 重新加载以更新统计信息
            }
            else
            {
                StatusMessage = "批量删除失败";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"批量删除失败：{ex.Message}";
            MessageBox.Show($"批量删除时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 导入Excel命令
    /// </summary>
    [RelayCommand]
    private async Task ImportExcelAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "选择Excel文件",
            Filter = "Excel文件|*.xlsx;*.xls|所有文件|*.*",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() != true) return;

        var filePath = openFileDialog.FileName;

        // 检查文件是否正在被使用
        if (IsFileInUse(filePath))
        {
            MessageBox.Show("文件正在被其他程序使用，请关闭该文件后重试。", "文件被占用",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "正在验证Excel文件...";

            // 先进行详细验证
            var validationResult = await _localDrugService.ValidateExcelDetailAsync(filePath);

            if (!validationResult.IsValid)
            {
                StatusMessage = "文件验证失败";
                MessageBox.Show($"Excel文件验证失败：\n{validationResult.ErrorMessage}",
                    "验证失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 显示验证结果和确认导入
            var confirmMessage = $"文件验证通过！\n\n" +
                                 $"检测到的列：{string.Join(", ", validationResult.DetectedColumns)}\n" +
                                 $"预计导入数据：{validationResult.DataRowCount} 条\n\n";

            if (!string.IsNullOrEmpty(validationResult.WarningMessage))
            {
                confirmMessage += $"⚠️ {validationResult.WarningMessage}\n\n";
            }

            confirmMessage += "是否继续导入？";

            var confirmResult = MessageBox.Show(confirmMessage, "确认导入",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
            {
                StatusMessage = "用户取消导入";
                return;
            }

            StatusMessage = "正在导入Excel数据...";

            var result = await _localDrugService.ImportFromExcelAsync(filePath);

            StatusMessage = result.Message;

            if (result.Success)
            {
                await LoadDataAsync(); // 重新加载数据

                var resultMessage = $"导入完成！\n\n" +
                                    $"总记录：{result.TotalRecords}\n" +
                                    $"成功：{result.SuccessRecords}\n" +
                                    $"重复：{result.DuplicateRecords}\n" +
                                    $"失败：{result.FailedRecords}";

                if (result.ErrorDetails.Count > 0)
                {
                    resultMessage += $"\n\n详细信息：\n{string.Join("\n", result.ErrorDetails.Take(3))}";
                    if (result.ErrorDetails.Count > 3)
                    {
                        resultMessage += $"\n... 还有 {result.ErrorDetails.Count - 3} 条信息";
                    }
                }

                MessageBox.Show(resultMessage, "导入结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var errorMessage = result.Message;
                if (result.ErrorDetails.Count > 0)
                {
                    errorMessage += "\n\n错误详情：\n" + string.Join("\n", result.ErrorDetails.Take(5));
                    if (result.ErrorDetails.Count > 5)
                    {
                        errorMessage += $"\n... 还有 {result.ErrorDetails.Count - 5} 个错误";
                    }
                }
                MessageBox.Show(errorMessage, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入失败：{ex.Message}";
            MessageBox.Show($"导入Excel时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }


    /// <summary>
    /// 导出Excel命令
    /// </summary>
    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Title = "保存Excel文件",
            Filter = "Excel文件|*.xlsx|所有文件|*.*",
            DefaultExt = "xlsx",
            FileName = $"药物数据_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };

        if (saveFileDialog.ShowDialog() != true) return;

        try
        {
            IsLoading = true;
            StatusMessage = "正在导出Excel数据...";

            var success = await _localDrugService.ExportToExcelAsync(saveFileDialog.FileName);

            if (success)
            {
                StatusMessage = "导出成功";
                MessageBox.Show($"数据已成功导出到：\n{saveFileDialog.FileName}", "导出成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "导出失败";
                MessageBox.Show("导出失败，请检查文件路径和权限", "导出失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败：{ex.Message}";
            MessageBox.Show($"导出Excel时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 刷新命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync() => await LoadDataAsync();

    /// <summary>
    /// 上一页命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadDataAsync();
        }
    }

    /// <summary>
    /// 下一页命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadDataAsync();
        }
    }

    /// <summary>
    /// 跳转到指定页命令
    /// </summary>
    [RelayCommand]
    private async Task GoToPageAsync(int page)
    {
        if (page >= 1 && page <= TotalPages && page != CurrentPage)
        {
            CurrentPage = page;
            await LoadDataAsync();
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// 更新导航状态
    /// </summary>
    private void UpdateNavigationState()
    {
        CanGoPrevious = CurrentPage > 1;
        CanGoNext = CurrentPage < TotalPages;
    }

    /// <summary>
    /// 显示添加对话框
    /// </summary>
    private void ShowAddDialog()
    {
        var dialog = new DrugEditDialog();
        if (dialog.ShowDialog() == true && dialog.DrugInfo != null)
        {
            _ = AddDrugAsync(dialog.DrugInfo);
        }
    }

    /// <summary>
    /// 显示编辑对话框
    /// </summary>
    private void ShowEditDialog(LocalDrugInfo? drug)
    {
        if (drug == null) return;

        var dialog = new DrugEditDialog(drug);
        if (dialog.ShowDialog() == true && dialog.DrugInfo != null)
        {
            _ = UpdateDrugAsync(dialog.DrugInfo);
        }
    }

    /// <summary>
    /// 检查文件是否正在被使用
    /// </summary>
    private static bool IsFileInUse(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// 添加药物
    /// </summary>
    private async Task AddDrugAsync(LocalDrugInfo localDrugInfo)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "正在添加药物...";

            var newDrug = await _localDrugService.AddDrugAsync(localDrugInfo);
            DrugItems.Insert(0, newDrug);

            StatusMessage = "药物添加成功";
            await LoadDataAsync(); // 重新加载以更新统计信息
        }
        catch (Exception ex)
        {
            StatusMessage = $"添加失败：{ex.Message}";
            MessageBox.Show($"添加药物时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 更新药物
    /// </summary>
    private async Task UpdateDrugAsync(LocalDrugInfo localDrugInfo)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "正在更新药物...";

            var updatedDrug = await _localDrugService.UpdateDrugAsync(localDrugInfo);

            var index = DrugItems.ToList().FindIndex(d => d.Id == updatedDrug.Id);
            if (index >= 0)
            {
                DrugItems[index] = updatedDrug;
            }

            StatusMessage = "药物更新成功";
        }
        catch (Exception ex)
        {
            StatusMessage = $"更新失败：{ex.Message}";
            MessageBox.Show($"更新药物时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 删除药物
    /// </summary>
    private async Task DeleteDrugAsync(LocalDrugInfo? drug)
    {
        if (drug == null) return;

        var result = MessageBox.Show($"确定要删除药物\"{drug.DrugName}\"吗？", "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsLoading = true;
            StatusMessage = "正在删除药物...";

            var success = await _localDrugService.DeleteDrugAsync(drug.Id);
            if (success)
            {
                DrugItems.Remove(drug);
                StatusMessage = "药物删除成功";
                await LoadDataAsync(); // 重新加载以更新统计信息
            }
            else
            {
                StatusMessage = "删除失败";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败：{ex.Message}";
            MessageBox.Show($"删除药物时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Property Change Handlers

    /// <summary>
    /// 当前页变化时更新导航状态
    /// </summary>
    partial void OnCurrentPageChanged(int value) => UpdateNavigationState();

    /// <summary>
    /// 总页数变化时更新导航状态
    /// </summary>
    partial void OnTotalPagesChanged(int value) => UpdateNavigationState();

    #endregion
}