using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Models;
using System.ComponentModel.DataAnnotations;

namespace DrugSearcher.ViewModels;

/// <summary>
/// 药物编辑对话框的ViewModel
/// </summary>
public partial class DrugEditDialogViewModel : ObservableValidator
{
    public DrugEditDialogViewModel(LocalDrugInfo? drugInfo = null)
    {
        IsEditMode = drugInfo != null;

        // 初始化所有属性，确保不为null
        InitializeProperties(drugInfo);
    }

    #region Properties

    /// <summary>
    /// 是否为编辑模式
    /// </summary>
    public bool IsEditMode { get; }

    /// <summary>
    /// 原始药物信息（编辑模式下使用）
    /// </summary>
    public LocalDrugInfo? OriginalDrugInfo { get; private set; }

    /// <summary>
    /// 对话框结果
    /// </summary>
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// 生成的药物信息
    /// </summary>
    public LocalDrugInfo? ResultDrugInfo { get; private set; }

    /// <summary>
    /// 药物名称
    /// </summary>
    [ObservableProperty]
    [Required(ErrorMessage = "药物名称不能为空")]
    [StringLength(200, ErrorMessage = "药物名称长度不能超过200个字符")]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _drugName = string.Empty;

    /// <summary>
    /// 通用名称
    /// </summary>
    [ObservableProperty]
    [StringLength(200, ErrorMessage = "通用名称长度不能超过200个字符")]
    private string _genericName = string.Empty;

    /// <summary>
    /// 药物规格
    /// </summary>
    [ObservableProperty]
    [StringLength(100, ErrorMessage = "规格长度不能超过100个字符")]
    private string _specification = string.Empty;

    /// <summary>
    /// 生产厂家
    /// </summary>
    [ObservableProperty]
    [StringLength(200, ErrorMessage = "生产厂家长度不能超过200个字符")]
    private string _manufacturer = string.Empty;

    /// <summary>
    /// 批准文号
    /// </summary>
    [ObservableProperty]
    [StringLength(100, ErrorMessage = "批准文号长度不能超过100个字符")]
    private string _approvalNumber = string.Empty;

    /// <summary>
    /// 适应症
    /// </summary>
    [ObservableProperty]
    private string _indications = string.Empty;

    /// <summary>
    /// 用法用量
    /// </summary>
    [ObservableProperty]
    private string _dosage = string.Empty;

    /// <summary>
    /// 中医病名
    /// </summary>
    [ObservableProperty]
    [StringLength(500, ErrorMessage = "中医病名长度不能超过500个字符")]
    private string _tcmDisease = string.Empty;

    /// <summary>
    /// 中医辨病辨证
    /// </summary>
    [ObservableProperty]
    private string _tcmSyndrome = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    [ObservableProperty]
    private string _remarks = string.Empty;

    /// <summary>
    /// 药物说明
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// 不良反应
    /// </summary>
    [ObservableProperty]
    private string _sideEffects = string.Empty;

    /// <summary>
    /// 注意事项
    /// </summary>
    [ObservableProperty]
    private string _precautions = string.Empty;

    #endregion

    #region Commands

    /// <summary>
    /// 保存命令 - 使用RelayCommand属性自动生成
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        try
        {
            if (!CanSave()) return;

            if (IsEditMode && OriginalDrugInfo != null)
            {
                // 编辑模式：更新现有药物信息
                ResultDrugInfo = CreateUpdatedDrugInfo();
            }
            else
            {
                // 添加模式：创建新药物信息
                ResultDrugInfo = CreateNewDrugInfo();
            }

            DialogResult = true;
        }
        catch (Exception ex)
        {
            // 记录错误但不抛出异常
            System.Diagnostics.Debug.WriteLine($"保存药物信息时发生错误: {ex.Message}");
            DialogResult = false;
        }
    }

    #endregion

    #region Initialization Methods

    /// <summary>
    /// 初始化属性
    /// </summary>
    private void InitializeProperties(LocalDrugInfo? drugInfo)
    {
        if (drugInfo != null)
        {
            OriginalDrugInfo = drugInfo;

            // 安全地设置属性值，确保不为null
            DrugName = drugInfo.DrugName;
            GenericName = drugInfo.GenericName ?? string.Empty;
            Specification = drugInfo.Specification ?? string.Empty;
            Manufacturer = drugInfo.Manufacturer ?? string.Empty;
            ApprovalNumber = drugInfo.ApprovalNumber ?? string.Empty;
            Indications = drugInfo.Indications ?? string.Empty;
            Dosage = drugInfo.Dosage ?? string.Empty;
            TcmDisease = drugInfo.TcmDisease ?? string.Empty;
            TcmSyndrome = drugInfo.TcmSyndrome ?? string.Empty;
            Remarks = drugInfo.Remarks ?? string.Empty;
            Description = drugInfo.Description ?? string.Empty;
            SideEffects = drugInfo.AdverseReactions ?? string.Empty;
            Precautions = drugInfo.Precautions ?? string.Empty;
        }
        else
        {
            // 新增模式，确保所有属性都有默认值
            DrugName = string.Empty;
            GenericName = string.Empty;
            Specification = string.Empty;
            Manufacturer = string.Empty;
            ApprovalNumber = string.Empty;
            Indications = string.Empty;
            Dosage = string.Empty;
            TcmDisease = string.Empty;
            TcmSyndrome = string.Empty;
            Remarks = string.Empty;
            Description = string.Empty;
            SideEffects = string.Empty;
            Precautions = string.Empty;
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// 验证是否可以保存
    /// </summary>
    private bool CanSave()
    {
        try
        {
            ValidateAllProperties();
            return !HasErrors && !string.IsNullOrWhiteSpace(DrugName);
        }
        catch (Exception)
        {
            // 如果验证过程中出现异常，返回false
            return false;
        }
    }

    /// <summary>
    /// 创建更新的药物信息
    /// </summary>
    private LocalDrugInfo CreateUpdatedDrugInfo()
    {
        if (OriginalDrugInfo == null)
            throw new InvalidOperationException("原始药物信息不能为空");

        return new LocalDrugInfo
        {
            Id = OriginalDrugInfo.Id,
            DrugName = SafeTrim(DrugName),
            GenericName = SafeTrimOrNull(GenericName),
            Specification = SafeTrimOrNull(Specification),
            Manufacturer = SafeTrimOrNull(Manufacturer),
            ApprovalNumber = SafeTrimOrNull(ApprovalNumber),
            Indications = SafeTrimOrNull(Indications),
            Dosage = SafeTrimOrNull(Dosage),
            TcmDisease = SafeTrimOrNull(TcmDisease),
            TcmSyndrome = SafeTrimOrNull(TcmSyndrome),
            Remarks = SafeTrimOrNull(Remarks),
            Description = SafeTrimOrNull(Description),
            AdverseReactions = SafeTrimOrNull(SideEffects),
            Precautions = SafeTrimOrNull(Precautions),
            DataSource = OriginalDrugInfo.DataSource,
            CreatedAt = OriginalDrugInfo.CreatedAt,
            UpdatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 创建新药物信息
    /// </summary>
    private LocalDrugInfo CreateNewDrugInfo()
    {
        return new LocalDrugInfo
        {
            DrugName = SafeTrim(DrugName),
            GenericName = SafeTrimOrNull(GenericName),
            Specification = SafeTrimOrNull(Specification),
            Manufacturer = SafeTrimOrNull(Manufacturer),
            ApprovalNumber = SafeTrimOrNull(ApprovalNumber),
            Indications = SafeTrimOrNull(Indications),
            Dosage = SafeTrimOrNull(Dosage),
            TcmDisease = SafeTrimOrNull(TcmDisease),
            TcmSyndrome = SafeTrimOrNull(TcmSyndrome),
            Remarks = SafeTrimOrNull(Remarks),
            Description = SafeTrimOrNull(Description),
            AdverseReactions = SafeTrimOrNull(SideEffects),
            Precautions = SafeTrimOrNull(Precautions),
            DataSource = DataSource.LocalDatabase,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 安全地修剪字符串
    /// </summary>
    private static string SafeTrim(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// 安全地修剪字符串，如果为空则返回null
    /// </summary>
    private static string? SafeTrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    #endregion

    #region Property Change Handlers

    /// <summary>
    /// 药物名称变化时重新验证保存命令
    /// </summary>
    partial void OnDrugNameChanged(string value)
    {
        // SaveCommand 的 CanExecute 会自动通过 NotifyCanExecuteChangedFor 更新
    }

    #endregion
}