# TraeCheckIn

Trae（trae.cn）自动签到服务：基于 .NET 8 Worker Service 的定时签到工具，支持多账号、多时间点调度、Token 有效期预警与邮件通知，可安装为 Windows 服务长期驻留运行。

## 功能特性

- **定时签到**：支持每日多个触发时间点（`Schedule:Times`，`HH:mm` 格式）
- **单次执行模式**：通过 `--run-once` 参数立即签到一次后退出，适合配合系统计划任务使用
- **错过补偿**：服务启动时若当天时间点已全部错过，自动补检一次（内部当日去重，已签到自动跳过）
- **多账号支持**：`TraeAccount:Accounts` 配置多个账号，每个账号可绑定独立的设备身份请求头（`x-device-id` 等）
- **双认证方式**：Token（浏览器抓取凭证）或 Password（账号密码登录换取凭证）
- **Token 有效期预警**：启动时解析 JWT 过期时间，凭证临近过期时在日志中提前告警
- **失败重试**：可配置最大重试次数与重试间隔（`MaxRetry` / `RetryDelaySeconds`）
- **状态持久化**：签到状态保存至本地 JSON 文件，重启后当日去重依然生效
- **通知**：日志通知 + SMTP 邮件通知，成功/失败可分别开关
- **结构化日志**：Serilog 输出到控制台与按天滚动日志文件（默认保留 30 天）
- **Windows 服务**：原生支持 `sc.exe` 安装为系统服务

## 项目结构

解决方案采用 DDD 分层架构，依赖方向单向：`Worker → Application / Infrastructure → Domain`。

```
src/
├── TraeCheckIn.Domain/           # 领域层：实体、枚举、业务异常、JWT 解析（零外部依赖）
├── TraeCheckIn.Application/      # 应用层：签到用例编排、认证服务、接口定义、配置模型
├── TraeCheckIn.Infrastructure/   # 基础设施层：HTTP 客户端、状态存储、调度计算、通知实现
└── TraeCheckIn.Worker/           # 宿主层：BackgroundService 调度循环、DI 注册、程序入口
```

| 项目 | 职责 | 关键类型 |
|------|------|---------|
| TraeCheckIn.Domain | 领域模型与业务规则 | `CheckInRecord`、`CheckInStatus`、`JwtTokenInspector` |
| TraeCheckIn.Application | 用例编排与抽象接口 | `CheckInAppService`、`CheckInExecutor`、`AuthService` |
| TraeCheckIn.Infrastructure | 技术实现 | `TraeApiClient`、`JsonCheckInStateStore`、`NextRunCalculator`、`EmailNotifier` |
| TraeCheckIn.Worker | 宿主与调度 | `CheckInWorker`、`Program` |

## 快速开始

### 环境要求

- .NET SDK 8.0

### 配置

1. 复制示例配置为正式配置：

```powershell
Copy-Item src\TraeCheckIn.Worker\appsettings.example.json src\TraeCheckIn.Worker\appsettings.json
```

2. 编辑 `appsettings.json`，至少填写账号凭证。**Token 方式**：登录 trae.cn 后从浏览器开发者工具中复制 Cookie 凭证：

```json
{
  "TraeAccount": {
    "AuthType": "Token",
    "Token": "n_mh=xxx; sessionid=xxx; ..."
  }
}
```

3. 多账号配置示例（每个账号建议配置独立请求头，保证 `x-device-id` 等身份头互不相同）：

```json
{
  "TraeAccount": {
    "Accounts": [
      {
        "Name": "账号A",
        "AuthType": "Token",
        "Token": "n_mh=xxx; ...",
        "Headers": {
          "x-device-id": "设备A的ID"
        }
      },
      {
        "Name": "账号B",
        "AuthType": "Token",
        "Token": "n_mh=yyy; ...",
        "Headers": {
          "x-device-id": "设备B的ID"
        }
      }
    ]
  }
}
```

> 安全提示：凭证与密码属于敏感信息，请勿将 `appsettings.json` 提交到仓库。开发环境可使用用户机密（User Secrets）或环境变量注入，例如 `dotnet user-secrets set "TraeAccount:Token" "xxx"`。

### 构建与运行

```powershell
# 构建
dotnet build TraeCheckIn.sln

# 前台调试运行（按配置时间点定时签到）
dotnet run --project src\TraeCheckIn.Worker

# 单次执行模式：立即签到一次后退出
dotnet run --project src\TraeCheckIn.Worker -- --run-once

# 通过命令行覆盖签到时间点
dotnet run --project src\TraeCheckIn.Worker -- --Schedule:Times=09:30,14:00
```

### 发布与安装为 Windows 服务

```powershell
# 发布（自包含可选）
dotnet publish src\TraeCheckIn.Worker -c Release -o publish

# 安装并启动 Windows 服务（以管理员身份运行 PowerShell）
sc.exe create TraeCheckIn binPath= "D:\path\to\publish\TraeCheckIn.Worker.exe" start= auto
sc.exe start TraeCheckIn

# 卸载
sc.exe stop TraeCheckIn
sc.exe delete TraeCheckIn
```

## 配置说明

| 配置节 | 说明 | 关键项 |
|--------|------|--------|
| `TraeAccount` | 账号凭证 | `AuthType`（Token/Password）、`Token`、`Username`/`Password`、`Headers`（账号专属请求头）、`Accounts`（多账号列表） |
| `Http` | HTTP 客户端行为 | `BaseUrl`、`UserAgent`、`Headers`（全局请求头）、`TimeoutSeconds` |
| `CheckIn` | 签到流程 | `SuccessKeywords`、`AlreadyCheckedKeywords`、`MaxRetry`、`RetryDelaySeconds`、`StateFilePath` |
| `Schedule` | 调度时间点 | `Times`（`HH:mm` 数组，可配置多个） |
| `Notification` | 通知开关 | `NotifyOnSuccess` / `NotifyOnFailure`、`EnableEmail`、`Smtp`（SMTP 发件配置） |

## 日志

- 运行日志写入程序目录下 `logs/checkin-yyyyMMdd.log`，按天滚动，默认保留 30 天
- 签到结果、下一次触发时间、Token 过期预警等关键信息均有结构化记录

## 状态文件

- 签到状态默认保存在 `state/checkin-state.json`（相对程序运行目录，可通过 `CheckIn:StateFilePath` 调整）
- 用于记录每个账号当日签到结果，实现当日去重与重启补偿；删除该文件不会影响签到功能本身
