using Framework.Serialization;
using Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Modifiers
{
    public enum ModifierNodeType
    {
        NONE,
        SORTSTRING,
        STRING,
        SORTSTRING_CHART,
        STRING_CHART,
        UINT64,
        INT64,
        UINT32,
        INT32,
        UINT16,
        INT16,
        BOOL,
        FLOAT,
        FLOATARRAY
    }

    public class ModifierNode
    {
        public readonly string outputName;
        public readonly ModifierNodeType type;

        public ModifierNode(string outputName, ModifierNodeType type)
        {
            this.outputName = outputName;
            this.type = type;
        }

        public Modifier CreateModifier(TxtFileReader reader)
        {
            try
            {
                switch (type)
                {
                    case ModifierNodeType.SORTSTRING:       return new(outputName, new SortString(reader.ExtractUTF8String(false)));
                    case ModifierNodeType.SORTSTRING_CHART: return new(outputName, new SortString(reader.ExtractUTF8String(true)));
                    case ModifierNodeType.STRING:           return new(outputName, reader.ExtractUTF8String(false));
                    case ModifierNodeType.STRING_CHART:     return new(outputName, reader.ExtractUTF8String(true));
                    case ModifierNodeType.UINT64:           return new(outputName, reader.ReadUInt64());
                    case ModifierNodeType.INT64:            return new(outputName, reader.ReadInt64());
                    case ModifierNodeType.UINT32:           return new(outputName, reader.ReadUInt32());
                    case ModifierNodeType.INT32:            return new(outputName, reader.ReadInt32());
                    case ModifierNodeType.UINT16:           return new(outputName, reader.ReadUInt16());
                    case ModifierNodeType.INT16:            return new(outputName, reader.ReadInt16());
                    case ModifierNodeType.BOOL:             return new(outputName, reader.ReadBoolean());
                    case ModifierNodeType.FLOAT:            return new(outputName, reader.ReadFloat());
                    case ModifierNodeType.FLOATARRAY:
                        {
                            float flt1 = reader.ReadFloat();
                            float flt2 = reader.ReadFloat();
                            return new(outputName, flt1, flt2);
                        }
                }
            }
            catch (Exception _)
	        {
                switch (type)
                {
                    case ModifierNodeType.UINT64:     return new(outputName, (ulong)0);
                    case ModifierNodeType.INT64:      return new(outputName, (long)0);
                    case ModifierNodeType.UINT32:     return new(outputName, (uint)0);
                    case ModifierNodeType.INT32:      return new(outputName, (int)0);
                    case ModifierNodeType.UINT16:     return new(outputName, (ushort)0);
                    case ModifierNodeType.INT16:      return new(outputName, (short)0);
                    case ModifierNodeType.BOOL:       return new(outputName, false);
                    case ModifierNodeType.FLOAT:      return new(outputName, .0f);
                    case ModifierNodeType.FLOATARRAY: return new(outputName, 0, 0);
                }
            }
            throw new Exception("How in the fu-");
        }
    }
}
