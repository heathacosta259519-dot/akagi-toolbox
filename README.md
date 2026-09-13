# Akagi's Toolbox

实用小工具集合，随手写、随手用。

A collection of handy little tools.

## 工具列表

- **[context-menu-editor](tools/context-menu-editor/)** — Windows 右键菜单编辑器：简单模式下直接把真实右键菜单构建出来显示（文字 / 顺序 / 子菜单 / 分隔线一致，每项标出归属程序，勾掉即隐藏、可随时恢复），菜单在独立子进程里构建以免被第三方扩展拖死；高级模式可查看完整注册表细节。

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
