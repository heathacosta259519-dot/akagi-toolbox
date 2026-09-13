# 右键菜单编辑器（Context Menu Editor）

> 把不想要的 Windows 右键菜单项**隐藏**掉，随时再**恢复**。所有操作非破坏性、可逆——本工具不删除任何菜单项注册表键。

## 两种模式

### 简单模式（默认）

![简单模式界面](docs/simple-mode.png)

不用理解任何注册表概念，直接对着"你右键时看到的菜单"操作：

- 先选场景：**文件右键 / 文件夹右键 / 文件夹空白处 / 桌面空白处 / 驱动器右键**
- 列表里就是该场景下真实出现的菜单项：显示文字、图标、来源程序一目了然
- **取消勾选 = 隐藏，重新勾选 = 恢复**；只看已隐藏、搜索都在
- 同一扩展在多处注册（32 位 / 64 位、多个位置、本机 / 当前用户）会自动合并成一项，操作时一并处理，不会漏
- 选择会记住：下次打开保持上次的模式与场景

### 高级模式

![高级模式界面](docs/advanced-mode.png)

面向需要看细节的人：按注册位置、作用域（本机 / 当前用户）、视图（64 / 32 位）展开的完整清单（本机约 190 条），额外提供类型、命令 / CLSID、注册表路径等列，以及「已屏蔽残留」清理。

## 系统要求

- Windows 10 1809+ / Windows 11
- 无需安装 .NET 运行时（自包含单文件 exe）
- 运行需要管理员权限（UAC）：绝大多数菜单项位于 HKLM，写入需要提权

## 使用

1. 从 [Releases](https://github.com/heathacosta259519-dot/akagi-toolbox/releases) 下载 zip，解压得到 `ContextMenuEditor.exe`
2. 双击运行；默认进入简单模式，选好场景后**取消勾选 = 隐藏**，**重新勾选 = 恢复**
3. 多数改动在新打开的菜单里立即生效；若没生效，点「重启资源管理器」
4. 想反悔时打开「操作历史」，选中记录点「撤销选中」
5. 需要看原始注册信息时，把「模式」切到「高级模式」；双击任意一行可看详情并一键在 regedit 中定位

## 工作原理

| 对象 | 隐藏方式 | 恢复方式 |
|---|---|---|
| 静态菜单项（`shell\<verb>`） | 给 verb 键写入空值 `LegacyDisable` | 删除该值 |
| COM 扩展（`shellex\ContextMenuHandlers`） | 把扩展的 CLSID 加入 Windows 官方屏蔽列表 `Shell Extensions\Blocked` | 从屏蔽列表移除 |
| Win11 经典右键菜单（实验） | 写入 CLSID 覆盖键 | 删除该键 |

这些机制都是"标记式"的：原始的菜单项注册信息原封不动，因此隐藏/恢复可以无限来回切换。这也意味着你随时可以手动检查或清理（详情对话框提供「在注册表中打开」）。

操作日志：`%APPDATA%\ContextMenuEditor\journal.ndjson`
模式偏好：`%APPDATA%\ContextMenuEditor\settings.json`（仅记录模式与场景，删除即恢复默认）

## 常见问题

**简单模式为什么只有几十项，高级模式有上百项？**
简单模式按"菜单里看到的项"去重合并（同一扩展的 32/64 位、多位置注册会合并），高级模式则是按注册位置逐一展开的原始数据，两者是同一批数据的不同视角。

**改了菜单没变化？**
先点「重启资源管理器」。另外 Win11 的「新式菜单」只显示部分项目，其余项在「显示更多选项」（Shift+F10）里。

**怎么彻底还原成原样？**
在列表里把勾重新勾上即可。即使卸载本工具，已隐藏的项也会保持状态——把 `LegacyDisable` 值删掉、或把 CLSID 从 Blocked 列表移除就恢复了。

**为什么需要管理员权限？**
大部分菜单项注册在 HKLM（机器级作用域），写入需要提权。工具全程使用管理员权限，操作前都有确认提示。

**杀毒软件报毒？**
自包含的 .NET 单文件 exe 体积较大，偶尔会被启发式引擎误报。可用 `build.ps1` 从源码自行构建核对。

**列表里「暂不支持」是什么？**
少数新式菜单项（`ExplorerCommandHandler`）不使用经典命令机制，当前版本暂不支持隐藏，已列入路线图。

## 从源码构建

前置：.NET SDK 8+（Windows）

```powershell
cd tools/context-menu-editor
dotnet test .\tests\ContextMenuEditor.Tests    # 运行测试
.\build.ps1                                    # 测试 + 发布单文件 exe + 打包 zip
```

产物在 `dist/`：`ContextMenuEditor.exe` 与 `context-menu-editor-vX.Y.Z-win-x64.zip`。

## 开发说明

- 结构：`src/ContextMenuEditor`（主程序，WinForms / net8.0-windows）、`tests/ContextMenuEditor.Tests`（xunit）
- 简单模式的分组去重逻辑在 `Services/SimpleMenuBuilder.cs`，场景定义在 `Models/MenuScene.cs`
- 注册表写点只有两处（`Services/ToggleService.cs`），扫描逻辑在 `Services/RegistryScanner.cs`
- 测试在 HKCU 沙盒里创建临时菜单项做「隐藏 → 恢复」往返验证，不会触碰真实菜单项

## 路线图

- 支持「发送到」「新建（ShellNew）」菜单
- 对新型 `ExplorerCommand` 项提供"导出备份 + 移除"回退机制
- 配置导出 / 导入（在多台机器之间同步菜单方案）

## 许可证

[MIT](../../LICENSE)
