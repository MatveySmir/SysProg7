using SysLab6;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Interop;

namespace SysLab7
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Идентификатор оконного сообщения Windows, сообщающий о завершении работы потока
        const int WM_THREAD_FINISHED = User32.WM_USER + 1;
        const int WM_THREAD_HAVE_RESULT = User32.WM_USER + 2;

        // Механизм передачи сообщений из основного потока в первый рабочий поток
        private List<ThreadStruct> MessagesForThreads;

        public MainWindow()
        {
            InitializeComponent();
            // Создание механизма получения сообщений первым рабочим потоком
            MessagesForThreads = new List<ThreadStruct>();
        }

        // Метод инициализации механизма получения сообщений от второго рабочего потока
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Создание экземпляра класса HwndSource
            HwndSource HwndSource = PresentationSource.FromVisual(this) as HwndSource;
            // Устанавка перехватчика оконных сообщений Windows для окна текущей формы
            HwndSource.AddHook(WndProc);
        }

        // Функция-перехватчик оконных сообщений для текущей формы
        private IntPtr WndProc(IntPtr Hwnd, int Msg, IntPtr WParam, IntPtr LParam, ref bool Handled)
        {
            switch (Msg)
            {
                case WM_THREAD_FINISHED:
                    int Id = (int)WParam;   //получаем Id нашего потока
                    int index = MessagesForThreads.Count - 1;   // вводим индексы(с конца)
                    while (index >= 0 && MessagesForThreads[index].Id != Id) index--; // начинаем перебирать потоки по индексу и чтобы Id потока совпадал с полученным Id
                    if (index >= 0) // если нашли поток
                    {
                        MessagesForThreads.RemoveAt(index);// удаляем его из списка
                    }
                    WorkingThreadCountTextBox.Text = MessagesForThreads.Count.ToString(); // вписываем новое значение в TextBox(оставшиеся потоки)

                    if (MessagesForThreads.Count == 0) // если рабочих потоков не осталось
                    {
                        StartButton.Content = "Запустить процесс поиска простых чисел";
                    }
                    
                    // Установка флага успешной обработки сообщения
                    Handled = true;
                    break;
                case WM_THREAD_HAVE_RESULT:
                    ResultsListBox.Items.Add(WParam.ToString());
                    //// Установка флага успешной обработки сообщения
                    Handled = true;
                    break;
            }
            // Возврат формального результата работы функции
            return IntPtr.Zero;
        }

        // Метод установки доступности элементов пользовательского интерфейса
        private void SetEnabled(bool enabled)
        {
            StartButton.IsEnabled = enabled;
            // Обновление пользовательского интерфейса
            System.Windows.Forms.Application.DoEvents();
        }

        private int GetStartNum()
        {
            try
            {
                return int.Parse(StartNumTextBox.Text);
            }
            catch (FormatException)
            {
                ResultsListBox.Items.Add("Ошибка ввода начального значения");
                return -1;
            }
        }

        private int GetFinishNum()
        {
            try
            {
                return int.Parse(FinishNumTextBox.Text);
            }
            catch (FormatException)
            {
                ResultsListBox.Items.Add("Ошибка ввода конечного значения");
                return -1;
            }
        }

        private int GetThreadCount()
        {
            try
            {
                return int.Parse(ThreadCountTextBox.Text);
            }
            catch (FormatException)
            {
                ResultsListBox.Items.Add("Ошибка ввода количества потоков");
                return -1;
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessagesForThreads.Count == 0)
            {
                int num1 = GetStartNum();
                int num2 = GetFinishNum();
                int count = GetThreadCount();
                // создаем поток,немного изменить для последнего потока(если поток не последний, то....)
                for (int i = 0; i < count; i++) {

                    int part = (num2 - num1) / count;
                    int StartNum = num1 + i * part;
                    int FinishNum = StartNum + part;

                    Thread Thread = new Thread(ExecuteThread);
                    ThreadParams Params = new ThreadParams();

                    Params.StartNum = StartNum;
                    Params.FinishNum = FinishNum;

                    Params.WindowHandle = new WindowInteropHelper(this).Handle;
                    Params.ThreadStruct = new ThreadStruct();
                    Params.ThreadStruct.Id =Thread.ManagedThreadId;
                    Params.ThreadStruct.Queue = new ConcurrentQueue<QueueMessage>();
                    MessagesForThreads.Add(Params.ThreadStruct);
                    Thread.Start(Params);
                    WorkingThreadCountTextBox.Text = MessagesForThreads.Count.ToString(); // отображаем количество рабочих потоков
                    System.Windows.Forms.Application.DoEvents();
                }
                StartButton.Content = "Остановить процесс поиска простых чисел";
            }
            else
            {
                for (int i = 0; i < MessagesForThreads.Count; i++)
                {
                    QueueMessage Msg = new QueueMessage();
                    Msg.Type = 1;
                    MessagesForThreads[i].Queue.Enqueue(Msg); //отправить сообщения в нужные потоки, чтобы те прекратили работу.
                }
                SetEnabled(false);
                StartButton.Content = "Запустить процесс поиска простых чисел";
            }
        }

        private void ExecuteThread(object Params)
        {
            ThreadParams threadParams = Params as ThreadParams;
            int startNum = threadParams.StartNum;
            int finishNum = threadParams.FinishNum;
            bool flag = false;
            ThreadStruct threadStruct = threadParams.ThreadStruct;
            while (!flag && startNum <= finishNum)
            {
                if (IsNumberPrime(startNum))
                {
                    User32.PostMessage(threadParams.WindowHandle, WM_THREAD_HAVE_RESULT, startNum, 0);
                }
                startNum++;
                if (threadStruct.Queue.TryDequeue(out var result))
                {
                    int type = result.Type;
                    if (type == 1)
                    {
                        flag = true;
                    }
                }
            }
            User32.PostMessage(threadParams.WindowHandle, WM_THREAD_FINISHED, Thread.CurrentThread.ManagedThreadId, 0);
        }

        // Метод анализа числа
        private bool IsNumberPrime(long Num)
        {
            Num = Math.Abs(Num);
            if (Num > 1)
            {
                long Divider = 2;
                while (Num % Divider != 0) { Divider++; }
                return (Num == Divider);
            }
            else
            {
                return true;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = MessagesForThreads.Count > 0;
        }
    }
}
