using DrugSearcher.Constants;
using DrugSearcher.Enums;
using DrugSearcher.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DrugSearcher.Data;

/// <summary>
/// 应用程序数据库上下文，管理设置数据的持久化
/// 包括数据库表配置、默认数据种子和数据库初始化
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    #region DbSet 属性

    /// <summary>
    /// 设置项数据集
    /// </summary>
    [field: MaybeNull, AllowNull]
    public DbSet<SettingItem>? Settings { get; set; }

    #endregion

    #region 模型配置

    /// <summary>
    /// 配置数据模型
    /// </summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        try
        {
            // 配置数据表
            ConfigureSettingsTable(modelBuilder);

            // 种子默认数据
            SeedDefaultSettings(modelBuilder);

            Debug.WriteLine("数据模型配置完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"配置数据模型时出错: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 配置设置表结构
    /// </summary>
    /// <param name="modelBuilder">模型构建器</param>
    private static void ConfigureSettingsTable(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SettingItem>();

        // 设置表名
        entity.ToTable("Settings");

        // 配置主键
        entity.HasKey(s => s.Id);

        // 配置必需属性
        ConfigureRequiredProperties(entity);

        // 配置可选属性
        ConfigureOptionalProperties(entity);

        // 配置索引
        ConfigureIndexes(entity);
    }

    /// <summary>
    /// 配置必需属性
    /// </summary>
    /// <param name="entity">实体配置器</param>
    private static void ConfigureRequiredProperties(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SettingItem> entity)
    {
        entity.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(SettingConstraints.MAX_KEY_LENGTH)
            .HasComment("设置项键名");

        entity.Property(s => s.ValueType)
            .IsRequired()
            .HasMaxLength(SettingConstraints.MAX_VALUE_TYPE_LENGTH)
            .HasComment("值类型名称");

        entity.Property(s => s.CreatedAt)
            .IsRequired()
            .HasComment("创建时间");

        entity.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasComment("更新时间");
    }

    /// <summary>
    /// 配置可选属性
    /// </summary>
    /// <param name="entity">实体配置器</param>
    private static void ConfigureOptionalProperties(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SettingItem> entity)
    {
        entity.Property(s => s.Value)
            .HasMaxLength(SettingConstraints.MAX_VALUE_LENGTH)
            .HasComment("设置值");

        entity.Property(s => s.Description)
            .HasMaxLength(SettingConstraints.MAX_DESCRIPTION_LENGTH)
            .HasComment("设置描述");

        entity.Property(s => s.Category)
            .HasMaxLength(SettingConstraints.MAX_CATEGORY_LENGTH)
            .HasComment("设置分类");

        entity.Property(s => s.UserId)
            .HasMaxLength(SettingConstraints.MAX_USER_ID_LENGTH)
            .HasComment("用户ID");
    }

    /// <summary>
    /// 配置数据库索引
    /// </summary>
    /// <param name="entity">实体配置器</param>
    private static void ConfigureIndexes(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SettingItem> entity)
    {
        // 设置唯一索引：Key + UserId（确保每个用户的设置键唯一）
        entity.HasIndex(s => new { s.Key, s.UserId })
            .IsUnique()
            .HasDatabaseName("IX_Settings_Key_UserId");

        // 设置分类索引（优化按分类查询）
        entity.HasIndex(s => s.Category)
            .HasDatabaseName("IX_Settings_Category");

        // 设置用户索引（优化按用户查询）
        entity.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_Settings_UserId");

        // 设置更新时间索引（优化按时间排序）
        entity.HasIndex(s => s.UpdatedAt)
            .HasDatabaseName("IX_Settings_UpdatedAt");
    }

    /// <summary>
    /// 种子默认设置数据
    /// </summary>
    /// <param name="modelBuilder">模型构建器</param>
    private static void SeedDefaultSettings(ModelBuilder modelBuilder)
    {
        var defaultSettings = CreateDefaultSettingsForSeeding();
        modelBuilder.Entity<SettingItem>().HasData(defaultSettings);
    }

    /// <summary>
    /// 创建用于数据种子的默认设置
    /// </summary>
    /// <returns>默认设置列表</returns>
    private static SettingItem[] CreateDefaultSettingsForSeeding()
    {
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return
        [
            CreateDefaultSetting(1, SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE, nameof(Boolean), "true",
                "关闭窗口时最小化到托盘", SettingCategories.TRAY, baseTime),

            CreateDefaultSetting(2, SettingKeys.SHOW_TRAY_ICON, nameof(Boolean), "true",
                "显示系统托盘图标", SettingCategories.TRAY, baseTime),

            CreateDefaultSetting(3, SettingKeys.SHOW_TRAY_NOTIFICATIONS, nameof(Boolean), "true",
                "显示托盘通知", SettingCategories.TRAY, baseTime),

            CreateDefaultSetting(4, SettingKeys.THEME_MODE, nameof(ThemeMode), "Light",
                "主题模式", SettingCategories.UI, baseTime),

            CreateDefaultSetting(5, SettingKeys.THEME_COLOR, nameof(ThemeColor), "Blue",
                "主题颜色", SettingCategories.UI, baseTime),

            CreateDefaultSetting(6, SettingKeys.FONT_SIZE, nameof(Int32), "12",
                "字体大小", SettingCategories.UI, baseTime),

            CreateDefaultSetting(7, SettingKeys.LANGUAGE, nameof(String), "zh-CN",
                "界面语言", SettingCategories.UI, baseTime),

            CreateDefaultSetting(8, SettingKeys.AUTO_STARTUP, nameof(Boolean), "false",
                "开机自启动", SettingCategories.APPLICATION, baseTime)
        ];
    }

    /// <summary>
    /// 创建单个默认设置项
    /// </summary>
    /// <param name="id">设置ID</param>
    /// <param name="key">设置键</param>
    /// <param name="valueType">值类型</param>
    /// <param name="value">默认值</param>
    /// <param name="description">描述</param>
    /// <param name="category">分类</param>
    /// <param name="baseTime">基准时间</param>
    /// <returns>设置项</returns>
    private static SettingItem CreateDefaultSetting(int id, string key, string valueType, string value,
        string description, string category, DateTime baseTime) => new()
        {
            Id = id,
            Key = key,
            ValueType = valueType,
            Value = value,
            Description = description,
            Category = category,
            UserId = null, // 全局设置
            CreatedAt = baseTime,
            UpdatedAt = baseTime
        };

    #endregion

    #region 数据库初始化

    /// <summary>
    /// 确保数据库和表结构已创建
    /// </summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        try
        {
            Debug.WriteLine("开始确保数据库已创建...");

            // 创建数据库（如果不存在）
            var created = await Database.EnsureCreatedAsync();

            if (created)
            {
                Debug.WriteLine("数据库已创建并初始化默认设置");
                Console.WriteLine("数据库已创建并初始化默认设置");
            }
            else
            {
                Debug.WriteLine("数据库已存在，检查默认设置...");
                // 数据库已存在，检查是否需要添加默认设置
                await EnsureDefaultSettingsExistAsync();
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"创建数据库时发生错误: {ex.Message}";
            Debug.WriteLine(errorMessage);
            Console.WriteLine(errorMessage);
            throw;
        }
    }

    /// <summary>
    /// 确保默认设置存在
    /// </summary>
    private async Task EnsureDefaultSettingsExistAsync()
    {
        try
        {
            // 检查是否有任何全局设置
            var hasGlobalSettings = await (Settings ?? throw new InvalidOperationException()).Where(s => s.UserId == null).AnyAsync();

            if (!hasGlobalSettings)
            {
                Debug.WriteLine("未找到全局默认设置，正在添加...");
                await AddDefaultSettingsAsync();
            }
            else
            {
                Debug.WriteLine("全局默认设置已存在");
                // 检查是否缺少某些必需的设置
                await EnsureRequiredSettingsExistAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"确保默认设置时发生错误: {ex.Message}");
            Console.WriteLine($"确保默认设置时发生错误: {ex.Message}");
            // 不抛出异常，允许应用程序继续运行
        }
    }

    /// <summary>
    /// 添加默认设置
    /// </summary>
    private async Task AddDefaultSettingsAsync()
    {
        var defaultSettings = GetRuntimeDefaultSettings();
        if (Settings != null) await Settings.AddRangeAsync(defaultSettings);
        var savedCount = await SaveChangesAsync();

        Debug.WriteLine($"已添加 {savedCount} 个默认设置");
        Console.WriteLine($"已添加 {savedCount} 个默认设置");
    }

    /// <summary>
    /// 确保必需的设置存在
    /// </summary>
    private async Task EnsureRequiredSettingsExistAsync()
    {
        var requiredSettings = GetRequiredSettingKeys();
        var existingKeys = await (Settings ?? throw new InvalidOperationException())
            .Where(s => s.UserId == null)
            .Select(s => s.Key)
            .ToListAsync();

        var missingKeys = requiredSettings.Except(existingKeys).ToList();

        if (missingKeys.Count > 0)
        {
            Debug.WriteLine($"发现缺少 {missingKeys.Count} 个必需设置，正在添加...");

            var missingSettings = GetRuntimeDefaultSettings()
                .Where(s => missingKeys.Contains(s.Key))
                .ToList();

            await Settings.AddRangeAsync(missingSettings);
            var savedCount = await SaveChangesAsync();

            Debug.WriteLine($"已添加 {savedCount} 个缺失的设置");
        }
    }

    /// <summary>
    /// 获取必需的设置键列表
    /// </summary>
    /// <returns>必需设置键列表</returns>
    private static List<string> GetRequiredSettingKeys() => [
            SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE,
            SettingKeys.SHOW_TRAY_ICON,
            SettingKeys.SHOW_TRAY_NOTIFICATIONS,
            SettingKeys.THEME_MODE,
            SettingKeys.THEME_COLOR,
            SettingKeys.FONT_SIZE,
            SettingKeys.LANGUAGE,
            SettingKeys.AUTO_STARTUP
        ];

    /// <summary>
    /// 获取运行时默认设置（不包含ID，用于运行时添加）
    /// </summary>
    /// <returns>默认设置列表</returns>
    private static List<SettingItem> GetRuntimeDefaultSettings()
    {
        var currentTime = DateTime.UtcNow;

        return
        [
            CreateRuntimeDefaultSetting(SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE, nameof(Boolean), "true",
                "关闭窗口时最小化到托盘", SettingCategories.TRAY, currentTime),


            CreateRuntimeDefaultSetting(SettingKeys.SHOW_TRAY_ICON, nameof(Boolean), "true",
                "显示系统托盘图标", SettingCategories.TRAY, currentTime),


            CreateRuntimeDefaultSetting(SettingKeys.SHOW_TRAY_NOTIFICATIONS, nameof(Boolean), "true",
                "显示托盘通知", SettingCategories.TRAY, currentTime),


            CreateRuntimeDefaultSetting(SettingKeys.THEME_MODE, nameof(ThemeMode), "Light",
                "主题模式", SettingCategories.UI, currentTime),


            CreateRuntimeDefaultSetting(SettingKeys.THEME_COLOR, nameof(ThemeColor), "Blue",
                "主题颜色", SettingCategories.UI, currentTime),


            CreateRuntimeDefaultSetting(SettingKeys.FONT_SIZE, nameof(Int32), "12",
                "字体大小", SettingCategories.UI, currentTime),


            CreateRuntimeDefaultSetting(SettingKeys.LANGUAGE, nameof(String), "zh-CN",
                "界面语言", SettingCategories.UI, currentTime),


            CreateRuntimeDefaultSetting(SettingKeys.AUTO_STARTUP, nameof(Boolean), "false",
                "开机自启动", SettingCategories.APPLICATION, currentTime)
        ];
    }

    /// <summary>
    /// 创建运行时默认设置项（不包含ID）
    /// </summary>
    /// <param name="key">设置键</param>
    /// <param name="valueType">值类型</param>
    /// <param name="value">默认值</param>
    /// <param name="description">描述</param>
    /// <param name="category">分类</param>
    /// <param name="currentTime">当前时间</param>
    /// <returns>设置项</returns>
    private static SettingItem CreateRuntimeDefaultSetting(string key, string valueType, string value,
        string description, string category, DateTime currentTime) => new()
        {
            Key = key,
            ValueType = valueType,
            Value = value,
            Description = description,
            Category = category,
            UserId = null, // 全局设置
            CreatedAt = currentTime,
            UpdatedAt = currentTime
        };

    #endregion
}