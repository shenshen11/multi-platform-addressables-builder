# 单 Editor 多平台 Addressables 构建工具计划书

## 1. 背景

当前项目使用 Unity Addressables（AA）进行资源管理，资源最终会通过 Addressables 构建流程生成 AssetBundle、catalog、hash 等产物。

项目长期存在 Android 和 QNX 两个平台的开发任务。两个平台之间的开发内容有时不同，但也存在一部分公共美术源资源，例如公共 UI、公共模型、公共材质、公共动画、公共音频等。

当前团队希望开发一个工具，用来减少双平台资源构建过程中的重复操作。核心目标是：

> 在同一个 Unity Editor 实例中，由用户选择构建平台和构建资源范围，工具自动完成平台切换、Addressables 配置切换、资源组选择和构建调用，最终生成对应平台可用的 AB/Addressables 资源产物。

需要特别明确：

- 本工具不是为了生成一个跨平台通用 AB 包。
- 本工具是为了在一个 Editor 工作流中，分别构建 Android 和 QNX 对应平台可用的资源包。
- 公共资源仍然会根据目标平台分别生成对应平台产物。
- 工具要减少的是人工重复操作，而不是取消平台构建本身。

---

## 2. 目标

### 2.1 核心目标

实现一个 Unity Editor 工具，支持用户在一个 Editor 实例中完成以下操作：

- 选择构建 Android、QNX 或 Android + QNX。
- 选择构建公共资源、平台专属资源或全部资源。
- 自动切换 Unity BuildTarget。
- 自动切换 Addressables Profile。
- 自动控制 Addressables Group 是否参与构建。
- 自动调用 Unity Addressables 标准构建流程。
- 自动输出到平台独立目录。
- 自动生成构建报告。
- 构建完成后恢复原始 Editor 状态。

### 2.2 非目标

第一版不解决以下问题：

- 不实现自研 AB 底层构建流程。
- 不绕过 Unity / Addressables 标准构建管线。
- 不承诺不触发 reimport。
- 不实现跨平台通用 AB 二进制复用。
- 不做 CDN 上传。
- 不做 OTA 发布流程。
- 不做多 workspace 并行构建。
- 不做复杂的增量构建系统。

---

## 3. 核心理解

### 3.1 构建依赖两类上下文

Addressables 构建依赖两类上下文：

```text
Unity 平台上下文 + Addressables 配置上下文
```

Unity 平台上下文包括：

- 当前 active BuildTarget。
- 当前 BuildTargetGroup。
- 当前平台下的资源导入结果。
- 当前平台下的纹理压缩结果。
- 当前平台下的 Shader 编译结果。
- 当前平台下的音频、模型等导入设置。
- 当前 Library 中的平台缓存。

Addressables 配置上下文包括：

- 当前 active Profile。
- Group 是否 Include In Build。
- Group Schema。
- BuildPath。
- LoadPath。
- Remote Catalog 配置。
- Content State 路径。
- 资源 Address / Label / Group 信息。

因此，正确构建一个平台的资源，需要同时满足：

```text
平台上下文正确
Addressables 配置正确
```

只切平台但不切 Profile，可能输出路径和 catalog 错误。

只切 Profile 但不切平台，可能构建出错误平台的资源导入产物。

---

## 4. 推荐工具定位

工具定位为：

> 单 Editor 实例内的多平台 Addressables 构建编排工具。

它负责：

- 收集用户构建意图。
- 根据配置准备平台上下文。
- 根据配置准备 Addressables 上下文。
- 调用 Unity / Addressables 标准 Build Pipeline。
- 管理构建状态。
- 恢复 Editor 状态。
- 生成构建报告。

它不负责：

- 自己生成 AB 文件格式。
- 自己序列化 Unity 资源。
- 自己处理纹理压缩、Shader 编译、依赖打包。

这些底层工作交给 Unity 标准构建流程。

---

## 5. 整体工作流

用户操作流程：

```text
打开 Unity 工程
打开工具窗口
选择构建平台：Android / QNX / Android + QNX
选择构建范围：Common / PlatformOnly / All
点击 Build
等待工具自动执行
查看构建结果和报告
```

工具内部流程：

```text
1. 记录当前 Editor 状态
2. 读取工具配置
3. 生成构建任务列表
4. 构建 Android
   - Switch Platform 到 Android
   - 切换 Addressables Profile 到 Android
   - Include Common + Android Groups
   - Exclude QNX Groups
   - 调用 Addressables Build
   - 记录 Android 产物
5. 构建 QNX
   - Switch Platform 到 QNX
   - 切换 Addressables Profile 到 QNX
   - Include Common + QNX Groups
   - Exclude Android Groups
   - 调用 Addressables Build
   - 记录 QNX 产物
6. 恢复原始 Editor 状态
7. 生成构建报告
```

如果用户只选择一个平台，则只执行对应平台的流程。

---

## 6. 模块设计

### 6.1 Editor UI 模块

职责：

- 提供 Unity EditorWindow 界面。
- 让用户选择构建平台。
- 让用户选择构建资源范围。
- 让用户选择或查看构建配置。
- 提供 Build、Validate、Open Report 等按钮。
- 只负责收集用户输入和展示结果，不直接承载核心构建逻辑。
- 调用 Controller 层暴露的统一构建入口，方便后续 CLI/CI 复用。

建议界面选项：

```text
Build Platforms:
  [ ] Android
  [ ] QNX

Resource Scope:
  Common Only
  Platform Only
  Common + Platform
  All Included By Platform

Build Options:
  [ ] Clean before build
  [ ] Restore original platform after build
  [ ] Restore original Addressables profile after build
  [ ] Restore original group states after build

Actions:
  Validate
  Build Selected
  Open Output
  Open Latest Report
```

该模块只负责用户交互，不直接写具体构建逻辑。

---

### 6.2 配置模块

职责：

- 保存项目级构建配置。
- 描述平台和 Addressables 配置之间的关系。
- 描述 Group 归属规则。
- 让工具适配不同项目，而不是把规则写死在代码中。

建议配置内容：

```text
Platforms:
  Android:
    BuildTarget
    BuildTargetGroup
    Addressables Profile
    Output Path
    Content State Path

  QNX:
    BuildTarget 或项目定制目标名
    BuildTargetGroup
    Addressables Profile
    Output Path
    Content State Path

Group Rules:
  Common_*  -> Common       -> Android + QNX
  Android_* -> AndroidOnly  -> Android
  QNX_*     -> QNXOnly      -> QNX

General Options:
  是否恢复原平台
  是否恢复原 Profile
  是否恢复 Group 状态
  报告输出目录
```

建议实现形式：

- 第一版可以使用 ScriptableObject。
- 后续如需 CI 或跨项目文本化配置，可以增加 JSON 导入导出。

---

### 6.3 平台切换模块

职责：

- 封装 Unity 的平台切换逻辑。
- 根据配置切换到目标 BuildTarget。
- 判断当前平台是否已经是目标平台。
- 处理切换失败。

核心行为：

```text
如果当前 active BuildTarget 已经是目标平台：
  不切换，继续构建

如果当前 active BuildTarget 不是目标平台：
  调用 Unity SwitchActiveBuildTarget
  等待 Unity 完成切换和必要 reimport
```

注意事项：

- 切平台可能触发 reimport。
- 切平台可能触发脚本编译和 domain reload。
- 工具不能承诺不触发 reimport。
- 工具只能自动化这个过程，减少人工操作。

---

### 6.4 Addressables Profile 管理模块

职责：

- 根据目标平台切换 Addressables active Profile。
- 验证目标 Profile 是否存在。
- 记录原始 Profile，方便构建结束后恢复。

示例：

```text
构建 Android:
  active Profile = Android

构建 QNX:
  active Profile = QNX
```

Profile 中应配置平台相关路径：

```text
Android:
  RemoteBuildPath = BuildOutput/Android/ServerData
  RemoteLoadPath  = 对应 Android 的加载路径

QNX:
  RemoteBuildPath = BuildOutput/QNX/ServerData
  RemoteLoadPath  = 对应 QNX 的加载路径
```

Profile 是强平台相关配置，不能混用。

---

### 6.5 Addressables Group 规则模块

职责：

- 根据目标平台和资源范围，决定哪些 Group 参与构建。
- 临时修改 Group 的 Include In Build 状态。
- 构建结束后恢复原状态。

建议 Group 分类：

```text
Common_*:
  Android 和 QNX 都需要构建

Android_*:
  只参与 Android 构建

QNX_*:
  只参与 QNX 构建
```

构建 Android 时：

```text
Include:
  Common_*
  Android_*

Exclude:
  QNX_*
```

构建 QNX 时：

```text
Include:
  Common_*
  QNX_*

Exclude:
  Android_*
```

如果用户选择 Common Only：

```text
Android 构建只 Include Common_*
QNX 构建只 Include Common_*
```

注意：

- Group 规则本身可以平台无关。
- Group 构建产物一定是平台相关的。
- Common Group 会分别生成 Android 和 QNX 对应平台产物。

> **[批注]** 命名约定作为唯一分类机制有脆性风险。
>
> 通配符规则将工具的正确性完全绑定到团队命名纪律上。对于规范化团队这是可接受的起点，但以下场景仍会导致静默错误：第三方插件创建的 Group、历史遗留 Group、临时测试 Group，它们的名字不遵守约定，会变成"未被任何规则匹配的 Group"，默认行为取决于 ResourceScope（`AllIncludedByPlatform` 时会被错误地包含进构建）。
>
> **建议补充显式覆盖机制**：在 `MpabGroupRule` 上增加 `ExplicitGroupNames` 列表字段，当该列表非空时，优先按 GUID/名字精确匹配，通配符仅作为兜底推断规则。这样团队可以对不符合命名约定的 Group 做显式归类，而不必重命名资产。
>
> 已在代码中实现：`MpabGroupRule.ExplicitGroupNames`，`MpabAddressablesGroupRuleEvaluator` 优先精确匹配，其次通配符匹配。

---

### 6.6 构建编排模块

职责：

- 根据用户选择生成平台构建任务。
- 作为核心逻辑层被 UI 和未来 CLI 共同调用。
- 不依赖 EditorWindow 状态，输入应来自明确的 BuildRequest / 配置对象。
- 按顺序执行每个平台的构建。
- 调用平台切换模块。
- 调用 Profile 管理模块。
- 调用 Group 规则模块。
- 调用 Addressables 标准构建 API。
- 收集每个平台构建结果。

推荐构建顺序：

```text
用户选择顺序或配置顺序
例如：
  Android -> QNX
```

该模块是工具核心，但它不直接处理底层 AB 生成。

底层构建交给：

```text
AddressableAssetSettings.BuildPlayerContent
```

或项目当前 Addressables 版本对应的构建 API。

---

### 6.7 状态机模块

职责：

- 保存当前构建流程进度。
- 应对平台切换、reimport、脚本编译、domain reload 可能导致的流程中断。
- 支持构建失败时恢复状态。
- 支持后续扩展为中断后继续构建。

> **[批注]** 状态机续跑机制的前提条件在计划书中未说清，实际实现风险较高。
>
> 计划书将 domain reload 续跑列为状态机的核心理由，但漏掉了关键前提：**session 文件写好之后，必须有东西来读它并驱动恢复**。Domain reload 之后内存全部清空，普通的 `while` 循环、`async` 任务、实例方法全部消失。要实现真正的续跑，需要在 `[InitializeOnLoad]` 静态构造函数里注册 `EditorApplication.update` 回调，在每次 Editor 启动或 reload 后检查 session 文件并恢复状态机。
>
> **当前实现（第一版）的真实能力**：`MpabBuildOrchestrator.Run()` 是一个同步长函数。`WaitForCompilation()` 用 `Thread.Sleep` 阻塞主线程等待编译，这在 Editor 主线程中可以工作，但如果此时发生 domain reload，整个调用栈会消失。**也就是说，当前状态机保证的是"正常完成时步骤可追踪"和"异常时能进入 Restore 流程"，而不是真正的 domain reload 续跑。**
>
> 这对第一版是合理的取舍。建议在产品说明中明确：当前版本如果在 `SwitchPlatform` 阶段发生 domain reload 导致中断，用户下次打开 Editor 会看到残留的 session 文件，需要手动触发"恢复或放弃"。真正的自动续跑是后续版本的能力。

为什么需要状态机：

Unity 在切换平台时，可能触发：

```text
资源重新导入
脚本重新编译
Assembly Reload
Domain Reload
Editor 状态刷新
```

如果构建流程只写成一个长函数：

```text
Build Android
Switch QNX
Build QNX
Restore
```

中间发生 reload 后，内存中的构建进度可能丢失，导致：

```text
后续平台没有构建
Addressables Profile 没恢复
Group Include 状态没恢复
Editor 停在错误平台
报告没有生成
```

状态机要记录：

```text
当前 sessionId
当前执行步骤
当前平台
待构建平台列表
已完成平台列表
原始 BuildTarget
原始 Addressables Profile
原始 Group Include 状态
错误信息
```

建议状态：

```text
Idle
Prepare
ValidatePreconditions
SwitchPlatform
WaitForCompilation
CheckCompilation
ApplyAddressablesConfig
SaveModifiedConfig
BuildAddressables
CollectResult
NextPlatform
Restore
SaveRestoredConfig
GenerateReport
Done
Failed
```

状态存储位置建议：

```text
Library/MultiPlatformAddressablesBuilder/build_session.json
```

---

### 6.8 状态恢复模块

职责：

- 构建结束后恢复原始 Editor 状态。
- 构建失败后尽量恢复可控状态。

需要恢复的内容：

```text
原始 BuildTarget
原始 Addressables Profile
原始 Group Include In Build 状态
```

恢复策略建议：

```text
正常完成：
  按用户配置恢复原状态

构建失败：
  先恢复 Addressables Profile 和 Group 状态
  是否恢复 BuildTarget 取决于配置
  生成失败报告

Unity 中断或崩溃：
  下次打开 Editor 时检测 session 文件
  提示用户继续、恢复或放弃
```

> **[批注]** 恢复机制能应对"正常失败"，但应对不了"Editor 崩溃"，这个边界计划书没有明确。
>
> 恢复流程依赖 `try/catch` 里的 `TryRestoreAfterFailure`。但如果 `SaveAssets` 已经把临时修改（Group IncludeInBuild、Profile）落盘，之后 Editor 崩溃，这些临时修改就永久写进了 `.asset` 文件。下次打开 Editor，git 会看到被污染的 Addressables 配置文件，且没有自动恢复的机制。
>
> 这不是否定恢复机制的价值——它能覆盖绝大多数场景。但需要在工具文档中明确告知用户：**如果构建期间 Editor 崩溃，可能需要手动执行 `git checkout -- <Addressables-settings-files>` 来还原配置**。具体需要还原的文件包括 `AddressableAssetSettings.asset` 和各 Group 的 `.asset` 文件。
>
> 建议在报告和 UI 中显示 `RestoreSucceeded` 字段（已在 `MpabBuildReport` 中添加），让用户知道恢复是否完整执行。

---

### 6.9 构建报告模块

职责：

- 记录每次构建结果。
- 方便另一个平台开发者或发布人员确认产物来源。
- 方便排查错平台、错 Profile、错 Group 等问题。

报告建议字段：

```text
sessionId
buildTime
UnityVersion
AddressablesVersion
requestedPlatforms
resourceScope

每个平台：
  platformName
  buildTarget
  addressablesProfile
  includedGroups
  excludedGroups
  outputPath
  catalogPath
  contentStatePath
  status
  errorMessage
  duration
```

报告输出目录建议：

```text
BuildOutput/MultiPlatformAddressablesBuilder/Reports
```

报告格式：

```text
JSON
```

后续可以扩展为可视化报告。

> **[批注]** 报告字段设计有部分冗余，缺少工具层独有的关键信息。
>
> `catalogPath`、`contentStatePath`、`AddressablesVersion` 等字段 Addressables 自己的构建日志里已经有了。工具报告重复这些信息意义不大，反而缺少真正有诊断价值的工具层字段：
>
> - **`unmatchedGroups`**：有哪些 Group 没有被任何规则匹配（"漏网之鱼"），这是排查错包的关键信息，Addressables 报告里没有。
> - **`restoreSucceeded`**：恢复操作是否成功执行，以及恢复了哪些内容。
> - **`explicitOverrides`**：哪些 Group 是通过显式列表（而非通配符推断）被归类的。
>
> 已在 `MpabBuildReport` 和 `MpabPlatformBuildReport` 中添加 `RestoreSucceeded`、`UnmatchedGroups` 字段。`UnmatchedGroups` 已在 `MpabPlatformBuildReport` 中存在，报告写入时应确保填充。

---

### 6.10 校验模块

职责：

- 在正式构建前检查配置和资源组织是否明显错误。
- 尽量在构建前暴露问题，避免构建后才发现错包。

第一版建议校验：

```text
当前是否处于 Play Mode 或即将进入 Play Mode
配置文件是否存在
目标平台是否启用
BuildTarget 名称是否合法
Addressables Profile 是否存在
Group 规则是否匹配到实际 Group
是否存在未匹配的 Group
输出目录是否为空或平台隔离
Android 构建是否误包含 QNX Group
QNX 构建是否误包含 Android Group
平台切换后脚本是否编译成功
```

后续可增加依赖校验：

```text
Common Group 是否依赖 AndroidOnly Group
Common Group 是否依赖 QNXOnly Group
AndroidOnly 是否依赖 QNXOnly
QNXOnly 是否依赖 AndroidOnly
```

依赖校验可以使用 Unity AssetDatabase 获取资源依赖关系。

> **[批注]** "Group 规则是否匹配到实际 Group"这条校验方向反了，容易变成噪音警告。
>
> 计划书的校验逻辑是：如果某条规则（如 `QNX_*`）没有匹配到任何 Group，给出警告。但在只配置了 Android 资源的项目里，`QNX_*` 本来就没有对应 Group，这条警告会频繁触发并被开发者忽略——变成"狼来了"。
>
> **更有诊断价值的方向是反向校验**：有哪些 Group 没有被任何规则匹配到（"漏网之鱼"）。这才是真正的配置问题——这些 Group 会在 `ResourceScope = AllIncludedByPlatform` 时被错误地包含进所有平台的构建。
>
> 已在 `MpabBuildValidator.ValidateGroups` 中调整：保留两种警告，但对"规则无匹配 Group"降级为 Info 级别（可被忽略），对"Group 无规则匹配"保持 Warning 级别（需要用户关注）。

---

### 6.11 配置保存模块

职责：

- 在修改 Addressables settings、Profile、Group Include In Build 等可序列化配置后，统一处理 SetDirty 和 SaveAssets。
- 确保 BuildPlayerContent 调用前，目标平台的构建配置已经落盘。
- 确保恢复原始状态后，恢复结果也已经落盘。
- 避免各模块分散调用保存逻辑，导致保存顺序不一致。

需要保存的典型对象：

```text
AddressableAssetSettings
Addressables Group Schema
Profile 相关配置
其他项目中被工具临时修改的构建配置资产
```

推荐流程：

```text
1. 记录原始 Profile 和 Group 状态
2. 切换到目标平台
3. 应用目标平台的 Addressables Profile 和 Group 状态
4. 对被修改对象调用 EditorUtility.SetDirty
5. 调用 AssetDatabase.SaveAssets
6. 调用 Addressables Build
7. 恢复原始 Profile 和 Group 状态
8. 再次调用 SetDirty + SaveAssets
```

注意：

- SaveAssets 是必要的，但必须与恢复机制绑定。
- 临时修改 Group 状态后如果不恢复，会污染项目配置。
- 如果构建失败，也要尽量进入恢复流程并保存恢复后的状态。

---

### 6.12 编译等待与编译错误拦截模块

职责：

- 在 Switch Platform 后等待 Unity 脚本编译完成。
- 在 Addressables Build 前检查是否存在脚本编译错误。
- 如果编译失败，直接中断构建流程，进入 Failed 状态。

原因：

切换平台后可能因为平台宏、程序集定义、插件兼容性等原因触发编译问题。例如：

```text
#if UNITY_ANDROID
#if UNITY_QNX
平台专属插件缺失
平台专属 API 不存在
程序集引用不完整
```

如果脚本已经编译失败，再继续调用 Addressables Build 没有意义。

建议检查：

```text
EditorApplication.isCompiling
EditorUtility.scriptCompilationFailed
```

推荐流程：

```text
SwitchPlatform
WaitForCompilation
CheckCompilation
如果编译失败：
  写入错误信息
  进入 Failed
  执行恢复流程
  生成失败报告
如果编译成功：
  继续 ApplyAddressablesConfig
```

该模块应作为状态机的一部分，而不是只在 UI 点击时检查一次。

---

### 6.13 Controller / CLI 入口预留模块

职责：

- 将 UI 层与核心构建逻辑解耦。
- 提供统一的构建入口，供 EditorWindow 和未来命令行构建共同使用。
- 为后续 CI/CD 接入预留能力。

建议结构：

```text
EditorWindow:
  负责用户选择和展示结果

BuilderController:
  负责接收 BuildRequest，调用构建编排模块

BuildOrchestrator:
  负责真正执行状态机和构建流程

CLI Entry:
  后续通过 Unity -batchmode -executeMethod 调用 BuilderController
```

第一版可以先实现 Controller 静态入口，例如：

```text
BuilderController.RunBuild(request)
```

未来命令行可以复用同一套核心逻辑：

```text
Unity.exe -batchmode -quit -projectPath <project> -executeMethod <CliBuildEntry> -config <configPath> -platforms Android,QNX
```

注意：

- 不要把构建逻辑写在 EditorWindow.OnGUI 中。
- UI 只是入口之一，不应该成为核心逻辑依赖。
- 这样后续接 Jenkins、GitLab CI 或内部构建平台时，不需要重写构建流程。

---

### 6.14 日志模块

职责：

- 输出清晰构建日志。
- 标记当前步骤。
- 标记平台切换、Profile 切换、Group 状态变化和构建结果。

建议日志格式：

```text
[MPAB] Session started: 20260612_153000
[MPAB] Target platforms: Android, QNX
[MPAB] Switching platform: Android
[MPAB] Applying Addressables profile: Android
[MPAB] Included groups: Common_UI, Android_HMI
[MPAB] Building Addressables content...
[MPAB] Android build succeeded
[MPAB] Switching platform: QNX
[MPAB] Applying Addressables profile: QNX
[MPAB] Included groups: Common_UI, QNX_Cluster
[MPAB] QNX build succeeded
[MPAB] Restoring original Editor state
[MPAB] Report written: ...
```

日志不替代报告，但可以帮助实时排查。

---

## 7. 推荐项目目录结构

如果做成可复用 Unity Package，建议结构如下：

```text
Packages/
  com.company.multi-platform-addressables-builder/
    package.json
    README.md
    Editor/
      Config/
      Platform/
      Addressables/
      Rules/
      Build/
      RuntimeState/
      Reports/
      UI/
      Validation/
      Tests/
```

项目内配置建议放在：

```text
Assets/Build/MultiPlatformAddressablesBuildConfig.asset
```

构建运行状态建议放在：

```text
Library/MultiPlatformAddressablesBuilder/
```

构建产物建议放在：

```text
BuildOutput/
  Android/
  QNX/
  MultiPlatformAddressablesBuilder/
    Reports/
```

---

## 8. 第一版开发范围

第一版建议实现：

```text
1. EditorWindow 工具界面
2. ScriptableObject 配置
3. Android / QNX 平台配置
4. Addressables Profile 自动切换
5. Addressables Group Include/Exclude 控制
6. 单平台构建
7. 多平台顺序构建
8. 构建状态持久化
9. 构建完成状态恢复
10. 构建报告生成
11. Play Mode 强拦截
12. 平台切换后的编译等待和编译错误拦截
13. Addressables 配置修改后的 SetDirty + SaveAssets
14. UI 与核心逻辑分离，并预留 CLI 调用入口
```

第一版暂不实现：

```text
1. 多 workspace
2. 并行构建
3. CDN 上传
4. OTA 发布
5. 复杂增量构建
6. 完整依赖污染分析
7. 运行时加载验证
```

---

## 9. 关键技术风险

### 9.1 QNX BuildTarget 不标准

Android 通常是标准 Unity BuildTarget。

QNX 可能是：

```text
标准 BuildTarget
厂商定制 BuildTarget
特殊构建插件
特殊菜单入口
特殊命令行参数
```

工具设计时不要把 QNX 写死为某个固定 enum。

建议：

- 第一版支持通过字符串配置 BuildTarget。
- 如果 QNX 不是标准 BuildTarget，预留自定义平台构建器接口。

---

### 9.2 平台切换触发 reimport

这是 Unity 机制决定的。

工具不能避免，但可以自动化。

需要在产品说明中明确：

```text
工具会自动切换平台；
平台切换可能触发 reimport；
构建耗时取决于项目资源规模和平台导入缓存。
```

---

### 9.3 Domain Reload 中断流程

如果平台切换触发脚本重编译，普通长函数可能中断。

因此建议使用状态机。

第一版至少要保存：

```text
当前步骤
当前平台
原始状态
已完成平台
```

---

### 9.4 Addressables API 版本差异

不同 Addressables 版本的构建 API 可能略有差异。

建议：

- 封装 AddressablesEditorAdapter。
- 不让构建编排模块直接依赖具体 API。
- 在项目接入阶段确认 Addressables package 版本。

---

### 9.5 Group 状态污染

构建时会临时修改 Group Include In Build 状态。

如果构建失败或中断，可能导致项目配置被污染。

因此需要：

- 构建前保存原始 Group 状态。
- 构建后恢复。
- 失败时恢复。
- 下次打开 Editor 时可检测未完成 session。

---

### 9.6 Play Mode 下误触发构建

如果用户在 Play Mode 或即将进入 Play Mode 时触发构建，切平台、修改 Addressables 配置和 SaveAssets 都可能导致严重错误。

处理策略：

```text
在 ValidatePreconditions 阶段强制检查：
EditorApplication.isPlaying
EditorApplication.isPlayingOrWillChangePlaymode
```

如果任一条件为 true，直接阻止构建并提示用户退出 Play Mode。

---

### 9.7 平台切换后脚本编译失败

平台切换后，代码可能因为平台宏、插件差异或程序集引用问题编译失败。

处理策略：

```text
等待 EditorApplication.isCompiling 为 false
检查 EditorUtility.scriptCompilationFailed
如果为 true：
  中断构建
  进入 Failed 状态
  恢复 Addressables 状态
  生成失败报告
```

---

### 9.8 配置未保存导致构建读取旧状态

修改 Profile 或 Group Include In Build 后，如果没有保存，Addressables Build 可能读取到旧配置，或者在 Domain Reload 后丢失临时状态。

处理策略：

```text
应用平台构建配置后：SetDirty + SaveAssets
恢复原始配置后：SetDirty + SaveAssets
```

保存逻辑应由统一模块管理，避免散落在各业务模块中。

---

## 10. 验收标准

### 10.1 单平台构建

选择 Android 构建时：

- 工具能切换到 Android。
- 工具能切换到 Android Profile。
- 工具只 Include Common + Android Groups。
- 工具能调用 Addressables Build。
- Android 输出目录有产物。
- 构建完成后生成报告。

选择 QNX 构建时：

- 工具能切换到 QNX。
- 工具能切换到 QNX Profile。
- 工具只 Include Common + QNX Groups。
- 工具能调用 Addressables Build。
- QNX 输出目录有产物。
- 构建完成后生成报告。

### 10.2 双平台构建

选择 Android + QNX 时：

- 工具能按顺序构建两个平台。
- 两个平台产物分别输出到独立目录。
- 报告中有两个平台的构建记录。
- 构建完成后恢复原始 Profile 和 Group 状态。
- 如果配置要求恢复原平台，则恢复原始 BuildTarget。

### 10.3 配置校验

配置错误时：

- Profile 不存在，应给出明确错误。
- BuildTarget 不合法，应给出明确错误。
- Group 规则匹配不到任何 Group，应给出警告。
- 平台输出目录重叠，应给出错误或高危警告。
- Play Mode 下触发构建，应阻止并报错。
- 平台切换后脚本编译失败，应阻止 Addressables Build 并进入 Failed 状态。
- 修改 Addressables 配置后，应在 Build 前保存；恢复配置后，应再次保存。

---

## 11. 推荐开发顺序

### 阶段 1：工具骨架

- 创建 Unity Package。
- 创建 EditorWindow。
- 创建配置 ScriptableObject。
- 显示平台列表和资源范围选择。

### 阶段 2：配置解析

- 实现平台配置读取。
- 实现 Group 规则匹配。
- 实现 Profile 查找。
- 实现基础 Validate。

### 阶段 3：单平台构建

- 实现平台切换模块。
- 实现 Play Mode 强校验。
- 实现平台切换后的编译等待和编译错误拦截。
- 实现 Profile 切换。
- 实现 Group Include/Exclude。
- 调用 Addressables Build。
- 生成单平台报告。

### 阶段 4：双平台顺序构建

- 实现多平台任务列表。
- 实现构建编排模块。
- 支持 Android -> QNX 顺序构建。
- 支持构建结果汇总。

### 阶段 5：状态机和恢复

- 实现 session 文件。
- 记录当前步骤。
- 保存原始平台、Profile 和 Group 状态。
- 构建结束恢复。
- 构建失败恢复。
- 实现 Addressables 配置修改后的 SetDirty + SaveAssets。
- 实现恢复原始配置后的 SetDirty + SaveAssets。

### 阶段 6：Controller 与 CLI 预留

- 将 EditorWindow 与核心构建逻辑彻底分离。
- 实现 BuilderController 统一入口。
- 预留未来 batchmode executeMethod 调用方式。
- 确保 UI 和未来 CLI 使用同一套 BuildRequest 与 Orchestrator。

### 阶段 7：项目实测

- 在真实项目中配置 Android/QNX Profile。
- 配置 Common/Android/QNX Groups。
- 测试 Android 单平台构建。
- 测试 QNX 单平台构建。
- 测试双平台顺序构建。
- 根据真实 QNX 构建链路调整平台切换接口。

---

## 12. 给开发 Agent 的实现建议

### 12.1 不要自己实现 AB 构建

底层构建交给 Addressables。

工具只做：

```text
配置准备
平台切换
Profile 切换
Group 筛选
Build 调用
状态管理
报告生成
```

### 12.2 不要写死项目路径

不要在代码中写死：

```text
Assets/GameResources/Common
BuildOutput/Android
BuildOutput/QNX
```

这些应来自配置。

### 12.3 不要写死 QNX 细节

QNX 在不同 Unity 版本或厂商集成中可能不一样。

平台模块要预留扩展点。

### 12.4 优先保证状态恢复

这个工具会临时修改 Editor 状态。

必须认真处理：

```text
构建完成恢复
构建失败恢复
中断后提示恢复
```

否则工具会让项目状态变得不可控。

### 12.5 第一版保持克制

第一版只要能稳定完成：

```text
一个 Editor 实例
自动切平台
自动切 Profile
自动选择 Group
调用 Addressables Build
输出双平台产物
生成报告
恢复状态
```

就已经满足主要目标。

### 12.6 保存和恢复必须成对出现

如果工具临时修改 Addressables 配置，必须保证：

```text
修改后保存
构建后恢复
恢复后再保存
```

不能只保存临时状态而不恢复，否则会污染项目配置。

### 12.7 UI 不要承载核心逻辑

EditorWindow 只做参数收集和结果展示。核心逻辑必须放到 Controller / Orchestrator 中，方便后续 CLI 和 CI/CD 复用。

---

## 13. 一句话总结

本工具的核心不是重写 Unity 构建系统，而是：

> 给用户一个可控入口，把多平台 Addressables 构建所需的平台上下文和配置上下文自动准备好，然后调用 Unity 标准构建流程，分别生成 Android 和 QNX 对应平台可用的 AB 产物。

它解决的是：

```text
人工切平台
人工切 Profile
人工选择 Group
人工重复构建
人工整理输出
人工排查错平台产物
```

它不解决的是：

```text
跨平台通用 AB
不切平台构建任意平台
完全避免 reimport
替代 Addressables 构建管线
```
