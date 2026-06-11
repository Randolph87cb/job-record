# 工作时间记录 Windows 安装与发布说明

## 1. 文档目标

本文档说明当前项目在 Windows 下的打包、安装和发布约定，覆盖以下内容：

- 应用发布产物生成方式
- `Inno Setup` 安装包生成方式
- 版本号来源规则
- 安装器自定义图标与版本信息
- 开机自动启动安装选项

本文档面向维护者，不面向普通最终用户。

## 2. 当前发布方案

当前使用两层发布链路：

1. `dotnet publish`
2. `Inno Setup` 生成安装器

相关文件：

- 打包脚本：[build-installer.ps1](D:\workspace\小工具\工作时间记录\scripts\build-installer.ps1)
- Inno 安装脚本：[JobRecord.iss](D:\workspace\小工具\工作时间记录\installer\JobRecord.iss)
- Inno 自动安装脚本：[install-inno-setup.ps1](D:\workspace\小工具\工作时间记录\scripts\install-inno-setup.ps1)
- 临时品牌图标：[JobRecord.ico](D:\workspace\小工具\工作时间记录\assets\branding\JobRecord.ico)

## 3. 打包命令

在项目根目录执行：

```powershell
& ".\scripts\build-installer.ps1" -Version 1.2.3
```

如果当前提交已经打了精确版本标签，也可以不传 `-Version`：

```powershell
& ".\scripts\build-installer.ps1"
```

脚本会自动执行：

1. 检查并定位 `ISCC.exe`
2. 本机缺少 `Inno Setup` 时自动下载安装
3. 执行 `dotnet publish`
4. 调用 `ISCC.exe` 编译安装包

## 4. 版本号规则

版本优先级固定如下：

1. 显式传入的 `-Version`
2. 当前 `HEAD` 上的精确 Git tag
3. 若以上都没有，脚本直接失败

支持的 Git tag 格式：

- `v1.2.3`
- `1.2.3`
- `v1.2.3-beta.1`
- `1.2.3-beta.1`

说明：

- 只接受当前提交上的精确标签，不接受“最近标签”
- 不传 `-Version` 且当前提交没有精确标签时，不允许继续打 `Release`

版本映射规则：

- 展示版本：`1.2.3` 或 `1.2.3-beta.1`
- `AssemblyVersion`：`1.2.3.0`
- `FileVersion`：`1.2.3.0`
- `InformationalVersion`：`1.2.3+sha.<shortSha>`
- 安装器数值版本：`1.2.3.0`

## 5. 产物位置

发布目录：

```text
artifacts/publish/win-x64
```

安装包目录：

```text
artifacts/installer
```

例如：

- [JobRecord-Setup-1.2.3.exe](D:\workspace\小工具\工作时间记录\artifacts\installer\JobRecord-Setup-1.2.3.exe)

## 6. 图标与版本信息

当前应用和安装器使用同一份图标资源：

- [JobRecord.ico](D:\workspace\小工具\工作时间记录\assets\branding\JobRecord.ico)

其中：

- 应用 `exe` 图标由 [JobRecord.App.csproj](D:\workspace\小工具\工作时间记录\src\JobRecord.App\JobRecord.App.csproj) 的 `ApplicationIcon` 指定
- 安装器图标由 [JobRecord.iss](D:\workspace\小工具\工作时间记录\installer\JobRecord.iss) 的 `SetupIconFile` 指定

公共版本元数据集中在：

- [Directory.Build.props](D:\workspace\小工具\工作时间记录\Directory.Build.props)

当前对外可见的关键信息包括：

- 产品名：`工作时间记录`
- 公司：`Randolph87cb`
- 文件版本：来自脚本解析后的数值版本
- 产品版本：展示版本或带短 SHA 的信息版本

## 7. 安装器行为

安装器默认按“当前用户安装”工作：

- 安装目录：`%LocalAppData%\Programs\工作时间记录`
- 不要求管理员权限
- 支持开始菜单快捷方式
- 可选桌面快捷方式

安装器默认提供并勾选：

- `开机自动启动`

实现方式：

- 写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- 值名：`工作时间记录`
- 值内容：安装目录下的 `JobRecord.App.exe`

如果安装时取消勾选：

- 不写入启动项

卸载时：

- 自动删除该注册表值

## 8. 验证建议

每次调整打包链路后，至少验证以下内容：

1. 显式版本打包成功
2. 无 tag 且未传 `-Version` 时脚本失败
3. 精确 tag 自动取版本成功
4. 安装器图标显示正常
5. 应用 `exe` 图标显示正常
6. 文件版本与产品版本正确
7. 默认安装会写入开机启动项
8. 取消开机启动任务后不会写入启动项
9. 卸载后启动项被清理

可用的验证方式包括：

- `Get-Item <exe>.VersionInfo`
- 查看 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- 静默安装 / 静默卸载

## 9. 后续可扩展项

后续如果需要继续增强，可考虑：

- 安装器中文语言包
- 正式品牌图标替换
- Git tag 后自动打包
- 发布流程自动生成版本说明
