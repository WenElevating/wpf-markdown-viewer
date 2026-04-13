# Deep Interview Spec: WPF Markdown Editor Control

## Metadata
- Interview ID: wpf-md-editor-001
- Rounds: 4
- Final Ambiguity Score: 11%
- Type: greenfield
- Generated: 2026-04-13
- Threshold: 0.2 (20%)
- Status: PASSED

## Clarity Breakdown
| Dimension | Score | Weight | Weighted |
|-----------|-------|--------|----------|
| Goal Clarity | 0.95 | 40% | 0.38 |
| Constraint Clarity | 0.85 | 30% | 0.26 |
| Success Criteria Clarity | 0.90 | 30% | 0.27 |
| **Total Clarity** | | | **0.91** |
| **Ambiguity** | | | **9%** |

## Goal

实现一个高性能、可主题化的 WPF Markdown 编辑器 UserControl，基于设计文档 `2026-04-13-wpf-markdown-editor-design.md` 的完整规范，作为可嵌入组件供其他 WPF 应用集成使用。

**核心功能：**
- 实时预览（<50ms 更新延迟）
- 并排布局（编辑器 + 预览）
- 自定义渲染（Markdown → WPF FlowDocument，无 HTML/WebView2）
- 完整 Markdown 支持（标题、段落、代码块、表格、引用、列表、图片）
- 主题系统（亮色/暗色）

## Constraints

### 技术约束
- **目标框架：** 
  - `WpfMarkdownEditor.Core`: `net8.0`（无 WPF 依赖）
  - `WpfMarkdownEditor.Wpf`: `net8.0-windows`
- **零外部依赖：** 纯 .NET 8，无外部 NuGet 包
- **开发环境：** .NET 8 SDK + Visual Studio 2022

### 解析器约束
- **CommonMark 严格合规：** 90% Spec Tests 通过率
- **GFM 扩展：** 表格、删除线
- **测试框架：** xUnit
- **覆盖率目标：** 80% 行覆盖率

### 功能约束
- **词法分析器（语法高亮）：** 
  - 可选模块，仅在 Wpf 项目中
  - 支持 C#、JavaScript、Python
  - 零外部依赖（自建实现）
- **远程图片缓存：** 
  - 临时缓存到系统 Temp 目录
  - 会话结束自动清理

### 简化决策（通过反驳模式确认）
- CommonMark 通过率：95% → **90%**
- 词法分析器语言：7种 → **3种（C#/JS/Python）**
- CI/CD：**移除初始交付**，后续迭代添加

## Non-Goals

- 非 WPF 平台支持（如 Avalonia、MAUI）
- 独立应用程序（仅 UserControl）
- WebView2 或 HTML 渲染引擎
- 数学公式（LaTeX）渲染
- 外部语法高亮库（ColorCode.NET 等）
- CI/CD 流水线（初始版本不包含）

## Acceptance Criteria

- [ ] CommonMark Spec Tests 达到 90% 通过率
- [ ] Core 库达到 80% 行覆盖率
- [ ] 两个 NuGet 包可成功构建
  - `WpfMarkdownEditor.Core` (net8.0)
  - `WpfMarkdownEditor.Wpf` (net8.0-windows)
- [ ] 示例 WPF 应用可正常运行
- [ ] Light 和 Dark 主题渲染正确
- [ ] 预览更新延迟 < 50ms（对于典型文档）
- [ ] 所有 Block 类型正确解析和渲染
  - Heading (H1-H6)
  - Paragraph
  - CodeBlock (fenced/indented)
  - Table
  - Blockquote
  - List (ordered/unordered)
  - ThematicBreak
  - Image
- [ ] 所有 Inline 类型正确解析和渲染
  - Text
  - Bold
  - Italic
  - BoldItalic
  - Code
  - Link
  - Image

## Assumptions Exposed & Resolved

| 假设 | 挑战 | 决议 |
|------|------|------|
| 设计文档的简单语法高亮足够 | 用户需要高级语法高亮 | 自建词法分析器作为可选模块，支持 C#/JS/Python |
| CommonMark 95% 通过率 | 复杂度风险（反驳模式） | 简化到 90%，允许更多边缘情况失败 |
| 7种语言语法高亮 | 工作量过大（简化模式） | 减少到 3种核心语言 |
| CI/CD 必须包含 | 简化加速交付（简化模式） | 移出初始交付范围 |
| 零依赖 + 高级高亮冲突 | 约束优先级决策 | 自建词法分析器，保持零依赖 |

## Technical Context

### 项目结构
```
WpfMarkdownEditor/
├── src/
│   ├── WpfMarkdownEditor.Core/           # Parser & AST (net8.0)
│   │   ├── Parsing/
│   │   │   ├── MarkdownParser.cs
│   │   │   ├── Block.cs
│   │   │   ├── Inline.cs
│   │   │   └── Blocks/                   # Block 实现
│   │   └── Inlines/                      # Inline 实现
│   │
│   └── WpfMarkdownEditor.Wpf/            # WPF 控件库 (net8.0-windows)
│       ├── Controls/
│       │   ├── MarkdownEditor.xaml
│       │   └── MarkdownPreview.xaml
│       ├── Rendering/
│       │   ├── FlowDocumentRenderer.cs
│       │   └── Renderers/
│       ├── Themes/
│       │   ├── EditorTheme.cs
│       │   └── LightTheme.xaml / DarkTheme.xaml
│       └── SyntaxHighlighting/           # 可选模块
│           └── Lexer.cs
│
└── tests/
    ├── WpfMarkdownEditor.Core.Tests/
    └── WpfMarkdownEditor.Wpf.Tests/
```

### 高层架构
```
TextBox (Editor) → MarkdownParser → Block AST → FlowDocumentRenderer → FlowDocument (Preview)
```

## Ontology (Key Entities)

| Entity | Type | Fields | Relationships |
|--------|------|--------|---------------|
| MarkdownEditor | core domain | Markdown, Theme, ShowPreview, PreviewWidth | contains TextBox, contains FlowDocumentReader |
| Block | core domain | LineStart, LineEnd | base for HeadingBlock, ParagraphBlock, CodeBlock, etc. |
| Inline | core domain | - | base for TextInline, BoldInline, ItalicInline, etc. |
| MarkdownParser | core domain | - | parses string → List<Block>, implements CommonMark |
| FlowDocumentRenderer | core domain | theme, renderers | renders List<Block> → FlowDocument |
| EditorTheme | core domain | BackgroundColor, ForegroundColor, fonts, colors | configures renderer |
| IBlockRenderer | core domain | - | interface for per-block rendering |
| SyntaxHighlighter | optional module | lexer, languages | Wpf-only, supports C#/JS/Python |
| Lexer | optional module | languageDetection, tokenization | implements SyntaxHighlighter |
| WpfMarkdownEditor.Core | supporting | - | NuGet package, net8.0, pure AST |
| WpfMarkdownEditor.Wpf | supporting | - | NuGet package, net8.0-windows |
| SampleApplication | supporting | - | demonstrates MarkdownEditor usage |
| CommonMark Spec Tests | external | testCases | validates parser compliance (90% pass rate) |

## Ontology Convergence

| Round | Entity Count | New | Changed | Stable | Stability Ratio |
|-------|-------------|-----|---------|--------|-----------------|
| 1 | 12 | 12 | - | - | N/A |
| 2 | 17 | 5 | - | 12 | 70% |
| 3 | 17 | 0 | 4 | 13 | 76% |
| 4 | 9 | 0 | 2 | 7 | 78% |

**收敛分析：** 实体模型在 Round 2 达到稳定，后续轮次主要是约束细化而非新增概念。最终 9 个核心实体定义清晰。

## Interview Transcript

<details>
<summary>Full Q&A (4 rounds)</summary>

### Round 1
**Q:** 设计文档已定义完整架构。请问这个项目的主要目标是什么？
**A:** 完全按文档实现
**Ambiguity:** 100% → (待评分)

**Q:** 设计文档提到"CommonMark spec with GFM extensions"。解析器应该达到什么级别的兼容性？
**A:** CommonMark 严格合规

**Q:** 设计文档提到"80%+ test coverage on Core library"。您希望使用哪个测试框架？
**A:** xUnit（推荐）

**Q:** 设计文档第8节提到代码块语法高亮采用"Simple approach"。这是否满足需求？
**A:** 高级语法高亮

**Q:** 设计文档提到远程图片"cache to temp"。您希望采用什么缓存策略？
**A:** 临时缓存（推荐）

**Q:** 项目完成时应该交付哪些产物？
**A:** 两个 NuGet 类库 + 示例应用程序 + CI/CD 配置

**Q:** 您的开发环境是什么？
**A:** .NET 8 SDK + Visual Studio 2022

**Round 1 Ambiguity:** 9%

### Round 2
**Q:** 您选择了"高级语法高亮"，但设计文档强调"零外部依赖"。如何处理这个约束冲突？
**A:** 自建词法分析器（保持零依赖）

**Q:** 您提到需要 CI/CD 配置。希望使用哪个平台？
**A:** GitHub Actions（推荐）

**Q:** 您选择了"CommonMark 严格合规"。如何验证解析器的合规性？
**A:** 集成 CommonMark Spec Tests

**Round 2 Ambiguity:** 12%

### Round 3
**Q:** CommonMark Spec Tests 有 600+ 测试用例。您希望解析器达到什么通过率阈值？
**A:** 95% 通过率

**Q:** 自建词法分析器需要支持哪些语言的语法高亮？
**A:** C#, JavaScript/TypeScript, Python, Java, Go, Rust, SQL

**Q:** 【反驳模式】三个目标叠加会显著增加开发复杂度...
**A:** 同意，这个复杂度风险值得注意

**Q:** 基于复杂度风险认知，词法分析器应如何设计？
**A:** 完全可选模块

**Round 3 Ambiguity:** 15%

### Round 4
**Q:** .NET 目标框架应该如何配置？
**A:** 按项目区分（Core: net8.0, Wpf: net8.0-windows）

**Q:** 80% 测试覆盖率应该使用什么指标？
**A:** 行覆盖率（推荐）

**Q:** 【简化模式】是否有任何约束可以进一步简化？
**A:** 降低 CommonMark 通过率 + 减少词法分析器语言数 + 移除 CI/CD 配置

**Q:** 确认简化方案：90% 通过率 + 3种语言 + 无 CI/CD
**A:** 确认上述简化

**Q:** 远程图片"临时缓存"具体应该存储在哪里？
**A:** 系统 Temp 目录（推荐）

**Final Ambiguity:** 11% ✓ PASSED

</details>

## Reference Design Document

完整设计规范见：`docs/superpowers/specs/2026-04-13-wpf-markdown-editor-design.md`

该文档定义了：
- AST 模型（Block/Inline 类型层次）
- MarkdownParser 实现策略
- FlowDocumentRenderer 架构
- EditorTheme 主题系统
- MarkdownEditor 控件 API
- 性能策略（防抖、异步渲染）
- 图片处理方案
- 语法高亮简单方案
