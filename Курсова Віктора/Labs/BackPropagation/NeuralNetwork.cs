using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetwork
{
    #region Функції активації та їхні похідні
    // enum для перелічення можливих функцій активації
    public enum TransferFunction
    {
        None,
        Sigmoid,
        BipolarSigmoid,
        Linear
    } 
    
    public static class TransferFunctions
    {
        // Функція активації
        public static double Evaluate(TransferFunction tFunc, double input)
        {
            switch (tFunc)
            {
                case TransferFunction.Sigmoid:
                    return sigmoid(input);
                case TransferFunction.BipolarSigmoid:
                    return bipolarsigmoid(input);
                case TransferFunction.Linear:
                    return linear(input);
                case TransferFunction.None :
                default:
                    return 0.0;
            }
        }

        // Похідна функції активації
        public static double DerivativeEvaluate(TransferFunction tFunc, double input)
        {
            switch (tFunc)
            {
                case TransferFunction.Linear:
                    return linear_derivative(input);
                case TransferFunction.Sigmoid:
                    return sigmoid_derivative(input);
                case TransferFunction.BipolarSigmoid:
                    return bipolarsigmoid_derivative(input);
                case TransferFunction.None :
                default:
                    return 0.0;

            }

        }

        // лінійна функція
        private static double linear(double x)
        {
            return x;
        }

        // похідна лінійної функції
        private static double linear_derivative(double x)
        {
            return 1.0;
        }

        // Сигмоїда
        private static double sigmoid(double x)
        {
            return 1.0 / (1.0 + Math.Exp(-x));
        }

        // Похідна сигмоїди
        private static double sigmoid_derivative(double x)
        {
            return sigmoid(x) * (1 - sigmoid(x));
        }

        private static double bipolarsigmoid(double x)
        {
            return 2.0 / (1.0 + Math.Exp(-x)) - 1;
        }

        private static double bipolarsigmoid_derivative(double x)
        {
            return 0.5 * (1 + bipolarsigmoid(x)) * (1 - bipolarsigmoid(x));
        }

    }
    #endregion

    public class BackPropagationNetwork
    {
        #region Поля
        public int layerCount; // прихований шар + вихідний шар
        public int inputSize; // к-сть нейронів у 0 шарі
        public int[] layerSize; // величини к-сті нейронів у прихованому та вихідному шарах 
        private TransferFunction[] transferFunction; // масив функцій активації

        public double[][] layerOtput; // вихідні дані шару
        public double[][] layerInput; // вхідні дані шару
        private double[][] bias; // відхилення
        private double[][] delta; // дельта помилки
        private double[][] previosBiasDelta; // дельта попереднього відхилення

        private double[][][] weight; // ваги, де [0] - шар, [1] - попередній нейрон, [2] - поточний нейрон
        private double[][][] previousWeightDelta; // дельта попередньої ваги

        #endregion

        #region Конструктор
        public BackPropagationNetwork(int[] layerSizes, TransferFunction[] TransferFunctions)
        {
            // Перевірка вхідних даних
            if (TransferFunctions.Length != layerSizes.Length || TransferFunctions[0] != TransferFunction.None)
                throw new ArgumentException("The network cannot be created with these parameters");
            
            // Ініціалізація шарів мережі
            layerCount = layerSizes.Length - 1;
            inputSize = layerSizes[0];
            layerSize = new int[layerCount];
            transferFunction = new TransferFunction[layerCount];

            for (int i = 0; i<layerCount; i++) 
                layerSize[i] = layerSizes[i + 1];

            for (int i = 0; i < layerCount; i++)
                transferFunction[i] = TransferFunctions[i + 1];

            // Визначення вимірів масивів
            bias = new double[layerCount][];
            previosBiasDelta = new double[layerCount][];
            delta = new double[layerCount][];
            layerOtput = new double[layerCount][];
            layerInput = new double[layerCount][];

            weight = new double[layerCount][][];
            previousWeightDelta = new double[layerCount][][];

            // Заповнення двовимірних масивів
            for (int l = 0; l<layerCount; l++)
            {
                bias[l] = new double[layerSize[l]];
                previosBiasDelta[l] = new double[layerSize[l]];
                delta[l] = new double[layerSize[l]];
                layerOtput[l] = new double[layerSize[l]];
                layerInput[l] = new double[layerSize[l]];

                weight[l] = new double[l == 0 ? inputSize : layerSize[l-1]][];
                previousWeightDelta[l] = new double[l == 0 ? inputSize : layerSize[l-1]][];

                for (int i = 0; i<(l == 0 ? inputSize : layerSize[l - 1]); i++)
                {
                    weight[l][i] = new double[layerSize[l]];
                    previousWeightDelta[l][i] = new double[layerSize[l]];
                }
            }

            // Ініціалізація ваг
            for (int l = 0; l < layerCount; l++)
            {
                for (int i = 0; i < layerSize[l]; i++)
                {
                    bias[l][i] = Gaussian.GetRandomGaussian();
                    previosBiasDelta[l][i] = 0.0;
                    layerInput[l][i] = 0.0;
                    layerOtput[l][i] = 0.0;
                    delta[l][i] = 0.0;
                }

                for (int i = 0; i < (l == 0 ? inputSize : layerSize[l - 1]); i++)
                {
                    for (int j = 0; j < layerSize[l]; j++)
                    {
                        weight[l][i][j] = Gaussian.GetRandomGaussian();
                        previousWeightDelta[l][i][j] = 0.0;
                    }
                }
            }
        }

        #endregion

        #region Methods
        public void Run(ref double[] input, out double[] output)
        {
            // Перевірка, чи введені дані відповідають кількості нейронів у вхідному шарі
            if (input.Length != inputSize)
                throw new ArgumentException("Input data isn't of the correct dimension");

            // Вихідне значення функції
            output = new double[layerSize[layerCount-1]];

            // Нормалізація вхідних значень
            double max = input.Max();
           
            // Запуск мережі
            for(int l = 0; l<layerCount; l++)
            {
                for(int j = 0; j < layerSize[l]; j++)
                {
                    double sum = 0.0;
                   
                    for(int i = 0; i<(l == 0 ? inputSize : layerSize[l-1]); i++)
                        sum += weight[l][i][j] * (l == 0 ? input[i] : layerOtput[l-1][i]);

                    sum += bias[l][j];
                    layerInput[l][j] = sum;

                    layerOtput[l][j] = TransferFunctions.Evaluate(transferFunction[l], sum);   
           
                }
            }

            // копіюємо вихід мережі у вихідний масив
            for(int i = 0; i < layerSize[layerCount-1]; i++)
            {
                output[i] = layerOtput[layerCount - 1][i];
            }

        }

        // Функція навчання
        public double Train(ref double[] input, ref double[] desired, double TrainingRate, double Momentum)
        {
            // Перевірка вхідних параметрів
            if (input.Length != inputSize)
                throw new ArgumentException("Invalid input parameter", "input");

            if (desired.Length != layerSize[layerCount - 1])
                throw new ArgumentException("Invalid input parameter", "desired");

            // Локальні змінні
            double error = 0.0, sum = 0.0, weigtdelta = 0.0, biasDelta = 0.0;
            double[] output = new double[layerSize[layerCount-1]];

            // Запуск мережі
            Run(ref input, out output);

            //Розмножуємо похибку у зворотньму порядку
            for (int l = layerCount - 1; l>=0; l--)
            {
                //Вихідний шар
                if(l == layerCount - 1)
                {
                    for (int k = 0; k < layerSize[l]; k++)
                    {
                        delta[l][k] = output[k] - desired[k];
                        error += Math.Pow(delta[l][k], 2);
                        delta[l][k] *= TransferFunctions.DerivativeEvaluate(transferFunction[l], layerInput[l][k]);
                    }
                   
                }
                //Прихований шар
                else
                {
                    for (int i =0; i<layerSize[l]; i++)
                    {
                        sum = 0.0;
                        for (int j = 0; j < layerSize[l+1]; j++)
                        {
                            sum += weight[l + 1][i][j] * delta[l+1][j];
                        }
                        sum *= TransferFunctions.DerivativeEvaluate(transferFunction[l], layerInput[l][i]);
                        delta[l][i] = sum;
                    }
                }
            }

            // Оновлення ваг та відхилень
            for (int l = 0; l<layerCount; l++)
                for (int i = 0; i < (l == 0 ? inputSize : layerSize[l-1]); i++)
                    for (int j = 0; j < layerSize[l]; j++)
                    {
                        weigtdelta = TrainingRate * delta[l][j] * (l == 0 ? input[i] : layerOtput[l - 1][i]) + Momentum * previousWeightDelta[l][i][j];
                        weight[l][i][j] -= weigtdelta;

                        previousWeightDelta[l][i][j] = weigtdelta;
                    }

            for(int l =0; l < layerCount; l++)
                for(int i = 0; i < layerSize[l]; i++)
                {
                    biasDelta = TrainingRate * delta[l][i] + Momentum * previosBiasDelta[l][i];
                    bias[l][i] -= biasDelta;

                    previosBiasDelta[l][i] = biasDelta;
                }

            return error;     
        }

        // Функція для навчання тестової вибірки
        public void TrainNetwork(double[][] patterns, double[][] answers, double min_error, double TrainingRate, double Momentum)
        {
            int amountOfPatterns = patterns.GetUpperBound(0) + 1;
            Random rnd = new Random();
            double error;
            do
            {
                List<BackPropagationContainer> TrainingSet = new List<BackPropagationContainer>();

                for (int k = 0; k < amountOfPatterns; k++)
                {
                    BackPropagationContainer obj = new BackPropagationContainer();
                    obj.trainVector = patterns[k];
                    obj.answer = answers[k];
                    TrainingSet.Add(obj);
                }

                error = 0.0;

                for (int k = 0; k < amountOfPatterns; k++)
                {
                    int index = rnd.Next(amountOfPatterns - k);
                    error += Train(ref TrainingSet[index].trainVector, ref TrainingSet[index].answer, TrainingRate, Momentum);
                    TrainingSet.RemoveAt(index);
                }
                error /= amountOfPatterns;
            } while (error > min_error);

        }

        public int getCluster(double[] input, double[] output)
        {
            Run(ref input, out output);
            if ((output[0] >= 0) && (output[0] < 0.5))
                return 0;
            else
                return 1;
        }
        #endregion

        #region Public data

        public string Name = "Default";

        #endregion
    }

    // клас - контейнер для мережі BackPropagation
    public class BackPropagationContainer
    {
        public double[] trainVector;
        public double[] answer;
    }

    // Клас для генерації даних
    public static class GenerateTestNumbers
    {
        public static double[] Exponential(double rate, int count, Random rnd2)
        {
            double x;
            double[] array = new double[count];
            int i = 0;
            while (i < count)
            {

                x = Math.Log(rnd2.NextDouble()) * (-1 / rate);
                array[i] = x;
                i++;

            }
            Array.Sort(array);

            return array;
        }

        public static double[][] GenerateOutput(int parameters, int amount)
        {
            Random rnd = new Random();
            Random rnd2 = new Random();
            double[][] array = new double[amount][];

            for(int i =0; i < amount; i++)
            {
                array[i] = new double[parameters];
            }

            for(int i = 0; i < amount; i++)
            {
                if (i % 2 == 0)
                {
                    for (int j = 0; j < parameters; j++)
                    {
                        array[i][j] = rnd.NextDouble() * (100 - 0) + 0;
                    }
                }
                else
                {
                    array[i] = Exponential(3, parameters, rnd2);
                }
            }

            return array;
        }
    }

    // Клас для нормалізаціїї даних
    public static class Normalize
    {
        // Нормалізація даних для навчання FNNN мережі
        public static double[][] FormDataFNNN(double[] tA, double[] tE, int trainPatterns)
        {
            double[][] inputFNNN = new double[trainPatterns][];
            for (int i = 0; i < trainPatterns; i++)
            {
                inputFNNN[i] = new double[3];

                inputFNNN[i][0] = tA[i] * tE[i];
                inputFNNN[i][1] = Math.Sin(tA[i]);
                inputFNNN[i][2] = Math.Cos(tE[i]);
            }

            return inputFNNN;
        }

        // Нормалізація даних для навчання FNNN мережі
        public static double[][] FormDataFNNN(double[][] tParameters, int trainPatterns)
        {
            double[][] inputFNNN = new double[trainPatterns][];
            for (int i = 0; i < trainPatterns; i++)
            {
                inputFNNN[i] = new double[3];

                inputFNNN[i][0] = tParameters[i][0] * tParameters[i][1];
                inputFNNN[i][1] = Math.Sin(tParameters[i][0]);
                inputFNNN[i][2] = Math.Cos(tParameters[i][1]);
            }

            return inputFNNN;
        }

    }

    // Клас для створення випадкових чисел з нормальним розподілом
    public static class Gaussian
    {
        private static Random gen = new Random();

        public static double GetRandomGaussian()
        {
            return GetRandomGaussian(0.0, 1.0);
        }

        public static double GetRandomGaussian(double mean, double stddev)
        {
            double rVal1, rVal2;
            GetRandomGaussian(mean, stddev, out rVal1, out rVal2);
            return rVal1;
        }

        public static void GetRandomGaussian(double mean, double stddev, out double val1, out double val2)
        {
            double u, v, s, t;

            do
            {
                u = 2 * gen.NextDouble() - 1;
                v = 2 * gen.NextDouble() - 1;
            } while (u * u + v * v > 1 || (u == 0 && v == 0));

            s = u * u + v * v;
            t = Math.Sqrt((-2.0 * Math.Log(s)) / s);

            val1 = stddev * t * u + mean;
            val2 = stddev * t * v + mean;


        }
    }

    // Клас для обчислення коефіцієнтів для нормального розподілу
    public static class Statistic
    {
        // Допоміжний метод для створення масиву даних
        public static double[][] CreateArrayFromDictionary(double[][] inputs)
        {
            int trainPatterns = inputs.GetUpperBound(0) + 1;
            int param = inputs[0].Length;
            double[] h = new double[trainPatterns];

            for (int i = 0; i < trainPatterns; i++)
            {
                h[i] = Interval(inputs[i]);
            }

            Dictionary<double, int>[] pairs = new Dictionary<double, int>[trainPatterns];

            for (int i = 0; i < trainPatterns; i++)
            {
                pairs[i] = new Dictionary<double, int>();
            }

            for (int i = 0; i < trainPatterns; i++)
            {
                for (double curr = inputs[i].Min(); curr <= inputs[i].Max(); curr += h[i])
                {
                    int count = 0;
                    for (int j = 0; j < inputs[i].Length; j++)
                    {
                        if (curr <= inputs[i][j] && inputs[i][j] < curr + h[i])
                            count++;
                    }
                    pairs[i].Add((curr + h[i] + curr) / 2.0, count);
                }
            }

            double[][] outputArray = new double[trainPatterns][];
            for (int i = 0; i < trainPatterns; i++)
            {
                outputArray[i] = new double[param];
            }

            for (int i = 0; i < trainPatterns; i++)
            {
                int counter = 0;
                for (int j = 0; j < pairs[i].Count; j++)
                {
                    for (int k = 0; k < pairs[i].ElementAt(j).Value; k++)
                    {
                        outputArray[i][counter] = pairs[i].ElementAt(j).Key;
                        counter++;
                    }
                }
            }
            return outputArray;

        }

        // Інтервал
        private static double Interval(double[] arr)
        {
            double h = (arr.Max() - arr.Min()) / (1.0 + 3.322 * Math.Log(arr.Length));
            // Заокруглення інтервалу :
            if ((h - 1) >= 50)
                return Math.Round(h); // до цілих чисел
            else
                return Math.Round(h, 1); // до першого розряду після коми
        }

        // Обчислення дисперсії
        private static double Dispertion(double[] array)
        {
            double average = array.Average();
            double sum = 0.0;
            for (int i = 0; i < array.Length; i++)
                sum += Math.Pow((array[i] - average), 2);
            return sum / array.Length;
        }

        // Середньоквадратичне відхилення
        private static double AvarageDispertion(double[] array)
        {
            return Math.Sqrt(Dispertion(array));
        }

        // Коефіцієнт асиметрії
        private static double skewness(double[] array, int pow)
        {
            Dictionary<double, int> d = Frequency(array);
            double average = array.Average();
            double sum = 0.0;

            for (int k = 0; k < d.Count; k++)
            {
                sum += Math.Pow(d.ElementAt(k).Key - average, pow) * d.ElementAt(k).Value;
            }

            int sumValues = 0;
            for (int i = 0; i < d.Count; i++)
            {
                sumValues += d.ElementAt(i).Value;
            }
            return sum / (array.Length * Math.Pow(AvarageDispertion(array), pow));

        }

        // Коефіцієнт ексцесу
        private static double Kurtosis(double[] array, int pow)
        {
            return skewness(array, 4) - 3;
        }

        // Частоти
        private static Dictionary<double, int> Frequency(double[] array)
        {
            Array.Sort(array);
            Dictionary<double, int> d = new Dictionary<double, int>();
            int value;
            for (int i = 0; i < array.Length; i += value)
            {
                value = 0;
                for (int j = 0; j < array.Length; j++)
                {
                    if (array[i] == array[j]) value++;
                }
                d.Add(array[i], value);
            }

            return d;
        }

        // Дисперсія асиметрії
        private static double SkewnessDispertion(double[] array)
        {
            int length = array.Length;
            double a = 6 * (length - 2);
            double b = (length + 1) * (length + 3);
            double c = a / b;
            //Console.WriteLine(String.Format("Дисперсiя асиметрiї {0:0.000}", c));
            return c;
        }

        // Дисперсія ексцесу
        private static double KurtosisDispertion(double[] array)
        {
            double length = array.Length;
            double a, b, c, d;

            a = (24 * length) / Math.Pow((length - 1), 2);
            b = (length - 2) / (length + 3);
            c = (length - 3) / (length + 5);

            d = a * b * c;
            //Console.WriteLine(String.Format("Дисперсiя ексцесу {0:0.000}", d));
            return d;
        }

        // Параметри tA і tE
        public static void t(double[] array, out double tA, out double tE)
        {
            tA = Math.Abs(skewness(array, 3)) / Math.Sqrt(SkewnessDispertion(array));
            tE = Math.Abs(Kurtosis(array, 4)) / Math.Sqrt(KurtosisDispertion(array));
        }

        // Перевірка нормального закону розподілу
        public static int CheckNormal(double tA, double tE)
        {
            return (tA <= 3) && (tE <= 5) ? 1 : 0;   
        }

    }
}
