# Win11 Fluent Design UI Redesign

**Date:** 2026-04-14
**Status:** Approved
**Scope:** WpfMarkdownEditor.Wpf (控件库) + WpfMarkdownEditor.Sample (示例应用)

## Background

当前存在两个问题：
1. 暗色主题下 FlowDocumentReader 工具栏按钮不可见（黑色前景 + 黑色背景）
2. 整体 UI 风格需要升级到 Windows 11 Fluent Design 水平

用户希望进行全面重新设计，新增四个功能：侧边栏文件树、格式化工具栏、文档大纲导航、多标签页支持。保留 FlowDocument 作为预览渲染方案。

## Architecture

### Core Constraint: MarkdownEditor Independence

MarkdownEditor 控件库（WpfMarkdownEditor.Wpf）保持完全独立，不知道文件树、标签页、大纲等应用层组件的存在。控件库通过公共 API 和事件暴露接口，应用层负责组合。

```
┌─────────────────────────────────────────────────┐
│  Sample App (应用层)                             │
│  ┌─────────────────────────────────────────────┐ │
│  │ MainWindow — 应用外壳                       │ │
│  │  ├─ TabBar (标签页管理)                      │ │
│  │  ├─ FormattingToolbar (格式化工具栏)         │ │
│  │  ├─ FileTreeView (左侧文件树)                │ │
│  │  ├─ OutlineView (右侧文档大纲)               │ │
│  │  ├─ StatusBar (状态栏)                       │ │
│  │  └─ MarkdownEditor ← 控件库组件              │ │
│  └─────────────────────────────────────────────┘ │
│                                                   │
│  ThemeSystem (Win11 Fluent Design token 体系)     │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│  WpfMarkdownEditor.Wpf (控件库层 — 独立)         │
│  ├─ MarkdownEditor 控件                          │
│  │   ├─ Editor TextBox                           │
│  │   ├─ Preview (FlowDocumentReader)             │
│  │   └─ Theme API (ApplyTheme)                   │
│  ├─ EditorTheme (主题数据)                       │
│  └─ FlowDocumentRenderer                         │
└─────────────────────────────────────────────────┘
```

### Data Flow

```
Sample App (UI 组合层)
│
├─ FormattingToolbar ──调用──→ MarkdownEditor.WrapSelection()
│                            MarkdownEditor.InsertAtCursor()
│
├─ OutlineView ──订阅──→ MarkdownEditor.OutlineChanged 事件
│
├─ FileTreeView ──调用──→ TabManager.OpenFile(path)
│                        └→ MarkdownEditor.LoadFile(path)
│
└─ MarkdownEditor (控件库)
    ├─ 公共 API: WrapSelection, InsertAtCursor, LoadFile
    ├─ 事件: OutlineChanged, MarkdownChanged
    └─ 内部: Parser, Renderer, Theme
```

## Implementation Phases

### Phase 1: Visual Foundation

建立 Win11 Fluent Design token 体系，修复暗色主题问题。

#### 1.1 Fluent Design Token System

结构化颜色 token，替代零散 brush 定义：

```
Token 层级:
├── Layer (基础色)
│   ├── Background/Base        — 主背景 (#F3F3F3 / #202020)
│   ├── Background/Alt         — 次级背景 (#FAFAFA / #282828)
│   ├── Background/Card        — 卡片/悬浮层 (#FFFFFF / #2D2D2D)
│   └── Background/Overlay     — 弹窗/菜单 (#FFFFFFE0 / #2D2D2DE0)
│
├── Text (文字色)
│   ├── Primary                — 主文字 (#1A1A1A / #FFFFFF)
│   ├── Secondary              — 次要文字 (#616161 / #9E9E9E)
│   ├── Tertiary               — 禁用文字 (#A0A0A0 / #6D6D6D)
│   └── OnAccent               — 强调色上的文字 (#FFFFFF / #000000)
│
├── Stroke (边框/分割线)
│   ├── Card                   — 卡片边框 (#E5E5E5 / #3D3D3D)
│   ├── Divider                — 分割线 (#E5E5E5 / #2D2D2D)
│   └── Surface                — 表面边框 (#E5E5E550 / #FFFFFF15)
│
├── Accent (强调色)
│   ├── Default                — 主强调 (#005FB8 / #60CDFF)
│   ├── Hover                  — 悬停 (#1A7FCC / #7AD5FF)
│   └── Pressed                — 按下 (#004A93 / #4DB8E8)
│
├── Control (控件交互态)
│   ├── Hover                  — 悬停背景 (#F5F5F5 / #383838)
│   ├── Pressed                — 按下背景 (#E8E8E8 / #434343)
│   └── Selected               — 选中背景 (#E0E0E0 / #404040)
│
└── Shadow (阴影)
    └── Flyout                 — 弹出层阴影 (0 8px 16px rgba(0,0,0,0.14))
```

#### 1.2 Fix Dark Theme FlowDocumentReader Toolbar

**Root Cause:** FlowDocumentReader 内部生成的工具栏使用封装的内部模板，隐式样式覆盖无法完全控制所有内部元素。

**Fix:** 为 FlowDocumentReader 提供完整的自定义 ControlTemplate，直接控制工具栏区域的渲染。工具栏按钮统一使用 token 体系中的 brush。

#### 1.3 Control Library Style Improvements

- Editor TextBox 光标颜色跟随主题
- Splitter 拖拽区域加大（4px 可见区域 + 鼠标穿透区域）
- FlowDocumentReader 工具栏完整重写模板

These changes are internal to the control library and do not affect the public API.

### Phase 2: App Shell

构建新的窗口布局框架。

#### 2.1 Window Layout

```
Window (Chromeless, Mica)
├── Border (RootBorder, CornerRadius=8)
│   └── Grid (4 rows)
│       ├── Row 0: TitleBar + TabBar       (Height=36)
│       ├── Row 1: FormattingToolbar       (Height=40)
│       ├── Row 2: MainContent             (Height=*)
│       │   └── Grid (3 columns)
│       │       ├── Col 0: FileTreeView    (Auto, collapsible)
│       │       ├── Col 1: MarkdownEditor  (1*)
│       │       └── Col 2: OutlineView     (Auto, collapsible)
│       └── Row 3: StatusBar               (Height=24)
```

#### 2.2 TabBar (标签页栏)

- 集成在 36px 标题栏中
- 水平排列标签，每个显示文件名 + 关闭按钮
- 右侧 "+" 新建按钮
- 暗色主题：活动标签 #1E1E1E + 底部蓝色指示条，非活动标签 #2D2D2D
- 基础切换，不支持拖拽排序（见 Out of Scope）

#### 2.3 FormattingToolbar (格式化工具栏)

- 40px 高度，分组布局，组间用 1px 分割线分隔
- 按钮：文本格式(B/I/Code/Strikethrough) | 标题(H1-H3) | 列表(UL/OL/Quote) | 插入(Link/Image/Table)
- 右侧：主题切换 Segment + 预览开关
- Win11 微妙风格：hover 背景变化，无可见边框

#### 2.4 Collapsible Sidebars

**左侧文件树：**
- 默认宽度 200px，可拖拽调整
- 顶部 "Explorer" 标题栏
- Ctrl+B 折叠/展开
- 折叠后宽度为 0，使用 ColumnDefinition.Width 绑定 + DoubleAnimation 在 LayoutTransform 上实现平滑过渡（WPF 不直接支持 GridLength 动画）

**右侧大纲：**
- 默认宽度 180px，同理可折叠
- Ctrl+Shift+O 折叠/展开
- 显示 H1-H6 标题层级

#### 2.5 StatusBar (状态栏)

- 24px 高度
- 左侧：状态消息（"已保存"、"文件已加载" 等）
- 右侧：光标位置(Ln X, Col Y) | UTF-8 | Markdown
- 颜色用 Text/Tertiary token

### Phase 3: Features

填充三个新功能模块。

#### 3.1 FileTreeView (侧边栏文件树)

- **数据源：** 用户通过 "打开文件夹" 选择根目录，递归扫描 `.md` 文件
- **展示：** TreeView 控件，文件夹可展开/折叠，文件图标按类型区分
- **交互：** 单击文件 → 在新标签页打开（已打开则切换到对应标签）
- **折叠：** Ctrl+B，同文件树的动画方式
- **位置：** Sample 应用层，独立 UserControl

#### 3.2 FormattingToolbar (格式化工具栏)

**控件库扩展 — MarkdownEditor 新增公共方法：**
- `WrapSelection(string prefix, string suffix)` — 包裹选中文本（加粗、斜体、代码等）
- `InsertAtCursor(string text)` — 在光标处插入文本
- `GetCursorPosition()` — 获取光标位置
- `GetSelectionRange()` — 获取选中范围

**工具栏按钮组：**
- 格式：**B** *I* `</>` ~~S~~
- 标题：H1 H2 H3（在行首插入 `#`/`##`/`###`）
- 列表：UL OL > Blockquote
- 插入：Link Image Table（弹出输入对话框）

工具栏 UI 在 Sample 层，插入逻辑通过控件库公共 API 调用。

#### 3.3 OutlineView (文档大纲)

- **数据源：** MarkdownEditor 新增 `OutlineChanged` 事件，事件参数 `OutlineChangedEventArgs` 包含 `List<OutlineItem>`，每个 `OutlineItem` 包含 `{ int Level, string Text, int LineNumber }`
- **展示：** ListView，按层级缩进
- **交互：** 点击项滚动编辑器到对应行，编辑内容变化时实时更新，当前光标所在标题高亮
- **折叠：** Ctrl+Shift+O
- **位置：** OutlineView UI 在 Sample 层，数据来自控件库事件

## Files Involved

### Control Library (WpfMarkdownEditor.Wpf)

| File | Changes |
|------|---------|
| `Controls/MarkdownEditor.xaml` | FlowDocumentReader 完整 ControlTemplate，修复暗色主题样式 |
| `Controls/MarkdownEditor.xaml.cs` | 新增 WrapSelection, InsertAtCursor, GetCursorPosition, GetSelectionRange 方法；新增 OutlineChanged 事件 |
| `Theming/EditorTheme.cs` | 新增 `CursorColor` 属性，暗色主题白色光标、亮色主题黑色光标 |

### Sample App (WpfMarkdownEditor.Sample)

| File | Changes |
|------|---------|
| `App.xaml` | 新增 Fluent Design token ResourceDictionary，替换现有零散 brush |
| `Resources/ModernStyles.xaml` | 全面重写为 Win11 风格样式 |
| `MainWindow.xaml` | 完全重写为新布局（TabBar + Toolbar + Sidebars + StatusBar） |
| `MainWindow.xaml.cs` | 重写窗口逻辑（标签页管理、主题切换、侧边栏折叠） |
| 新增 `Controls/TabBar.xaml` | 标签页栏控件 |
| 新增 `Controls/FormattingToolbar.xaml` | 格式化工具栏控件 |
| 新增 `Controls/FileTreeView.xaml` | 文件树控件 |
| 新增 `Controls/OutlineView.xaml` | 文档大纲控件 |
| 新增 `Helpers/TabManager.cs` | 标签页/多文件管理 |
| 新增 `Resources/FluentTheme.xaml` | Fluent Design token 定义 |

## Out of Scope

- Markdown 语法解析器改动
- FlowDocumentRenderer 渲染逻辑改动
- 拖拽排序标签页（Phase 2 仅支持基础标签页切换）
- 文件监听/自动刷新（文件树不监听外部修改）
- 打印功能
- 拼写检查
- 自定义快捷键绑定
