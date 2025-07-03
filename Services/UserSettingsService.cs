using DrugSearcher.Common.Enums;
using DrugSearcher.Data;
using DrugSearcher.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DrugSearcher.Services;

/// <summary>
/// 用户设置服务，提供设置的读取、保存、缓存和管理功能
/// 支持异步操作和实时通知
/// </summary>
public class UserSettingsService : IUserSettingsService, INotifyPropertyChanged
{
    #region 私有字段

    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IDefaultSettingsProvider _defaultSettingsProvider;
    private readonly Dictionary<string, SettingDefinition> _settingDefinitions;
    private readonly Dictionary<string, object?> _settingsCache;
    private readonly string? _currentUserId;
    private readonly SemaphoreSlim _cacheLock;
    private bool _isInitialized;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化用户设置服务
    /// </summary>
    /// <param name="dbContextFactory">数据库上下文工厂</param>
    /// <param name="defaultSettingsProvider">默认设置提供程序</param>
    public UserSettingsService(
        IApplicationDbContextFactory dbContextFactory,
        IDefaultSettingsProvider defaultSettingsProvider)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _defaultSettingsProvider = defaultSettingsProvider ?? throw new ArgumentNullException(nameof(defaultSettingsProvider));

        _settingDefinitions = [];
        _settingsCache = [];
        _cacheLock = new SemaphoreSlim(1, 1);
        _currentUserId = GetCurrentUserId();

        InitializeDefaultDefinitions();

        // 异步初始化数据库和加载设置
        _ = InitializeAsync();
    }

    #endregion

    #region 初始化方法

    /// <summary>
    /// 异步初始化数据库和设置
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            Console.WriteLine("正在初始化设置服务...");

            // 确保数据库已创建
            await _dbContextFactory.EnsureDatabaseCreatedAsync();

            // 加载用户设置
            await LoadUserSettingsFromDatabaseAsync();

            _isInitialized = true;
            Console.WriteLine("设置服务初始化完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"设置服务初始化失败: {ex.Message}");

            // 即使初始化失败，也加载默认设置并标记为已初始化
            LoadDefaultSettingsToCache();
            _isInitialized = true;
        }
    }

    /// <summary>
    /// 确保服务已初始化
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }
    }

    /// <summary>
    /// 获取当前用户ID
    /// </summary>
    /// <returns>用户ID，如果是全局设置则返回null</returns>
    private string? GetCurrentUserId()
    {
        // 这里可以根据你的认证系统实现
        // 目前返回null表示全局设置
        return null;
    }

    /// <summary>
    /// 初始化默认设置定义
    /// </summary>
    private void InitializeDefaultDefinitions()
    {
        var definitions = _defaultSettingsProvider.GetDefaultDefinitions();
        foreach (var definition in definitions)
        {
            _settingDefinitions[definition.Key] = definition;
        }
    }

    /// <summary>
    /// 将默认设置加载到缓存中
    /// </summary>
    private void LoadDefaultSettingsToCache()
    {
        _settingsCache.Clear();
        foreach (var definition in _settingDefinitions.Values)
        {
            if (definition.DefaultValue != null)
            {
                _settingsCache[definition.Key] = definition.DefaultValue;
            }
        }
        Console.WriteLine("已加载默认设置到缓存");
    }

    /// <summary>
    /// 从数据库加载用户设置到缓存
    /// </summary>
    private async Task LoadUserSettingsFromDatabaseAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var userSettings = await dbContext.Settings
                .Where(s => s.UserId == _currentUserId)
                .ToListAsync();

            _settingsCache.Clear();

            // 先加载默认设置
            LoadDefaultSettingsToCache();

            // 再用用户设置覆盖默认值
            foreach (var setting in userSettings)
            {
                var convertedValue = ConvertFromString(setting.Value, setting.ValueType);
                _settingsCache[setting.Key] = convertedValue;
            }

            Console.WriteLine($"已从数据库加载 {userSettings.Count} 个用户设置项");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"从数据库加载设置失败: {ex.Message}");
            // 发生错误时使用默认值
            LoadDefaultSettingsToCache();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    #endregion

    #region 设置读取方法

    /// <summary>
    /// 异步获取设置值
    /// </summary>
    /// <typeparam name="T">设置值类型</typeparam>
    /// <param name="key">设置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>设置值</returns>
    public async Task<T> GetSettingAsync<T>(string key, T defaultValue = default!)
    {
        ValidateSettingKey(key);
        await EnsureInitializedAsync();

        await _cacheLock.WaitAsync();
        try
        {
            // 首先从缓存获取
            if (_settingsCache.TryGetValue(key, out var cachedValue))
            {
                return ConvertValue(cachedValue, defaultValue);
            }

            // 缓存中没有，尝试从数据库直接加载
            var databaseValue = await LoadSettingFromDatabaseAsync(key);
            if (databaseValue != null)
            {
                _settingsCache[key] = databaseValue;
                return ConvertValue(databaseValue, defaultValue);
            }

            // 数据库中也没有，使用定义的默认值
            if (_settingDefinitions.TryGetValue(key, out var definition))
            {
                var definitionDefaultValue = ConvertValue(definition.DefaultValue, defaultValue);
                _settingsCache[key] = definitionDefaultValue;
                return definitionDefaultValue;
            }

            return defaultValue;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 同步获取设置值（仅从缓存读取）
    /// </summary>
    /// <typeparam name="T">设置值类型</typeparam>
    /// <param name="key">设置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>设置值</returns>
    public T GetSetting<T>(string key, T defaultValue = default!)
    {
        if (!_isInitialized)
            return defaultValue;

        if (_settingsCache.TryGetValue(key, out var cachedValue))
        {
            return ConvertValue(cachedValue, defaultValue);
        }

        // 如果缓存中没有，返回定义的默认值或传入的默认值
        return _settingDefinitions.TryGetValue(key, out var definition)
            ? ConvertValue(definition.DefaultValue, defaultValue)
            : defaultValue;
    }

    /// <summary>
    /// 从数据库加载单个设置
    /// </summary>
    /// <param name="key">设置键</param>
    /// <returns>设置值，如果不存在则返回null</returns>
    private async Task<object?> LoadSettingFromDatabaseAsync(string key)
    {
        try
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var setting = await dbContext.Settings
                .FirstOrDefaultAsync(s => s.Key == key && s.UserId == _currentUserId);

            return setting != null ? ConvertFromString(setting.Value, setting.ValueType) : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"从数据库获取设置 '{key}' 失败: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region 设置保存方法

    /// <summary>
    /// 异步设置值
    /// </summary>
    /// <typeparam name="T">设置值类型</typeparam>
    /// <param name="key">设置键</param>
    /// <param name="value">设置值</param>
    public async Task SetSettingAsync<T>(string key, T value)
    {
        ValidateSettingKey(key);
        await EnsureInitializedAsync();

        // 验证设置值
        ValidateSettingValue(key, value);

        await _cacheLock.WaitAsync();
        try
        {
            var oldValue = _settingsCache.TryGetValue(key, out var cached) ? cached : null;

            // 立即更新缓存
            _settingsCache[key] = value;

            // 尝试保存到数据库
            await SaveSettingToDatabaseAsync(key, value);

            // 触发变更事件
            OnSettingChanged(key, oldValue, value, typeof(T));
            OnPropertyChanged(key);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 同步设置值（立即更新缓存，异步保存到数据库）
    /// </summary>
    /// <typeparam name="T">设置值类型</typeparam>
    /// <param name="key">设置键</param>
    /// <param name="value">设置值</param>
    public void SetSetting<T>(string key, T value)
    {
        // 立即更新缓存
        _settingsCache[key] = value;
        OnPropertyChanged(key);

        // 异步保存到数据库
        _ = Task.Run(async () =>
        {
            try
            {
                await SetSettingAsync(key, value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"异步保存设置 '{key}' 失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 批量设置多个值
    /// </summary>
    /// <param name="settings">设置字典</param>
    public async Task SetSettingsAsync(Dictionary<string, object?> settings)
    {
        foreach (var kvp in settings)
        {
            if (kvp.Value != null)
            {
                await SetSettingAsync(kvp.Key, kvp.Value);
            }
        }
    }

    /// <summary>
    /// 保存设置到数据库
    /// </summary>
    /// <typeparam name="T">设置值类型</typeparam>
    /// <param name="key">设置键</param>
    /// <param name="value">设置值</param>
    private async Task SaveSettingToDatabaseAsync<T>(string key, T value)
    {
        try
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var existingSetting = await dbContext.Settings
                .FirstOrDefaultAsync(s => s.Key == key && s.UserId == _currentUserId);

            var valueType = typeof(T).Name;
            var valueString = ConvertToString(value);

            if (existingSetting != null)
            {
                // 更新现有设置
                existingSetting.Value = valueString;
                existingSetting.ValueType = valueType;
                existingSetting.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // 创建新设置
                var definition = _settingDefinitions.TryGetValue(key, out var def) ? def : null;
                var newSetting = new SettingItem
                {
                    Key = key,
                    ValueType = valueType,
                    Value = valueString,
                    UserId = _currentUserId,
                    Description = definition?.Description,
                    Category = definition?.Category,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Settings.Add(newSetting);
            }

            await dbContext.SaveChangesAsync();
            Debug.WriteLine($"已保存设置 '{key}' = '{value}' 到数据库");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存设置 '{key}' 到数据库失败: {ex.Message}");
            // 即使数据库保存失败，缓存更新仍然有效
        }
    }

    #endregion

    #region 设置查询和管理方法

    /// <summary>
    /// 检查设置是否存在
    /// </summary>
    /// <param name="key">设置键</param>
    /// <returns>如果设置存在则返回true</returns>
    public async Task<bool> HasSettingAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        await EnsureInitializedAsync();
        await _cacheLock.WaitAsync();
        try
        {
            if (_settingsCache.ContainsKey(key))
                return true;

            try
            {
                await using var dbContext = _dbContextFactory.CreateDbContext();
                return await dbContext.Settings.AnyAsync(s => s.Key == key && s.UserId == _currentUserId);
            }
            catch
            {
                return false;
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 删除设置
    /// </summary>
    /// <param name="key">设置键</param>
    public async Task DeleteSettingAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        await EnsureInitializedAsync();
        await _cacheLock.WaitAsync();
        try
        {
            // 从缓存中删除
            _settingsCache.Remove(key);

            // 从数据库中删除
            await DeleteSettingFromDatabaseAsync(key);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 从数据库删除设置
    /// </summary>
    /// <param name="key">设置键</param>
    private async Task DeleteSettingFromDatabaseAsync(string key)
    {
        try
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var setting = await dbContext.Settings
                .FirstOrDefaultAsync(s => s.Key == key && s.UserId == _currentUserId);

            if (setting != null)
            {
                dbContext.Settings.Remove(setting);
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"从数据库删除设置 '{key}' 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 按分类获取设置
    /// </summary>
    /// <param name="category">分类名称</param>
    /// <returns>该分类下的所有设置</returns>
    public async Task<Dictionary<string, object?>> GetSettingsByCategoryAsync(string category)
    {
        await EnsureInitializedAsync();

        try
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var categorySettings = await dbContext.Settings
                .Where(s => s.UserId == _currentUserId && s.Category == category)
                .ToListAsync();

            var result = new Dictionary<string, object?>();
            foreach (var setting in categorySettings)
            {
                var value = ConvertFromString(setting.Value, setting.ValueType);
                result[setting.Key] = value;
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取分类 '{category}' 的设置失败: {ex.Message}");

            // 从缓存中获取该分类的设置
            var result = new Dictionary<string, object?>();
            foreach (var kvp in _settingsCache)
            {
                if (_settingDefinitions.TryGetValue(kvp.Key, out var definition) &&
                    definition.Category == category)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// 获取所有设置
    /// </summary>
    /// <returns>所有设置的字典</returns>
    public async Task<Dictionary<string, object?>> GetAllSettingsAsync()
    {
        await EnsureInitializedAsync();
        await LoadUserSettingsFromDatabaseAsync();
        return new Dictionary<string, object?>(_settingsCache);
    }

    #endregion

    #region 设置定义管理方法

    /// <summary>
    /// 注册设置定义
    /// </summary>
    /// <param name="definition">设置定义</param>
    public Task RegisterSettingDefinitionAsync(SettingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _settingDefinitions[definition.Key] = definition;

        // 如果缓存中没有这个设置且定义有默认值，添加到缓存
        if (!_settingsCache.ContainsKey(definition.Key) && definition.DefaultValue != null)
        {
            _settingsCache[definition.Key] = definition.DefaultValue;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取设置定义
    /// </summary>
    /// <param name="key">设置键</param>
    /// <returns>设置定义，如果不存在则返回null</returns>
    public Task<SettingDefinition?> GetSettingDefinitionAsync(string key)
    {
        _settingDefinitions.TryGetValue(key, out var definition);
        return Task.FromResult(definition);
    }

    /// <summary>
    /// 获取所有设置定义
    /// </summary>
    /// <returns>所有设置定义的列表</returns>
    public Task<List<SettingDefinition>> GetSettingDefinitionsAsync()
    {
        return Task.FromResult(_settingDefinitions.Values.ToList());
    }

    #endregion

    #region 重置方法

    /// <summary>
    /// 重置所有设置为默认值
    /// </summary>
    public async Task ResetToDefaultsAsync()
    {
        await EnsureInitializedAsync();
        await _cacheLock.WaitAsync();
        try
        {
            // 删除数据库中的所有用户设置
            await DeleteAllUserSettingsFromDatabaseAsync();

            // 重置缓存为默认值
            LoadDefaultSettingsToCache();

            OnSettingsReloaded();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 重置指定分类的设置为默认值
    /// </summary>
    /// <param name="category">分类名称</param>
    public async Task ResetCategoryToDefaultsAsync(string category)
    {
        await EnsureInitializedAsync();
        await _cacheLock.WaitAsync();
        try
        {
            // 删除数据库中该分类的设置
            await DeleteCategorySettingsFromDatabaseAsync(category);

            // 重置缓存中该分类的设置
            ResetCategorySettingsInCache(category);

            OnSettingsReloaded();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 删除数据库中的所有用户设置
    /// </summary>
    private async Task DeleteAllUserSettingsFromDatabaseAsync()
    {
        try
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var userSettings = await dbContext.Settings
                .Where(s => s.UserId == _currentUserId)
                .ToListAsync();

            dbContext.Settings.RemoveRange(userSettings);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"重置数据库设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除数据库中指定分类的设置
    /// </summary>
    /// <param name="category">分类名称</param>
    private async Task DeleteCategorySettingsFromDatabaseAsync(string category)
    {
        try
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var categorySettings = await dbContext.Settings
                .Where(s => s.UserId == _currentUserId && s.Category == category)
                .ToListAsync();

            dbContext.Settings.RemoveRange(categorySettings);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"重置分类 '{category}' 的数据库设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 重置缓存中指定分类的设置
    /// </summary>
    /// <param name="category">分类名称</param>
    private void ResetCategorySettingsInCache(string category)
    {
        var keysToReset = _settingsCache.Keys
            .Where(key => _settingDefinitions.TryGetValue(key, out var def) && def.Category == category)
            .ToList();

        foreach (var key in keysToReset)
        {
            _settingsCache.Remove(key);
            if (_settingDefinitions.TryGetValue(key, out var definition) && definition.DefaultValue != null)
            {
                _settingsCache[key] = definition.DefaultValue;
            }
        }
    }

    #endregion

    #region 导入导出方法

    /// <summary>
    /// 导出设置为JSON字符串
    /// </summary>
    /// <returns>包含所有设置和定义的JSON字符串</returns>
    public async Task<string> ExportSettingsAsync()
    {
        var allSettings = await GetAllSettingsAsync();
        var exportData = new
        {
            ExportDate = DateTime.UtcNow,
            Settings = allSettings,
            Definitions = _settingDefinitions.Values.Select(d => new
            {
                d.Key,
                ValueType = d.ValueType.Name,
                d.DefaultValue,
                d.Description,
                d.Category,
                d.IsReadOnly
            })
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    /// <summary>
    /// 从JSON字符串导入设置
    /// </summary>
    /// <param name="json">包含设置的JSON字符串</param>
    public async Task ImportSettingsAsync(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("Settings", out var settingsElement))
            {
                var settings = new Dictionary<string, object?>();
                foreach (var property in settingsElement.EnumerateObject())
                {
                    var value = ExtractJsonValue(property.Value);
                    settings[property.Name] = value;
                }

                await SetSettingsAsync(settings);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("导入设置失败", ex);
        }
    }

    #endregion

    #region 验证和转换辅助方法

    /// <summary>
    /// 验证设置键的有效性
    /// </summary>
    /// <param name="key">设置键</param>
    private static void ValidateSettingKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("设置键不能为空", nameof(key));
    }

    /// <summary>
    /// 验证设置值的有效性
    /// </summary>
    /// <typeparam name="T">设置值类型</typeparam>
    /// <param name="key">设置键</param>
    /// <param name="value">设置值</param>
    private void ValidateSettingValue<T>(string key, T value)
    {
        if (_settingDefinitions.TryGetValue(key, out var definition))
        {
            if (definition.IsReadOnly)
                throw new InvalidOperationException($"设置 '{key}' 是只读的");

            if (definition.Validator != null && !definition.Validator(value))
                throw new ArgumentException($"设置值 '{value}' 不符合验证规则");
        }
    }

    /// <summary>
    /// 转换值到指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    private static T ConvertValue<T>(object? value, T defaultValue)
    {
        switch (value)
        {
            case null:
                return defaultValue;
            case T directValue:
                return directValue;
            default:
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                }
                catch
                {
                    return defaultValue;
                }
        }
    }

    /// <summary>
    /// 将字符串转换为指定类型的对象
    /// </summary>
    /// <param name="value">字符串值</param>
    /// <param name="valueType">目标类型名称</param>
    /// <returns>转换后的对象</returns>
    private static object? ConvertFromString(string? value, string valueType)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return valueType switch
        {
            nameof(Boolean) => bool.Parse(value),
            nameof(Int32) => int.Parse(value),
            nameof(Int64) => long.Parse(value),
            nameof(Double) => double.Parse(value, CultureInfo.InvariantCulture),
            nameof(Decimal) => decimal.Parse(value, CultureInfo.InvariantCulture),
            nameof(DateTime) => DateTime.Parse(value, CultureInfo.InvariantCulture),
            nameof(String) => value,
            nameof(ThemeMode) => Enum.Parse<ThemeMode>(value),
            nameof(ThemeColor) => Enum.Parse<ThemeColor>(value),
            _ => value
        };
    }

    /// <summary>
    /// 将对象转换为字符串表示
    /// </summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>字符串表示</returns>
    private static string? ConvertToString(object? value)
    {
        return value switch
        {
            null => null,
            bool b => b.ToString().ToLowerInvariant(),
            double d => d.ToString(CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            Enum e => e.ToString(),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// 从JSON元素提取值
    /// </summary>
    /// <param name="element">JSON元素</param>
    /// <returns>提取的值</returns>
    private static object? ExtractJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 设置值变更事件
    /// </summary>
    public event EventHandler<SettingChangedEventArgs>? SettingChanged;

    /// <summary>
    /// 设置重新加载事件
    /// </summary>
    public event EventHandler? SettingsReloaded;

    /// <summary>
    /// 属性变更事件
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 触发设置变更事件
    /// </summary>
    /// <param name="key">设置键</param>
    /// <param name="oldValue">旧值</param>
    /// <param name="newValue">新值</param>
    /// <param name="valueType">值类型</param>
    protected virtual void OnSettingChanged(string key, object? oldValue, object? newValue, Type valueType)
    {
        SettingChanged?.Invoke(this, new SettingChangedEventArgs(key, oldValue, newValue, valueType));
    }

    /// <summary>
    /// 触发设置重新加载事件
    /// </summary>
    protected virtual void OnSettingsReloaded()
    {
        SettingsReloaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 触发属性变更事件
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}