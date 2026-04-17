# VisionMaster WPF Workbench

## 项目目标

这是一个参考海康 VisionMaster 工作台风格实现的 WPF 桌面项目。当前版本重点完成了四层分层结构、流程图编辑器、受控线程池执行框架，以及基于海康 `MvCameraControl.Net.dll` 的相机扫描与采集窗口。

## 分层结构

项目按如下目录拆分：

- `01_VmFunBase`
  - 基类层
  - 提供 `ViewModelBase`、`RelayCommand` 等基础 MVVM 能力
- `02_VmFunModule`
  - 算子与流程模块层
  - 提供工具箱目录、流程节点/端口/连线模型、流程编辑器、线程池执行服务
- `03_VmCamera`
  - 相机插件层
  - 封装海康 SDK 初始化、相机扫描、打开/关闭、开始/停止采集、软触发、取流回调
- `04_VisionMaster`
  - 主程序层
  - 提供 WPF 工作台主窗口、相机管理窗口、流程编辑交互和图像预览

## 技术选型

当前技术选型固定为：

- `.NET Framework 4.8`
- `WPF`
- `MVVM`
- `WindowChrome`
- `Canvas + Thumb` 流程编辑
- `Task + SemaphoreSlim + CancellationToken` 受控线程池执行
- `MvCameraControl.Net.dll` 海康工业相机 SDK 接口封装

对应职责如下：

- `WindowChrome`
  - 负责无边框窗口拖动、双击标题栏最大化和系统按钮命中
- `MVVM`
  - 负责页面状态、工具箱、流程图模型、相机窗口状态和执行状态管理
- `Canvas + Thumb`
  - 负责节点拖动、端口拖拽建线、连线选中删除、端点重连
- `Task + SemaphoreSlim + CancellationToken`
  - 负责单流程单活跃执行、后台线程池调度、UI 与执行线程隔离
- `MvCameraControl.Net.dll`
  - 负责海康相机设备枚举、连接、参数设置、取流和软触发

## 当前已实现能力

- VisionMaster 风格深色工作台主界面
- `WindowChrome` 标题栏拖动与窗口控制按钮
- 左侧分类工具箱与模块添加
- 中央流程画布
  - 节点拖动
  - 端口拖拽建线
  - 连线选中删除
  - 连线端点重连
- 右侧流程概览与历史结果区
- 受控线程池执行服务
  - 拓扑排序
  - 串行执行
  - 取消当前运行
  - 状态与历史结果事件回传
- 海康相机管理窗口
  - 扫描相机
  - 打开/关闭相机
  - 连续采集
  - 软触发采集
  - 图像预览

## 流程编辑器规则

- 普通处理节点：`1 入 1 出`
- 图像源节点：`0 入 1 出`
- 输出结果节点：`1 入 0 出`
- 禁止节点自连
- 禁止同一输入端口被多条连线同时占用
- 节点移动时，关联连线会实时重算正交路径

## 线程模型

- UI 更新始终运行在 `Dispatcher` 线程
- 流程执行任务运行在线程池
- 同一时刻只允许一个流程处于活动运行状态
- 停止流程通过 `CancellationToken` 发出取消信号
- 历史记录和状态栏由执行服务统一回传

## 海康 SDK 接入说明

当前海康相机能力来自：

- `05_Depends\Libs\MvCameraControl.Net.dll`
- `05_Depends\Libs\MvCameraControl.Net.XML`

封装位置：

- `03_VmCamera\VmCameraSdk.cs`
- `03_VmCamera\HikCameraService.cs`
- `03_VmCamera\CameraDeviceInfo.cs`

当前封装已覆盖：

- SDK 初始化与释放
- 相机设备扫描
- 设备可访问性判断
- 打开与关闭设备
- 设置连续采集/软触发模式
- 开始与停止取流
- 软触发命令
- 图像帧回调

## 后续扩展点

- 将相机参数页扩展为完整海康参数树
- 接入真实图像源模块参数面板
- 把相机采集结果写回流程图像预览区
- 增加流程保存/加载
- 增加算子插件注册与动态加载
- 引入真实算法执行器和模块参数面板