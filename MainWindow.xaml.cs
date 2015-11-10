using Microsoft.International.Converters;
using Microsoft.International.Converters.PinYinConverter;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System;
using System.Globalization;
using System.Runtime.Serialization.Formatters.Binary;

namespace wpfNihongoDict
{
    [Serializable]
    public class Word : IComparable
    {
        public string Kana { get; set; }
        public string Kanji { get; set; }
        public int Kana2KanjiSuccessCount { get; set; }
        public int Kana2KanjiFailCount { get; set; }
        public int Kanji2KanaSuccessCount { get; set; }
        public int Kanji2KanaFailCount { get; set; }
        public List<string> Romaji;

        public Word(string str)
        {
            Kana2KanjiFailCount = 0;
            Kana2KanjiSuccessCount = 0;
            Kanji2KanaFailCount = 0;
            Kanji2KanaSuccessCount = 0;
            bool state = false;
            foreach (string s in str.Replace('ｎ', 'ん').Replace('Ｎ', 'ン').Split('\t', ' '))
                if (s != "")
                    if (!state)
                    {
                        Kanji = s;
                        state = true;
                    }
                    else
                    {
                        Kana = s;
                        break;
                    }
        }

        private double Rank
        {
            get
            {
                return Kana2KanjiFailCount / (Kana2KanjiSuccessCount + 0.1) + (Kanji2KanaFailCount / (Kanji2KanaSuccessCount + 0.1)) * 2;
            }
        }

        public int CompareTo(object obj)
        {
            if (obj as Word == null)
                return -1;
            return -Rank.CompareTo((obj as Word).Rank);
        }
    }

    public class SummaryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Format("正：{0}，误：{1}", values);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string DictPath
        {
            get
            {
                return ofd.FileName;
            }

            set
            {
                ofd.FileName = value;
                PropertyChanged(this, new PropertyChangedEventArgs("DictPath"));
            }
        }

        public int SuccessCount
        {
            get
            {
                return successCount;
            }

            set
            {
                successCount = value;
                PropertyChanged(this, new PropertyChangedEventArgs("SuccessCount"));
            }
        }
        public int FailCount {
            get
            {
                return failCount;
            }
            set
            {
                failCount = value;
                PropertyChanged(this, new PropertyChangedEventArgs("FailCount"));
            }
        }

        private OpenFileDialog ofd = new OpenFileDialog();
        private List<Word> words = new List<Word>();
        private bool isPinyinMode = false, isInputMode = false;
        private Random rand = new Random();
        private Word currentWord;
        private int currentKanjiIndex, currentKanaIndex, successCount = 0, failCount = 0;
        private Dictionary<char, char> kanji2Hanzi = new Dictionary<char, char>()
        {
            { '図', '图' },
            { '気', '气' },
            { '発', '发' },
            { '舎', '舍' },
            { '駅', '驿' },
            { '売', '卖' }
        };

        public event PropertyChangedEventHandler PropertyChanged;

        private void PushLog(string log)
        {
            lstLog.Items.Add("[" + DateTime.Now.ToShortTimeString() + "] " + log);
            lstLog.ScrollIntoView(lstLog.Items[lstLog.Items.Count - 1]);
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            words.Clear();
            SuccessCount = 0;
            FailCount = 0;
            lstLog.Items.Clear();
            PushLog("词库与进度均已清空。请重新载入词库。");
            DictPath = "";
            isInputMode = false;
            txtAnnotation.Text = "注音符号";
            txtInputBuffer.Text = "";
            txtKanji.Text = "汉字";
        }

        private void window_Closing(object sender, CancelEventArgs e)
        {
            // 保存进度
            using (FileStream fs = new FileStream("savefile", FileMode.Create))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write("#SAVEFILE#"); // MAGIC
                    bw.Write(DictPath);
                    bw.Write(SuccessCount);
                    bw.Write(FailCount);
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(fs, words);
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // 自动读取
            if (File.Exists("savefile"))
                using (FileStream fs = new FileStream("savefile", FileMode.Open))
                {
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        if (br.ReadString() == "#SAVEFILE#")
                        {
                            DictPath = br.ReadString();
                            PropertyChanged(this, new PropertyChangedEventArgs("DictPath"));
                            SuccessCount = br.ReadInt32();
                            FailCount = br.ReadInt32();
                            BinaryFormatter bf = new BinaryFormatter();
                            List<Word> restoredWords = bf.Deserialize(fs) as List<Word>;
                            if (restoredWords != null)
                            {
                                words = restoredWords;
                                PushLog(string.Format("进度已读取。共有{0}个词。", words.Count));
                            }
                            else
                            {
                                SuccessCount = 0;
                                FailCount = 0;
                                DictPath = "";
                            }
                        }
                    }
                }
        }

        private void btnLoadDict_Click(object sender, RoutedEventArgs e)
        {
            if (ofd.ShowDialog() == true)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("DictPath"));

                // 读取文件
                using (StreamReader sr = new StreamReader(DictPath))
                {
                    while (!sr.EndOfStream)
                        words.Add(new Word(sr.ReadLine()));
                }

                PushLog(string.Format("词库载入完毕，共 {0} 个单词。", words.Count));

                InputLanguageManager.SetInputLanguage(this, new CultureInfo("en-US"));
            }
        }

        private void window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!isInputMode)
            {
                if (e.Key == Key.Tab)
                    btnSkip_Click(null, null);
                return;
            }

            switch (e.Key)
            {
                case Key.Tab:
                    btnSkip_Click(null, null);
                    return;

                case Key.Back:
                    if (txtInputBuffer.Text != "")
                        txtInputBuffer.Text = txtInputBuffer.Text.Remove(txtInputBuffer.Text.Length - 1);
                    break;

                case Key.A:
                    txtInputBuffer.Text += 'a';
                    break;

                case Key.B:
                    txtInputBuffer.Text += 'b';
                    break;

                case Key.C:
                    txtInputBuffer.Text += 'c';
                    break;

                case Key.D:
                    txtInputBuffer.Text += 'd';
                    break;

                case Key.E:
                    txtInputBuffer.Text += 'e';
                    break;

                case Key.F:
                    txtInputBuffer.Text += 'f';
                    break;

                case Key.G:
                    txtInputBuffer.Text += 'g';
                    break;

                case Key.H:
                    txtInputBuffer.Text += 'h';
                    break;

                case Key.I:
                    txtInputBuffer.Text += 'i';
                    break;

                case Key.J:
                    txtInputBuffer.Text += 'j';
                    break;

                case Key.K:
                    txtInputBuffer.Text += 'k';
                    break;

                case Key.L:
                    txtInputBuffer.Text += 'l';
                    break;

                case Key.M:
                    txtInputBuffer.Text += 'm';
                    break;

                case Key.N:
                    txtInputBuffer.Text += 'n';
                    break;

                case Key.O:
                    txtInputBuffer.Text += 'o';
                    break;

                case Key.P:
                    txtInputBuffer.Text += 'p';
                    break;

                case Key.Q:
                    txtInputBuffer.Text += 'q';
                    break;

                case Key.R:
                    txtInputBuffer.Text += 'r';
                    break;

                case Key.S:
                    txtInputBuffer.Text += 's';
                    break;

                case Key.T:
                    txtInputBuffer.Text += 't';
                    break;

                case Key.U:
                    txtInputBuffer.Text += 'u';
                    break;

                case Key.V:
                    txtInputBuffer.Text += 'v';
                    break;

                case Key.W:
                    txtInputBuffer.Text += 'w';
                    break;

                case Key.X:
                    txtInputBuffer.Text += 'x';
                    break;

                case Key.Y:
                    txtInputBuffer.Text += 'y';
                    break;

                case Key.Z:
                    txtInputBuffer.Text += 'z';
                    break;
            }

            // 试着比对文本框内容
            string content = txtInputBuffer.Text;
            if (content == "")
                return;

            if (isPinyinMode)
            {
                try
                {
                    char kanji = currentWord.Kanji[currentKanjiIndex], hanzi;
                    if (kanji == '々')
                        kanji = currentWord.Kanji[currentKanjiIndex - 1];
                    if (kanji2Hanzi.TryGetValue(kanji, out hanzi))
                        kanji = hanzi;
                    ChineseChar chr = new ChineseChar(kanji);
                    foreach (string pinyin in chr.Pinyins)
                        if (pinyin != null && content == pinyin.Substring(0, pinyin.Length - 1).ToLower())
                        {
                            // 增加一个汉字到大字行
                            txtKanji.Text += currentWord.Kanji[currentKanjiIndex];

                            // 清空缓冲区
                            txtInputBuffer.Clear();

                            if (++currentKanjiIndex == currentWord.Kanji.Length)
                            {
                                SuccessCount++;
                                currentWord.Kana2KanjiSuccessCount++;
                                PushLog("完成了词 " + currentWord.Kanji + " 的汉字输入");
                                isInputMode = false;
                            }
                            return;
                        }
                }
                catch (NotSupportedException)
                {
                    // 如果词语是混合词
                    string kana = KanaConverter.RomajiToHiragana(content);

                    // 防止误输入拗音
                    if (currentWord.Kanji.IndexOf(kana, currentKanjiIndex) == currentKanjiIndex &&
                        (kana.Length == 3 || // 促音 + 拗音，相等就一定可以输入
                        (currentWord.Kanji[currentKanjiIndex] == 'っ' && kana.Length == 2 &&
                        currentWord.Kanji.IndexOfAny(new[] { 'ゃ', 'ゅ', 'ょ' }, currentKanjiIndex + 2) != currentKanjiIndex + 2) || // 促音非拗音，第三个字不是拗音即可
                        (currentWord.Kana[currentKanaIndex] != 'っ' && kana.Length == 2) || // 非促音拗音
                        (currentWord.Kanji[currentKanjiIndex] != 'っ' && kana.Length == 1 &&
                        currentWord.Kanji.IndexOfAny(new[] { 'ゃ', 'ゅ', 'ょ' }, currentKanjiIndex + 1) != currentKanjiIndex + 1) // 非促音非拗音，第二个字不是拗音即可
                        ))
                    {
                        // 增加假名到大字行
                        txtKanji.Text += kana;

                        // 清空缓冲区
                        txtInputBuffer.Clear();

                        if ((currentKanjiIndex += kana.Length) == currentWord.Kanji.Length)
                        {
                            SuccessCount++;
                            currentWord.Kana2KanjiSuccessCount++;
                            PushLog("完成了词 " + currentWord.Kanji + " 的汉字输入");
                            isInputMode = false;
                            return;
                        }
                    }
                }
            }
            else
            {
                string kana = KanaConverter.RomajiToHiragana(content);

                // 防止误输入拗音
                if (currentWord.Kana.IndexOf(kana, currentKanaIndex) == currentKanaIndex &&
                    (kana.Length == 3 || // 促音 + 拗音，相等就一定可以输入
                        (currentWord.Kana[currentKanaIndex] == 'っ' && kana.Length == 2 &&
                        currentWord.Kana.IndexOfAny(new[] { 'ゃ', 'ゅ', 'ょ' }, currentKanaIndex + 2) != currentKanaIndex + 2) || // 促音非拗音，第三个字不是拗音即可
                        (currentWord.Kana[currentKanaIndex] != 'っ' && kana.Length == 2) || // 非促音拗音
                        (currentWord.Kana[currentKanaIndex] != 'っ' && kana.Length == 1 &&
                        currentWord.Kana.IndexOfAny(new[] { 'ゃ', 'ゅ', 'ょ' }, currentKanaIndex + 1) != currentKanaIndex + 1) // 非促音非拗音，第二个字不是拗音即可
                        ))
                {
                    // 增加假名到小字行
                    txtAnnotation.Text += kana;

                    // 清空缓冲区
                    txtInputBuffer.Clear();

                    if ((currentKanaIndex += kana.Length) == currentWord.Kana.Length)
                    {
                        SuccessCount++;
                        currentWord.Kanji2KanaSuccessCount++;
                        PushLog("完成了词 " + currentWord.Kanji + " 的假名输入");
                        isInputMode = false;
                        return;
                    }
                }
            }
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            if (!isInputMode)
            {
                if (words.Count == 0)
                    return;

                // 读取新词
                words.Sort();
                int index = (int) Math.Floor(rand.NextDouble() * rand.NextDouble() * words.Count);
                if (index == words.Count)
                    index = 0;
                currentWord = words[index];

                if (cmbQuestionMode.SelectedIndex == 0)
                    isPinyinMode = false;
                else if (cmbQuestionMode.SelectedIndex == 1)
                    isPinyinMode = true;
                else
                    isPinyinMode = rand.Next(2) == 1;

                if (isPinyinMode)
                {
                    txtKanji.Text = "";
                    txtAnnotation.Text = currentWord.Kana;
                }
                else
                {
                    txtAnnotation.Text = "";
                    txtKanji.Text = currentWord.Kanji;
                }

                currentKanaIndex = 0;
                currentKanjiIndex = 0;
                isInputMode = true;
                txtInputBuffer.Clear();
                return;
            }

            PushLog("放弃了词 " + currentWord.Kanji);
            FailCount++;
            txtKanji.Text = currentWord.Kanji;
            txtAnnotation.Text = currentWord.Kana;
            if (isPinyinMode)
                currentWord.Kana2KanjiFailCount++;
            else
                currentWord.Kanji2KanaFailCount++;
            txtInputBuffer.Clear();
            isInputMode = false;
        }
    }
}
