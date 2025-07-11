# 药智数据网络爬虫使用指南

## 概述

本项目实现了一个用于爬取药智数据网站（https://www.yaozs.com/）药物说明书的网络爬虫。该爬虫具有以下特性：

- 支持单个药物和批量药物信息获取
- 实现了请求频率控制和礼貌性爬取
- 包含缓存机制避免重复请求
- 支持错误处理和重试机制
- 遵循robots.txt规则
- 提供完整的依赖注入支持

## 核心组件

### 1. 模型类

#### DrugInfo
扩展了药物信息模型，支持以下字段：
- 基本信息：药物名称、通用名称、商品名称、汉语拼音
- 审批信息：批准文号、药品分类、药品性质
- 详细信息：主要成份、适应症、用法用量、不良反应
- 注意事项：禁忌、注意事项、药物相互作用
- 其他信息：贮藏、包装、有效期、执行标准

#### DrugCrawlerConfiguration
爬虫配置类，支持以下配置：
- 请求间隔控制
- 超时设置
- 重试机制
- 缓存配置
- 并发限制

### 2. 服务类

#### IOnlineDrugService
在线药物服务接口，提供以下方法：
- `SearchOnlineDrugsAsync` - 按关键词搜索
- `GetDrugDetailByIdAsync` - 按ID获取详情
- `GetDrugDetailByExternalIdAsync` - 按外部ID获取详情
- `GetDrugsBatchAsync` - 批量获取
- `GetAvailableDrugCountAsync` - 获取可用总数

#### YaoZsOnlineDrugService
药智数据爬虫实现，特性：
- 多种HTML解析策略（表格、定义列表、Div结构）
- 智能文本清理和数据验证
- 请求频率控制
- 并发控制
- 缓存支持
- 错误处理和重试

## 使用方法

### 1. 依赖注入配置

```csharp
using DrugSearcher.Configuration;

// 在 App.xaml.cs 或其他启动类中
var services = new ServiceCollection();

// 添加爬虫服务
services.AddCrawlerServices(config =>
{
    config.RequestIntervalMs = 1000;     // 1秒间隔
    config.MaxConcurrentRequests = 3;    // 最大并发数
    config.EnableLogging = true;         // 启用日志
    config.EnableCache = true;           // 启用缓存
    config.RespectRobotsTxt = true;      // 遵守robots.txt
    config.RetryCount = 3;               // 重试次数
});

var serviceProvider = services.BuildServiceProvider();
```

### 2. 基本使用示例

```csharp
// 获取服务
var drugService = serviceProvider.GetRequiredService<IOnlineDrugService>();

// 搜索药物
var searchResults = await drugService.SearchOnlineDrugsAsync("对乙酰氨基酚");

// 获取特定药物
var drug = await drugService.GetDrugDetailByExternalIdAsync("sms000001");

// 批量获取
var batchResults = await drugService.GetDrugsBatchAsync(1, 100);
```

### 3. 批量同步示例

```csharp
public async Task<List<DrugInfo>> BatchSyncDrugs(
    IProgress<string> progress,
    CancellationToken cancellationToken = default)
{
    var drugService = serviceProvider.GetRequiredService<IOnlineDrugService>();
    var allDrugs = new List<DrugInfo>();
    const int batchSize = 100;
    const int maxId = 124051;

    for (int startId = 1; startId <= maxId; startId += batchSize)
    {
        if (cancellationToken.IsCancellationRequested)
            break;

        var endId = Math.Min(startId + batchSize - 1, maxId);
        progress.Report($"正在同步 {startId} - {endId} 范围内的药物数据...");
        
        var batchResults = await drugService.GetDrugsBatchAsync(startId, endId, cancellationToken);
        allDrugs.AddRange(batchResults);

        var currentProgress = (double)endId / maxId * 100;
        progress.Report($"已完成 {currentProgress:F1}%，共获取 {allDrugs.Count} 个药物");
    }

    return allDrugs;
}
```

## 配置说明

### 基础配置
- `BaseUrl`: 基础URL（默认：https://www.yaozs.com/）
- `UserAgent`: 用户代理字符串
- `RequestIntervalMs`: 请求间隔（毫秒）
- `RequestTimeoutMs`: 请求超时时间（毫秒）

### 重试和错误处理
- `RetryCount`: 重试次数（默认：3）
- `RetryIntervalMs`: 重试间隔（毫秒）

### 缓存配置
- `EnableCache`: 是否启用缓存（默认：true）
- `CacheExpirationHours`: 缓存过期时间（小时）

### 并发控制
- `MaxConcurrentRequests`: 最大并发请求数（默认：5）

### 礼貌性爬取
- `RespectRobotsTxt`: 是否遵循robots.txt（默认：true）
- `EnableLogging`: 是否启用日志（默认：true）

## 注意事项

1. **请求频率**: 建议设置适当的请求间隔（1-2秒），避免对目标网站造成过大压力
2. **错误处理**: 爬虫具有完整的错误处理机制，网络异常时会自动重试
3. **数据验证**: 所有爬取的数据都经过清理和验证
4. **缓存使用**: 启用缓存可以显著提高性能，避免重复请求
5. **robots.txt**: 默认遵守网站的robots.txt规则，确保合规性

## 扩展性

该爬虫设计支持扩展：
- 可以轻松添加新的HTML解析策略
- 支持自定义数据清理规则
- 可以扩展支持其他药物数据网站
- 提供完整的接口抽象，便于测试和替换实现

## 测试

项目包含完整的测试示例，可以通过以下方式运行：

```bash
dotnet run --project DrugSearcher.csproj
```

测试程序会验证爬虫的基本功能，包括单个药物获取和批量获取。