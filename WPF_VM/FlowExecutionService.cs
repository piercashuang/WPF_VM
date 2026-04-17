using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WPF_VM
{
    public interface IFlowExecutionService
    {
        event EventHandler<FlowExecutionStatusChangedEventArgs> StatusChanged;

        event EventHandler<FlowExecutionHistoryEventArgs> HistoryRecorded;

        bool IsRunning { get; }

        Task RunAsync(string flowName, IReadOnlyList<FlowNodeViewModel> nodes, IReadOnlyList<FlowConnectionViewModel> connections);

        void CancelCurrentRun();
    }

    public sealed class FlowExecutionService : IFlowExecutionService
    {
        private readonly Dispatcher dispatcher;
        private readonly SemaphoreSlim runGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource currentRunTokenSource;
        private bool isRunning;

        public FlowExecutionService(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public event EventHandler<FlowExecutionStatusChangedEventArgs> StatusChanged;

        public event EventHandler<FlowExecutionHistoryEventArgs> HistoryRecorded;

        public bool IsRunning
        {
            get { return isRunning; }
            private set { isRunning = value; }
        }

        public async Task RunAsync(string flowName, IReadOnlyList<FlowNodeViewModel> nodes, IReadOnlyList<FlowConnectionViewModel> connections)
        {
            if (!runGate.Wait(0))
            {
                PublishStatus("流程正在运行中，请先等待当前任务结束。", null, true);
                return;
            }

            currentRunTokenSource = new CancellationTokenSource();
            var token = currentRunTokenSource.Token;
            var stopwatch = Stopwatch.StartNew();

            IsRunning = true;
            PublishStatus(string.Format("开始执行 {0}。", flowName), TimeSpan.Zero, true);

            try
            {
                var orderedNodes = TopologicalSort(nodes, connections);

                foreach (var node in orderedNodes)
                {
                    token.ThrowIfCancellationRequested();
                    PublishStatus(string.Format("正在执行模块：{0}", node.Title), stopwatch.Elapsed, true);

                    await Task.Run(async () =>
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Delay(180, token).ConfigureAwait(false);
                    }, token).ConfigureAwait(false);

                    PublishHistory(string.Format("{0} 执行完成", node.Title));
                }

                stopwatch.Stop();
                PublishStatus(string.Format("{0} 执行完成。", flowName), stopwatch.Elapsed, false);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                PublishStatus("流程已取消。", stopwatch.Elapsed, false);
                PublishHistory("流程执行已取消");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PublishStatus(string.Format("流程执行失败：{0}", ex.Message), stopwatch.Elapsed, false);
                PublishHistory(string.Format("流程执行失败：{0}", ex.Message));
            }
            finally
            {
                IsRunning = false;

                if (currentRunTokenSource != null)
                {
                    currentRunTokenSource.Dispose();
                    currentRunTokenSource = null;
                }

                runGate.Release();
            }
        }

        public void CancelCurrentRun()
        {
            if (currentRunTokenSource != null && !currentRunTokenSource.IsCancellationRequested)
            {
                currentRunTokenSource.Cancel();
                PublishStatus("已发送停止信号，正在等待流程结束。", null, true);
            }
        }

        private IList<FlowNodeViewModel> TopologicalSort(IReadOnlyList<FlowNodeViewModel> nodes, IReadOnlyList<FlowConnectionViewModel> connections)
        {
            var nodeOrder = nodes.Select((node, index) => new { node.Id, Index = index })
                .ToDictionary(item => item.Id, item => item.Index);

            var portToNode = nodes
                .SelectMany(node => node.InputPorts.Concat(node.OutputPorts))
                .ToDictionary(port => port.Id, port => port.NodeId);

            var outgoing = nodes.ToDictionary(node => node.Id, _ => new List<Guid>());
            var indegree = nodes.ToDictionary(node => node.Id, _ => 0);

            foreach (var connection in connections)
            {
                var sourceNodeId = portToNode[connection.SourcePortId];
                var targetNodeId = portToNode[connection.TargetPortId];

                if (sourceNodeId == targetNodeId)
                {
                    continue;
                }

                outgoing[sourceNodeId].Add(targetNodeId);
                indegree[targetNodeId]++;
            }

            var queue = new SortedSet<NodeQueueItem>(
                indegree
                    .Where(item => item.Value == 0)
                    .Select(item => new NodeQueueItem(item.Key, nodeOrder[item.Key])));

            var result = new List<FlowNodeViewModel>();

            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);

                result.Add(nodes.First(node => node.Id == current.NodeId));

                foreach (var nextNodeId in outgoing[current.NodeId])
                {
                    indegree[nextNodeId]--;

                    if (indegree[nextNodeId] == 0)
                    {
                        queue.Add(new NodeQueueItem(nextNodeId, nodeOrder[nextNodeId]));
                    }
                }
            }

            if (result.Count != nodes.Count)
            {
                throw new InvalidOperationException("流程图中存在循环依赖，当前版本无法执行环状连线。");
            }

            return result;
        }

        private void PublishStatus(string message, TimeSpan? elapsed, bool running)
        {
            if (dispatcher.CheckAccess())
            {
                StatusChanged?.Invoke(this, new FlowExecutionStatusChangedEventArgs(message, elapsed, running));
                return;
            }

            dispatcher.BeginInvoke(new Action(() =>
                StatusChanged?.Invoke(this, new FlowExecutionStatusChangedEventArgs(message, elapsed, running))));
        }

        private void PublishHistory(string module)
        {
            var args = new FlowExecutionHistoryEventArgs(DateTime.Now.ToString("HH:mm:ss"), module);

            if (dispatcher.CheckAccess())
            {
                HistoryRecorded?.Invoke(this, args);
                return;
            }

            dispatcher.BeginInvoke(new Action(() => HistoryRecorded?.Invoke(this, args)));
        }

        private sealed class NodeQueueItem : IComparable<NodeQueueItem>
        {
            public NodeQueueItem(Guid nodeId, int order)
            {
                NodeId = nodeId;
                Order = order;
            }

            public Guid NodeId { get; private set; }

            public int Order { get; private set; }

            public int CompareTo(NodeQueueItem other)
            {
                if (other == null)
                {
                    return 1;
                }

                var orderCompare = Order.CompareTo(other.Order);
                return orderCompare != 0 ? orderCompare : NodeId.CompareTo(other.NodeId);
            }
        }
    }

    public sealed class FlowExecutionStatusChangedEventArgs : EventArgs
    {
        public FlowExecutionStatusChangedEventArgs(string message, TimeSpan? elapsed, bool isRunning)
        {
            Message = message;
            Elapsed = elapsed;
            IsRunning = isRunning;
        }

        public string Message { get; private set; }

        public TimeSpan? Elapsed { get; private set; }

        public bool IsRunning { get; private set; }
    }

    public sealed class FlowExecutionHistoryEventArgs : EventArgs
    {
        public FlowExecutionHistoryEventArgs(string time, string module)
        {
            Time = time;
            Module = module;
        }

        public string Time { get; private set; }

        public string Module { get; private set; }
    }
}
