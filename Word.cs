using System.Collections.Generic;
using System;

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
}
