using System;
using System.Windows.Threading;

namespace FFVideoConverter
{
    class MethodRunner<T>
    {
        private readonly DispatcherTimer timer;
        private readonly Action<T> action;
        private T argument;
        bool shouldRun;

        public MethodRunner(Action<T> action, TimeSpan maxFrequency)
        {
            this.action = action;
            timer = new DispatcherTimer();
            timer.Interval = maxFrequency;
            timer.Tick += Timer_Tick; ;
        }

        public void Run(T argument)
        {
            if (timer.IsEnabled == false)
            {
                action(argument);
                timer.Start();
            }
            else
            {
                this.argument = argument;
                shouldRun = true;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (shouldRun)
            {
                shouldRun = false;
                action(argument);
            }
            else
            {
                timer.Stop();
            }
        }
    }
}