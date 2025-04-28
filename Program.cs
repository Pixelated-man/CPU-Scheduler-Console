using System;
using System.Collections.Generic;
using System.Linq;

namespace CPU_Scheduler_Console
{
    public class Process
    {
        public int Id { get; set; }
        public int ArrivalTime { get; set; }
        public int BurstTime { get; set; }
        public int RemainingTime { get; set; }
        public int Priority { get; set; }
        public int CurrentPriority { get; set; }
        public int TimeInCPU { get; set; }
        public int WaitingTime { get; set; }
        public int TurnaroundTime { get; set; }

        public Process(int id, int arrivalTime, int burstTime, int priority = 0)
        {
            Id = id;
            ArrivalTime = arrivalTime;
            BurstTime = burstTime;
            RemainingTime = burstTime;
            Priority = priority;
            CurrentPriority = 0;
            TimeInCPU = 0;
        }
    }

    public class SchedulerMetrics
    {
        public double AverageWaitingTime { get; set; }
        public double AverageTurnaroundTime { get; set; }
        public double CPUUtilization { get; set; }
        public double Throughput { get; set; }

        public void CalculateMetrics(List<Process> processes, int totalTime)
        {
            if (processes.Count == 0) return;

            AverageWaitingTime = processes.Average(p => p.WaitingTime);
            AverageTurnaroundTime = processes.Average(p => p.TurnaroundTime);
            CPUUtilization = (double)processes.Sum(p => p.BurstTime) / totalTime * 100;
            Throughput = (double)processes.Count / totalTime;
        }
    }

    //algorithms
    public abstract class SchedulingAlgorithm
    {
        public List<Process> ReadyQueue { get; set; } = new List<Process>();
        public Process RunningProcess { get; set; }
        public abstract string AlgorithmName { get; }

        public abstract Process GetNextProcess();
        public abstract void Execute();
        public virtual void AddProcess(Process process) => ReadyQueue.Add(process);
    }

    public class FCFS : SchedulingAlgorithm
    {
        public override string AlgorithmName => "First-Come, First-Served";
        public override Process GetNextProcess() => ReadyQueue.OrderBy(p => p.ArrivalTime).FirstOrDefault();
        public override void Execute() => RunningProcess = GetNextProcess();
    }

    public class SJF : SchedulingAlgorithm
    {
        public override string AlgorithmName => "Shortest Job First";
        public override Process GetNextProcess() => ReadyQueue.OrderBy(p => p.BurstTime).FirstOrDefault();
        public override void Execute() => RunningProcess = GetNextProcess();
    }

    public class RR : SchedulingAlgorithm
    {
        private const int Quantum = 4;
        public override string AlgorithmName => "Round Robin";

        public override Process GetNextProcess() => ReadyQueue.FirstOrDefault();

        public override void Execute()
        {
            if (RunningProcess == null || RunningProcess.TimeInCPU >= Quantum)
            {
                if (RunningProcess != null && RunningProcess.RemainingTime > 0)
                {
                    ReadyQueue.Add(RunningProcess);
                }
                RunningProcess = GetNextProcess();
                ReadyQueue.Remove(RunningProcess);
                if (RunningProcess != null) RunningProcess.TimeInCPU = 0;
            }
            RunningProcess.TimeInCPU++;
        }
    }

    public class Priority : SchedulingAlgorithm
    {
        public override string AlgorithmName => "Priority Scheduling";
        public override Process GetNextProcess() => ReadyQueue.OrderBy(p => p.Priority).FirstOrDefault();
        public override void Execute() => RunningProcess = GetNextProcess();
    }

    public class SRTF : SchedulingAlgorithm
    {
        public override string AlgorithmName => "Shortest Remaining Time First";
        public override Process GetNextProcess() => ReadyQueue.OrderBy(p => p.RemainingTime).FirstOrDefault();

        public override void Execute()
        {
            var nextProcess = GetNextProcess();
            if (nextProcess != null && (RunningProcess == null ||
                nextProcess.RemainingTime < RunningProcess.RemainingTime))
            {
                ReadyQueue.Remove(nextProcess);
                if (RunningProcess != null) ReadyQueue.Add(RunningProcess);
                RunningProcess = nextProcess;
            }
        }
    }

    public class MLFQ : SchedulingAlgorithm
    {
        private readonly List<Queue<Process>> _queues = new List<Queue<Process>>();
        private readonly int[] _timeQuantums = { 4, 8, 16 };

        public override string AlgorithmName => "Multi-Level Feedback Queue";

        public MLFQ()
        {
            for (int i = 0; i < 3; i++) _queues.Add(new Queue<Process>());
        }

        public override Process GetNextProcess()
        {
            for (int i = 0; i < 3; i++)
                if (_queues[i].Count > 0) return _queues[i].Peek();
            return null;
        }

        public override void AddProcess(Process process)
        {
            process.CurrentPriority = 0;
            _queues[0].Enqueue(process);
        }

        public override void Execute()
        {
            var nextProcess = GetNextProcess();
            if (nextProcess != null)
            {
                int queueLevel = nextProcess.CurrentPriority;
                int timeQuantum = _timeQuantums[queueLevel];

                if (RunningProcess != nextProcess)
                {
                    _queues[queueLevel].Dequeue();
                    RunningProcess = nextProcess;
                }
                else if (RunningProcess.TimeInCPU >= timeQuantum)
                {
                    _queues[queueLevel].Dequeue();
                    if (queueLevel < 2) RunningProcess.CurrentPriority++;
                    _queues[RunningProcess.CurrentPriority].Enqueue(RunningProcess);
                    RunningProcess = GetNextProcess();
                }
                RunningProcess.TimeInCPU++;
            }
        }
    }

    //scheduler
    public class Scheduler
    {
        public SchedulerMetrics Run(string algorithmName, List<Process> processes)
        {
            SchedulingAlgorithm algorithm = CreateAlgorithm(algorithmName);
            int currentTime = 0;
            int completedProcesses = 0;
            int totalProcesses = processes.Count;

            while (completedProcesses < totalProcesses)
            {
                // Add arrived processes
                foreach (var p in processes.Where(p => p.ArrivalTime == currentTime))
                    algorithm.AddProcess(p);

                // Execute scheduler
                algorithm.Execute();
                currentTime++;

                // Update completed processes
                if (algorithm.RunningProcess != null && --algorithm.RunningProcess.RemainingTime <= 0)
                {
                    algorithm.RunningProcess.TurnaroundTime = currentTime - algorithm.RunningProcess.ArrivalTime;
                    algorithm.RunningProcess.WaitingTime = algorithm.RunningProcess.TurnaroundTime - algorithm.RunningProcess.BurstTime;
                    completedProcesses++;
                }
            }

            var metrics = new SchedulerMetrics();
            metrics.CalculateMetrics(processes, currentTime);
            return metrics;
        }

        private SchedulingAlgorithm CreateAlgorithm(string name)
        {
            return name switch
            {
                "FCFS" => new FCFS(),
                "SJF" => new SJF(),
                "RR" => new RR(),
                "Priority" => new Priority(),
                "SRTF" => new SRTF(),
                "MLFQ" => new MLFQ(),
                _ => throw new ArgumentException("Invalid algorithm")
            };
        }
    }

    //interfasce
    class Program
    {
        static void Main()
        {
            var scheduler = new Scheduler();
            var processes = new List<Process>
            {
                new Process(1, 0, 5),
                new Process(2, 1, 3),
                new Process(3, 2, 8),
                new Process(4, 3, 6)
            };

            Console.WriteLine("Available Algorithms:");
            Console.WriteLine("1. FCFS\n2. SJF\n3. RR\n4. Priority\n5. SRTF\n6. MLFQ");
            Console.Write("Select algorithm (1-6): ");
            int choice = int.Parse(Console.ReadLine());

            string algorithm = choice switch
            {
                1 => "FCFS",
                2 => "SJF",
                3 => "RR",
                4 => "Priority",
                5 => "SRTF",
                6 => "MLFQ",
                _ => "FCFS"
            };

            var results = scheduler.Run(algorithm, processes);

            Console.WriteLine($"\nResults for {algorithm}:");
            Console.WriteLine($"Average Waiting Time: {results.AverageWaitingTime:F2} ms");
            Console.WriteLine($"Average Turnaround Time: {results.AverageTurnaroundTime:F2} ms");
            Console.WriteLine($"CPU Utilization: {results.CPUUtilization:F2}%");
            Console.WriteLine($"Throughput: {results.Throughput:F2} processes/ms");
        }
    }
}