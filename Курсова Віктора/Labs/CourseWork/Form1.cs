using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using Npgsql;
using NeuralNetwork;
using System.IO;


namespace CourseWork
{
    public partial class Form1 : Form
    {
        // змінні для зчитування з бд
        DataSet ds = new DataSet(); // множина даних для запиту
        DataTable dt = new DataTable(); // таблиця даних для запиту
        string[] ParametersNames; // назви параметрів (свердловини)
        Stopwatch sWatch = new Stopwatch(); // клас для вимірювання часу навчання мережі BackPropagation
        Stopwatch sWatch2 = new Stopwatch(); // клас для вимірювання часу навчання мережі FNNN
        TimeSpan tSpan; // клас для перетворення даних про час навчання

        // змінні для створення таблиць із результатами роботи мережі (BackPropagation)
        DataTable ResultTrainBackPropagation; // результати роботи BackPropagation для навчальної вибірки
        DataTable ResultTestBackPropagation; //  результати роботи BackPropagation для тестової вибірки

        // змінні для створення таблиць із результатами роботи мережі (FNNN)
        DataTable ResultTrainFNNN; // результати роботи FNNN для навчальної вибірки
        DataTable ResultTestFNNN; //  результати роботи FNNN для тестової вибірки

        // навчальна вибірка
        int TRAINING_PATTERNS; // кількість паттернів у навчальній вибірці
        int PARAMETERS; // кількість параметрів  
        int TestAmount; // кількість випадкової вибірки для тестування мереж

        // Параметри BackPropagation
        int[] layerSizes; // кількість шарів та нейронів у шарах
        // активаційні функції для кожного шару
        TransferFunction[] TFuncs = new TransferFunction[3] {TransferFunction.None,
                                                               TransferFunction.Sigmoid,
                                                               TransferFunction.Sigmoid};
        double LEARNING_RATE1; // швидкість навчання 
        double MOMENTUM; // коефіцієнт для навчання
        double MIN_ERROR; // мінімальна похибка для навчання
        double[] output = new double[1];

        // Параметри FNNN
        int[] layerSizesFNNN; // кількість шарів та нейронів у шарах
        // активаційні функції для кожного шару
        TransferFunction[] TFuncsFNNN = new TransferFunction[3] {TransferFunction.None,
                                                               TransferFunction.Sigmoid,
                                                               TransferFunction.Sigmoid};
        double LEARNING_RATE2; // швидкість навчання 
        double MOMENTUM2; // коефіцієнт для навчання
        double MIN_ERROR2; // мінімальна похибка для навчання
        double[] output2 = new double[1];

        // масиви параметрів та відповідей
        double[][] inputs; // завантажені дані
        double[][] answers; // відповіді (нормальний закон розподілу)
        double[][] inputsFrequency; // варіаційний ряд завантажених даних
        double[][] tParameters; // коефіцієнти
        double[][] FNNNinputs; // параметри для мережі FNNN

        // Випадкова тестова вибірка
        double[][] testArray;
        double[][] testArrayTParameters;
        double[][] testArrayFNNNinputs;

        // мережа BackPropagation
        BackPropagationNetwork bpn = null;

        // мережа FNNN
        BackPropagationNetwork fnnn = null;

        public Form1()
        {
            InitializeComponent();

            this.RandomResults.DataSource = ResultTestBackPropagation;
            saveResultsFileDialog.Filter = "Текстові документи|*.txt";
            saveFileDialog1.Filter = "Текстові документи|*.txt";
            saveFileDialog3.Filter = "Текстові документи|*.txt";
            saveFileDialog4.Filter = "Текстові документи|*.txt";

        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        // завантаження даних з бд для навчання мереж
        private void DownloadFromDB_Click(object sender, EventArgs e)
        {
            try
            {
                //  під'єднання до бд
                string connstring = "Server=127.0.0.1;Port=5432;User Id=postgres;Password=1postgres;Database=Labs;";
                NpgsqlConnection conn = new NpgsqlConnection(connstring);
                conn.Open();
                string sql = "SELECT * FROM Task1 ORDER BY id";
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(sql, conn);
                ds.Reset();
                da.Fill(ds);
                dt = ds.Tables[0];

                conn.Close();

                TRAINING_PATTERNS = dt.Columns.Count - 1;
                PARAMETERS = dt.Rows.Count;

                inputs = new double[TRAINING_PATTERNS][];
                answers = new double[TRAINING_PATTERNS][];
                tParameters = new double[TRAINING_PATTERNS][];

                for (int i = 0; i < TRAINING_PATTERNS; i++)
                {
                    inputs[i] = new double[PARAMETERS];
                    answers[i] = new double[1];
                    tParameters[i] = new double[2];
                }

                // зчитування параметрів
                for (int i = 0; i < TRAINING_PATTERNS; i++)
                {
                    for (int k = 0; k < dt.Rows.Count; k++)
                        inputs[i][k] = Convert.ToDouble(dt.Rows[k][i + 1]);

                }

                // Варіаційний ряд
                inputsFrequency = Statistic.CreateArrayFromDictionary(inputs);
                double tA, tE;

                for (int i = 0; i < TRAINING_PATTERNS; i++)
                {
                    Statistic.t(inputsFrequency[i], out tA, out tE);
                    tParameters[i][0] = tA;
                    tParameters[i][1] = tE;
                }

                // Формування відповідей
                for (int i = 0; i < TRAINING_PATTERNS; i++)
                    answers[i][0] = Statistic.CheckNormal(tParameters[i][0], tParameters[i][1]);

                // Формування параметрів для мережі FNNN
                FNNNinputs = Normalize.FormDataFNNN(tParameters, TRAINING_PATTERNS);


                ParametersNames = new string[PARAMETERS];

                for(int i = 0; i < dt.Rows.Count; i++)
                    ParametersNames[i] = Convert.ToString(i + 1);

                MessageBox.Show("Дані для навчання мережі завантажено");
            }
            catch (Exception msg)
            {
                MessageBox.Show(msg.ToString());
                throw;
            }

        }

        // генерування тестових даних
        private void GenerateRandom_Click(object sender, EventArgs e)
        {
            if(PARAMETERS == 0)
            {
                MessageBox.Show("Спочатку завантажте дані");
                return;
            }
            bool result = Int32.TryParse(this.testAmountText.Text, out TestAmount);
            if (result == false)
            {
                MessageBox.Show("Введіть кількість векторів з випадковими даними, які потрібно згенерувати");
                return;
            }
            
            if (TestAmount <= 0)
            {
                MessageBox.Show("Недопустиме значення");
                return;
            }   
            testArray = GenerateTestNumbers.GenerateOutput(PARAMETERS, TestAmount); // створення тестової вибірки
            testArray = Statistic.CreateArrayFromDictionary(testArray);

            double tA, tE;
            testArrayTParameters = new double[TestAmount][];

            for (int i = 0; i < TestAmount; i++)
                testArrayTParameters[i] = new double[2];
            
            for (int i = 0; i < TestAmount; i++)
            {
                Statistic.t(testArray[i], out tA, out tE);
                testArrayTParameters[i][0] = tA;
                testArrayTParameters[i][1] = tE;
            }

            // Формування параметрів для мережі FNNN
            testArrayFNNNinputs = Normalize.FormDataFNNN(testArrayTParameters, TestAmount);

            MessageBox.Show("Тестова випадкова вибірка згенерована");
        }

        // вихід із програми
        private void Exit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // створення мережі BackPropagation
        private void CreateBackProp_Click(object sender, EventArgs e)
        {
            if(PARAMETERS == 0)
            {
                MessageBox.Show("Спочатку завантажте дані");
                return;
            }
            int hidden;
            bool resultHidden = Int32.TryParse(this.Hidden_Neurons.Text, out hidden);
            bool resultLearning_Rate1 = Double.TryParse(this.Learning_rate1.Text, out LEARNING_RATE1);
            bool resultMomentum = Double.TryParse(this.Momentum.Text, out MOMENTUM);
            bool resultMin_error = Double.TryParse(this.MinError1.Text, out MIN_ERROR);

            if(resultHidden == false || resultLearning_Rate1 == false || resultMomentum == false || resultMin_error == false)
            {
                MessageBox.Show("Не всі обов'язкові поля заповнені для створення мережі BackPropagation");
                return;
            }
         
            layerSizes = new int[3]{ 2, hidden, 1 }; // кількість шарів та нейронів у шарах

            bpn = new BackPropagationNetwork(layerSizes, TFuncs);
            MessageBox.Show("Мережу BackPropagation створено");
        }

        // Навчання мережі BackPropagation
        private void Train_Click(object sender, EventArgs e)
        {
            if (bpn == null)
            {
                MessageBox.Show("Спочатку створіть мережу або завантажте її з файлу");
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            sWatch.Reset();
            sWatch.Start();
            
            bpn.TrainNetwork(tParameters, answers, MIN_ERROR, LEARNING_RATE1, MOMENTUM);
            sWatch.Stop();
            Cursor.Current = Cursors.Arrow;

            tSpan = sWatch.Elapsed;
            this.Time1.Text = Convert.ToString(tSpan);
            MessageBox.Show("Мережа BackPropagation навчена");
        }

        // Створення мережі FNNN
        private void CreateFNNN_Click(object sender, EventArgs e)
        {
            if (PARAMETERS == 0)
            {
                MessageBox.Show("Спочатку завантажте дані");
                return;
            }

            int hidden;
            bool resultHidden = Int32.TryParse(this.HiddenNeuronsFNNN.Text, out hidden);
            bool resultLearning_rate2 = Double.TryParse(LearningRateFNNN.Text, out LEARNING_RATE2);
            bool resultMin_error2 = Double.TryParse(MinErrorFNNN.Text, out MIN_ERROR2);
            bool resultMomentum = Double.TryParse(MomentumFNNN.Text, out MOMENTUM2);

            if (resultLearning_rate2 == false || resultMin_error2 == false || resultMomentum == false || resultHidden == false)
            {
                MessageBox.Show("Не всі обов'язкові поля заповнені для створення мережі FNNN");
                return;
            }

            layerSizesFNNN = new int[3] { 3, hidden, 1 }; // кількість шарів та нейронів у шарах

            fnnn = new BackPropagationNetwork(layerSizesFNNN, TFuncsFNNN);
            MessageBox.Show("Мережу FNNN створено");

        }

        // Навчання мережі FNNN
        private void TrainFNNN_Click(object sender, EventArgs e)
        {
            if (fnnn == null)
            {
                MessageBox.Show("Спочатку створіть мережу або завантажте її з файлу");
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            sWatch2.Reset();
            sWatch2.Start();

            fnnn.TrainNetwork(FNNNinputs, answers, MIN_ERROR2, LEARNING_RATE2, MOMENTUM2);
            sWatch2.Stop();
            Cursor.Current = Cursors.Arrow;
            tSpan = sWatch2.Elapsed;
            this.TimeFNNN.Text = Convert.ToString(tSpan);
            MessageBox.Show("Мережа FNNN навчена");
        }

        // Тестування навчальної вибірки (BackPropagation)
        private void TestTrain_Click(object sender, EventArgs e)
        {
            if (bpn == null)
                MessageBox.Show("Спочатку створіть мережу або завантажте її із файла");
            else
            {
                ResultTrainBackPropagation = new DataTable();
                this.TrainResults.DataSource = ResultTrainBackPropagation;

                // Створення колонок
                ResultTrainBackPropagation.Columns.Add("id");
                for (int i = 0; i < PARAMETERS; i++)
                {
                    ResultTrainBackPropagation.Columns.Add(ParametersNames[i]);
                }

                ResultTrainBackPropagation.Columns.Add("Розподіл");

                for (int i = 0; i < TRAINING_PATTERNS; i++)
                {
                    DataRow row = ResultTrainBackPropagation.NewRow();
                    row[0] = i + 1;
                    for (int k = 0; k < PARAMETERS; k++)
                    {
                        row[k + 1] = inputsFrequency[i][k];
                    }
                    row["Розподіл"] = bpn.getCluster(tParameters[i], output);
                    ResultTrainBackPropagation.Rows.Add(row);
                }
                
            }

        }

        // Тестування навчальної вибірки (FNNN)
        private void TestTrainFNNN_Click(object sender, EventArgs e)
        {
            if (fnnn == null)
                MessageBox.Show("Спочатку створіть мережу або завантажте її із файла");
            else
            {
                ResultTrainFNNN = new DataTable();
                this.TrainResultsFNNN.DataSource = ResultTrainFNNN;

                // Створення колонок
                ResultTrainFNNN.Columns.Add("id");
                for (int i = 0; i < PARAMETERS; i++)
                {
                    ResultTrainFNNN.Columns.Add(ParametersNames[i]);
                }

                ResultTrainFNNN.Columns.Add("Розподіл");

                for (int i = 0; i < TRAINING_PATTERNS; i++)
                {
                    DataRow row = ResultTrainFNNN.NewRow();
                    row[0] = i + 1;
                    for (int k = 0; k < PARAMETERS; k++)
                    {
                        row[k + 1] = inputsFrequency[i][k];
                    }
                    row["Розподіл"] = fnnn.getCluster(FNNNinputs[i], output2);
                    ResultTrainFNNN.Rows.Add(row);
                }

            }

        }

        // Тестування випадкової вибірки (BackPropagation)
        private void TestRandom_Click(object sender, EventArgs e)
        {
            if (bpn == null)
                MessageBox.Show("Спочатку створіть мережу або завантажте її із файла");
            else
            {
                ResultTestBackPropagation = new DataTable();
                this.RandomResults.DataSource = ResultTestBackPropagation;

                // Створення колонок
                ResultTestBackPropagation.Columns.Add("id");
                for (int i = 0; i < PARAMETERS; i++)
                {
                    ResultTestBackPropagation.Columns.Add(ParametersNames[i]);
                }

                ResultTestBackPropagation.Columns.Add("Розподіл");

                for (int i = 0; i < TestAmount; i++)
                {
                    DataRow row = ResultTestBackPropagation.NewRow();
                    row[0] = i + 1;
                    for (int k = 0; k < PARAMETERS; k++)
                    {
                        row[k + 1] = testArray[i][k];
                    }
                    row["Розподіл"] = bpn.getCluster(testArrayTParameters[i], output);
                    ResultTestBackPropagation.Rows.Add(row);
                }
            }
        }

        // Тестування випадкової вибірки (FNNN)
        private void TestGeneratedLVQ_Click(object sender, EventArgs e)
        {
            if (fnnn == null)
                MessageBox.Show("Спочатку створіть мережу або завантажте її із файла");
            else
            {
                ResultTestFNNN = new DataTable();
                this.GeneratedResultsFNNN.DataSource = ResultTestFNNN;

                // Створення колонок
                ResultTestFNNN.Columns.Add("id");
                for (int i = 0; i < PARAMETERS; i++)
                {
                    ResultTestFNNN.Columns.Add(ParametersNames[i]);
                }

                ResultTestFNNN.Columns.Add("Розподіл");

                for (int i = 0; i < TestAmount; i++)
                {
                    DataRow row = ResultTestFNNN.NewRow();
                    row[0] = i + 1;
                    for (int k = 0; k < PARAMETERS; k++)
                    {
                        row[k + 1] = testArray[i][k];
                    }
                    row["Розподіл"] = fnnn.getCluster(testArrayFNNNinputs[i], output2);
                    ResultTestFNNN.Rows.Add(row);
                }
            }
        }

        // Запис результатів для навчальної вибірки (BackPropagation)
        private void SaveTrain_Click(object sender, EventArgs e)
        {
            if (PARAMETERS == 0)
            {
                MessageBox.Show("Спочатку завантажте дані");
                return;
            }
            saveResultsFileDialog.ShowDialog();
        }

        // виклик діалогу для збереження результатів у файл (TrainResults, BackPropagation)
        private void saveResultsFileDialog_FileOk(object sender, CancelEventArgs e)
        {
           
            try
            {
                string filepath = saveResultsFileDialog.FileName;
                string text = "ID".PadRight(8);
                for(int i = 0; i < ParametersNames.Length; i++)
                {
                    text += ParametersNames[i].PadRight(8);
                }
                text += "Розподіл".PadRight(8);
                text += Environment.NewLine;
                foreach (DataGridViewRow row in TrainResults.Rows)
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        text += cell.Value.ToString().PadRight(8);
                       
                    }
                    text += Environment.NewLine + Environment.NewLine;
                }
                File.AppendAllText(filepath, text);
                MessageBox.Show("Результати навчальної вибірки збережено");
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        // Запис результатів для навчальної вибірки (FNNN)
        private void SaveTrainFNNN_Click(object sender, EventArgs e)
        {
            if (PARAMETERS == 0)
            {
                MessageBox.Show("Спочатку завантажте дані");
                return;
            }
            saveFileDialog3.ShowDialog();
        }

        // виклик діалогу для збереження результатів у файл (TrainResults, FNNN)
        private void saveFileDialog3_FileOk(object sender, CancelEventArgs e)
        {
            try
            {
                string filepath = saveFileDialog3.FileName;
                string text = "ID".PadRight(8);
                for (int i = 0; i < ParametersNames.Length; i++)
                {
                    text += ParametersNames[i].PadRight(8);
                }
                text += "Розподіл".PadRight(8);
                text += Environment.NewLine;
                foreach (DataGridViewRow row in TrainResultsFNNN.Rows)
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        text += cell.Value.ToString().PadRight(8);

                    }
                    text += Environment.NewLine + Environment.NewLine;
                }
                File.AppendAllText(filepath, text);
                MessageBox.Show("Результати навчальної вибірки збережено");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        // Запис результатів для випадкової вибірки (BackPropagation)
        private void SaveRandom_Click(object sender, EventArgs e)
        {
            if(PARAMETERS == 0)
            {
                MessageBox.Show("Спочатку завантажте дані");
                return;
            }
            saveFileDialog1.ShowDialog();
        }

        // виклик діалогу для збереження результатів у файл (RandomResults, BackPropagation)
        private void saveFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            try
            {
                string filepath = saveFileDialog1.FileName;
                string text = "ID".PadRight(8);
                for (int i = 0; i < ParametersNames.Length; i++)
                {
                    text += ParametersNames[i].PadRight(8);
                }
                text += "Розподіл".PadRight(8);
                text += Environment.NewLine;
                foreach (DataGridViewRow row in RandomResults.Rows)
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        text += Convert.ToString(Math.Round(Convert.ToDouble(cell.Value.ToString()), 3)).PadRight(8);

                    }
                    text += Environment.NewLine + Environment.NewLine;
                }
                File.AppendAllText(filepath, text);
                MessageBox.Show("Результати випадкової вибірки збережено");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        // Запис результатів для випадкової вибірки (FNNN)
        private void SaveGeneratedLVQ_Click(object sender, EventArgs e)
        {
            if (PARAMETERS == 0)
            {
                MessageBox.Show("Спочатку завантажте дані");
                return;
            }
            saveFileDialog4.ShowDialog();
        }

        // виклик діалогу для збереження результатів у файл (GeneratedResults, FNNN)
        private void saveFileDialog4_FileOk(object sender, CancelEventArgs e)
        {
            try
            {
                string filepath = saveFileDialog4.FileName;
                string text = "ID".PadRight(8);
                for (int i = 0; i < ParametersNames.Length; i++)
                {
                    text += ParametersNames[i].PadRight(8);
                }
                text += "Розподіл".PadRight(8);
                text += Environment.NewLine;
                foreach (DataGridViewRow row in GeneratedResultsFNNN.Rows)
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        text += Convert.ToString(Math.Round(Convert.ToDouble(cell.Value.ToString()), 3)).PadRight(8);

                    }
                    text += Environment.NewLine + Environment.NewLine;
                }
                File.AppendAllText(filepath, text);
                MessageBox.Show("Результати випадкової вибірки збережено");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void openFileDialog_FileOk(object sender, CancelEventArgs e)
        {

        }
    }
}
