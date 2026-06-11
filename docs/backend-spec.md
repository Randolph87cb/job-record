# 工作时间记录 后端功能确认文档

相关文档：

- [Windows 安装与发布说明](D:\workspace\小工具\工作时间记录\docs\release-packaging.md)

## 1. 文档目标

本文档用于确认第一版产品的后端职责与业务规则，作为后续 WPF 客户端开发的数据与逻辑依据。

这里的“后端”指：

- 本地数据存储
- 任务与计时业务逻辑
- 统计汇总逻辑
- 设置与状态持久化

不包含：

- 云端服务
- 多端同步
- 用户账号体系
- 团队协作能力

## 2. 版本范围

第一版定位为单机版 Windows 桌面工具。

推荐技术边界：

- 客户端：`.NET 8 + WPF`
- 本地数据库：`SQLite`
- 数据访问：可用 `Entity Framework Core`，也可直接用轻量 ORM

第一版默认离线可用，所有核心功能不依赖网络。

## 3. 核心业务目标

后端需要保证以下核心行为稳定成立：

1. 用户可以创建多个任务。
2. 同一时刻只能有一个任务处于进行中。
3. 任务的实际工作时间按多个时间段记录，而不是只记录一个累计值。
4. 用户切换任务时，系统会自动结束上一个任务的当前计时段，并开始新任务的计时段。
5. 用户可以随时查看今日总时长、单任务累计时长和完成情况。
6. 用户可以为任务配置子任务，并查看子任务耗时与完成时间。
7. 应用重启后，任务状态、计时信息、布局设置和历史记录不会丢失。

## 4. 后端模块划分

建议拆成以下模块：

### 4.1 任务模块

负责：

- 创建任务
- 创建子任务
- 编辑任务基础信息
- 删除未使用任务或归档任务
- 查询任务列表
- 改变任务状态

### 4.2 计时模块

负责：

- 开始任务
- 暂停任务
- 恢复任务
- 完成任务
- 切换任务
- 处理当前进行中计时段

### 4.3 统计模块

负责：

- 统计今日总工作时长
- 统计单任务累计时长
- 统计单子任务累计时长
- 统计任务完成数量
- 统计高优任务耗时
- 输出按日汇总数据

### 4.4 设置模块

负责：

- 保存布局模式
- 保存悬浮条位置与尺寸
- 保存自动收起参数
- 保存开机自启等偏好设置

### 4.5 托盘与运行状态模块

负责：

- 持久化当前悬浮条是否显示
- 记录上次退出时的运行状态
- 在程序重启后恢复必要状态

## 5. 数据模型

## 5.1 Task

表示一个工作任务。

字段建议：

- `Id`
- `Title`
- `Priority`
- `Status`
- `EstimateMinutes`
- `Notes`
- `CreatedAt`
- `UpdatedAt`
- `StartedAt`
- `CompletedAt`
- `SortOrder`
- `IsArchived`

字段说明：

- `Priority`：`P1 / P2 / P3`
- `Status`：`Pending / Running / Paused / Completed`
- `StartedAt`：首次开始时间，可为空
- `CompletedAt`：完成时间，可为空
- `SortOrder`：用于列表手动排序

## 5.2 TimeEntry

表示任务的一段连续工作时间。

字段建议：

- `Id`
- `TaskId`
- `SubTaskId`
- `StartAt`
- `EndAt`
- `DurationSeconds`
- `EntryType`
- `CreatedAt`

字段说明：

- `TaskId`：关联任务
- `SubTaskId`：可选，关联子任务；为空表示该时间段只归属于父任务，未细分到子任务
- `StartAt`：计时开始时间
- `EndAt`：计时结束时间，进行中时可为空
- `DurationSeconds`：结束时写入，便于统计
- `EntryType`：第一版可保留扩展位，默认 `Manual`

说明：

- 第一版统计应以 `TimeEntry` 为准，不以界面显示时间为准。
- 每次开始、恢复、切换任务，都会新增一条 `TimeEntry`。
- 父任务总耗时按 `TaskId` 汇总；子任务明细耗时按 `SubTaskId` 汇总。

## 5.3 SubTask

表示某个任务下的细分工作项。

字段建议：

- `Id`
- `TaskId`
- `Title`
- `Status`
- `EstimateMinutes`
- `Notes`
- `CreatedAt`
- `UpdatedAt`
- `StartedAt`
- `CompletedAt`
- `SortOrder`
- `IsArchived`

字段说明：

- `TaskId`：所属父任务
- `Status`：`Pending / Running / Paused / Completed`
- `StartedAt`：首次开始时间，可为空
- `CompletedAt`：子任务完成时间，可为空
- `SortOrder`：用于父任务内的子任务排序

说明：

- 子任务不进入顶层任务列表。
- 同一时刻仍然只允许一个 `TimeEntry.EndAt = null`。
- 子任务计时时，`TimeEntry.TaskId` 和 `TimeEntry.SubTaskId` 同时写入，确保父任务总耗时和子任务明细可分别统计。

## 5.4 AppSettings

表示应用级设置。

字段建议：

- `Id`
- `DockMode`
- `BarWidth`
- `BarHeight`
- `MarginTop`
- `MarginSide`
- `AutoCollapseEnabled`
- `AutoCollapseSeconds`
- `LaunchAtStartup`
- `MinimizeToTray`
- `Theme`
- `UpdatedAt`

字段说明：

- `DockMode`：`TopCenter / LeftEdge / RightEdge`
- `Theme`：第一版可先只支持 `System`

## 5.5 RuntimeState

表示程序恢复时需要的运行状态。

字段建议：

- `Id`
- `CurrentTaskId`
- `IsBarVisible`
- `IsExpanded`
- `LastActiveAt`
- `UpdatedAt`

说明：

- `CurrentTaskId` 用于恢复当前上下文。
- 如果应用异常退出，启动时需要根据该表与未结束的 `TimeEntry` 做修正。

## 6. 核心状态机

任务状态流转规则如下：

- `Pending -> Running`
- `Running -> Paused`
- `Paused -> Running`
- `Running -> Completed`
- `Paused -> Completed`

第一版不支持：

- `Completed -> Running`
- `Completed -> Pending`

如需“重新开启已完成任务”，建议后续版本通过“复制任务”处理，而不是回退状态。

## 7. 核心业务规则

## 7.1 只允许一个任务进行中

这是系统最高优先级约束。

规则：

- 任意时刻只能有一个 `Task.Status = Running`
- 任意时刻只能有一个 `TimeEntry.EndAt = null`
- 后端在开始任务前必须先检查是否已有运行中任务

如果存在运行中任务：

- 若用户点的是当前任务，则忽略重复开始
- 若用户点的是其他任务，则执行“切换任务”

## 7.2 开始任务

输入：

- `TaskId`

行为：

1. 校验任务存在且未完成。
2. 若已有其他任务运行中，则先结束那个任务的当前时间段。
3. 将目标任务状态改为 `Running`。
4. 若任务从未开始过，写入 `StartedAt`。
5. 新增一条 `TimeEntry`，其 `StartAt = 当前时间`，`EndAt = null`。
6. 更新 `RuntimeState.CurrentTaskId`。

## 7.3 暂停任务

输入：

- `TaskId`

行为：

1. 校验该任务当前为 `Running`。
2. 找到该任务未结束的 `TimeEntry`。
3. 写入 `EndAt = 当前时间`。
4. 计算并写入 `DurationSeconds`。
5. 将任务状态改为 `Paused`。
6. 清空 `RuntimeState.CurrentTaskId`。

## 7.4 恢复任务

输入：

- `TaskId`

行为：

1. 校验任务当前为 `Paused`。
2. 若其他任务正在运行，则先结束其他任务。
3. 将当前任务状态改为 `Running`。
4. 新增一条新的 `TimeEntry`。

注意：

- 恢复任务不是续写旧记录，而是新开一条时间段。

## 7.5 完成任务

输入：

- `TaskId`

行为：

1. 检查该任务下是否存在未完成子任务。
2. 若存在未完成子任务，拒绝完成父任务。
3. 若任务处于 `Running`，先结束其未完成时间段。
4. 若任务处于 `Paused`，不再新建时间段。
5. 将任务状态改为 `Completed`。
6. 写入 `CompletedAt`。
7. 清空 `RuntimeState.CurrentTaskId`。

## 7.6 切换任务

输入：

- `FromTaskId`
- `ToTaskId`

行为：

1. 若 `FromTaskId` 处于运行中，则先执行暂停的底层收口动作，但状态不记为用户手动暂停，可直接转为 `Pending` 或保持 `Paused`。
2. 建议第一版统一转为 `Paused`，便于用户理解。
3. 对 `ToTaskId` 执行开始任务逻辑。

建议：

- 第一版所有切换后的旧任务状态统一设为 `Paused`，规则清楚，便于前端展示。

## 7.7 编辑任务

允许编辑：

- 标题
- 优先级
- 预估时长
- 备注
- 排序

限制：

- 已完成任务默认允许编辑标题和备注，但不建议改动状态
- 正在运行的任务不允许删除

## 7.8 删除任务

第一版建议采用“归档”而不是物理删除。

规则：

- 已产生 `TimeEntry` 的任务，删除动作默认改为 `IsArchived = true`
- 从未开始且无历史记录的任务，允许物理删除

这样可以避免统计数据断裂。

## 7.9 创建子任务

输入：

- `TaskId`
- `Title`

行为：

1. 校验父任务存在且未归档。
2. 校验子任务标题不为空且不超过长度限制。
3. 在父任务内追加 `SortOrder`。
4. 新建 `SubTask`，默认状态为 `Pending`。
5. 更新父任务 `UpdatedAt`。

## 7.10 开始子任务

输入：

- `TaskId`
- `SubTaskId`

行为：

1. 校验父任务存在、未归档且未完成。
2. 校验子任务属于该父任务、未归档且未完成。
3. 若已有其他任务或子任务正在计时，先关闭其当前 `TimeEntry`。
4. 将父任务和目标子任务状态改为 `Running`。
5. 若父任务或子任务从未开始过，分别写入 `StartedAt`。
6. 新增一条 `TimeEntry`，同时写入 `TaskId` 和 `SubTaskId`。
7. 更新 `RuntimeState.CurrentTaskId` 为父任务 `Id`。

## 7.11 暂停子任务

输入：

- `SubTaskId`

行为：

1. 校验子任务当前为 `Running`。
2. 关闭父任务当前未结束的 `TimeEntry`。
3. 将子任务状态改为 `Paused`。
4. 将父任务状态改为 `Paused`。
5. 清空 `RuntimeState.CurrentTaskId`。

## 7.12 完成子任务

输入：

- `SubTaskId`

行为：

1. 若子任务处于 `Running`，先关闭父任务当前未结束的 `TimeEntry`。
2. 将子任务状态改为 `Completed`。
3. 写入子任务 `CompletedAt`。
4. 若子任务完成前处于运行中，将父任务状态改为 `Paused`，避免父任务仍显示运行但没有活动计时段。

说明：

- 完成子任务不会自动完成父任务。
- 父任务完成前必须先完成其所有未归档子任务。

## 8. 统计规则

## 8.1 今日总工作时长

定义：

- 统计自然日内所有 `TimeEntry` 的有效时长之和

注意：

- 如果一个时间段跨天，需要按日期拆分统计，不可全部算到开始那天。

## 8.2 单任务累计时长

定义：

- 某任务所有 `TimeEntry.DurationSeconds` 的总和

## 8.3 单子任务累计时长

定义：

- 某子任务所有 `TimeEntry.SubTaskId = SubTask.Id` 的有效时长之和

说明：

- 如果某些时间段没有 `SubTaskId`，这些时间段只计入父任务总耗时，不计入任何子任务明细。

## 8.4 今日完成任务数

定义：

- `CompletedAt` 落在今日范围内的任务数量

## 8.5 优先级耗时统计

定义：

- 按 `P1 / P2 / P3` 汇总对应任务的累计时长

用途：

- 便于后续扩展“高优任务时间是否被挤占”

## 8.6 当前任务实时显示时间

定义：

- 若任务进行中，界面显示时间 = 历史累计时长 + 当前未结束时间段的实时差值
- 若当前正在展示子任务，界面可显示子任务累计时长 + 子任务当前未结束时间段的实时差值

注意：

- 实时值可以在前端刷新，但最终统计写库时必须由后端逻辑收口。

## 9. 异常与边界场景

## 9.1 应用异常退出

场景：

- 用户任务进行中，应用被强制关闭或系统重启

建议规则：

1. 启动时检查是否存在 `EndAt = null` 的 `TimeEntry`
2. 若存在，按“异常结束”处理
3. 默认将结束时间补为“本次启动时间”
4. 任务状态改为 `Paused`
5. 如果未结束时间段关联了子任务，对应子任务状态也改为 `Paused`

说明：

- 这样虽然不完美，但比丢失整段记录更稳定
- 后续版本可增加“恢复上次计时”提示

## 9.2 重复点击开始

规则：

- 若目标任务已在运行中，直接忽略
- 不新建重复 `TimeEntry`

## 9.3 系统时间变化

场景：

- 用户手动改系统时间
- 系统休眠恢复

第一版建议：

- 以本地系统时间为准
- 若发现 `EndAt < StartAt`，记为异常记录并在日志中保留
- 第一版不处理跨时区复杂场景

## 9.4 任务标题为空

规则：

- 不允许创建空标题任务
- 可限制标题长度，例如 `1-80` 字符

## 9.5 任务重复命名

规则：

- 允许重名
- 通过 `Id` 区分

## 10. 对前端暴露的业务服务

建议以后端服务类方式组织，而不是先做 HTTP API。

推荐服务：

### 10.1 TaskService

职责：

- 创建任务
- 创建子任务
- 编辑任务
- 查询任务列表
- 查询子任务列表
- 查询当前任务
- 归档任务

建议方法：

- `CreateTask`
- `CreateSubTask`
- `UpdateTask`
- `GetTaskById`
- `GetActiveTask`
- `GetTaskList`
- `GetSubTasks`
- `ArchiveTask`

### 10.2 TimerService

职责：

- 开始
- 暂停
- 恢复
- 完成
- 切换
- 开始子任务
- 暂停子任务
- 恢复子任务
- 完成子任务

建议方法：

- `StartTask`
- `PauseTask`
- `ResumeTask`
- `CompleteTask`
- `SwitchTask`
- `StartSubTask`
- `PauseSubTask`
- `ResumeSubTask`
- `CompleteSubTask`

### 10.3 StatisticsService

职责：

- 今日汇总
- 任务时长统计
- 子任务时长统计
- 优先级统计

建议方法：

- `GetTodaySummary`
- `GetTaskDuration`
- `GetSubTaskDuration`
- `GetTodayCompletedTasks`
- `GetPrioritySummary`

### 10.4 SettingsService

职责：

- 获取设置
- 保存设置
- 持久化布局参数

建议方法：

- `GetSettings`
- `SaveSettings`
- `UpdateDockMode`
- `UpdateBarLayout`

## 11. 建议的数据表

第一版建议至少包含：

- `Tasks`
- `SubTasks`
- `TimeEntries`
- `AppSettings`
- `RuntimeState`

如后续需要审计，可再补：

- `OperationLogs`

第一版不是必需。

## 12. 第一版不做的后端能力

以下能力明确不纳入第一版：

- 远程同步
- 多设备数据合并
- 账号登录
- 团队共享任务
- 自动识别当前软件并归类任务
- 标签树、子任务树
- 周报、月报、导出 Excel
- 提醒推送服务
- 复杂权限系统

## 13. 验收标准

后端部分完成后，至少应满足以下验证结果：

1. 创建多个任务后，只能启动其中一个。
2. 启动 A 任务后再启动 B 任务，A 自动停止，B 自动开始。
3. 暂停后恢复，会新增新的 `TimeEntry`。
4. 完成任务后，不再存在未结束的 `TimeEntry`。
5. 重启应用后，历史任务和累计时长仍然存在。
6. 今日统计与各任务累计时长可正确计算。
7. 应用异常退出后，未结束时间段能被修正，不出现脏状态。

## 14. 下一步建议

基于这份文档，下一步可继续落以下内容：

1. 数据库表结构设计
2. WPF 项目目录结构
3. 领域模型与服务接口定义
4. 顶部横条与侧边悬浮条的状态联动设计
