# Markdown Parser 演进路线

> 目标：把当前“轻量可用的手写 Markdown parser”逐步演进为边界清晰、测试扎实、适合 WPF 编辑器长期维护的解析基础设施。

---

## 当前定位

当前 parser 位于 `src/WpfMarkdownEditor.Core/Parsing`，采用手写扫描方式：

```text
MarkdownParser
   ↓
LineReader
   ↓
BlockParser
   ↓
InlineParser
   ↓
Block / Inline model
   ↓
WPF renderer
```

这套结构的优点是：

- Core 不依赖 WPF。
- Block 和 Inline 模型适合 WPF 渲染消费。
- 常见 Markdown 语法已经有较好的单元测试覆盖。
- 对实时编辑器友好，解析失败时多数场景会 fallback 为文本或段落。

但它目前应被视为：

```text
项目自定义 Markdown subset parser
```

而不是完整 CommonMark 或 GitHub Flavored Markdown parser。

---

## 10 分 Parser 的定义

本项目里的 10 分 parser 不只是“支持更多语法”，而是要同时满足：

- 语法兼容目标明确。
- AST/model 能表达复杂 Markdown 结构。
- Block 和 inline 解析规则可独立演进。
- 错误恢复行为稳定、可预测。
- Parser、renderer、translation extractor 使用同一套结构认知。
- 大文档和实时编辑场景下性能可控。
- 有规范测试、快照测试、容错测试、性能测试保护。

---

## 非目标

短期内不建议追求：

- 一次性完整实现 CommonMark。
- 一次性完整实现 GitHub Flavored Markdown。
- 把任意 HTML 直接交给 WPF 渲染。
- 为了抽象而重写整个 parser。
- 在 renderer 中补 parser 无法表达的 Markdown 结构。

如果未来产品目标变成“高保真兼容 GitHub Markdown”，应重新评估 Markdig 等成熟库，而不是无限扩展当前手写 parser。

---

## 阶段 1：建立正确性边界

目标：先明确“什么算正确”，再继续加功能。

### 1.1 支持矩阵

新增或维护 `docs/markdown-parser-support.md`，列出 Markdown 特性支持状态：

```markdown
| Feature | Status | Notes |
|---|---|---|
| ATX heading | Supported | Supports optional closing # |
| Setext heading | Supported | Parsed from paragraph + underline |
| Fenced code block | Supported | Supports ``` and ~~~ |
| GFM table | Partial | Escaped pipe behavior needs explicit tests |
| HTML block | Partial | Project-defined safe subset |
| Reference link | Unsupported | Falls back to text |
| Footnote | Unsupported | Falls back to paragraph/text |
```

状态建议固定为：

```text
Supported
Partial
Unsupported
Intentional deviation
```

### 1.2 AST 快照输出

为测试增加 normalized AST printer，用文本形式稳定描述 parser 输出：

```text
Heading(level=1)
  Text("Hello ")
  Bold
    Text("world")
Paragraph
  Link(url="https://example.com")
    Text("site")
```

用途：

- 复杂语法回归测试。
- parser 重构前后行为对比。
- review 时快速看出结构变化。

建议位置：

```text
tests/WpfMarkdownEditor.Core.Tests/Parsing/Support/MarkdownAstPrinter.cs
```

### 1.3 规范代表用例

增加代表性 CommonMark/GFM 用例，不要求全量通过，但要标注意图：

```text
tests/WpfMarkdownEditor.Core.Tests/Parsing/Spec/
  CommonMarkRepresentativeTests.cs
  GfmRepresentativeTests.cs
```

每条测试应说明：

- 这是项目支持的行为。
- 这是暂不支持但应安全 fallback 的行为。
- 这是与 CommonMark 的有意差异。

### 1.4 容错测试

新增 malformed Markdown recovery tests：

```text
UnclosedLink_FallsBackToText
UnclosedCodeSpan_FallsBackToText
UnclosedFence_ProducesCodeBlockToEndOfDocument
InvalidTableSeparator_ProducesParagraphs
MalformedHtml_DoesNotThrow
```

实时编辑器的 parser 原则：

```text
Never throw for user markdown.
When uncertain, preserve original text.
```

---

## 阶段 2：增强模型表达能力

目标：让 Core model 能表达复杂 Markdown，而不是让 renderer 猜结构。

### 2.1 SourceSpan

为 parser model 设计原文位置：

```csharp
public readonly record struct SourceSpan(
    int Start,
    int Length,
    int StartLine,
    int EndLine);
```

收益：

- 预览点击定位源文。
- diagnostics 精确定位。
- 翻译 segment 映射原文。
- 将来支持增量解析。
- 快照测试更稳定。

建议先设计，不必一次性所有 node 都补齐。

### 2.2 嵌套 Block

当前模型更适合简单 Markdown。后续需要支持：

```markdown
> - item
>   ```csharp
>   code
>   ```
```

理想结构：

```text
BlockquoteBlock
  ListBlock
    ListItem
      ParagraphBlock
      CodeBlock
```

重点审视：

- `BlockquoteBlock`
- `ListBlock`
- `ListItem`
- `ParagraphBlock`
- `TableBlock`

建议方向：

```text
ListItem contains IReadOnlyList<Block>
BlockquoteBlock contains IReadOnlyList<Block>
ParagraphBlock contains IReadOnlyList<Inline>
```

不要把块级结构压进 inline 文本里。

### 2.3 Parse Result

考虑从：

```csharp
List<Block> Parse(string markdown)
```

扩展出：

```csharp
MarkdownParseResult ParseDocument(string markdown);
```

示例：

```csharp
public sealed class MarkdownParseResult
{
    public IReadOnlyList<Block> Blocks { get; init; } = [];
    public IReadOnlyList<MarkdownDiagnostic> Diagnostics { get; init; } = [];
}
```

保持旧 API 兼容：

```csharp
public List<Block> Parse(string markdown) => ParseDocument(markdown).Blocks.ToList();
```

---

## 阶段 3：解析器模块化

目标：避免 `BlockParser` 和 `InlineParser` 继续膨胀成大模块。

### 3.1 Block Rule Pipeline

把 `TryParseBlock` 的大 if 链逐步迁移为规则列表：

```csharp
internal interface IBlockParserRule
{
    bool TryParse(BlockParserContext context, out Block? block);
}
```

候选 rule：

```text
AtxHeadingRule
ThematicBreakRule
FencedCodeBlockRule
BlockquoteRule
ListRule
IndentedCodeBlockRule
HtmlBlockRule
TableRule
ParagraphRule
```

优先级必须显式记录，避免新增语法时破坏旧行为。

### 3.2 Parser Options

引入解析选项：

```csharp
public sealed class MarkdownParserOptions
{
    public bool EnableGfmTables { get; init; } = true;
    public bool EnableHtml { get; init; } = true;
    public bool EnableAutoLinks { get; init; } = true;
    public bool EnableTaskLists { get; init; } = false;
}
```

用途：

- 测试不同 feature flag。
- 将来允许用户配置 Markdown 兼容模式。
- 避免在 parser 里硬编码所有行为。

### 3.3 Inline Delimiter Stack

如果要提升 emphasis/link 兼容性，应把 inline parser 从简单 marker 处理升级为 delimiter stack。

目标语法：

```markdown
***bold italic***
**foo *bar* baz**
foo_bar_baz
a***b**c*
```

关键能力：

- opening delimiter
- closing delimiter
- left-flanking
- right-flanking
- intraword underscore
- nested emphasis
- link/image delimiter resolution

这是高风险改造，必须先有快照测试和代表性规范测试。

---

## 阶段 4：统一 Parser、Renderer、Translation

目标：Markdown 结构只解析一次，避免不同模块各自理解 Markdown。

当前项目里至少有三类消费者：

```text
WPF renderer
Translation segment extractor
Search / navigation / future editor features
```

长期方向：

```text
MarkdownParser -> Block/Inline AST
Renderer -> consumes AST
MarkdownSegmentExtractor -> consumes AST
Search/navigation -> consumes AST + SourceSpan
```

如果 translation extractor 继续自行解析 Markdown，容易出现：

```text
Renderer 认为这是 link
Extractor 认为这是 plain text
```

这种结构认知不一致会导致翻译破坏 Markdown。

### 4.1 Visitor

为 AST 增加 visitor 或遍历 helper：

```csharp
public interface IMarkdownNodeVisitor
{
    void VisitParagraph(ParagraphBlock block);
    void VisitText(TextInline inline);
    void VisitLink(LinkInline inline);
}
```

也可以先做静态遍历工具，避免过早引入复杂 visitor。

### 4.2 Translation-aware Nodes

明确哪些 inline 可翻译：

```text
TextInline: translatable
CodeInline: not translatable
LinkInline.Text: translatable
LinkInline.Url: not translatable
ImageInline.Alt: maybe translatable
HtmlInline.Text: depends on policy
```

这些规则应与 parser model 绑定，而不是散在字符串处理逻辑里。

---

## 阶段 5：性能和编辑器体验

目标：大文档、快速输入、后台解析都稳定。

### 5.1 后台解析

WPF 层不应在 UI 线程解析大文档。建议模式：

```text
Text changed
  ↓ debounce
Cancel previous parse
  ↓
Task.Run(parse)
  ↓
Dispatcher apply latest result
```

要求：

- 旧解析结果不能覆盖新文本。
- 取消不应作为异常冒泡到 UI。
- UI 线程只做最终应用。

### 5.2 性能基准

建立 benchmark 项目或稳定性能测试：

```text
benchmarks/WpfMarkdownEditor.Benchmarks
  Parse_SmallDocument
  Parse_LargeReadme
  Parse_ManyLinks
  Parse_LargeTable
  Parse_NestedLists
  Parse_MixedChineseEnglish
```

关注：

- parse time
- allocations
- large document memory pressure
- regex catastrophic backtracking

### 5.3 Fuzz Tests

增加随机输入稳定性测试：

```text
Parse_RandomUnicode_DoesNotThrow
Parse_RandomMarkdownLikeText_DoesNotThrow
Parse_LongRepeatedDelimiters_CompletesQuickly
```

重点输入：

```markdown
****************************************************************
[[[[[[[[[[[[[[[[[
!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
<><><><><><><><><>
```````````````````
```

---

## Markdig 评估点

如果目标变成高兼容 Markdown，建议评估：

```text
Markdig parse AST
   ↓
Adapter
   ↓
Project Block/Inline model
   ↓
WPF renderer / translation extractor
```

优点：

- CommonMark/GFM 兼容性更成熟。
- 减少自研 parser 规范压力。
- 可以保留当前 WPF renderer 和项目 model。

代价：

- Core 引入外部依赖。
- 需要维护 Markdig AST 到项目 AST 的 adapter。
- Translation extractor 可能需要更精细的 source mapping。
- 某些编辑器容错行为可能和当前 parser 不一致。

决策标准：

```text
如果产品要求 GitHub Markdown 高保真：优先评估 Markdig。
如果产品要求可控轻量子集和翻译安全：继续增强自研 parser。
```

---

## 推荐优先级

### P0：马上值得做

- 支持矩阵文档。
- AST 快照 printer。
- representative spec tests。
- malformed recovery tests。
- parser 不抛异常的 fuzz smoke tests。

### P1：下一轮架构增强

- SourceSpan 设计。
- `MarkdownParseResult` + diagnostics。
- 嵌套 block model 草案。
- Translation extractor 与 AST 对齐方案。

### P2：复杂重构

- Block rule pipeline。
- Inline delimiter stack。
- Parser options。
- HTML policy 集中化。

### P3：成熟编辑器体验

- 后台解析和取消。
- BenchmarkDotNet。
- 大文档策略。
- 增量解析预研。

---

## 验收标准

后续每个 parser 相关改动至少满足：

- 有对应 Core parsing 测试。
- 不把解析逻辑放到 WPF renderer。
- 不让 Core 引用 WPF 类型。
- 新语法明确加入支持矩阵。
- 对 malformed input 有 fallback 行为。
- 大型规则新增时不继续扩大单个大 parser 类。

对于高风险改动，还需要：

- AST 快照测试。
- representative CommonMark/GFM 用例。
- `dotnet test WpfMarkdownEditor.sln --no-restore` 通过。
- 必要时补性能基线。

---

## 总结

当前 parser 的方向是正确的：Core 手写轻量解析，产出 UI 无关模型，WPF 层负责渲染。

下一步不应该急着重写，而应该先补齐：

```text
正确性边界
测试基线
AST 表达能力
错误恢复策略
性能观测
```

当这些基础建立后，再决定继续增强自研 parser，还是引入 Markdig 作为底层规范 parser。
