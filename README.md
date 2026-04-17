# WPF_VM

## 项目简介

这是一个参考海康 VisionMaster 工作台风格实现的 WPF 桌面项目，目标是搭建一个可扩展的视觉流程编辑器壳子，为后续接入海康相机 SDK、流程执行引擎和参数面板打基础。

当前版本已经具备：

- 深色工作台主界面
- `WindowChrome` 标题栏拖动与系统窗口控制
- 左侧工具分类与模块工具箱
- 中央流程图编辑区
- 节点拖动、端口拖拽建线、连线删除、端点重连
- 右侧图像/结果面板
- 基于受控线程池的流程执行服务雏形

## 当前界面结构

- 顶部：菜单栏、工具栏、窗口控制按钮
- 左侧：视觉模块分类和快捷工具区
- 中央：流程画布，支持节点和连线交互
- 右侧：流程图像预览、模块结果
- 底部：历史结果、帮助、状态栏

## 技术选型

本项目当前技术选型固定为：

- `.NET Framework 4.8`
- `WPF`
- `MVVM`
- `WindowChrome`
- `Canvas + Thumb` 流程编辑交互
- `Task + SemaphoreSlim + CancellationToken` 受控线程池执行模型

对应职责如下：

- `WindowChrome`：负责无边框窗口的系统拖动、双击最大化和窗口控制按钮兼容
- `MVVM`：负责界面状态、工具箱数据、流程图数据和执行状态管理
- `Canvas + Thumb`：负责节点拖动、端口拖拽建线、连线重连
- `Task + SemaphoreSlim + CancellationToken`：负责流程执行期间的后台调度、单流程单活跃运行和取消控制

## 流程编辑器交互规则

当前流程编辑器按以下规则工作：

- 普通处理节点：`1 入 1 出`
- 图像源节点：`0 入 1 出`
- 输出结果节点：`1 入 0 出`
- 节点可以在流程画布中拖动
- 从输出端口拖到输入端口可以创建连线
- 已存在的连线支持选中后按 `Delete` 删除
- 已选中的连线支持拖动起点或终点进行重连
- 禁止节点自连
- 禁止同一个输入端口被多条连线同时占用

连线视觉风格固定为深色画布上的橙色正交折线，尽量贴近 VisionMaster 的交互观感。

## 线程模型

界面线程和流程执行线程明确分离：

- UI 更新始终运行在 `Dispatcher` 线程
- 流程执行运行在线程池任务中
- 同一时间只允许一个流程处于活动运行状态
- 停止流程时通过 `CancellationToken` 发出取消信号
- 历史记录、状态栏和耗时信息由执行服务统一回传

本轮执行层先实现串行拓扑调度，不做并行分支执行优化。

## 后续扩展点

后续可以在现有结构上继续接入：

- 海康 `MvCameraControl.Net.dll` 图像采集
- 流程参数编辑面板
- 模块配置弹窗
- 流程保存/加载
- 算法模块注册机制
- 真正的节点执行器和结果对象传递

## 目录说明

- [MainWindow.xaml](D:\Repositories\test\WPF_VM\WPF_VM\MainWindow.xaml)：主界面
- [MainViewModel.cs](D:\Repositories\test\WPF_VM\WPF_VM\MainViewModel.cs)：页面级 ViewModel
- [FlowEditorViewModel.cs](D:\Repositories\test\WPF_VM\WPF_VM\FlowEditorViewModel.cs)：流程编辑器模型与交互状态
- [FlowExecutionService.cs](D:\Repositories\test\WPF_VM\WPF_VM\FlowExecutionService.cs)：流程执行服务
