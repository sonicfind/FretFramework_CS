using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Framework.Song.Tracks.Notes.Drums;
using System.Xml.Linq;
using Framework.Song.Tracks.Notes.Interfaces;

namespace Framework.Song.Tracks.Notes.Guitar_Pro
{
    public enum StringMode
    {
        Normal,
        Bend,
        Muted,
        Tapped,
        Harmonics,
        Pinch_Harmonics
    };

    public interface IFretted
    {
        public int MAX { get; }
        public int Value
        {
            get;
            set;
        }
    }

    public struct Fret_17 : IFretted
    {
        public int MAX => 17;

        private int _value = -1;
        public int Value
        {
            get { return _value; }
            set
            {
                if (_value < 0 || _value > MAX)
                    throw new ArgumentOutOfRangeException("Value");
                _value = value;
            }
        }
        public Fret_17() { }
        public Fret_17(int value) { Value = value; }
    }

    public struct Fret_22 : IFretted
    {
        public int MAX => 22;

        private int _value = -1;
        public int Value
        {
            get { return _value; }
            set
            {
                if (_value < 0 || _value > MAX)
                    throw new ArgumentOutOfRangeException("Value");
                _value = value;
            }
        }
        public Fret_22() { }
        public Fret_22(int value) { Value = value; }
    }

    public enum ProSlide
    {
        None,
        Normal,
        Reversed
    };

    public enum EmphasisType
    {
        None,
        High,
        Middle,
        Low
    };

    public struct ProString<FretType> : IEnableable
        where FretType : struct, IFretted
    {
        private TruncatableSustain _duration;
        public ulong Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }

        public bool IsActive() { return Duration > 0; }
        public void Disable() { _duration = 0; }

        public FretType fret;

        public StringMode Mode { get; set; }

        public ProString() { }
    };

    public unsafe struct Guitar_Pro<FretType> : INote
         where FretType : unmanaged, IFretted
    {
        private ProString<FretType> string_1;
        private ProString<FretType> string_2;
        private ProString<FretType> string_3;
        private ProString<FretType> string_4;
        private ProString<FretType> string_5;
        private ProString<FretType> string_6;
        public ref ProString<FretType> this[uint lane]
        {
            get
            {
                fixed (ProString<FretType>* strings = &string_1)
                    return ref strings[lane];
            }
        }
        public bool HOPO { get; set; }
        public bool ForceNumbering { get; set; }
        public ProSlide Slide { get; set; }
        public EmphasisType Emphasis { get; set; }
        public Guitar_Pro() { }

        public bool HasActiveNotes()
        {
            fixed (ProString<FretType>* strings = &string_1)
                for(uint i = 0; i < 6; ++i)
                    if (strings[i].IsActive())
                        return true;
            return false;
        }

        public ProSlide WheelSlide()
        {
            if (Slide == ProSlide.None)
                Slide = ProSlide.Normal;
            else if (Slide == ProSlide.Normal)
                Slide = ProSlide.Reversed;
            else
                Slide = ProSlide.None;
            return Slide;
        }

        public EmphasisType WheelEmphasis()
        {
            if (Emphasis == EmphasisType.None)
                Emphasis = EmphasisType.High;
            else if (Emphasis == EmphasisType.High)
                Emphasis = EmphasisType.Middle;
            else if (Emphasis == EmphasisType.Middle)
                Emphasis = EmphasisType.Low;
            else
                Emphasis = EmphasisType.None;
            return Emphasis;
        }
    }
}
