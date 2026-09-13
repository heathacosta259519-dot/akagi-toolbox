# Akagi's Toolbox

实用小工具集合，随手写、随手用。

A collection of handy little tools.

## 工具列表

- **[context-menu-editor](tools/context-menu-editor/)** — Windows 右键菜单编辑器：简单模式下按场景标签页（文件 / 文件夹 / 文件夹空白处…）直接对着「右键看到的菜单」开关项目，自动合并多处注册、展开子菜单；高级模式可查看完整注册表细节。非破坏性、可随时恢复。

## 目录约定

每个工具放在 `tools/<tool-name>/` 下，相互独立、自包含：

```
tools/
└── <tool-name>/
    ├── README.md   # 用途、依赖、用法
    └── ...         # 源码（语言不限）
```

- 语言不限：Python、Shell、PowerShell、C++、C#、Rust……按工具场景挑最合适的
- 每个工具可单独复制出去使用，不依赖仓库的其它部分
- 工具自带 README，写清楚依赖、安装与用法

## 使用方法

```bash
git clone https://github.com/heathacosta259519-dot/akagi-toolbox.git
cd akagi-toolbox/tools/<tool-name>
```

具体安装方式见各工具自己的 README。

## 许可证

[MIT License](LICENSE)

---

## English

Practical small tools, each self-contained under `tools/<tool-name>/` with its own README. Languages vary by tool — use any folder on its own.

Licensed under the [MIT License](LICENSE).
